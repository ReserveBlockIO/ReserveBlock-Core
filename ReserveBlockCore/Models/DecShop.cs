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
using System.Net;
using Trillium.Syntax;
using ReserveBlockCore.DST;
using ReserveBlockCore.Engines;
using ReserveBlockCore.Models.DST;

namespace ReserveBlockCore.Models
{
    public class DecShop
    {
        #region Class Variables
        [BsonId]
        public int Id { get; set; }
        public string UniqueId { get; set; }
        public string Name { get; set; } //User Submitted - 64 length
        public string DecShopURL { get; set; } //User Submitted - 64 length - Do not add rbx://
        public string? ThirdPartyBaseURL { get; set; }
        public string? ThirdPartyAPIURL { get; set; }
        public string Description { get; set; } //User Submitted - 200 words, or 1200 in length
        public string OwnerAddress { get; set; } //User Submitted -starts with R
        public DecShopHostingType HostingType { get; set; } //User Submitted
        public string IP { get; set; } //User Submitted if HostingType  == SelfHosted - 32 length
        public int Port { get; set; } //User Submitted if HostingType  == SelfHosted
        public int STUNServerGroup { get; set; }
        public long OriginalBlockHeight { get; set; }
        public string? OriginalTXHash { get; set; } = null;
        public long LatestBlockHeight { get; set; }
        public string? LatestTXHash { get; set; } = null;
        public long UpdateTimestamp { get; set; }
        public bool AutoUpdateNetworkDNS { get; set; } //User Submitted - recommend defaulting to true
        public bool NeedsPublishToNetwork { get; set; }
        public bool IsOffline { get; set; }
        public bool IsPublished { get; set; }
        public int CollectionCount { get; set; } = Collection.GetLiveCollections();
        public int ListingCount { get; set; } = Listing.GetLiveListingsCount();
        public int AuctionCount { get; set; } = Auction.GetLiveAuctionsCount();
        public bool IsIPDifferent { get { return P2PClient.MostLikelyIP() == IP ? false : true; } }

        public class DecShopTxData
        {
            public string Function { get; set; }
            public DecShop DecShop { get; set; }
        }

        #endregion

        #region Build
        public (bool, string) Build()
        {
            var timestamp = TimeUtil.GetTime().ToString();
            UniqueId = $"{RandomStringUtility.GetRandomStringOnlyLetters(timestamp.Length)}{timestamp}";

            if (HostingType == DecShopHostingType.Network)
            {
                IP = P2PClient.MostLikelyIP();

                if(IP == "NA")
                {
                    return (false, "Could not find IP automatically.");
                }

                Port = Globals.DSTClientPort;
            }

            if (DecShopURL.ToLower().Contains("rbx://"))
                return (false, "Please do not include 'rbx://' in your URL. It is automatically added.");

            DecShopURL = $"rbx://{DecShopURL}";

            Random rnd = new Random();
            var groupNum = Globals.IsTestNet ? 1 : rnd.Next(1, 6);

            STUNServerGroup = groupNum;

            return (true, "");
        }

        #endregion

        #region DecShop State Trei DB
        public static ILiteCollection<DecShop>? DecShopTreiDb()
        {
            try
            {
                var decshops = DbContext.DB_DecShopStateTrei.GetCollection<DecShop>(DbContext.RSRV_DECSHOPSTATE_TREI);
                return decshops;
            }
            catch (Exception ex)
            {                
                ErrorLogUtility.LogError(ex.ToString(), "DecShop.DecShopTreiDb()");
                return null;
            }

        }

        #endregion

        #region DecShop Local DB
        public static ILiteCollection<DecShop>? DecShopLocalDB()
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

        #endregion

