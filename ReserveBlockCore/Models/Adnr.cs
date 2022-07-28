using ReserveBlockCore.Extensions;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System.Globalization;
using System.Numerics;

namespace ReserveBlockCore.Models
{
    public class Adnr
    {
        public int Id { get; set; }
        public string Address { get; set; }
        public string Name { get; set; }
        public string TxHash { get; set; }
        public long Timestamp { get; set; }

        public static LiteDB.ILiteCollection<Adnr>? GetAdnr()
        {
            try
            {
                var adnr = DbContext.DB_DNR.GetCollection<Adnr>(DbContext.RSRV_DNR);
                return adnr;
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.Message, "Adnr.GetAdnr()");
                return null;
            }

        }

        public static (bool, string) GetAddress(string addr)
        {
            bool result = false;
            string strResult = "";

            var adnr = GetAdnr();
            var adnrExist = adnr.FindOne(x => x.Name == addr);
            if (adnrExist != null)
            {
                strResult = adnrExist.Address;
                result = true;
            }

            return (result, strResult);
        }

        public static string SaveAdnr(Adnr adnrData)
        {
            var adnr = GetAdnr();
            if (adnr == null)
            {
                ErrorLogUtility.LogError("GetAdnr() returned a null value.", "Adnr.GetAdnr()");
            }
            else
            {
                var adnrRecData = adnr.FindOne(x => x.Name == adnrData.Name || x.Address == adnrData.Address);
                if (adnrRecData != null)
                {
                    return "Record Already Exist or Address is already associated with adnr.";
                }
                else
                {
                    adnr.InsertSafe(adnrData);
                }
            }

            return "Error Saving Beacon Data";

        }
        //need signature check here.
        public static void DeleteAssets(string name)
        {
            var adnr = GetAdnr();
            if (adnr == null)
            {
                ErrorLogUtility.LogError("GetAdnr() returned a null value.", "Adnr.GetAdnr()");
            }
            else
            {
                adnr.DeleteManySafe(x => x.Name == name);
            }
        }

        public static async Task<(Transaction?, string)> CreateAdnrTx(string address, string name)
        {
            Transaction? adnrTx = null;

            var account = AccountData.GetSingleAccount(address);
            if (account == null)
            {
                ErrorLogUtility.LogError($"Address is not found for : {address}", "Adnr.CreateAdnrTx(string address, string name)");
                return (null, $"Address is not found for : {address}");
            }

            var txData = "";
            var timestamp = TimeUtil.GetTime();

            BigInteger b1 = BigInteger.Parse(account.PrivateKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

            txData = JsonConvert.SerializeObject(new { Function = "AdnrCreate()", Name = name });

            adnrTx = new Transaction
            {
                Timestamp = timestamp,
                FromAddress = address,
                ToAddress = "Adnr_Base",
                Amount = 1.0M,
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
                ErrorLogUtility.LogError($"Signing TX failed for ADNR Request on address {address} for name {name}", "Adnr.CreateAdnrTx(string address, string name)");
                return (null, $"Signing TX failed for ADNR Request on address {address} for name {name}");
            }

            adnrTx.Signature = sig;

            try
            {
                var result = await TransactionValidatorService.VerifyTXDetailed(adnrTx);
                if (result.Item1 == true)
                {
                    TransactionData.AddToPool(adnrTx);
                    AccountData.UpdateLocalBalance(adnrTx.FromAddress, (adnrTx.Fee + adnrTx.Amount));
                    P2PClient.SendTXMempool(adnrTx);//send out to mempool
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
                Console.WriteLine("Error: {0}", ex.Message);
            }

            return (null, "Error. Please see message above.");
        }

        //public static async Task<(Transaction?, string)> TransferAdnrTx(string address, string name)
        //{
        //}

        //public static async Task<(Transaction?, string)> DeleteAdnrTx(string address, string name)
        //{
        //}

    }
    
}
