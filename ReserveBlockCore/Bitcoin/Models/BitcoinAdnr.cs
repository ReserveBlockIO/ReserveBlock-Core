using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System.Globalization;
using System.Numerics;

namespace ReserveBlockCore.Bitcoin.Models
{
    public class BitcoinAdnr
    {
        #region Variables
        public long Id { get; set; }
        public string BTCAddress { get; set; }
        public string RBXAddress { get; set; }
        public string Name { get; set; }
        public string TxHash { get; set; }
        public long Timestamp { get; set; }

        #endregion

        #region GetBitcoinAdnr DB
        public static LiteDB.ILiteCollection<BitcoinAdnr>? GetBitcoinAdnr()
        {
            try
            {
                var adnr = DbContext.DB_DNR.GetCollection<BitcoinAdnr>(DbContext.RSRV_BITCOIN_ADNR);
                return adnr;
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.ToString(), "Adnr.GetAdnr()");
                return null;
            }

        }

        #endregion

        #region GetAddress(string name)
        public static (bool, string) GetAddress(string name)
        {
            bool result = false;
            string strResult = "";

            var adnr = GetBitcoinAdnr();
            var adnrExist = adnr.FindOne(x => x.Name == name.ToLower());
            if (adnrExist != null)
            {
                strResult = adnrExist.BTCAddress;
                result = true;
            }

            return (result, strResult);
        }
        #endregion

        #region GetAdnr(string addr)
        public static string? GetAdnr(string addr)
        {
            string? strResult = null;

            var adnr = GetBitcoinAdnr();
            if (adnr != null)
            {
                var adnrExist = adnr.FindOne(x => x.BTCAddress == addr);
                if (adnrExist != null)
                {
                    strResult = adnrExist.Name;
                }
            }
            return strResult;
        }
        #endregion

        #region SaveAdnr(Adnr adnrData)
        public static string SaveAdnr(BitcoinAdnr adnrData)
        {
            var adnr = GetBitcoinAdnr();
            if (adnr == null)
            {
                ErrorLogUtility.LogError("GetAdnr() returned a null value.", "Adnr.GetAdnr()");
            }
            else
            {
                var adnrRecData = adnr.FindOne(x => x.Name == adnrData.Name || x.BTCAddress == adnrData.BTCAddress);
                if (adnrRecData != null)
                {
                    return "Record Already Exist or Address is already associated with adnr.";
                }
                else
                {
                    adnrData.Name = adnrData.Name.ToLower();//save as a lower so when we query later
                    adnr.InsertSafe(adnrData);
                }
            }

            return "Error Saving ADNR";

        }
        #endregion

        #region DeleteAdnr(string address)
        public static void DeleteAdnr(string address)
        {
            var adnr = GetBitcoinAdnr();
            if (adnr == null)
            {
                ErrorLogUtility.LogError("GetAdnr() returned a null value.", "Adnr.GetAdnr()");
            }
            else
            {
                adnr.DeleteManySafe(x => x.BTCAddress == address);
            }
        }
        #endregion

