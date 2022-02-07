using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Models;
using ReserveBlockCore.Utilities;
using System.Globalization;
using System.Numerics;

namespace ReserveBlockCore.Services
{
    public static class WalletService
    {
        public static void StartSend()
        {
            Console.Clear();
            var accountList = AccountData.GetAccountsWithBalance();
            var accountNumberList = new Dictionary<string, Account>();


            if (accountList.Count() > 0)
            {
                int count = 1;
                accountList.ToList().ForEach(x => {
                    accountNumberList.Add(count.ToString(), x);
                    Console.WriteLine("********************************************************************");
                    Console.WriteLine("Please choose an address below by typing its # and pressing enter.");

                    Console.WriteLine("\n #" + count.ToString() );
                    Console.WriteLine("\nAddress :\n{0}", x.Address);
                    Console.WriteLine("\nAccount Balance:\n{0}", x.Balance);
                    count++;
                });

                var walletChoice = Console.ReadLine();
                var wallet = accountNumberList[walletChoice];
                Console.WriteLine("********************************************************************");

                Console.WriteLine("From Address address:");
                string fromAddress = wallet.Address;
                Console.WriteLine(fromAddress);

                Console.WriteLine("\nPlease enter the recipient address!:");
                string toAddress = Console.ReadLine();

                Console.WriteLine("\nPlease enter the amount (number)!:");
                string strAmount = Console.ReadLine();

                if (string.IsNullOrEmpty(fromAddress) ||
                string.IsNullOrEmpty(toAddress) ||
                string.IsNullOrEmpty(strAmount))
                {

                    Console.WriteLine("\n\nError! Please input all fields: sender, recipient, and the amount.\n");
                    return;
                }


                var addrCheck = AddressValidateUtility.ValidateAddress(toAddress);

                if (addrCheck == false)
                {
                    Console.WriteLine("\nError! You have entered an invalid RBX Address!");
                    return;
                }

                decimal amount = new decimal();

                try
                {
                    amount = decimal.Parse(strAmount);
                }
                catch
                {
                    Console.WriteLine("\nError! You have entered an incorrect value for  the amount!");
                    return;
                }

                //RWCjeJ1pcwEqRS9ksgQs3987x78WVYsaFT
                var result = SendTXOut(fromAddress, toAddress, amount);
                Console.WriteLine(result);
            }

            else
            {
                Console.WriteLine("********************************************************************");
                Console.WriteLine("No wallets found with a balance.");
                Console.WriteLine("Returning you to main menu.");
                Thread.Sleep(5000);
                StartupService.MainMenu();
            }
        }
        private static string SendTXOut(string FromAddress, string ToAddress, decimal Amount)
        {
            string output = "Bad TX Format... Please Try Again";
            var account = AccountData.GetSingleAccount(FromAddress);
            if (account == null)
            {
                output = "Bad Account Information or Null Account Info";
                return output;
            }


            var nTx = new Transaction
            {
                Timestamp = TimeUtil.GetTime(),
                FromAddress = FromAddress,
                ToAddress = ToAddress,
                Amount = Amount,
                Fee = 0, //add feel calc method here
                Nonce = AccountStateTrei.GetNextNonce(FromAddress), 
            };
            
            nTx.Build();

            //balance check on funds
            //This will change to state trei.
            var senderBalance = AccountStateTrei.GetAccountBalance(account.Address);
            if ((nTx.Amount + nTx.Fee) > senderBalance)
            {
                output = "Insufficient Funds";
                Console.WriteLine("\nError! Sender ({0}) don't have enough balance!", account.Address);
                Console.WriteLine("Sender ({0}) balance is {1}", account.Address, senderBalance);
                return output;
            }

            BigInteger b1 = BigInteger.Parse(account.PrivateKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

            var txHash = nTx.Hash;
            var signature = TransactionData.CreateSignature(txHash, privateKey, account.PublicKey);
            if (signature == "ERROR")
                return "ERROR! There was an error signing your transaction. Please verify private key belongs to public address.";

            nTx.Signature = signature; //sigScript  = signature + '.' (this is a split char) + pubKey in Base58 format

            try
            {
                var result = VerifyTX(nTx, account);
                if(result == true)
                {
                    output = "Success! TxId: " + txHash;
                }
                else
                {
                    output = "Fail! Transaction Verify has failed. This is due to TX changes since it was hashed.";
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
            }

            return output;
        }

        private static bool VerifyTX(Transaction txRequest, Account account)
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

            if(!newTxn.Hash.Equals(txRequest.Hash))
            {
                return txResult;
            }

            //If we get here that means the hash rest passed above.
            var isTxValid = TransactionData.VerifySignature(txRequest.Hash, txRequest.Signature);
            if(isTxValid)
            {
                txResult = true;
            }
            else
            {
                return txResult;
            }

            //^*************************************************************
            //Needed to be done still!
            //Subtract amount from balance
            TransactionData.AddToPool(txRequest);
            AccountData.UpdateLocalBalance(newTxn.FromAddress, (newTxn.Fee + newTxn.Amount));
            //Show funds pending for incoming address
            //^*************************************************************


            //Return verification result.
            return txResult;

        }
    }
}
