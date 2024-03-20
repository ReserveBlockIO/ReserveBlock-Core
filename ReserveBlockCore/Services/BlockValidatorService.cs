using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReserveBlockCore.Data;
using ReserveBlockCore.Extensions;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.Nodes;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Utilities;
using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Text;

namespace ReserveBlockCore.Services
{
    public class BlockValidatorService
    {
        public static SemaphoreSlim ValidateBlocksSemaphore = new SemaphoreSlim(1, 1);
        public static SemaphoreSlim ValidateBlockSemaphore = new SemaphoreSlim(1, 1);

        public static void UpdateMemBlocks(Block block)
        {
            foreach (var trans in block.Transactions)
                Globals.MemBlocks[trans.Hash] = block.Height;

            var HeightDiff = Globals.LastBlock.Height - 400;
            foreach(var pair in Globals.MemBlocks.Where(x => x.Value <= HeightDiff))
            {
                Globals.MemBlocks.TryRemove(pair.Key, out _);
            }            
        }

        public static async Task ValidationDelay()
        {
            await ValidateBlocks();
            while (ValidateBlocksSemaphore.CurrentCount == 0 || Globals.BlocksDownloadSlim.CurrentCount == 0)
                await Task.Delay(4);
        }

        public static async Task ValidateBlocks()
        {
            try
            {
                await ValidateBlocksSemaphore.WaitAsync();
                while (BlockDownloadService.BlockDict.Any())
                {
                    var nextHeight = Globals.LastBlock.Height + 1;
                    var heights = BlockDownloadService.BlockDict.Keys.OrderBy(x => x).ToArray();
                    var offsetIndex = 0;
                    var heightOffset = 0L;
                    for (; offsetIndex < heights.Length; offsetIndex++)
                    {
                        heightOffset = heights[offsetIndex];
                        if (heightOffset < nextHeight)
                            BlockDownloadService.BlockDict.TryRemove(heightOffset, out _);
                        else
                            break;
                    }

                    if (heightOffset != nextHeight)
                        break;
                    heights = heights.Where(x => x >= nextHeight).Select((x, i) => (height: x, index: i)).TakeWhile(x => x.height == x.index + heightOffset)
                        .Select(x => x.height).ToArray();
                    foreach (var height in heights)
                    {
                        if (!BlockDownloadService.BlockDict.TryRemove(height, out var blockInfo))
                            continue;
                        var (block, ipAddress) = blockInfo;

                        var startupDownload = Globals.BlocksDownloadSlim.CurrentCount == 0 ? true : false;
                        var stopwatch1 = new Stopwatch();
                        stopwatch1.Start();
                        var result = await ValidateBlock(block, false, startupDownload);
                        stopwatch1.Stop();
                        if (!result && block.Height == Globals.LastBlock.Height + 1)
                        {
                            if (Globals.AdjudicateAccount != null)
                                continue;
                            BanService.BanPeer(ipAddress, ipAddress + " at height " + height, "BlockValidatorService.ValidateBlocks()");
                            ErrorLogUtility.LogError("Banned IP address: " + ipAddress + " at height " + height, "ValidateBlocks");
                            if (Globals.Nodes.TryRemove(ipAddress, out var node) && node.Connection != null)
                                await node.Connection.DisposeAsync();
                            ConsoleWriterService.Output($"Block: {block.Height} was rejected from: {block.Validator}");
                        }
                        else
                        {
                            if (Globals.IsChainSynced)
                            {
                                if(!Globals.BasicCLI)
                                {
                                    ConsoleWriterService.OutputSameLineMarked(($"Time: [yellow]{DateTime.Now}[/] | Block [green]({block.Height})[/] added from: [purple]{block.Validator}[/] | Delay: [aqua]{Globals.BlockTimeDiff}[/]/s"));
                                }
                                else
                                {
                                    ConsoleWriterService.OutputSameLineMarked($"Time: [yellow]{DateTime.Now}[/] | Block [green]({block.Height})[/]");
                                }
                                
                            }
                            else
                            {
                                ConsoleWriterService.OutputSameLine($"\rBlocks Syncing... Current Block: {block.Height} - Speed: {stopwatch1.ElapsedMilliseconds}/ms");
                            }
                                
                        }
                    }
                }
            }
            catch { }
            finally
            {
                try { ValidateBlocksSemaphore.Release(); } catch { }
            }
        }
        public static async Task<bool> ValidateBlock(Block block, bool ignoreAdjSignatures, bool blockDownloads = false, bool validateOnly = false)
        {
            try
            {
                await ValidateBlockSemaphore.WaitAsync();
                if (block?.Height <= Globals.LastBlock.Height)
                    return block.Hash == Globals.LastBlock.Hash;

                try
                {

                    if (block?.Height % 1000 == 0)
                    {
                        await DbContext.CheckPoint();
                    }

                    if(!validateOnly)
                        DbContext.BeginTrans();

                    bool result = false;

                    if (block == null)
                    {
                        DbContext.Rollback("BlockValidatorService.ValidateBlock()");
                        return result; //null block submitted. reject 
                    }

                    if(block.PrevHash == "0")
                    {
                        DbContext.Rollback("BlockValidatorService.ValidateBlock()-1");
                        return result;
                    }

                    if (block.Height == 0)
                    {
                        if (block.ChainRefId != BlockchainData.ChainRef)
                        {
                            DbContext.Rollback("BlockValidatorService.ValidateBlock()-2");
                            return result; //block rejected due to chainref difference
                        }
                        //Genesis Block
                        result = true;
                        BlockchainData.AddBlock(block);
                        await StateData.UpdateTreis(block);
                        foreach (Transaction transaction in block.Transactions)
                        {
                            //Adds receiving TX to wallet
                            var account = AccountData.GetAccounts().FindOne(x => x.Address == transaction.ToAddress);
                            if (account != null)
                            {
                                AccountData.UpdateLocalBalanceAdd(transaction.ToAddress, transaction.Amount);
                                var txdata = TransactionData.GetAll();
                                txdata.InsertSafe(transaction);
                            }

                        }

                        UpdateMemBlocks(block);//update mem blocks
                        DbContext.Commit();
                        return result;
                    }
                    if (block.Height != 0)
                    {
                        var verifyBlockSig = SignatureService.VerifySignature(block.Validator, block.Hash, block.ValidatorSignature);

                        //validates the signature of the validator that crafted the block
                        if (verifyBlockSig != true)
                        {
                            DbContext.Rollback("BlockValidatorService.ValidateBlock()-3");
                            return result;//block rejected due to failed validator signature
                        }
                    }


                    //Validates that the block has same chain ref
                    if (block.ChainRefId != BlockchainData.ChainRef)
                    {
                        DbContext.Rollback("BlockValidatorService.ValidateBlock()-5");
                        return result;//block rejected due to chainref difference
                    }

                    var blockVersion = BlockVersionUtility.GetBlockVersion(block.Height);

                    if (block.Version != blockVersion)
                    {
                        DbContext.Rollback("BlockValidatorService.ValidateBlock()-6");
                        return result;
                    }

                    if(block.Version == 4)
                    {
                        var version4Result = await BlockVersionUtility.Version4Rules(block);
                        if(!version4Result.Item1)
                        {
                            DbContext.Rollback($"BlockValidatorService.ValidateBlock()-7-4. {version4Result.Item2}");
                            return result;
                        }
                    }
                    else if (block.Version == 3 && !ignoreAdjSignatures)
                    {
                        var version3Result = await BlockVersionUtility.Version3Rules(block);
                        if (!version3Result.Item1)
                        {
                            DbContext.Rollback($"BlockValidatorService.ValidateBlock()-7-3. {version3Result.Item2}");
                            return result;
                        }
                            
                    }
                    else if (block.Version == 2)
                    {
                        //Run block version 2 rules
                        var version2Result = await BlockVersionUtility.Version2Rules(block);
                        if (!version2Result)
                        {
                            DbContext.Rollback("BlockValidatorService.ValidateBlock()-8");
                            return result;
                        }
                    }
                    else
                    {
                        //no rules
                    }

                    //ensures the timestamps being produced are correct
                    if (block.Height != 0)
                    {
                        var prevTimestamp = Globals.LastBlock.Timestamp;
                        var currentTimestamp = TimeUtil.GetTime(60);
                        if (prevTimestamp > block.Timestamp || block.Timestamp > currentTimestamp)
                        {
                            DbContext.Rollback("BlockValidatorService.ValidateBlock()-9");
                            return result;
                        }
                    }

                    var newBlock = new Block
                    {
                        Height = block.Height,
                        Timestamp = block.Timestamp,
                        Transactions = block.Transactions,
                        Validator = block.Validator,
                        ChainRefId = block.ChainRefId,
                        TotalValidators = block.TotalValidators,
                        ValidatorAnswer = block.ValidatorAnswer
                    };

                    newBlock.Build();

                    //This will also check that the prev hash matches too
                    if (!newBlock.Hash.Equals(block.Hash))
                    {
                        DbContext.Rollback("BlockValidatorService.ValidateBlock()-10");
                        return result;//block rejected
                    }

                    if (!newBlock.MerkleRoot.Equals(block.MerkleRoot))
                    {
                        DbContext.Rollback("BlockValidatorService.ValidateBlock()-11");
                        return result;//block rejected
                    }

                    if (block.Height != 0)
                    {
                        var blockCoinBaseResult = BlockchainData.ValidateBlock(block); //this checks the coinbase tx

                        //Need to check here the prev hash if it is correct!

                        if (blockCoinBaseResult == false)
                        {
                            DbContext.Rollback("BlockValidatorService.ValidateBlock()-12");
                            return result;//block rejected
                        }

                        if (block.Transactions.Count() > 0)
                        {
                            //validate transactions.
                            bool rejectBlock = false;
                            foreach (Transaction blkTransaction in block.Transactions)
                            {
                                if (blkTransaction.FromAddress != "Coinbase_TrxFees" && blkTransaction.FromAddress != "Coinbase_BlkRwd")
                                {
                                    var txResult = await TransactionValidatorService.VerifyTX(blkTransaction, blockDownloads, true);
                                    if(txResult.Item1 == false)
                                    {
                                        //testing
                                    }
                                    if(!Globals.GUI && !Globals.BasicCLI && !blockDownloads)
                                    {
                                        //if (!txResult.Item1)
                                        //    await TransactionValidatorService.BadTXDetected(blkTransaction);
                                    }
                                    rejectBlock = txResult.Item1 == false ? rejectBlock = true : false;
                                    //check for duplicate tx
                                    if (blkTransaction.TransactionType != TransactionType.TX &&
                                        blkTransaction.TransactionType != TransactionType.ADNR &&
                                        blkTransaction.TransactionType != TransactionType.VOTE &&
                                        blkTransaction.TransactionType != TransactionType.VOTE_TOPIC &&
                                        blkTransaction.TransactionType != TransactionType.DSTR &&
                                        blkTransaction.TransactionType != TransactionType.RESERVE && 
                                        blkTransaction.TransactionType != TransactionType.NFT_SALE)
                                    {
                                        if (blkTransaction.Data != null)
                                        {
                                            try
                                            {
                                                AccountStateTrei? stateTreiAcct = null;
                                                stateTreiAcct = StateData.GetSpecificAccountStateTrei(blkTransaction.FromAddress);
                                                var scInfo = TransactionUtility.GetSCTXFunctionAndUID(blkTransaction);
                                                if (!scInfo.Item1)
                                                    return false;

                                                string scUID = scInfo.Item3;
                                                string function = scInfo.Item4;
                                                JArray? scDataArray = scInfo.Item5;
                                                bool skip = scInfo.Item2;

                                                if (scDataArray != null && skip)
                                                {
                                                    var scData = scDataArray[0];

                                                    function = (string?)scData["Function"];

                                                    if (!string.IsNullOrWhiteSpace(function))
                                                    {
                                                        switch(function)
                                                        {
                                                            case "Transfer()":
                                                                {
                                                                    var otherTxs = block.Transactions.Where(x => x.FromAddress == blkTransaction.FromAddress && x.Hash != blkTransaction.Hash).ToList();
                                                                    if (otherTxs.Count() > 0)
                                                                    {
                                                                        foreach (var otx in otherTxs)
                                                                        {
                                                                            if (otx.TransactionType == TransactionType.NFT_TX ||
                                                                                otx.TransactionType == TransactionType.NFT_BURN ||
                                                                                otx.TransactionType == TransactionType.NFT_MINT)
                                                                            {
                                                                                scUID = (string?)scData["ContractUID"];
                                                                                if (otx.Data != null)
                                                                                {
                                                                                    var memscInfo = TransactionUtility.GetSCTXFunctionAndUID(otx);
                                                                                    if (memscInfo.Item2)
                                                                                    {
                                                                                        var ottxDataArray = JsonConvert.DeserializeObject<JArray>(otx.Data);
                                                                                        if (ottxDataArray != null)
                                                                                        {
                                                                                            var ottxData = ottxDataArray[0];

                                                                                            var ottxFunction = (string?)ottxData["Function"];
                                                                                            var ottxscUID = (string?)ottxData["ContractUID"];
                                                                                            if (!string.IsNullOrWhiteSpace(ottxFunction))
                                                                                            {
                                                                                                if (ottxscUID == scUID)
                                                                                                {
                                                                                                    rejectBlock = true;
                                                                                                }
                                                                                            }
                                                                                        }
                                                                                    }
                                                                                    
                                                                                }
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                                break;
                                                            case "Burn()":
                                                                {
                                                                    var otherTxs = block.Transactions.Where(x => x.FromAddress == blkTransaction.FromAddress && x.Hash != blkTransaction.Hash).ToList();
                                                                    if (otherTxs.Count() > 0)
                                                                    {
                                                                        foreach (var otx in otherTxs)
                                                                        {
                                                                            if (otx.TransactionType == TransactionType.NFT_TX ||
                                                                                otx.TransactionType == TransactionType.NFT_BURN ||
                                                                                otx.TransactionType == TransactionType.NFT_MINT)
                                                                            {
                                                                                scUID = (string?)scData["ContractUID"];
                                                                                if (otx.Data != null)
                                                                                {
                                                                                    var memscInfo = TransactionUtility.GetSCTXFunctionAndUID(otx);
                                                                                    if (memscInfo.Item2)
                                                                                    {
                                                                                        var ottxDataArray = JsonConvert.DeserializeObject<JArray>(otx.Data);
                                                                                        if (ottxDataArray != null)
                                                                                        {
                                                                                            var ottxData = ottxDataArray[0];

                                                                                            var ottxFunction = (string?)ottxData["Function"];
                                                                                            var ottxscUID = (string?)ottxData["ContractUID"];
                                                                                            if (!string.IsNullOrWhiteSpace(ottxFunction))
                                                                                            {
                                                                                                if (ottxscUID == scUID)
                                                                                                {
                                                                                                    rejectBlock = true;
                                                                                                }
                                                                                            }
                                                                                        }
                                                                                    }
                                                                                }
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                                break;
                                                            case string i when i == "TokenTransfer()" || i == "TokenBurn()":
                                                                {
                                                                    var otherTxs = block.Transactions.Where(x => x.FromAddress == blkTransaction.FromAddress && x.Hash != blkTransaction.Hash).ToList();
                                                                    if (otherTxs.Count() > 0)
                                                                    {
                                                                        decimal xferBurnAmount = 0.0M;
                                                                        var originaljobj = JObject.Parse(blkTransaction.Data);
                                                                        var tokenTicker = originaljobj["TokenTicker"]?.ToObject<string?>();
                                                                        var amount = originaljobj["Amount"]?.ToObject<decimal?>();

                                                                        if (amount == null)
                                                                            rejectBlock = true;

                                                                        var tokenAccount = stateTreiAcct.TokenAccounts?.Where(x => x.TokenTicker == tokenTicker).FirstOrDefault();

                                                                        if (tokenAccount == null)
                                                                            rejectBlock = true;

                                                                        xferBurnAmount += amount.Value;

                                                                        foreach (var otx in otherTxs)
                                                                        {
                                                                            if (otx.TransactionType == TransactionType.NFT_TX)
                                                                            {
                                                                                if (otx.Data != null)
                                                                                {
                                                                                    var memscInfo = TransactionUtility.GetSCTXFunctionAndUID(otx);
                                                                                    if (!memscInfo.Item2 && memscInfo.Item1)
                                                                                    {
                                                                                        var jobj = JObject.Parse(otx.Data);
                                                                                        var otscUID = jobj["ContractUID"]?.ToObject<string?>();
                                                                                        var otFunction = jobj["Function"]?.ToObject<string?>();

                                                                                        if (otscUID == scUID)
                                                                                        {
                                                                                            var otTokenTicker = jobj["TokenTicker"]?.ToObject<string?>();
                                                                                            var otAmount = jobj["Amount"]?.ToObject<decimal?>();
                                                                                            if (otFunction != null)
                                                                                            {
                                                                                                if (otFunction == "TokenTransfer()" || otFunction == "TokenBurn()")
                                                                                                {
                                                                                                    if (otAmount != null)
                                                                                                    {
                                                                                                        if (otTokenTicker == tokenTicker)
                                                                                                        {
                                                                                                            xferBurnAmount += otAmount.Value;
                                                                                                        }

                                                                                                    }
                                                                                                }
                                                                                            }
                                                                                        }
                                                                                    }
                                                                                }
                                                                            }
                                                                        }

                                                                        if (xferBurnAmount > tokenAccount.Balance) 
                                                                            rejectBlock = true; //failed due to overspend/overburn
                                                                    }
                                                                }
                                                                break;
                                                            default: { } break;
                                                        }
                                                        
                                                    }
                                                }
                                            }
                                            catch
                                            {
                                                rejectBlock = true;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    //do nothing as its the coinbase fee
                                }

                                if (rejectBlock)
                                    break;
                            }

                            if (rejectBlock)
                            {
                                DbContext.Rollback("BlockValidatorService.ValidateBlock()-13");
                                return result;//block rejected due to bad transaction(s)
                            }

                            result = true;

                            if (validateOnly)
                                return result;

                            BlockchainData.AddBlock(block);//add block to chain.
                            UpdateMemBlocks(block);//update mem blocks
                            
                            await StateData.UpdateTreis(block); //update treis
                            await ReserveService.Run(); //updates treis for reserve pending txs

                            var mempool = TransactionData.GetPool();

                            if (block.Transactions.Count() > 0)
                            {
                                foreach (var localFromTransaction in block.Transactions)
                                {
                                    Globals.BroadcastedTrxDict.TryRemove(localFromTransaction.Hash, out _);
                                    Globals.ConsensusBroadcastedTrxDict.TryRemove(localFromTransaction.Hash, out _);

                                    if (mempool != null)
                                    {
                                        var mempoolTx = mempool.FindAll().Where(x => x.Hash == localFromTransaction.Hash);
                                        if (mempoolTx.Count() > 0)
                                        {
                                            mempool.DeleteManySafe(x => x.Hash == localFromTransaction.Hash);
                                        }
                                    }
                                    try
                                    {
                                        //Process transactions sent ->From<- wallet
                                        var fromAccount = AccountData.GetAccounts().FindOne(x => x.Address == localFromTransaction.FromAddress);
                                        if (fromAccount != null)
                                        {
                                            await BlockTransactionValidatorService.ProcessOutgoingTransaction(localFromTransaction, fromAccount, block.Height);
                                        }
                                        var reserveAccount = ReserveAccount.GetReserveAccounts()?.Where(x => x.Address == localFromTransaction.FromAddress).FirstOrDefault();
                                        if (reserveAccount != null)
                                        {
                                            //change account types
                                            await BlockTransactionValidatorService.ProcessOutgoingReserveTransaction(localFromTransaction, reserveAccount, block.Height);
                                        }
                                    }
                                    catch { }
                                    
                                }

                                foreach (var localToTransaction in block.Transactions)
                                {
                                    string? nftSellerAddress = null;
                                    Transaction? nftSellerTX = null;
                                    string? nftRoyaltyAddress = null;
                                    Transaction? nftRoyaltyTX = null;
                                    try
                                    {
                                        if (localToTransaction.TransactionType == TransactionType.NFT_SALE)
                                        {
                                            var txData = localToTransaction.Data;
                                            var jobj = JObject.Parse(txData);
                                            var function = (string?)jobj["Function"];
                                            if (function != null)
                                            {
                                                if (function == "Sale_Complete()")
                                                {
                                                    var transactions = jobj["Transactions"]?.ToObject<List<Transaction>?>();
                                                    if (transactions != null)
                                                    {
                                                        if (transactions.Count() > 1)
                                                        {
                                                            var txToSeller = transactions.Where(x => x.Data.Contains("1/2")).FirstOrDefault();
                                                            var txToRoyaltyPayee = transactions.Where(x => x.Data.Contains("2/2")).FirstOrDefault();

                                                            nftSellerAddress = txToSeller.ToAddress;
                                                            nftSellerTX = txToSeller;

                                                            nftRoyaltyAddress = txToRoyaltyPayee.ToAddress;
                                                            nftRoyaltyTX = txToRoyaltyPayee;
                                                        }
                                                        else
                                                        {
                                                            var txToSeller = transactions.FirstOrDefault();
                                                            nftSellerAddress = txToSeller.ToAddress;
                                                            nftSellerTX = txToSeller;
                                                        }
                                                    }
                                                }
                                            }

                                        }
                                    }
                                    catch { }
                                    try
                                    {
                                        //Process transactions sent ->To<- wallet
                                        var account = AccountData.GetAccounts().FindOne(x => x.Address == localToTransaction.ToAddress);
                                        if (account != null)
                                        {
                                            await BlockTransactionValidatorService.ProcessIncomingTransactions(localToTransaction, account, block.Height);
                                        }

                                        //these are for NFT sales 1. seller 2. royalty
                                        if(nftSellerAddress != null)
                                        {
                                            var nftSellerAccount = AccountData.GetAccounts().FindOne(x => x.Address == nftSellerAddress);
                                            if (nftSellerTX != null && nftSellerAccount != null)
                                            {
                                                await BlockTransactionValidatorService.ProcessIncomingTransactions(nftSellerTX, nftSellerAccount, block.Height);
                                            }
                                        }
                                        if (nftRoyaltyAddress != null)
                                        {
                                            var nftRoyaltyAccount = AccountData.GetAccounts().FindOne(x => x.Address == nftRoyaltyAddress);
                                            if (nftRoyaltyTX != null && nftRoyaltyAccount != null)
                                            {
                                                await BlockTransactionValidatorService.ProcessIncomingTransactions(nftRoyaltyTX, nftRoyaltyAccount, block.Height);
                                            }
                                        }
                                        var reserveAccount = ReserveAccount.GetReserveAccounts()?.Where(x => x.Address == localToTransaction.ToAddress).FirstOrDefault();
                                        if(reserveAccount != null)
                                        {
                                            //change accounts types
                                            await BlockTransactionValidatorService.ProcessIncomingReserveTransactions(localToTransaction, reserveAccount, block.Height);
                                        }
                                    }
                                    catch { }
                                }
                            }

                        }

                        await TransactionData.UpdateWalletTXTask();

                        DbContext.Commit();

                        return result;//block accepted
                    }
                    else
                    {
                        //Genesis Block
                        result = true;
                        BlockchainData.AddBlock(block);
                        await StateData.UpdateTreis(block);
                        DbContext.Commit();
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    DbContext.Rollback("BlockValidatorService.ValidateBlock()-14");
                    Console.WriteLine($"Error: {ex.ToString()}");
                }
            }
            catch { }
            finally
            {
                try { ValidateBlockSemaphore.Release(); } catch { }
            }
                        
            return false;
        }

        //This method does not add block or update any treis
        public static async Task<bool> ValidateBlockForTask(Block block, bool ignoreAdjSignatures, bool blockDownloads = false)
        {
            bool result = false;

            if (block == null) return result; //null block submitted. reject 

            if (block.Height != 0)
            {
                var verifyBlockSig = SignatureService.VerifySignature(block.Validator, block.Hash, block.ValidatorSignature);

                //validates the signature of the validator that crafted the block
                if (verifyBlockSig != true)
                {
                    ValidatorLogUtility.Log("Block failed with bad validator signature", "BlockValidatorService.ValidateBlockForTask()");
                    return result;//block rejected due to failed validator signature
                }
            }

            if(block.Height != 0)
            {
                //ensures the timestamps being produced are correct
                var prevTimestamp = Globals.LastBlock.Timestamp;
                var currentTimestamp = TimeUtil.GetTime(60);
                if (prevTimestamp > block.Timestamp || block.Timestamp > currentTimestamp)
                {
                    return result;
                }
            }

            //Validates that the block has same chain ref
            if (block.ChainRefId != BlockchainData.ChainRef)
            {
                ValidatorLogUtility.Log("Block validated failed due to Chain Reference ID's being different", "BlockValidatorService.ValidateBlockForTask()");
                return result;//block rejected due to chainref difference
            }

            var blockVersion = BlockVersionUtility.GetBlockVersion(block.Height);

            if (block.Version != blockVersion)
            {
                ValidatorLogUtility.Log("Block validated failed due to block versions not matching", "BlockValidatorService.ValidateBlockForTask()");
                return result;
            }

            var newBlock = new Block
            {
                Height = block.Height,
                Timestamp = block.Timestamp,
                Transactions = block.Transactions,
                Validator = block.Validator,
                ChainRefId = block.ChainRefId,
                TotalValidators = block.TotalValidators,
                ValidatorAnswer = block.ValidatorAnswer
            };

            newBlock.Build();

            //This will also check that the prev hash matches too
            if (!newBlock.Hash.Equals(block.Hash))
            {
                ValidatorLogUtility.Log("Block validated failed due to block hash not matching", "BlockValidatorService.ValidateBlockForTask()");
                return result;//block rejected
            }

            if (!newBlock.MerkleRoot.Equals(block.MerkleRoot))
            {
                ValidatorLogUtility.Log("Block validated failed due to merkel root not matching", "BlockValidatorService.ValidateBlockForTask()");
                return result;//block rejected
            }
            var blockCoinBaseResult = BlockchainData.ValidateBlock(block); //this checks the coinbase tx

            //Need to check here the prev hash if it is correct!

            if (blockCoinBaseResult == false)
                return result;//block rejected

            if (block.Transactions.Count() > 0)
            {
                //validate transactions.
                bool rejectBlock = false;
                foreach (Transaction transaction in block.Transactions)
                {
                    if (transaction.FromAddress != "Coinbase_TrxFees" && transaction.FromAddress != "Coinbase_BlkRwd")
                    {
                        var txResult = await TransactionValidatorService.VerifyTX(transaction, blockDownloads);
                        rejectBlock = txResult.Item1 == false ? rejectBlock = true : false;
                        if (rejectBlock)
                        {
                            RemoveTxFromMempool(transaction);//this should not happen, but if client did fail to properly handle tx it will reject it here.
                        }
                    }
                    else { }//do nothing as its the coinbase fee

                    if (rejectBlock)
                        break;
                }

                

                if (rejectBlock)
                {
                    ValidatorLogUtility.Log("Block validated failed due to transactions not validating", "BlockValidatorService.ValidateBlockForTask()");
                    return result;//block rejected due to bad transaction(s)
                }
                    

                result = true;
            }
            return result;//block accepted
        }

        private static async void RemoveTxFromMempool(Transaction tx)
        {
            var mempool = TransactionData.GetPool();
            if(mempool != null)
            {
                if (mempool.Count() > 0)
                {
                    mempool.DeleteManySafe(x => x.Hash == tx.Hash);
                }
            }
        }

    }
}
