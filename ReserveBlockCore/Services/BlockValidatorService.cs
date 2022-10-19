using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Utilities;
using System;
using System.Text;

namespace ReserveBlockCore.Services
{
    public class BlockValidatorService
    {
        public static int IsValidatingBlocks = 0;

        public static void UpdateMemBlocks(Block block)
        {
            Globals.MemBlocks.TryDequeue(out Block test);
            Globals.MemBlocks.Enqueue(block);
        }

        public static async Task ValidationDelay()
        {
            await ValidateBlocks();
            while (IsValidatingBlocks == 1 || Globals.BlocksDownloading == 1)
                await Task.Delay(4);
        }
        public static async Task ValidateBlocks()
        {
            if (Interlocked.Exchange(ref BlockValidatorService.IsValidatingBlocks, 1) != 0)            
                return;

            try
            {
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
                            BlockDownloadService.BlockDict.TryRemove(heightOffset, out var test);
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
                        var result = await ValidateBlock(block, true);                        
                        if (!result)
                        {                            
                            Peers.BanPeer(ipAddress, ipAddress + " at height " + height, "ValidateBlocks");
                            ErrorLogUtility.LogError("Banned IP address: " + ipAddress + " at height " + height, "ValidateBlocks");
                            if(Globals.Nodes.TryRemove(ipAddress, out var node))
                                await node.Connection.DisposeAsync();                            
                            Console.WriteLine("Block was rejected from: " + block.Validator);
                        }
                        else
                        {
                            if(Globals.IsChainSynced)
                                ConsoleWriterService.OutputSameLineMarked(($"Time: [yellow]{DateTime.Now}[/] | Block [green]({block.Height})[/] was added from: [purple]{block.Validator}[/] "));
                            else
                                Console.Write($"\rBlocks Syncing... Current Block: {block.Height} ");                                                        
                        }                        
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref BlockValidatorService.IsValidatingBlocks, 0);
            }
        }
        public static async Task<bool> ValidateBlock(Block block, bool blockDownloads = false)
        {
            try
            {
                DbContext.BeginTrans();
                bool result = false;

                if (block == null)
                {
                    DbContext.Rollback();
                    return result; //null block submitted. reject 
                }

                if (block.Height == 0)
                {
                    if (block.ChainRefId != BlockchainData.ChainRef)
                    {
                        DbContext.Rollback();
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
                        DbContext.Rollback();
                        return result;//block rejected due to failed validator signature
                    }
                }


                //Validates that the block has same chain ref
                if (block.ChainRefId != BlockchainData.ChainRef)
                {
                    DbContext.Rollback();
                    return result;//block rejected due to chainref difference
                }

                var blockVersion = BlockVersionUtility.GetBlockVersion(block.Height);

                if (block.Version != blockVersion)
                {
                    DbContext.Rollback();
                    return result;
                }

                if (block.Version > 1)
                {
                    //Run block version 2 rules
                    var version2Result = await BlockVersionUtility.Version2Rules(block);
                    if (!version2Result)
                        return result;
                }
                //ensures the timestamps being produced are correct
                if (block.Height != 0)
                {
                    var prevTimestamp = Globals.LastBlock.Timestamp;
                    var currentTimestamp = TimeUtil.GetTime(1);
                    if (prevTimestamp > block.Timestamp || block.Timestamp > currentTimestamp)
                    {
                        DbContext.Rollback();
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
                    DbContext.Rollback();
                    return result;//block rejected
                }

                if (!newBlock.MerkleRoot.Equals(block.MerkleRoot))
                {
                    DbContext.Rollback();
                    return result;//block rejected
                }

                if (block.Height != 0)
                {
                    var blockCoinBaseResult = BlockchainData.ValidateBlock(block); //this checks the coinbase tx

                    //Need to check here the prev hash if it is correct!

                    if (blockCoinBaseResult == false)
                    {
                        DbContext.Rollback();
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
                                if (blkTransaction.TransactionType != TransactionType.TX)
                                {
                                    if(blkTransaction.Data != null)
                                    {
                                        var scDataArray = JsonConvert.DeserializeObject<JArray>(blkTransaction.Data);
                                        if (scDataArray != null)
                                        {
                                            var scData = scDataArray[0];

                                            var function = (string?)scData["Function"];
                                            var scUID = (string?)scData["ContractUID"];
                                            if (!string.IsNullOrWhiteSpace(function))
                                            {
                                                var otherTxs = block.Transactions.Where(x => x.FromAddress == blkTransaction.FromAddress && x.Hash != blkTransaction.Hash).ToList();
                                                if (otherTxs.Count() > 0)
                                                {
                                                    foreach (var otx in otherTxs)
                                                    {
                                                        if (otx.TransactionType == TransactionType.NFT_TX || otx.TransactionType == TransactionType.NFT_BURN)
                                                        {
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
                            DbContext.Rollback();
                            return result;//block rejected due to bad transaction(s)
                        }

                        result = true;
                        BlockchainData.AddBlock(block);//add block to chain.
                        UpdateMemBlocks(block);//update mem blocks
                        StateData.UpdateTreis(block); //update treis
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
                                        Globals.BroadcastedTrxDict.TryRemove(localTransaction.Hash, out var test);
                                    }
                                }


                                //Adds receiving TX to wallet
                                var account = AccountData.GetAccounts().FindOne(x => x.Address == localTransaction.ToAddress);
                                if (account != null)
                                {
                                    if (localTransaction.TransactionType == TransactionType.TX)
                                    {
                                        AccountData.UpdateLocalBalanceAdd(localTransaction.ToAddress, localTransaction.Amount);
                                        var txdata = TransactionData.GetAll();
                                        txdata.InsertSafe(localTransaction);
                                    }
                                    if(localTransaction.TransactionType == TransactionType.NFT_TX || localTransaction.TransactionType == TransactionType.NFT_SALE)
                                    {
                                        var scDataArray = JsonConvert.DeserializeObject<JArray>(localTransaction.Data);
                                        if(scDataArray != null)
                                        {
                                            var scData = scDataArray[0];
                                            if (scData != null)
                                            {
                                                var function = (string?)scData["Function"];
                                                if(!string.IsNullOrWhiteSpace(function))
                                                {
                                                    if(function == "Transfer()")
                                                    {
                                                        var txdata = TransactionData.GetAll();
                                                        txdata.InsertSafe(localTransaction);
                                                    }
                                                }
                                            }
                                        }
                                        
                                    }
                                    if (Globals.IsChainSynced == true)//this is here so someone doesn't get spammed with API calls when starting wallet or syncing
                                    {
                                        //Call out to custom URL from config file with TX details
                                        if (!string.IsNullOrWhiteSpace(Globals.APICallURL))
                                        {
                                            APICallURLService.CallURL(localTransaction);
                                        }
                                    }
                                    if (localTransaction.TransactionType != TransactionType.TX)
                                    {


                                        if (localTransaction.TransactionType == TransactionType.NFT_MINT)
                                        {
                                            var scDataArray = JsonConvert.DeserializeObject<JArray>(localTransaction.Data);
                                            var scData = scDataArray[0];

                                            if (scData != null)
                                            {
                                                var function = (string?)scData["Function"];
                                                if (!string.IsNullOrWhiteSpace(function))
                                                {
                                                    if (function == "Mint()")
                                                    {
                                                        var scUID = (string?)scData["ContractUID"];
                                                        if (!string.IsNullOrWhiteSpace(scUID))
                                                        {
                                                            SmartContractMain.SmartContractData.SetSmartContractIsPublished(scUID);//flags local SC to isPublished now
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        if (localTransaction.TransactionType == TransactionType.NFT_TX)
                                        {
                                            var scDataArray = JsonConvert.DeserializeObject<JArray>(localTransaction.Data);
                                            var scData = scDataArray[0];

                                            var data = (string?)scData["Data"];
                                            var function = (string?)scData["Function"];
                                            if (!string.IsNullOrWhiteSpace(function))
                                            {
                                                switch (function)
                                                {
                                                    case "Transfer()":
                                                        if (!string.IsNullOrWhiteSpace(data))
                                                        {
                                                            var localFromAddress = AccountData.GetSingleAccount(localTransaction.FromAddress);

                                                            var locators = (string?)scData["Locators"];
                                                            var md5List = (string?)scData["MD5List"];
                                                            var scUID = (string?)scData["ContractUID"];

                                                            var transferTask = Task.Run(() => { SmartContractMain.SmartContractData.CreateSmartContract(data); });
                                                            bool isCompletedSuccessfully = transferTask.Wait(TimeSpan.FromMilliseconds(Globals.NFTTimeout * 1000));
                                                            //testing
                                                            //bool isCompletedSuccessfully = true;
                                                            //transferTask.Wait();
                                                            if (!isCompletedSuccessfully)
                                                            {
                                                                NFTLogUtility.Log("Failed to decompile smart contract for transfer in time.", "BlockValidatorService.ValidateBlock() - line 213");
                                                            }
                                                            else
                                                            {
                                                                //download files here.
                                                                if (localFromAddress == null)
                                                                {
                                                                    if (locators != "NA")
                                                                    {
                                                                        var assetList = await MD5Utility.GetAssetList(md5List);
                                                                        var aqResult = AssetQueue.CreateAssetQueueItem(scUID, account.Address, locators, md5List, assetList, AssetQueue.TransferType.Download, true);
                                                                        //await NFTAssetFileUtility.DownloadAssetFromBeacon(scUID, locators, md5List);
                                                                    }

                                                                }
                                                            }
                                                        }
                                                        break;
                                                    case "Evolve()":
                                                        if (!string.IsNullOrWhiteSpace(data))
                                                        {
                                                            var evolveTask = Task.Run(() => { EvolvingFeature.EvolveNFT(localTransaction); });
                                                            bool isCompletedSuccessfully = evolveTask.Wait(TimeSpan.FromMilliseconds(Globals.NFTTimeout * 1000));
                                                            if (!isCompletedSuccessfully)
                                                            {
                                                                NFTLogUtility.Log("Failed to decompile smart contract for evolve in time.", "BlockValidatorService.ValidateBlock() - line 224");
                                                            }
                                                        }
                                                        break;
                                                    case "Devolve()":
                                                        if (!string.IsNullOrWhiteSpace(data))
                                                        {
                                                            var devolveTask = Task.Run(() => { EvolvingFeature.DevolveNFT(localTransaction); });
                                                            bool isCompletedSuccessfully = devolveTask.Wait(TimeSpan.FromMilliseconds(Globals.NFTTimeout * 1000));
                                                            if (!isCompletedSuccessfully)
                                                            {
                                                                NFTLogUtility.Log("Failed to decompile smart contract for devolve in time.", "BlockValidatorService.ValidateBlock() - line 235");
                                                            }
                                                        }
                                                        break;
                                                    case "ChangeEvolveStateSpecific()":
                                                        if (!string.IsNullOrWhiteSpace(data))
                                                        {
                                                            var evoSpecificTask = Task.Run(() => { EvolvingFeature.EvolveToSpecificStateNFT(localTransaction); });
                                                            bool isCompletedSuccessfully = evoSpecificTask.Wait(TimeSpan.FromMilliseconds(Globals.NFTTimeout * 1000));
                                                            if (!isCompletedSuccessfully)
                                                            {
                                                                NFTLogUtility.Log("Failed to decompile smart contract for evo/devo specific in time.", "BlockValidatorService.ValidateBlock() - line 246");
                                                            }
                                                        }
                                                        break;
                                                    default:
                                                        break;
                                                }
                                            }
                                        }

                                        if (localTransaction.TransactionType == TransactionType.ADNR)
                                        {
                                            var scData = JObject.Parse(localTransaction.Data);

                                            if (scData != null)
                                            {
                                                var function = (string?)scData["Function"];
                                                if (!string.IsNullOrWhiteSpace(function))
                                                {
                                                    if (function == "AdnrTransfer()")
                                                    {
                                                        await Account.TransferAdnrToAccount(localTransaction.FromAddress, localTransaction.ToAddress);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }

                                //Adds sent TX to wallet
                                var fromAccount = AccountData.GetAccounts().FindOne(x => x.Address == localTransaction.FromAddress);
                                if (fromAccount != null)
                                {
                                    var txData = TransactionData.GetAll();
                                    var fromTx = localTransaction;
                                    fromTx.Amount = localTransaction.Amount * -1M;
                                    fromTx.Fee = localTransaction.Fee * -1M;
                                    txData.InsertSafe(fromTx);

                                    if (localTransaction.TransactionType != TransactionType.TX)
                                    {
                                        if (localTransaction.TransactionType == TransactionType.NFT_TX)
                                        {
                                            var scDataArray = JsonConvert.DeserializeObject<JArray>(localTransaction.Data);
                                            var scData = scDataArray[0];

                                            //do transfer logic here! This is for person giving away or feature actions
                                            var scUID = (string?)scData["ContractUID"];
                                            var function = (string?)scData["Function"];
                                            
                                            if (!string.IsNullOrWhiteSpace(function))
                                            {
                                                if (function == "Transfer()")
                                                {
                                                    if (!string.IsNullOrWhiteSpace(scUID))
                                                    {
                                                        var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
                                                        var scs = SmartContractMain.SmartContractData.GetSmartContract(scUID);

                                                        if(scs != null)
                                                        {
                                                            if (scs.Features != null)
                                                            {
                                                                if (scs.Features.Exists(x => x.FeatureName == FeatureName.Evolving))
                                                                {
                                                                    if (scStateTreiRec != null)
                                                                    {
                                                                        if (scStateTreiRec.MinterAddress != null)
                                                                        {
                                                                            var evoOwner = AccountData.GetAccounts().FindOne(x => x.Address == scStateTreiRec.MinterAddress);
                                                                            if (evoOwner == null)
                                                                            {
                                                                                SmartContractMain.SmartContractData.DeleteSmartContract(scUID);//deletes locally if they transfer it.
                                                                            }
                                                                        }
                                                                        else
                                                                        {
                                                                            SmartContractMain.SmartContractData.DeleteSmartContract(scUID);//deletes locally if they transfer it.
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        SmartContractMain.SmartContractData.DeleteSmartContract(scUID);//deletes locally if they transfer it.
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    SmartContractMain.SmartContractData.DeleteSmartContract(scUID);//deletes locally if they transfer it.
                                                                }
                                                            }
                                                            else
                                                            {
                                                                SmartContractMain.SmartContractData.DeleteSmartContract(scUID);//deletes locally if they transfer it.
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        if (localTransaction.TransactionType == TransactionType.NFT_BURN)
                                        {
                                            var scDataArray = JsonConvert.DeserializeObject<JArray>(localTransaction.Data);
                                            var scData = scDataArray[0];
                                            //do burn logic here! This is for person giving away or feature actions
                                            var scUID = (string?)scData["ContractUID"];
                                            var function = (string?)scData["Function"];
                                            if (!string.IsNullOrWhiteSpace(function))
                                            {
                                                if (function == "Burn()")
                                                {
                                                    if (!string.IsNullOrWhiteSpace(scUID))
                                                    {
                                                        SmartContractMain.SmartContractData.DeleteSmartContract(scUID);//deletes locally if they burn it.
                                                    }
                                                }
                                            }

                                        }

                                        if (localTransaction.TransactionType == TransactionType.ADNR)
                                        {
                                            var scData = JObject.Parse(localTransaction.Data);

                                            var function = (string?)scData["Function"];
                                            var name = (string?)scData["Name"];
                                            if (!string.IsNullOrWhiteSpace(function))
                                            {
                                                if (function == "AdnrCreate()")
                                                {
                                                    if (!string.IsNullOrWhiteSpace(name))
                                                    {
                                                        await Account.AddAdnrToAccount(localTransaction.FromAddress, name);
                                                    }
                                                }
                                                if (function == "AdnrDelete()")
                                                {
                                                    await Account.DeleteAdnrFromAccount(localTransaction.FromAddress);
                                                }
                                                if (function == "AdnrTransfer()")
                                                {
                                                    await Account.DeleteAdnrFromAccount(localTransaction.FromAddress);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                    }
                    DbContext.Commit();
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
            catch(Exception ex)
            {
                DbContext.Rollback();
                Console.WriteLine($"Error: {ex.Message}");
            }
            return false;
        }

        //This method does not add block or update any treis
        public static async Task<bool> ValidateBlockForTask(Block block, bool blockDownloads = false)
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
