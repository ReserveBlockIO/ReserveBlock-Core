using LiteDB;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;
using System.Data.Common;
using System.Transactions;

namespace ReserveBlockCore.Utilities
{
    public class BlockchainRescanUtility
    {
        public static async Task<bool> ValidateBlock(Block block, bool blockDownloads = false)
        {
            bool result = false;

            if (block == null) return result; //null block submitted. reject 

            if (block.Height == 0)
            {
                //Genesis Block
                result = true;
                //StateData.UpdateTreis(block);
                return result;
            }

            var verifyBlockSig = SignatureService.VerifySignature(block.Validator, block.Hash, block.ValidatorSignature);

            //validates the signature of the validator that crafted the block
            if (verifyBlockSig != true)
            {
                return result;//block rejected due to failed validator signature
            }
            //Validates that the block has same chain ref
            if (block.ChainRefId != BlockchainData.ChainRef)
            {
                return result;//block rejected due to chainref difference
            }

            var newBlock = new Block
            {
                Height = block.Height,
                Timestamp = block.Timestamp,
                Transactions = block.Transactions,
                Validator = block.Validator,
                ChainRefId = block.ChainRefId,
            };

            newBlock.Rebuild(block);

            //This will also check that the prev hash matches too
            if (!newBlock.Hash.Equals(block.Hash))
            {
                return result;//block rejected
            }

            if (!newBlock.MerkleRoot.Equals(block.MerkleRoot))
            {
                return result;//block rejected
            }

            if (block.Height != 0)
            {
                var blockCoinBaseResult = BlockchainData.ValidateBlock(block); //this checks the coinbase tx

                //Need to check here the prev hash if it is correct!

                if (blockCoinBaseResult == false)
                    return result;//block rejected

                if (block.Transactions.Count() > 0)
                {
                    //validate transactions.
                    bool rejectBlock = false;
                    foreach (Models.Transaction transaction in block.Transactions)
                    {
                        if (transaction.FromAddress != "Coinbase_TrxFees" && transaction.FromAddress != "Coinbase_BlkRwd")
                        {
                            var txResult = await TransactionValidatorService.VerifyTX(transaction, blockDownloads);
                            rejectBlock = txResult.Item1 == false ? rejectBlock = true : false;
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
            else
            {
                //Genesis Block
                result = true;
                //StateData.UpdateTreis(block);
                return result;
            }
            //Need to add validator validation method.

        }

        public static async Task RescanForTransactions(string address)
        {
            var blocks = BlockchainData.GetBlocks();
            var height = Convert.ToInt32(Globals.LastBlock.Height);

            LogUtility.Log($"Scanning started for address: {address} up to height: {height}", "BlockchainRescanUtility.RescanForTransactions()-1");

            var integerList = Enumerable.Range(0, height + 1);
            Parallel.ForEach(integerList, new ParallelOptions { MaxDegreeOfParallelism = 4 }, (blockHeight, loopState) =>
            {
                var txs = TransactionData.GetAll();
                var block = blocks.Query().Where(x => x.Height == blockHeight).FirstOrDefault();
                if(block != null) 
                {
                    var fromTxs = block.Transactions.Where(from => from.FromAddress == address).ToList();
                    var toTxs = block.Transactions.Where(to => to.ToAddress == address).ToList(); 

                    if(fromTxs.Count > 0)
                    {
                        foreach(var tx in fromTxs)
                        {
                            tx.Amount = tx.Amount * -1.0M;
                            tx.Fee = tx.Fee * -1.0M;
                            tx.TransactionStatus = Models.TransactionStatus.Success;
                            var txCheck = txs.FindOne(x => x.Hash == tx.Hash);
                            if (txCheck == null)
                            {
                                txs.InsertSafe(tx);
                            }
                        }
                    }

                    if(toTxs.Count > 0)
                    {
                        foreach (var tx in toTxs)
                        {
                            var txCheck = txs.FindOne(x => x.Hash == tx.Hash);
                            if (txCheck == null)
                            {
                                tx.TransactionStatus = Models.TransactionStatus.Success;
                                txs.InsertSafe(tx);
                            }
                        }
                    }
                }
            });
            LogUtility.Log($"Scanning completed for address: {address}", "BlockchainRescanUtility.RescanForTransactions()-2");
        }

    }
}
