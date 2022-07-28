using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.Utilities;
using System.Text;

namespace ReserveBlockCore.Services
{
    public class BlockValidatorService
    {
        //This is the valid block methods
        public static async Task<bool> ValidateBlock(Block block, bool blockDownloads = false)
        {
            bool result = false;

            if (block == null) return result; //null block submitted. reject 

            if (block.Height == 0)
            {
                if (block.ChainRefId != BlockchainData.ChainRef)
                {
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

                BlockQueueService.UpdateMemBlocks();//update mem blocks
                return result;
            }

            if(block.Height != 0)
            {
                var verifyBlockSig = SignatureService.VerifySignature(block.Validator, block.Hash, block.ValidatorSignature);

                //validates the signature of the validator that crafted the block
                if (verifyBlockSig != true)
                {
                    return result;//block rejected due to failed validator signature
                }
            }

            
            //Validates that the block has same chain ref
            if(block.ChainRefId != BlockchainData.ChainRef)
            {
                return result;//block rejected due to chainref difference
            }

            var blockVersion = BlockVersionUtility.GetBlockVersion(block.Height);

            if(block.Version != blockVersion)
            {
                return result;
            }

            //ensures the timestamps being produced are correct
            if(block.Height != 0)
            {
                var prevTimestamp = Program.LastBlock.Timestamp;
                var currentTimestamp = TimeUtil.GetTime(1);
                if (prevTimestamp > block.Timestamp || block.Timestamp > currentTimestamp)
                {
                    return result;
                }
            }
            

            var newBlock = new Block {
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
                return result;//block rejected
            }

            if(!newBlock.MerkleRoot.Equals(block.MerkleRoot))
            {
                return result;//block rejected
            }

            if(block.Height != 0)
            {
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
                        if(transaction.FromAddress != "Coinbase_TrxFees" && transaction.FromAddress != "Coinbase_BlkRwd")
                        {
                            var txResult = await TransactionValidatorService.VerifyTX(transaction, blockDownloads);
                            rejectBlock = txResult == false ? rejectBlock = true : false;
                        }
                        else
                        {
                            //do nothing as its the coinbase fee
                        }

                        if (rejectBlock)
                            break;
                    }
                    if (rejectBlock)
                        return result;//block rejected due to bad transaction(s)
                
                
                    result = true;
                    BlockchainData.AddBlock(block);//add block to chain.
                    BlockQueueService.UpdateMemBlocks(block);//update mem blocks
                    StateData.UpdateTreis(block);

                    var mempool = TransactionData.GetPool();

                    if(block.Transactions.Count() > 0)
                    {
                        foreach (Transaction transaction in block.Transactions)
                        {
                            if(mempool != null)
                            {
                                var mempoolTx = mempool.FindAll().Where(x => x.Hash == transaction.Hash);
                                if (mempoolTx.Count() > 0)
                                {
                                    mempool.DeleteManySafe(x => x.Hash == transaction.Hash);
                                }
                            }
                            
                            //Adds receiving TX to wallet
                            var account = AccountData.GetAccounts().FindOne(x => x.Address == transaction.ToAddress);
                            if (account != null)
                            {
                                AccountData.UpdateLocalBalanceAdd(transaction.ToAddress, transaction.Amount);
                                var txdata = TransactionData.GetAll();
                                txdata.InsertSafe(transaction);
                                if(Program.IsChainSynced == true)
                                {
                                    //Call out to custom URL from config file with TX details
                                    if(Program.APICallURL != null)
                                    {
                                        APICallURLService.CallURL(transaction);
                                    }
                                }
                                if(transaction.TransactionType != TransactionType.TX)
                                {
                                    var scDataArray = JsonConvert.DeserializeObject<JArray>(transaction.Data);
                                    var scData = scDataArray[0];

                                    if (transaction.TransactionType == TransactionType.NFT_MINT)
                                    {
                                        if (scData != null)
                                        {
                                            var function = (string?)scData["Function"];
                                            if (function != "")
                                            {
                                                if (function == "Mint()")
                                                {
                                                    var scUID = (string?)scData["ContractUID"];
                                                    if (scUID != "")
                                                    {
                                                        SmartContractMain.SmartContractData.SetSmartContractIsPublished(scUID);//flags local SC to isPublished now
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    if (transaction.TransactionType == TransactionType.NFT_TX)
                                    {
                                        var data = (string?)scData["Data"];
                                        var function = (string?)scData["Function"];
                                        if (function != "")
                                        {
                                            switch (function)
                                            {
                                                case "Transfer()":
                                                    if (data != "")
                                                    {
                                                        var locators = (string?)scData["Locators"];
                                                        var md5List = (string?)scData["MD5List"];
                                                        var scUID = (string?)scData["ContractUID"];

                                                        var transferTask = Task.Run(() => { SmartContractMain.SmartContractData.CreateSmartContract(data); });
                                                        bool isCompletedSuccessfully = transferTask.Wait(TimeSpan.FromMilliseconds(Program.NFTTimeout * 1000));
                                                        if(!isCompletedSuccessfully)
                                                        {
                                                            NFTLogUtility.Log("Failed to decompile smart contract for transfer in time.", "BlockValidatorService.ValidateBlock() - line 213");
                                                        }
                                                        else
                                                        {
                                                            //download files here.
                                                            if(locators != "NA")
                                                            {
                                                                await NFTAssetFileUtility.DownloadAssetFromBeacon(scUID, locators, md5List);
                                                            }
                                                            
                                                        }
                                                    }
                                                    break;
                                                case "Evolve()":
                                                    if(data != "")
                                                    {
                                                        var evolveTask = Task.Run(() => { EvolvingFeature.EvolveNFT(transaction); });
                                                        bool isCompletedSuccessfully = evolveTask.Wait(TimeSpan.FromMilliseconds(Program.NFTTimeout * 1000));
                                                        if (!isCompletedSuccessfully)
                                                        {
                                                            NFTLogUtility.Log("Failed to decompile smart contract for evolve in time.", "BlockValidatorService.ValidateBlock() - line 224");
                                                        }
                                                    }
                                                    break;
                                                case "Devolve()":
                                                    if (data != "")
                                                    {
                                                        var devolveTask = Task.Run(() => { EvolvingFeature.DevolveNFT(transaction); });
                                                        bool isCompletedSuccessfully = devolveTask.Wait(TimeSpan.FromMilliseconds(Program.NFTTimeout * 1000));
                                                        if (!isCompletedSuccessfully)
                                                        {
                                                            NFTLogUtility.Log("Failed to decompile smart contract for devolve in time.", "BlockValidatorService.ValidateBlock() - line 235");
                                                        }
                                                    }
                                                    break;
                                                case "ChangeEvolveStateSpecific()":
                                                    if(data != "")
                                                    {
                                                        var evoSpecificTask = Task.Run(() => { EvolvingFeature.EvolveToSpecificStateNFT(transaction); });
                                                        bool isCompletedSuccessfully = evoSpecificTask.Wait(TimeSpan.FromMilliseconds(Program.NFTTimeout * 1000));
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
                                }
                            }

                            //Adds sent TX to wallet
                            var fromAccount = AccountData.GetAccounts().FindOne(x => x.Address == transaction.FromAddress);
                            if (fromAccount != null)
                            {
                                var txData = TransactionData.GetAll();
                                var fromTx = transaction;
                                fromTx.Amount = transaction.Amount * -1M;
                                fromTx.Fee = transaction.Fee * -1M;
                                txData.InsertSafe(fromTx);

                                if(transaction.TransactionType != TransactionType.TX)
                                {
                                    var scDataArray = JsonConvert.DeserializeObject<JArray>(transaction.Data);
                                    var scData = scDataArray[0];

                                    if (transaction.TransactionType == TransactionType.NFT_TX)
                                    {
                                        //do transfer logic here! This is for person giving away or feature actions
                                        var scUID = (string?)scData["ContractUID"];
                                        var function = (string?)scData["Function"];
                                        if (function != "")
                                        {
                                            if (function == "Transfer()")
                                            {
                                                if (scUID != "")
                                                {
                                                    SmartContractMain.SmartContractData.DeleteSmartContract(scUID);//deletes locally if they transfer it.
                                                }
                                            }
                                        }
                                    }
                                    if (transaction.TransactionType == TransactionType.NFT_BURN)
                                    {
                                        //do burn logic here! This is for person giving away or feature actions
                                        var scUID = (string?)scData["ContractUID"];
                                        var function = (string?)scData["Function"];
                                        if (function != "")
                                        {
                                            if (function == "Burn()")
                                            {
                                                if (scUID != "")
                                                {
                                                    SmartContractMain.SmartContractData.DeleteSmartContract(scUID);//deletes locally if they burn it.
                                                }
                                            }
                                        }
            
                                    }
                                }
                            }
                        }
                    }

                }

                return result;//block accepted
            }
            else
            {
                //Genesis Block
                result = true;
                BlockchainData.AddBlock(block);
                StateData.UpdateTreis(block);
                return result;
            }

        }

        //This method does not add block or update any treis
        public static async Task<bool> ValidateBlockForTask(Block block, bool blockDownloads = false)
        {
            bool result = false;

            var badBlocks = BadBlocksUtility.GetBadBlocks();

            if (badBlocks.ContainsKey(block.Height))
            {
                var badBlockHash = badBlocks[block.Height];
                if (badBlockHash == block.Hash)
                {
                    ValidatorLogUtility.Log("Failed to validate block. Block has already been published", "BlockValidatorService.ValidateBlockForTask()");
                    return result;//reject because its on our bad block list
                }
            }

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
                var prevTimestamp = Program.LastBlock.Timestamp;
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
                        if(rejectBlock)
                        {
                            // This can cause a loop if a bad tx is continuously submitted. 
                            // Need to instead remove bad TX from block and reprocess block.
                            // Might need to improve response from this method. Return more detail response as to why Validation failed other than false
                            RemoveTxFromMempool(transaction);
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
