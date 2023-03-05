using ReserveBlockCore.Extensions;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Data.SqlTypes;
using LiteDB;

namespace ReserveBlockCore.Models
{
    public class DecShop
    {
        [BsonId]
        public int Id { get; set; }
        public string UniqueId { get; set; }
        public string Name { get; set; }
        public string DecShopURL { get; set; }
        public string Description { get; set; }
        public DecShopHostingType HostingType { get; set; }
        public string? IP { get; set; } = null;
        public long? BlockHeight { get; set; }
        public long? TXHash { get; set; }
        public string Address { get; set; }
        public bool NeedsPublishToNetwork { get; set; }
        public bool IsOffline { get; set; }

        public static LiteDB.ILiteCollection<DecShop>? DecShopTreiDb()
        {
            try
            {
                var decshops = DbContext.DB_DecShopStateTrei.GetCollection<DecShop>(DbContext.RSRV_DECSHOPSTATE_TREI);
                return decshops;
            }
            catch (Exception ex)
            {                
                ErrorLogUtility.LogError(ex.ToString(), "DecShop.GetDecShops()");
                return null;
            }

        }

        public static LiteDB.ILiteCollection<DecShop>? DecShopLocalDB()
        {
            try
            {
                var decshop = DbContext.DB_Wallet.GetCollection<DecShop>(DbContext.RSRV_DECSHOP);
                return decshop;
            }
            catch (Exception ex)
            {                
                ErrorLogUtility.LogError(ex.ToString(), "DecShop.GetMyDecShop()");
                return null;
            }

        }

        public static DecShop? GetMyDecShopInfo()
        {
            try
            {
                var decshop = DecShopLocalDB();

                if(decshop == null)
                {
                    return null;
                }

                var decshopInfo = decshop.FindAll().FirstOrDefault();
                if (decshopInfo == null)
                {
                    return null;
                }
                return decshopInfo;
            }
            catch (Exception ex)
            {                
                ErrorLogUtility.LogError(ex.ToString(), "DecShop.GetMyDecShopInfo()");
                return null;
            }
        }

        public static async Task<(bool, string)> SaveMyDecShopLocal(DecShop decshop)
        {
            try
            {
                var decshops = DecShopLocalDB();
                if (decshops == null)
                {
                    ErrorLogUtility.LogError("DecShops() returned a null value.", "DecShop.SaveMyDecShopInfo()");
                    return (false, "Failed to call database.");
                }
                else
                {
                    var result = CheckURL(decshop.DecShopURL);
                    if (!result)
                        return (false, "URL does not meet requirements.");

                    var wordCount = decshop.Description.ToWordCountCheck(200);
                    var descLength = decshop.Description.ToLengthCheck(1200);
                    var nameLength = decshop.Name.ToLengthCheck(64);

                    if (!wordCount || !descLength)
                        return (false, $"Failed to insert/update. Description Word Count Allowed: {200}. Description length allowed: {1200}");

                    if (!nameLength)
                        return (false, $"Failed to insert/update. Name length allowed: {64}");

                    decshop.NeedsPublishToNetwork = true;

                    var existingDecShopInfo = decshops.FindAll().FirstOrDefault();
                    if (existingDecShopInfo == null)
                    {
                        var urlvalidCheck = ValidStateTreiURL(decshop.DecShopURL);

                        if (!urlvalidCheck)
                            return (false, "URL is already taken");

                        var timestamp = TimeUtil.GetTime().ToString();
                        decshop.UniqueId = $"{RandomStringUtility.GetRandomStringOnlyLetters(timestamp.Length)}{timestamp}";

                        decshops.InsertSafe(decshop); //inserts new record
                        return (true, $"Decentralized Auction Shop has been created with name {decshop.Name}");
                    }
                    else
                    {
                        if(decshop.DecShopURL != existingDecShopInfo.DecShopURL)
                        {
                            var urlvalidCheck = ValidStateTreiURL(decshop.DecShopURL);

                            if (!urlvalidCheck)
                                return (false, "URL is already taken");
                        }

                        decshops.UpdateSafe(decshop);
                        return (true, $"Decentralized Auction Shop has been updated with name {decshop.Name}");
                    }
                }
            }
            catch(Exception ex)
            {
                ErrorLogUtility.LogError($"Error Saving: {ex.ToString()}", "DecShop.SaveMyDecShopLocal()");
                return (false, $"Unknown Error Saving/Updating Dec Shop. Error: {ex.ToString()}");
            }
            
        }

        public static async Task<(bool,string)> SaveDecShopStateTrei(DecShop decshop)
        {
            try
            {
                var decshops = DecShopTreiDb();
                if (decshops == null)
                {
                    ErrorLogUtility.LogError("DecShops() returned a null value.", "DecShop.SaveDecShopInfo()");
                    return (false, $"DB Error");
                }
                else
                {
                    var existingDecShopInfo = decshops.Query().Where(x => x.UniqueId == decshop.UniqueId).FirstOrDefault();
                    if (existingDecShopInfo == null)
                    {
                        var result = CheckURL(decshop.DecShopURL);
                        if (!result)
                            return (false, "URL does not meet requirements.");

                        var wordCount = decshop.Description.ToWordCountCheck(200);
                        var descLength = decshop.Description.ToLengthCheck(1200);
                        var nameLength = decshop.Name.ToLengthCheck(64);

                        if (!wordCount || !descLength)
                            return (false, $"Failed to insert/update. Description Word Count Allowed: {200}. Description length allowed: {1200}");

                        if (!nameLength)
                            return (false, $"Failed to insert/update. Name length allowed: {64}");

                        decshop.Id = 0;
                        decshops.InsertSafe(decshop); //inserts new record

                        return (true, $"Success");
                    }
                    return (false, $"Account Already Exist");
                }
            }
            catch { return (false, "Unhandled Exception"); }
        }