        #region Get Local DecShop Info
        public static DecShop? GetMyDecShopInfo()
        {
            try
            {
                var decshop = DecShopLocalDB();

                if (decshop == null)
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

        #endregion

        #region Get DecShop State Trei Leaf by UID
        public static DecShop? GetDecShopStateTreiLeaf(string dsUID)
        {
            var dstDB = DecShopTreiDb();
            
            if (dstDB != null)
            {
                var rec = dstDB.Query().Where(x => x.UniqueId == dsUID).FirstOrDefault();
                return rec;
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Get DecShop State Trei Leaf By URL
        public static async Task<DecShop?> GetDecShopStateTreiLeafByURL(string url)
        {
            var dstDB = DecShopTreiDb();
            if (dstDB != null)
            {
                var rec = dstDB.Query().Where(x => x.DecShopURL.ToLower() == url.ToLower()).FirstOrDefault();
                return rec;
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Save/Update Local DecShop
        public static async Task<(bool, string)> SaveMyDecShopLocal(DecShop decshop, bool needsPublish = true, bool isImport = false)
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
                    var urlLength = decshop.DecShopURL.ToLengthCheck(64);
                    var ipLength = decshop.IP.ToLengthCheck(32);

                    if (!wordCount || !descLength)
                        return (false, $"Failed to insert/update. Description Word Count Allowed: {200}. Description length allowed: {1200}");

                    if (!nameLength)
                        return (false, $"Failed to insert/update. Name length allowed: {64}");

                    if (!urlLength)
                        return (false, $"Failed to insert/update. URL length allowed: {64}");

                    if (!ipLength)
                        return (false, $"Failed to insert/update. IP length allowed: {64}");

                    decshop.NeedsPublishToNetwork = needsPublish;

                    var existingDecShopInfo = decshops.FindAll().FirstOrDefault();
                    if (existingDecShopInfo == null)
                    {

                        if (!isImport)
                        {
                            var urlvalidCheck = ValidStateTreiURL(decshop.DecShopURL);

                            if (!urlvalidCheck)
                                return (false, "URL is already taken");
                        }

                        decshops.InsertSafe(decshop); //inserts new record
                        return (true, $"Decentralized Auction Shop has been created with name {decshop.Name}");
                    }
                    else
                    {

                        if (decshop.DecShopURL != existingDecShopInfo.DecShopURL)
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
            catch (Exception ex)
            {
                ErrorLogUtility.LogError($"Error Saving: {ex.ToString()}", "DecShop.SaveMyDecShopLocal()");
                return (false, $"Unknown Error Saving/Updating Dec Shop. Error: {ex.ToString()}");
            }
        }

        #endregion

        #region Save DecShop State Trei Leaf
        public static (bool, string) SaveDecShopStateTrei(DecShop decshop)
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

        #endregion

        #region Update DecShop State Trei Leaf
        public static (bool, string) UpdateDecShopStateTrei(DecShop decshop)
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
                        if (existingDecShopInfo.DecShopURL != decshop.DecShopURL)
                        {
                            var urlvalidCheck = ValidStateTreiURL(decshop.DecShopURL);
                            if (!urlvalidCheck)
                                return (false, $"URL already exist");
                        }

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

                        decshop.OriginalBlockHeight = existingDecShopInfo.OriginalBlockHeight;
                        decshop.OriginalTXHash = existingDecShopInfo.OriginalTXHash;
                        decshop.Id = existingDecShopInfo.Id;

                        decshops.UpdateSafe(decshop); //inserts new record

                        return (true, $"Success");
                    }
                    return (false, $"Trei record does not exist.");
                }
            }
            catch { return (false, "Unhandled Exception"); }
        }

        #endregion

        #region Set DecShop Status
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
                if (decshop != null)
                {
                    myDecShop.IsOffline = !myDecShop.IsOffline;
                    if (myDecShop.IsOffline)
                    {
                        AuctionEngine.StopBidProcessing();
                    }
                    else
                    {
                        _ = AuctionEngine.StartBidProcessing();
                    }
                    decshop.UpdateSafe(myDecShop);
                    return myDecShop.IsOffline;
                }
            }

            return null;
        }

        #endregion

        #region Validate URL Unique against State Trei
        public static bool ValidStateTreiURL(string url)
        {
            var output = false;
            var db = DecShopTreiDb();
            var result = db.Query().Where(x => x.DecShopURL == url).FirstOrDefault();

            if (result == null)
                output = true;

            return output;
        }

        #endregion

