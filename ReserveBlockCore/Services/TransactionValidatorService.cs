using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Services
{
    public class TransactionValidatorService
    {
        public static bool VerifyTX(Transaction txRequest, bool blockDownloads = false)
        {
            bool txResult = false;

            var accStTrei = StateData.GetAccountStateTrei();
            var from = StateData.GetSpecificAccountStateTrei(txRequest.FromAddress);

            //Balance Check
            if(from == null)
            {
                //They may also just need the block that contains this TX.
                //We might want to queue a block check and download.
                return txResult;
            }
            else
            {
                if(from.Balance < (txRequest.Amount + txRequest.Fee))
                {
                    return txResult;//balance was less than the amount they are trying to send.
                }
            }

            //Prev Tx in Block Check - this is to prevent someone sending a signed TX again
            var memBlocksTxs = Program.MemBlocks.SelectMany(x => x.Transactions).ToList();
            var txExist = memBlocksTxs.Exists(x => x.Hash == txRequest.Hash);
            if(txExist)
            {
                return txResult;
            }

            //Timestamp Check
            if(!blockDownloads)
            {
                var currentTime = TimeUtil.GetTime();
                var timeDiff = currentTime - txRequest.Timestamp;
                var minuteDiff = timeDiff / 60M;

                if (minuteDiff > 5.0M)
                {
                    return txResult;
                }
            }
            

            //Hash Check
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

            //Signature Check - Final Check to return true.
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

        private static bool VerifyLocalTX(Transaction txRequest, Account account)
        {
            bool txResult = false;

            //Nonce Check

            //Prev Tx in Block Check

            //Timestamp Check

            //Hash Check
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

            if (account.IsValidating == true && (account.Balance - (newTxn.Fee + newTxn.Amount) < 1000))
            {
                Console.WriteLine("This transaction will deactivate your masternode. Are you sure you want to deactivate this address as a validator? (Type 'y' for yes and 'n' for no.)");
                var confirmChoice = Console.ReadLine();
                if (confirmChoice == null)
                {
                    return false;
                }
                else if (confirmChoice.ToLower() == "n")
                {
                    return false;
                }
                else
                {
                    var validator = Validators.Validator.GetAll().FindOne(x => x.Address.ToLower() == newTxn.FromAddress.ToLower() && x.NodeIP == "SELF");
                    ValidatorService.StopValidating(validator);
                    TransactionData.AddToPool(txRequest);
                    TransactionData.AddTxToWallet(txRequest);
                    AccountData.UpdateLocalBalance(newTxn.FromAddress, (newTxn.Fee + newTxn.Amount));
                    //StateData.UpdateAccountNonce(txRequest.FromAddress);
                    P2PClient.SendTXMempool(txRequest);//send out to mempool
                }
            }
            else
            {
                TransactionData.AddToPool(txRequest);
                AccountData.UpdateLocalBalance(newTxn.FromAddress, (newTxn.Fee + newTxn.Amount));
                //StateData.UpdateAccountNonce(txRequest.FromAddress);
                P2PClient.SendTXMempool(txRequest);//send out to mempool
            }



            //Return verification result.
            return txResult;

        }
    }
}
