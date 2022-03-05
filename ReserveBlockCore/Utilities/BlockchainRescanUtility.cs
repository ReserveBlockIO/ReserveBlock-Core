using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;

namespace ReserveBlockCore.Utilities
{
    public class BlockchainRescanUtility
    {
        public static bool ValidateBlock(Block block, bool blockDownloads = false)
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
                NextValidators = block.NextValidators
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
                    foreach (Transaction transaction in block.Transactions)
                    {
                        if (transaction.FromAddress != "Coinbase_TrxFees" && transaction.FromAddress != "Coinbase_BlkRwd")
                        {
                            var txResult = TransactionValidatorService.VerifyTX(transaction, blockDownloads);
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
                   
                    //BlockQueueService.UpdateMemBlocks();//update mem blocks
                    //StateData.UpdateTreis(block);

                    //foreach (Transaction transaction in block.Transactions)
                    //{
                    //    var mempool = TransactionData.GetPool();

                    //    var mempoolTx = mempool.FindAll().Where(x => x.Hash == transaction.Hash).FirstOrDefault();
                    //    if (mempoolTx != null)
                    //    {
                    //        mempool.DeleteMany(x => x.Hash == transaction.Hash);
                    //    }

                    //    var account = AccountData.GetAccounts().FindAll().Where(x => x.Address == transaction.ToAddress).FirstOrDefault();
                    //    if (account != null)
                    //    {
                    //        AccountData.UpdateLocalBalanceAdd(transaction.ToAddress, transaction.Amount);
                    //        var txdata = TransactionData.GetAll();
                    //        txdata.Insert(transaction);
                    //    }
                    //}
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



    }
}