        #region Create DecShop TX
        public static async Task<(Transaction?, string)> CreateDecShopTx(DecShop decshop)
        {
            Transaction? decShopTx = null;
            var address = decshop.OwnerAddress;
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

            txData = JsonConvert.SerializeObject(new { Function = "DecShopCreate()", DecShop = decshop });

            decShopTx = new Transaction
            {
                Timestamp = TimeUtil.GetTime(),
                FromAddress = address,
                ToAddress = "DecShop_Base",
                Amount = Globals.DecShopRequiredRBX,
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
                if (decShopTx.TransactionRating == null)
                {
                    var rating = await TransactionRatingService.GetTransactionRating(decShopTx, true);
                    decShopTx.TransactionRating = rating;
                }

                var result = await TransactionValidatorService.VerifyTX(decShopTx);
                if (result.Item1 == true)
                {
                    decShopTx.TransactionStatus = TransactionStatus.Pending;

                    await WalletService.SendTransaction(decShopTx, account);

                    return (decShopTx, "");
                }
                else
                {
                    ErrorLogUtility.LogError($"Transaction Failed Verify and was not Sent to Mempool. Error: {result.Item2}", "DecShop.CreateDecShopTx()");
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

        #region Update DecShop TX
        public static async Task<(Transaction?, string)> UpdateDecShopTx(DecShop decshop)
        {
            Transaction? decShopTx = null;
            var address = decshop.OwnerAddress;
            var name = decshop.Name;

            var account = AccountData.GetSingleAccount(address);
            if (account == null)
            {
                return (null, $"Address is not found for : {address}");
            }

            var txData = "";
            var timestamp = TimeUtil.GetTime();

            BigInteger b1 = BigInteger.Parse(account.GetKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

            txData = JsonConvert.SerializeObject(new { Function = "DecShopUpdate()", DecShop = decshop });

            decShopTx = new Transaction
            {
                Timestamp = TimeUtil.GetTime(),
                FromAddress = address,
                ToAddress = "DecShop_Base",
                Amount = Globals.DecShopUpdateRequiredRBX,
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
                ErrorLogUtility.LogError($"Signing TX failed for Decentralized Shop Request on address {address} for name {name}", "DecShop.UpdateDecShopTx()-1");
                return (null, $"Signing TX failed for DecShop Request on address {address} for name {name}");
            }

            decShopTx.Signature = sig;

            try
            {
                if (decShopTx.TransactionRating == null)
                {
                    var rating = await TransactionRatingService.GetTransactionRating(decShopTx, true);
                    decShopTx.TransactionRating = rating;
                }

                var result = await TransactionValidatorService.VerifyTX(decShopTx);
                if (result.Item1 == true)
                {
                    decShopTx.TransactionStatus = TransactionStatus.Pending;

                    await WalletService.SendTransaction(decShopTx, account);

                    return (decShopTx, "");
                }
                else
                {
                    ErrorLogUtility.LogError($"Transaction Failed Verify and was not Sent to Mempool. Error: {result.Item2}", "DecShop.UpdateDecShopTx()");
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

        #region Delete DecShop TX
        public static async Task<(Transaction?, string)> DeleteDecShopTx(string dsUID, string address)
        {
            Transaction? decShopTx = null;

            var account = AccountData.GetSingleAccount(address);
            if (account == null)
            {
                return (null, $"Address is not found for : {address}");
            }

            var txData = "";
            var timestamp = TimeUtil.GetTime();

            BigInteger b1 = BigInteger.Parse(account.GetKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

            txData = JsonConvert.SerializeObject(new { Function = "DecShopDelete()", UniqueId = dsUID });

            decShopTx = new Transaction
            {
                Timestamp = TimeUtil.GetTime(),
                FromAddress = address,
                ToAddress = "DecShop_Base",
                Amount = Globals.DecShopRequiredRBX,
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
                ErrorLogUtility.LogError($"Signing TX failed for Decentralized Shop Request.", "DecShop.DeleteDecShopTx()-1");
                return (null, $"Signing TX failed for DecShop Request.");
            }

            decShopTx.Signature = sig;

            try
            {
                if (decShopTx.TransactionRating == null)
                {
                    var rating = await TransactionRatingService.GetTransactionRating(decShopTx, true);
                    decShopTx.TransactionRating = rating;
                }

                var result = await TransactionValidatorService.VerifyTX(decShopTx);
                if (result.Item1 == true)
                {
                    decShopTx.TransactionStatus = TransactionStatus.Pending;

                    await WalletService.SendTransaction(decShopTx, account);

                    return (decShopTx, "");
                }
                else
                {
                    ErrorLogUtility.LogError($"Transaction Failed Verify and was not Sent to Mempool. Error: {result.Item2}", "DecShop.DeleteDecShopTx()");
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

        #region Check URL Regex
        public static bool CheckURL(string url)
        {
            bool output = false;

            url = url.ToLower().Replace("rbx://", "");

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
        SelfHosted,
        ThirdParty
    }
}
