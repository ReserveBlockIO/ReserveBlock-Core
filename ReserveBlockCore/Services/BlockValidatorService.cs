using ReserveBlockCore.Data;
using ReserveBlockCore.Models;

namespace ReserveBlockCore.Services
{
    public class BlockValidatorService
    {
        public static bool ValidateBlock(Block block)
        {
            bool result = false;

            var newBlock = new Block{
                Height = block.Height,
                Timestamp = block.Timestamp,
                Transactions = block.Transactions,
                Validator = block.Validator,
                ChainRefId = block.ChainRefId
            };

            newBlock.Build();

            if (!newBlock.Hash.Equals(block.Hash))
            {
                return result;//block rejected
            }

            if(block.Height != 0)
            {
                var blockCoinBaseResult = BlockchainData.ValidateBlock(block);

                if (blockCoinBaseResult == false)
                    return result;//block rejected

                if (block.Transactions.Count() > 0)
                {
                    //validate transactions.
                    bool rejectBlock = false;
                    foreach (Transaction transaction in block.Transactions)
                    {
                        var txResult = VerifyTX(transaction);
                        rejectBlock = txResult == false ? rejectBlock = true : false;

                        if (rejectBlock)
                            break;
                    }
                    if (rejectBlock)
                        return result;//block rejected
                }
                else
                {
                    result = true;
                    BlockchainData.AddBlock(block);//add block to chain.
                                                   //need to remove TX's from mempool if they are still there.
                    StateData.UpdateTreis(block); //this updates account trei and world trei.
                }

                return result;//block accepted
            }
            else
            {
                result = true;
                BlockchainData.AddBlock(block);
                StateData.UpdateTreis(block);
                return result;
            }
            //Need to add validator validation method.



            
        }

        private static bool VerifyTX(Transaction txRequest)
        {
            bool txResult = false;

            var newTxn = new Transaction()
            {
                Timestamp = txRequest.Timestamp,
                FromAddress = txRequest.FromAddress,
                ToAddress = txRequest.ToAddress,
                Amount = txRequest.Amount,
                Fee = txRequest.Fee,
                Nonce = txRequest.Nonce,
            };

            newTxn.Build();

            if (!newTxn.Hash.Equals(txRequest.Hash))
            {
                return txResult;
            }

            //If we get here that means the hash test passed above.
            var isTxValid = SignatureService.VerifySignature(txRequest.FromAddress, txRequest.Hash, txRequest.Signature);
            if (isTxValid)
            {
                txResult = true;
            }
            else
            {
                return txResult;
            }

            //Return verification result.
            return txResult;

        }

    }
}
