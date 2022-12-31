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
using System.Text;

namespace ReserveBlockCore.Services
{
    public class BlockValidatorService
    {
        public static SemaphoreSlim ValidateBlocksSemaphore = new SemaphoreSlim(1, 1);
        public static SemaphoreSlim ValidateBlockSemaphore = new SemaphoreSlim(1, 1);

        public static void UpdateMemBlocks(Block block)
        {
            if(Globals.MemBlocks.Count == 400)
                Globals.MemBlocks.TryDequeue(out Block test);
            Globals.MemBlocks.Enqueue(block);
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

                        var result = await ValidateBlock(block, false, startupDownload);
                        if (!result)
                        {
                            if (Globals.AdjudicateAccount != null)
                                continue;
                            Peers.BanPeer(ipAddress, ipAddress + " at height " + height, "ValidateBlocks");
                            ErrorLogUtility.LogError("Banned IP address: " + ipAddress + " at height " + height, "ValidateBlocks");
                            if (Globals.Nodes.TryRemove(ipAddress, out var node) && node.Connection != null)
                                await node.Connection.DisposeAsync();
                            ConsoleWriterService.Output("Block was rejected from: " + block.Validator);
                        }
                        else
                        {
                            if (Globals.IsChainSynced)
                                ConsoleWriterService.OutputSameLineMarked(($"Time: [yellow]{DateTime.Now}[/] | Block [green]({block.Height})[/] was added from: [purple]{block.Validator}[/] "));
                            else
                                ConsoleWriterService.OutputSameLine($"\rBlocks Syncing... Current Block: {block.Height} ");
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
        public static async Task<bool> ValidateBlock(Block block, bool ignoreAdjSignatures, bool blockDownloads = false)
        {
            try
            {
                await ValidateBlockSemaphore.WaitAsync();
                try
                {
                    DbContext.BeginTrans();
                    bool result = false;

                    if (block == null)
                    {
                        DbContext.Rollback("BlockValidatorService.ValidateBlock()");
                        return result; //null block submitted. reject 
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
                        StateData.UpdateTreis(block);
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

                    if (block.Version > 2 && !ignoreAdjSignatures)
                    {
                        var version3Result = await BlockVersionUtility.Version3Rules(block);
                        if (!version3Result)
                        {
                            DbContext.Rollback("BlockValidatorService.ValidateBlock()-7");
                            return result;
                        }
                            
                    }
                    else if (block.Version > 1)
                    {
                        //Run block version 2 rules
                        var version2Result = await BlockVersionUtility.Version2Rules(block);
                        if (!version2Result)
                        {
                            DbContext.Rollback("BlockValidatorService.ValidateBlock()-8");
                            return result;
                        }
                    }
                    //ensures the timestamps being produced are correct
                    if (block.Height != 0)
                    {
                        var prevTimestamp = Globals.LastBlock.Timestamp;
                        var currentTimestamp = TimeUtil.GetTime(1);
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
                                    var txResult = await TransactionValidatorService.VerifyTX(blkTransaction, blockDownloads);
                                    rejectBlock = txResult == false ? rejectBlock = true : false;
                                    //check for duplicate tx
                                    if (blkTransaction.TransactionType != TransactionType.TX &&
                                        blkTransaction.TransactionType != TransactionType.ADNR &&
                                        blkTransaction.TransactionType != TransactionType.VOTE &&
                                        blkTransaction.TransactionType != TransactionType.VOTE_TOPIC)
                                    {
                                        if (blkTransaction.Data != null)
                                        {
                                            var scDataArray = JsonConvert.DeserializeObject<JArray>(blkTransaction.Data);
                                            if (scDataArray != null)
                                            {
                                                var scData = scDataArray[0];

                                                var function = (string?)scData["Function"];

                                                if (!string.IsNullOrWhiteSpace(function))
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
                                                                var scUID = (string?)scData["ContractUID"];
                                                                if (otx.Data != null)
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
                            BlockchainData.AddBlock(block);//add block to chain.
                            UpdateMemBlocks(block);//update mem blocks
                            await StateData.UpdateTreis(block); //update treis
                            var mempool = TransactionData.GetPool();

                            if (block.Transactions.Count() > 0)
                            {
                                foreach (var localTransaction in block.Transactions)
                                {
                                    if (mempool != null)
                                    {
                                        var mempoolTx = mempool.FindAll().Where(x => x.Hash == localTransaction.Hash);
                                        if (mempoolTx.Count() > 0)
                                        {
                                            mempool.DeleteManySafe(x => x.Hash == localTransaction.Hash);
                                            Globals.BroadcastedTrxDict.TryRemove(localTransaction.Hash, out _);
                                            Globals.ConsensusBroadcastedTrxDict.TryRemove(localTransaction.Hash, out _);
                                        }
                                    }

                                    //Process transactions sent ->To<- wallet
                                    var account = AccountData.GetAccounts().FindOne(x => x.Address == localTransaction.ToAddress);
                                    if (account != null)
                                    {
                                        await BlockTransactionValidatorService.ProcessIncomingTransactions(localTransaction, account, block.Height);
                                    }

                                    //Process transactions sent ->From<- wallet
                                    var fromAccount = AccountData.GetAccounts().FindOne(x => x.Address == localTransaction.FromAddress);
                                    if (fromAccount != null)
                                    {
                                        await BlockTransactionValidatorService.ProcessOutgoingTransaction(localTransaction, fromAccount, block.Height);
                                    }
                                }
                            }

                        }

                        await TransactionData.UpdateWalletTXTask();
                        DbContext.Commit();
                        if (P2PClient.MaxHeight() <= block.Height)
                        {
                            if (Globals.LastBlock.Height >= Globals.BlockLock)
                            {
                                ValidatorProcessor.RandomNumberTaskV3(block.Height + 1);
                            }
                            else
                            {
                                ValidatorProcessor.RandomNumberTask_New(block.Height + 1);
                            }
                        }

                        Signer.UpdateSigningAddresses();
                        if (Globals.AdjudicateAccount != null)
                            await ClientCallService.FinalizeWork(block);

                        return result;//block accepted
                    }
                    else
                    {
                        //Genesis Block
                        result = true;
                        BlockchainData.AddBlock(block);
                        StateData.UpdateTreis(block);
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
                        rejectBlock = txResult == false ? rejectBlock = true : false;
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
