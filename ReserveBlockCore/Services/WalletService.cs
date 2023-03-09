using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Utilities;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace ReserveBlockCore.Services
{
    public static class WalletService
    {
        public static async Task StartSend()
        {
            try
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

                        Console.WriteLine("\n #" + count.ToString());
                        Console.WriteLine("\nAddress :\n{0}", x.Address);
                        Console.WriteLine("\nAccount Balance:\n{0}", x.Balance);
                        count++;
                    });
                    string? walletChoice = "";
                    walletChoice = await ReadLineUtility.ReadLine();
                    while (string.IsNullOrEmpty(walletChoice))
                    {
                        Console.WriteLine("Entry not recognized. Please try it again. Sorry for trouble!");
                        walletChoice = await ReadLineUtility.ReadLine();
                    }
                    var wallet = accountNumberList[walletChoice];
                    Console.WriteLine("********************************************************************");

                    Console.WriteLine("From Address address:");
                    string fromAddress = wallet.Address;
                    Console.WriteLine(fromAddress);

                    Console.WriteLine("\nPlease enter the recipient address!:");
                    string? toAddress = await ReadLineUtility.ReadLine();

                    Console.WriteLine("\nPlease enter the amount (number)!:");
                    string? strAmount = await ReadLineUtility.ReadLine();

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
                    var result = await SendTXOut(fromAddress, toAddress, amount);
                    Console.WriteLine(result);
                }

                else
                {
                    StartupService.MainMenu();
                    Console.WriteLine("No wallets found with a balance.");
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            
        }
        public static async Task<string> SendTXOut(string FromAddress, string ToAddress, decimal Amount, TransactionType tranType = TransactionType.TX)
        {
            string output = "Bad TX Format... Please Try Again";
            var account = AccountData.GetSingleAccount(FromAddress);
            if (account == null)
            {
                output = "Bad Account Information or Null Account Info";
                return output;
            }

            ToAddress = ToAddress.ToAddressNormalize();

            //var adnrCheck = ToAddress.ToLower().EndsWith(".rbx");

            //if (adnrCheck)
            //{
            //    var result = Adnr.GetAddress(ToAddress);
            //    if(result.Item1 == true)
            //    {
            //        ToAddress = result.Item2;
            //    }
            //    else
            //    {
            //        return "Address not recognized or is not stored in the ADNR state trei.";
            //    }
            //}

            var nTx = new Transaction
            {
                Timestamp = TimeUtil.GetTime(),
                FromAddress = FromAddress,
                ToAddress = ToAddress,
                Amount = Amount + 0.0M,
                Fee = 0, 
                Nonce = AccountStateTrei.GetNextNonce(FromAddress), 
                TransactionType = tranType,
            };

            //Calculate fee for tx.
            nTx.Fee = FeeCalcService.CalculateTXFee(nTx);

            nTx.Build();

            //balance check on funds
            //This will change to state trei.
            var senderBalance = AccountStateTrei.GetAccountBalance(account.Address);
            if ((nTx.Amount + nTx.Fee) > senderBalance)
            {
                output = "Insufficient Funds. You have: " + senderBalance.ToString() + " RBX on the network and the total was: " + (nTx.Amount + nTx.Fee).ToString() + " RBX";
                Console.WriteLine("\nError! Sender ({0}) don't have enough balance!", account.Address);
                Console.WriteLine("Sender ({0}) balance is {1}", account.Address, senderBalance);
                return output;
            }

            BigInteger b1 = BigInteger.Parse(account.GetKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

            var txHash = nTx.Hash;
            var signature = SignatureService.CreateSignature(txHash, privateKey, account.PublicKey);
            if (signature == "ERROR")
            {
                return "ERROR! There was an error signing your transaction. Please verify private key belongs to public address.";
            }

            nTx.Signature = signature; //sigScript  = signature + '.' (this is a split char) + pubKey in Base58 format


            try
            {
                var result = await VerifyTX(nTx, account);
                if(result == true)
                {
                    output = "Success! TxId: " + txHash;
                }
                else
                {
                    output = "Fail! Transaction Verify has failed.";
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine("Error: {0}", ex.ToString());
            }

            return output;
        }

        private static async Task<bool> VerifyTX(Transaction txRequest, Account account)
        {
            bool txResult = false;

            var txJsonSize = JsonConvert.SerializeObject(txRequest);
            var size = txJsonSize.Length;

            if (size > (1024 * 3))
            {
                return txResult;
            }

            var newTxn = new Transaction()
            {
                Timestamp = txRequest.Timestamp,
                FromAddress = txRequest.FromAddress,
                ToAddress = txRequest.ToAddress,
                Amount = txRequest.Amount,
                Fee = txRequest.Fee,
                Nonce = txRequest.Nonce,
                Data = txRequest.Data,
            };

            newTxn.Build();

            if(!newTxn.Hash.Equals(txRequest.Hash))
            {
                return txResult;
            }
            
            var txExist = Globals.MemBlocks.ContainsKey(txRequest.Hash);
            if (txExist)
            {
                return txResult;
            }

            //If we get here that means the hash test passed above.
            var isTxValid = SignatureService.VerifySignature(txRequest.FromAddress, txRequest.Hash, txRequest.Signature);
            if(isTxValid)
            {
                txResult = true;
            }
            else
            {
                return txResult;
            }

            if(txRequest.TransactionRating == null)
            {
                var rating = await TransactionRatingService.GetTransactionRating(txRequest);
                txRequest.TransactionRating = rating;
            }

            txRequest.TransactionStatus = TransactionStatus.Pending;

            if (account.IsValidating == true && (account.Balance - (newTxn.Fee + newTxn.Amount) < 1000))
            {
                var validator = Validators.Validator.GetAll().FindOne(x => x.Address.ToLower() == newTxn.FromAddress.ToLower());
                ValidatorService.StopValidating(validator);
                TransactionData.AddToPool(txRequest);
                TransactionData.AddTxToWallet(txRequest, true);
                AccountData.UpdateLocalBalance(newTxn.FromAddress, (newTxn.Fee + newTxn.Amount));
                await P2PClient.SendTXMempool(txRequest);//send out to mempool
            }
            else if(account.IsValidating)
            {
                TransactionData.AddToPool(txRequest);
                TransactionData.AddTxToWallet(txRequest, true);
                AccountData.UpdateLocalBalance(newTxn.FromAddress, (newTxn.Fee + newTxn.Amount));
                await P2PClient.SendTXToAdjudicator(txRequest);//send directly to adjs
            }
            else
            {
                TransactionData.AddToPool(txRequest);
                TransactionData.AddTxToWallet(txRequest, true);
                AccountData.UpdateLocalBalance(newTxn.FromAddress, (newTxn.Fee + newTxn.Amount));
                await P2PClient.SendTXMempool(txRequest);//send out to mempool
            }

            

            //Return verification result.
            return txResult;

        }

        //Needs refactor to use maximum peer spread.
        public static async Task SendTransaction(Transaction txRequest, Account account)
        {
            if (account.IsValidating == true && (account.Balance - (txRequest.Fee + txRequest.Amount) < 1000))
            {
                var validator = Validators.Validator.GetAll().FindOne(x => x.Address.ToLower() == txRequest.FromAddress.ToLower());
                ValidatorService.StopValidating(validator);
                TransactionData.AddToPool(txRequest);
                TransactionData.AddTxToWallet(txRequest, true);
                AccountData.UpdateLocalBalance(txRequest.FromAddress, (txRequest.Fee + txRequest.Amount));
                await P2PClient.SendTXMempool(txRequest);//send out to mempool
            }
            else if (account.IsValidating)
            {
                TransactionData.AddToPool(txRequest);
                TransactionData.AddTxToWallet(txRequest, true);
                AccountData.UpdateLocalBalance(txRequest.FromAddress, (txRequest.Fee + txRequest.Amount));
                await P2PClient.SendTXToAdjudicator(txRequest);//send directly to adjs
            }
            else
            {
                TransactionData.AddToPool(txRequest);
                TransactionData.AddTxToWallet(txRequest, true);
                AccountData.UpdateLocalBalance(txRequest.FromAddress, (txRequest.Fee + txRequest.Amount));
                await P2PClient.SendTXMempool(txRequest);//send out to mempool
            }
        }
    }
}