        #region BTCCreateAdnrTx(string address, string name, string btcAddress)
        public static async Task<(Transaction?, string)> CreateAdnrTx(string rbxAddress, string name, string btcAddress)
        {
            Transaction? adnrTx = null;

            var account = AccountData.GetSingleAccount(rbxAddress);
            if (account == null)
            {
                ErrorLogUtility.LogError($"RBX Address is not found for : {rbxAddress}", "BitcoinAdnr.CreateAdnrTx()");
                return (null, $"RBX Address is not found for : {rbxAddress}");
            }

            if(name.ToLower().Contains(".btc"))
                return (null, $"Name may not contain network suffix '.btc'!");

            var txData = "";
            var timestamp = TimeUtil.GetTime();

            BigInteger b1 = BigInteger.Parse(account.GetKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

            var btcAccount = BitcoinAccount.GetBitcoinAccount(btcAddress);
            if(btcAccount == null)
            {
                ErrorLogUtility.LogError($"BTC Address is not found for : {btcAddress}", "BitcoinAdnr.CreateAdnrTx()");
                return (null, $"BTC Address is not found for : {btcAddress}");
            }

            string btcSigMessage = TimeUtil.GetTime().ToString();
            var btcSignature = Services.SignatureService.CreateSignature(btcAccount.PrivateKey, btcSigMessage);
            
            if(btcSignature == "F" )
                return (null, $"Failed to produce signature for : {btcAddress}");

            var btcSignatureValid = Services.SignatureService.VerifySignature(btcSigMessage, btcSignature);

            if( btcSignatureValid == false )
                return (null, $"BTC Signature failed to validate for : {btcAddress}");

            txData = JsonConvert.SerializeObject(new 
            { 
                Function = "BTCAdnrCreate()", 
                Name = name, 
                BTCAddress = btcAddress,
                Signature = btcSignature,
                Message = btcSigMessage
            });

            adnrTx = new Transaction
            {
                Timestamp = timestamp,
                FromAddress = rbxAddress,
                ToAddress = "Adnr_Base",
                Amount = Globals.ADNRRequiredRBX,
                Fee = 0,
                Nonce = AccountStateTrei.GetNextNonce(rbxAddress),
                TransactionType = TransactionType.ADNR,
                Data = txData
            };

            adnrTx.Fee = FeeCalcService.CalculateTXFee(adnrTx);

            adnrTx.Build();

            var txHash = adnrTx.Hash;
            var sig = SignatureService.CreateSignature(txHash, privateKey, account.PublicKey);
            if (sig == "ERROR")
            {
                ErrorLogUtility.LogError($"Signing TX failed for ADNR Request on address {rbxAddress} for name {name}", "Adnr.CreateAdnrTx(string address, string name)");
                return (null, $"Signing TX failed for ADNR Request on address {rbxAddress} for name {name}");
            }

            adnrTx.Signature = sig;

            try
            {
                if (adnrTx.TransactionRating == null)
                {
                    var rating = await TransactionRatingService.GetTransactionRating(adnrTx);
                    adnrTx.TransactionRating = rating;
                }

                var result = await TransactionValidatorService.VerifyTX(adnrTx);
                if (result.Item1 == true)
                {
                    adnrTx.TransactionStatus = TransactionStatus.Pending;

                    if (account.IsValidating == true && (account.Balance - (adnrTx.Fee + adnrTx.Amount) < ValidatorService.ValidatorRequiredAmount()))
                    {
                        var validator = Validators.Validator.GetAll().FindOne(x => x.Address.ToLower() == adnrTx.FromAddress.ToLower());
                        ValidatorService.StopValidating(validator);
                        TransactionData.AddToPool(adnrTx);
                        TransactionData.AddTxToWallet(adnrTx, true);
                        AccountData.UpdateLocalBalance(adnrTx.FromAddress, (adnrTx.Fee + adnrTx.Amount));
                        await P2PClient.SendTXMempool(adnrTx);//send out to mempool
                    }
                    else if (account.IsValidating)
                    {
                        TransactionData.AddToPool(adnrTx);
                        TransactionData.AddTxToWallet(adnrTx, true);
                        AccountData.UpdateLocalBalance(adnrTx.FromAddress, (adnrTx.Fee + adnrTx.Amount));
                        await P2PValidatorClient.SendTXMempool(adnrTx);//send directly to adjs
                    }
                    else
                    {
                        TransactionData.AddToPool(adnrTx);
                        TransactionData.AddTxToWallet(adnrTx, true);
                        AccountData.UpdateLocalBalance(adnrTx.FromAddress, (adnrTx.Fee + adnrTx.Amount));
                        await P2PClient.SendTXMempool(adnrTx);//send out to mempool
                    }

                    return (adnrTx, "Success");
                }
                else
                {
                    ErrorLogUtility.LogError($"Transaction Failed Verify and was not Sent to Mempool. Error: {result.Item2}", "Adnr.CreateAdnrTx(string address, string name)");
                    return (null, $"Transaction Failed Verify and was not Sent to Mempool. Error: {result.Item2}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: {0}", ex.ToString());
            }

            return (null, "Error. Please see message above.");
        }

        #endregion

        #region TransferAdnrTx(string fromAddress, string toAddress)
        public static async Task<(Transaction?, string)> TransferAdnrTx(string fromAddress, string toAddress, string toBtcAddress, string btcFromAddress)
        {
            Transaction? adnrTx = null;

            var account = AccountData.GetSingleAccount(fromAddress);
            if (account == null)
            {
                ErrorLogUtility.LogError($"Address is not found for : {fromAddress}", "BitcoinAdnr.CreateAdnrTx(string address, string name)");
                return (null, $"Address is not found for : {fromAddress}");
            }

            var txData = "";
            var timestamp = TimeUtil.GetTime();

            BigInteger b1 = BigInteger.Parse(account.GetKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

            txData = JsonConvert.SerializeObject(new { Function = "BTCAdnrTransfer()", BTCToAddress = toBtcAddress, BTCFromAddress = btcFromAddress });

            adnrTx = new Transaction
            {
                Timestamp = timestamp,
                FromAddress = fromAddress,
                ToAddress = toAddress,
                Amount = Globals.ADNRRequiredRBX,
                Fee = 0,
                Nonce = AccountStateTrei.GetNextNonce(fromAddress),
                TransactionType = TransactionType.ADNR,
                Data = txData
            };

            adnrTx.Fee = FeeCalcService.CalculateTXFee(adnrTx);

            adnrTx.Build();

            var txHash = adnrTx.Hash;
            var sig = SignatureService.CreateSignature(txHash, privateKey, account.PublicKey);
            if (sig == "ERROR")
            {
                ErrorLogUtility.LogError($"Signing TX failed for ADNR Request on address {fromAddress}", "BitcoinAdnr.TransferAdnrTx(string fromAddress, string toAddress)");
                return (null, $"Signing TX failed for ADNR Request on address {fromAddress}");
            }

            adnrTx.Signature = sig;

            try
            {
                if (adnrTx.TransactionRating == null)
                {
                    var rating = await TransactionRatingService.GetTransactionRating(adnrTx);
                    adnrTx.TransactionRating = rating;
                }

                var result = await TransactionValidatorService.VerifyTX(adnrTx);
                if (result.Item1 == true)
                {
                    adnrTx.TransactionStatus = TransactionStatus.Pending;

                    if (account.IsValidating == true && (account.Balance - (adnrTx.Fee + adnrTx.Amount) < ValidatorService.ValidatorRequiredAmount()))
                    {
                        var validator = Validators.Validator.GetAll().FindOne(x => x.Address.ToLower() == adnrTx.FromAddress.ToLower());
                        ValidatorService.StopValidating(validator);
                        TransactionData.AddToPool(adnrTx);
                        TransactionData.AddTxToWallet(adnrTx, true);
                        AccountData.UpdateLocalBalance(adnrTx.FromAddress, (adnrTx.Fee + adnrTx.Amount));
                        await P2PClient.SendTXMempool(adnrTx);//send out to mempool

                    }
                    else if (account.IsValidating)
                    {
                        TransactionData.AddToPool(adnrTx);
                        TransactionData.AddTxToWallet(adnrTx, true);
                        AccountData.UpdateLocalBalance(adnrTx.FromAddress, (adnrTx.Fee + adnrTx.Amount));
                        await P2PValidatorClient.SendTXMempool(adnrTx);//send directly to adjs
                    }
                    else
                    {
                        TransactionData.AddToPool(adnrTx);
                        TransactionData.AddTxToWallet(adnrTx, true);
                        AccountData.UpdateLocalBalance(adnrTx.FromAddress, (adnrTx.Fee + adnrTx.Amount));
                        await P2PClient.SendTXMempool(adnrTx);//send out to mempool
                    }
                    return (adnrTx, "Success");
                }
                else
                {
                    ErrorLogUtility.LogError($"Transaction Failed Verify and was not Sent to Mempool. Error: {result.Item2}", "BitcoinAdnr.TransferAdnrTx(string fromAddress, string toAddress)");
                    return (null, $"Transaction Failed Verify and was not Sent to Mempool. Error: {result.Item2}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: {0}", ex.ToString());
                ErrorLogUtility.LogError($"Unhandled Error: Message: {ex.ToString()}", "BitcoinAdnr.TransferAdnrTx(string fromAddress, string toAddress)");
            }

            return (null, "Error. Please see message above.");
        }
        #endregion

        #region DeleteAdnrTx(string address)
        public static async Task<(Transaction?, string)> DeleteAdnrTx(string address, string btcFromAddress)
        {
            Transaction? adnrTx = null;

            var account = AccountData.GetSingleAccount(address);
            if (account == null)
            {
                ErrorLogUtility.LogError($"Address is not found for : {address}", "BitcoinAdnr.CreateAdnrTx(string address, string name)");
                return (null, $"Address is not found for : {address}");
            }

            var txData = "";
            var timestamp = TimeUtil.GetTime();

            BigInteger b1 = BigInteger.Parse(account.GetKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

            txData = JsonConvert.SerializeObject(new { Function = "BTCAdnrDelete()", BTCFromAddress = btcFromAddress });

            adnrTx = new Transaction
            {
                Timestamp = timestamp,
                FromAddress = address,
                ToAddress = "Adnr_Base",
                Amount = Globals.ADNRRequiredRBX,
                Fee = 0,
                Nonce = AccountStateTrei.GetNextNonce(address),
                TransactionType = TransactionType.ADNR,
                Data = txData
            };

            adnrTx.Fee = FeeCalcService.CalculateTXFee(adnrTx);

            adnrTx.Build();

            var txHash = adnrTx.Hash;
            var sig = SignatureService.CreateSignature(txHash, privateKey, account.PublicKey);
            if (sig == "ERROR")
            {
                ErrorLogUtility.LogError($"Signing TX failed for ADNR Delete Request on address {address}", "BitcoinAdnr.DeleteAdnrTx(string address)");
                return (null, $"Signing TX failed for ADNR Delete Request on address {address}");
            }

            adnrTx.Signature = sig;

            try
            {
                if (adnrTx.TransactionRating == null)
                {
                    var rating = await TransactionRatingService.GetTransactionRating(adnrTx);
                    adnrTx.TransactionRating = rating;
                }

                var result = await TransactionValidatorService.VerifyTX(adnrTx);
                if (result.Item1 == true)
                {
                    adnrTx.TransactionStatus = TransactionStatus.Pending;

                    if (account.IsValidating == true && (account.Balance - (adnrTx.Fee + adnrTx.Amount) < ValidatorService.ValidatorRequiredAmount()))
                    {
                        var validator = Validators.Validator.GetAll().FindOne(x => x.Address.ToLower() == adnrTx.FromAddress.ToLower());
                        ValidatorService.StopValidating(validator);
                        TransactionData.AddToPool(adnrTx);
                        TransactionData.AddTxToWallet(adnrTx, true);
                        AccountData.UpdateLocalBalance(adnrTx.FromAddress, (adnrTx.Fee + adnrTx.Amount));
                        await P2PClient.SendTXMempool(adnrTx);//send out to mempool
                                                              //await P2PValidatorClient.SendTXMempool(txRequest);
                                                              //add method to send to nearest validators too
                                                              //}
                    }
                    else if (account.IsValidating)
                    {
                        TransactionData.AddToPool(adnrTx);
                        TransactionData.AddTxToWallet(adnrTx, true);
                        AccountData.UpdateLocalBalance(adnrTx.FromAddress, (adnrTx.Fee + adnrTx.Amount));
                        await P2PValidatorClient.SendTXMempool(adnrTx);//send directly to adjs
                    }
                    else
                    {
                        TransactionData.AddToPool(adnrTx);
                        TransactionData.AddTxToWallet(adnrTx, true);
                        AccountData.UpdateLocalBalance(adnrTx.FromAddress, (adnrTx.Fee + adnrTx.Amount));
                        await P2PClient.SendTXMempool(adnrTx);//send out to mempool
                    }
                    return (adnrTx, "Success");
                }
                else
                {
                    ErrorLogUtility.LogError($"Transaction Failed Verify and was not Sent to Mempool. Error: {result.Item2}", "BitcoinAdnr.DeleteAdnrTx(string address)");
                    return (null, $"Transaction Failed Verify and was not Sent to Mempool. Error: {result.Item2}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: {0}", ex.ToString());
            }

            return (null, "Error. Please see message above.");
        }

        #endregion
    }
}
