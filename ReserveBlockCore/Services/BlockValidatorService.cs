using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.Utilities;

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
                    return result;//block rejected due to chainref difference
                }
                //Genesis Block
                result = true;
                BlockchainData.AddBlock(block);
                StateData.UpdateTreis(block);
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
                    BlockQueueService.UpdateMemBlocks();//update mem blocks
                    StateData.UpdateTreis(block);

                    var mempool = TransactionData.GetPool();

                    if(mempool != null)
                    {
                        foreach (Transaction transaction in block.Transactions)
                        {
                            var mempoolTx = mempool.FindAll().Where(x => x.Hash == transaction.Hash);
                            if (mempoolTx.Count() > 0)
                            {
                                mempool.DeleteMany(x => x.Hash == transaction.Hash);
                            }

                            //Adds receiving TX to wallet
                            var account = AccountData.GetAccounts().FindOne(x => x.Address == transaction.ToAddress);
                            if (account != null)
                            {
                                AccountData.UpdateLocalBalanceAdd(transaction.ToAddress, transaction.Amount);
                                var txdata = TransactionData.GetAll();
                                txdata.Insert(transaction);
                                var scDataArray = JsonConvert.DeserializeObject<JArray>(transaction.Data);
                                var scData = scDataArray[0];

                                if (transaction.TransactionType == TransactionType.NFT_MINT)
                                {
                                    //await TransactionValidatorService.AddNewlyMintedContract(transaction);
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
                                if(transaction.TransactionType == TransactionType.NFT_TX)
                                {
                                    var data = (string?)scData["Data"];
                                    var function = (string?)scData["Function"];
                                    if(function != "")
                                    {
                                        if(function == "Transfer()")
                                        {
                                            if (data != "")
                                            {
                                                SmartContractMain.SmartContractData.CreateSmartContract(data);
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
                                fromTx.Amount = 0.0M;
                                fromTx.Fee = transaction.Fee * -1M;
                                txData.Insert(fromTx);

                                var scDataArray = JsonConvert.DeserializeObject<JArray>(transaction.Data);
                                var scData = scDataArray[0];

                                if (transaction.TransactionType == TransactionType.NFT_TX)
                                {
                                    //do transfer logic here! This is for person giving away or feature actions
                                    var scUID = (string?)scData["ContractUID"];
                                    var function = (string?)scData["Function"];
                                    if(function != "")
                                    {
                                        if(function == "Transfer()")
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
                                    if (scUID != "")
                                    {
                                        SmartContractMain.SmartContractData.DeleteSmartContract(scUID);//deletes locally if they burn it.
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
                    return result;//block rejected due to failed validator signature
                }
            }


            //Validates that the block has same chain ref
            if (block.ChainRefId != BlockchainData.ChainRef)
            {
                return result;//block rejected due to chainref difference
            }

            var blockVersion = BlockVersionUtility.GetBlockVersion(block.Height);

            if (block.Version != blockVersion)
            {
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
                return result;//block rejected
            }

            if (!newBlock.MerkleRoot.Equals(block.MerkleRoot))
            {
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

                }

                return result;//block accepted
            

        }

    }
}
