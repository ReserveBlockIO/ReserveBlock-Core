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
    public class DecShop
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ShopUID { get; set; }
        public string Locator { get; set; }
        public string Address { get; set; }
        public bool IsOffline { get; set; }
        public string Signature { get; set; }

        public static LiteDB.ILiteCollection<DecShop>? DecShops()
        {
            try
            {
                var decshops = DbContext.DB_DecShopStateTrei.GetCollection<DecShop>(DbContext.RSRV_DECSHOPSTATE_TREI);
                return decshops;
            }
            catch (Exception ex)
            {
                DbContext.Rollback();
                ErrorLogUtility.LogError(ex.Message, "DecShop.GetDecShops()");
                return null;
            }

        }

        public static LiteDB.ILiteCollection<DecShop>? MyDecShop()
        {
            try
            {
                var decshop = DbContext.DB_Wallet.GetCollection<DecShop>(DbContext.RSRV_DECSHOP);
                return decshop;
            }
            catch (Exception ex)
            {
                DbContext.Rollback();
                ErrorLogUtility.LogError(ex.Message, "DecShop.GetMyDecShop()");
                return null;
            }

        }

        public static DecShop? GetMyDecShopInfo()
        {
            try
            {
                var decshop = MyDecShop();

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
                DbContext.Rollback();
                ErrorLogUtility.LogError(ex.Message, "DecShop.GetMyDecShopInfo()");
                return null;
            }
        }

        public static async Task<string> SaveMyDecShopInfo(DecShop decshop)
        {
            var decshops = MyDecShop();
            if (decshops == null)
            {
                ErrorLogUtility.LogError("DecShops() returned a null value.", "DecShop.SaveMyDecShopInfo()");
                return "Failed to call database.";
            }
            else
            {
                var existingDecShopInfo = decshops.FindAll().FirstOrDefault();
                if (existingDecShopInfo == null)
                {
                    decshops.InsertSafe(decshop); //inserts new record
                    return $"Decentralized Sales Shop has been created with name {decshop.Name}";
                }
                else
                {
                    //record exist
                    return "Shop already exist.";
                }
            }
        }

        public static void SaveDecShopInfo(DecShop decshop)
        {
            var decshops = DecShops();
            if (decshops == null)
            {
                ErrorLogUtility.LogError("DecShops() returned a null value.", "DecShop.SaveDecShopInfo()");
            }
            else
            {
                var existingDecShopInfo = decshops.FindAll().Where(x => x.Locator == decshop.Locator);
                if (existingDecShopInfo == null)
                {
                    decshops.InsertSafe(decshop); //inserts new record
                }
                else
                {
                    //record exist
                }
            }
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
                var decshop = MyDecShop();
                if(decshop != null)
                {
                    myDecShop.IsOffline = !myDecShop.IsOffline;
                    decshop.UpdateSafe(myDecShop);
                    return myDecShop.IsOffline;
                }
            }

            return null;
        }

        public static async Task<(Transaction?, string)> CreateDecShopTx(DecShop decshop)
        {
            Transaction? decShopTx = null;
            var address = decshop.Address;
            var name = decshop.Name;

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
            var signature = SignatureService.CreateSignature(decshop.ShopUID, privateKey, account.PublicKey);
            var hash = GetHash(address, name, signature, timestamp);

            txData = JsonConvert.SerializeObject(new { Function = "DecShopCreate()", 
                Address = address, 
                Name = name, 
                Timestamp = timestamp, 
                Hash = hash, 
                Signature = signature,
                Description = decshop.Description,
                ShopUID = decshop.ShopUID,
                Locator = decshop.Locator,
                IsOffline = decshop.IsOffline
            });

            decShopTx = new Transaction
            {
                Timestamp = TimeUtil.GetTime(),
                FromAddress = address,
                ToAddress = "DecShop_Base",
                Amount = 0.0M,
                Fee = 0,
                Nonce = AccountStateTrei.GetNextNonce(address),
                TransactionType = TransactionType.ADNR,
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
                var result = await TransactionValidatorService.VerifyTXDetailed(decShopTx);
                if (result.Item1 == true)
                {
                    TransactionData.AddToPool(decShopTx);
                    AccountData.UpdateLocalBalance(decShopTx.FromAddress, (decShopTx.Fee + decShopTx.Amount));
                    //P2PClient.SendTXMempool(decShopTx);//send out to mempool
                    return (decShopTx, "Success");
                }
                else
                {
                    ErrorLogUtility.LogError($"Transaction Failed Verify and was not Sent to Mempool. Error: {result.Item2}", "Adnr.CreateAdnrTx(string address, string name)");
                    return (null, $"Transaction Failed Verify and was not Sent to Mempool. Error: {result.Item2}");
                }
            }
            catch (Exception ex)
            {
                DbContext.Rollback();
                Console.WriteLine("Error: {0}", ex.Message);
            }

            return (null, "Error. Please see message above.");
        }
        private static string GetHash(string address, string name, string signature, long timestamp)
        {
            var data = address + name + signature + timestamp;
            return HashingService.GenerateHash(HashingService.GenerateHash(data));
        }
        public class DecShopInfoJson
        {
            public string IPAddress { get; set; }
            public int Port { get; set; }
            public string Name { get; set; }
            public string ShopUID { get; set; }
        }
    }
}