        public static async Task<(bool,string)> UpdateDecShopStateTrei(DecShop decshop)
        {
            try
            {
                var decshops = DecShopTreiDb();
                if (decshops == null)
                {
                    ErrorLogUtility.LogError("DecShops() returned a null value.", "DecShop.SaveDecShopInfo()");
                    return (false, $"DB Error");
                }
                else
                {
                    var existingDecShopInfo = decshops.Query().Where(x => x.UniqueId == decshop.UniqueId).FirstOrDefault();
                    if (existingDecShopInfo != null)
                    {
                        var urlvalidCheck = ValidStateTreiURL(existingDecShopInfo.DecShopURL);
                        if (urlvalidCheck)
                        {
                            var result = CheckURL(decshop.DecShopURL);
                            if (!result)
                                return (false, "URL does not meet requirements.");

                            var wordCount = decshop.Description.ToWordCountCheck(200);
                            var descLength = decshop.Description.ToLengthCheck(1200);
                            var nameLength = decshop.Name.ToLengthCheck(64);

                            if (!wordCount || !descLength)
                                return (false, $"Failed to insert/update. Description Word Count Allowed: {200}. Description length allowed: {1200}");

                            if (!nameLength)
                                return (false, $"Failed to insert/update. Name length allowed: {64}");
                        }
                        return (false, $"URL already exist");
                    }
                    return (false, $"Trei record does not exist.");
                }
            }
            catch { return (false, "Unhandled Exception"); }
        }

        public static bool? SetDecShopStatus()
        {
            var myDecShop = GetMyDecShopInfo();
            if (myDecShop == null)
            {
                ErrorLogUtility.LogError("GetMyDecShopInfo() returned a null value.", "DecShop.SetDecShopOffline()");
            }
            else
            {
                var decshop = DecShopLocalDB();
                if(decshop != null)
                {
                    myDecShop.IsOffline = !myDecShop.IsOffline;
                    decshop.UpdateSafe(myDecShop);
                    return myDecShop.IsOffline;
                }
            }

            return null;
        }

        public static bool ValidStateTreiURL(string url)
        {
            var output = false;
            var db = DecShopTreiDb();
            var result = db.Query().Where(x => x.DecShopURL == url).FirstOrDefault();

            if (result == null)
                output = true;

            return output;  
        }

        public static async Task<(Transaction?, string)> CreateDecShopTx(DecShop decshop)
        {
            Transaction? decShopTx = null;
            var address = decshop.Address;
            var name = decshop.Name;

            var urlValid = ValidStateTreiURL(decshop.DecShopURL);
            if (!urlValid)
                return (null, "The URL in this TX has already been used. URLs must be unique.");

            var account = AccountData.GetSingleAccount(address);
            if (account == null)
            {
                ErrorLogUtility.LogError($"Address is not found for : {address}", "Adnr.CreateAdnrTx(string address, string name)");
                return (null, $"Address is not found for : {address}");
            }

            var txData = "";
            var timestamp = TimeUtil.GetTime();

            BigInteger b1 = BigInteger.Parse(account.GetKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

            txData = JsonConvert.SerializeObject(new { Function = "DecShopCreate()", DecShop = decshop});

            decShopTx = new Transaction
            {
                Timestamp = TimeUtil.GetTime(),
                FromAddress = address,
                ToAddress = "DecShop_Base",
                Amount = 1.0M,
                Fee = 0,
                Nonce = AccountStateTrei.GetNextNonce(address),
                TransactionType = TransactionType.DSTR,
                Data = txData
            };

            decShopTx.Fee = FeeCalcService.CalculateTXFee(decShopTx);

            decShopTx.Build();

            var txHash = decShopTx.Hash;
            var sig = SignatureService.CreateSignature(txHash, privateKey, account.PublicKey);
            if (sig == "ERROR")
            {
                ErrorLogUtility.LogError($"Signing TX failed for Decentralized Shop Request on address {address} for name {name}", "DecShop.CreateDecShopTx(string address, string name)");
                return (null, $"Signing TX failed for DecShop Request on address {address} for name {name}");
            }

            decShopTx.Signature = sig;

            try
            {
                var result = await TransactionValidatorService.VerifyTX(decShopTx);
                if (result.Item1 == true)
                {
                    TransactionData.AddToPool(decShopTx);
                    AccountData.UpdateLocalBalance(decShopTx.FromAddress, (decShopTx.Fee + decShopTx.Amount));
                    //P2PClient.SendTXMempool(decShopTx);//send out to mempool
                    return (decShopTx, "CHANGE TO HASH!");
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
        private static string GetHash(string address, string name, string signature, long timestamp)
        {
            var data = address + name + signature + timestamp;
            return HashingService.GenerateHash(HashingService.GenerateHash(data));
        }
        public class DecShopTxData
        {
            public string Function { get; set; }
            public DecShop DecShop { get; set; }
        }

        #region Check URL Regex
        private static bool CheckURL(string url)
        {
            bool output = false;

            string pattern = @"^[A-Za-z][a-zA-Z0-9-.]{0,62}\z(?<=[a-zA-Z0-9])*$";
            Regex reg = new Regex(pattern);

            output = reg.IsMatch(url);

            return output;
        }
        #endregion
    }

    public enum DecShopHostingType
    {
        Network,
        PublicBeacon,
        SelfHosted
    }
}
