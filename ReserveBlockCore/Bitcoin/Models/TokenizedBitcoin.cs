using ReserveBlockCore.Data;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.Utilities;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ReserveBlockCore.Bitcoin.Models
{
    public class TokenizedBitcoin
    {
        #region Variables
        public long Id { get; set; }
        public string SmartContractUID { get; set; }
        public string RBXAddress { get; set; }
        public string? BTCAddress { get; set; }
        public decimal Balance { get; set; }
        public string TokenName { get; set; }
        public string TokenDescription { get; set; }
        public long SmartContractMainId { get; set; }
        public bool IsPublished { get; set; }

        #endregion

        public static LiteDB.ILiteCollection<TokenizedBitcoin> GetDb()
        {
            var db = DbContext.DB_Assets.GetCollection<TokenizedBitcoin>(DbContext.RSRV_BITCOIN_TOKENS);
            return db;
        }

        public static TokenizedBitcoin? GetTokenContract(string smartContractUID)
        {
            var scs = GetDb();
            if (scs != null)
            {
                var sc = scs.FindOne(x => x.SmartContractUID == smartContractUID);
                if (sc != null)
                {
                    return sc;
                }
            }

            return null;
        }
        public static async Task UpdateBalance(string address, decimal balance)
        {
            var scs = GetDb();
            if (scs != null)
            {
                var sc = scs.FindOne(x => x.BTCAddress == address);
                if (sc != null)
                {
                    sc.Balance = balance;
                    scs.UpdateSafe(sc);
                }
            }
        }

        public static async Task<List<TokenizedBitcoin>> GetTokenPublishedNoAddressList()
        {
            List<TokenizedBitcoin> tokenList = new List<TokenizedBitcoin>();
            var scs = GetDb();
            if (scs != null)
            {
                tokenList = scs.Find(x => x.IsPublished && x.BTCAddress == null).ToList();
                if (tokenList.Any())
                {
                    return tokenList;
                }
            }

            return tokenList;
        }

        public static async Task<List<TokenizedBitcoin>?> GetTokenPublishedList()
        {
            var scs = GetDb();
            if (scs != null)
            {
                var sc = scs.Find(x => x.IsPublished).ToList();
                if (sc.Any())
                {
                    return sc;
                }
            }

            return null;
        }

        public static async Task<List<TokenizedBitcoin>?> GetTokenNotPublishedList()
        {
            var scs = GetDb();
            if (scs != null)
            {
                var sc = scs.Find(x => !x.IsPublished).ToList();
                if (sc.Any())
                {
                    return sc;
                }
            }

            return null;
        }

        public static async Task<List<TokenizedBitcoin>?> GetTokenizedList()
        {
            var scs = GetDb();
            if (scs != null)
            {
                var sc = scs.FindAll().ToList();
                if (sc.Any())
                {
                    return sc;
                }
            }

            return null;
        }

        public static async Task SaveSmartContract(SmartContractMain scMain, string? scText)
        {
            var scs = GetDb();

            var exist = scs.FindOne(x => x.SmartContractUID == scMain.SmartContractUID);

            if (exist == null)
            {
                TokenizedBitcoin tokenizedBitcoin = new TokenizedBitcoin 
                { 
                    BTCAddress = null,
                    IsPublished = false,
                    RBXAddress = scMain.MinterAddress,
                    SmartContractMainId = scMain.Id,
                    SmartContractUID = scMain.SmartContractUID,
                    TokenDescription = scMain.Description,
                    TokenName = scMain.Name,
                };
                scs.InsertSafe(tokenizedBitcoin);
            }
            if (scText != null)
            {
                SaveSCLocaly(scMain, scText);
            }

        }

        public static async Task SetTokenContractIsPublished(string scUID)
        {
            var scs = GetDb();

            var scMain = GetTokenContract(scUID);

            if (scMain != null)
            {
                scMain.IsPublished = true;
                scs.UpdateSafe(scMain);
            }
        }

        public static async void SaveSCLocaly(SmartContractMain scMain, string scText)
        {
            try
            {
                string MainFolder = Globals.IsTestNet != true ? "RBX" : "RBXTest";
                var databaseLocation = Globals.IsTestNet != true ? "SmartContracts" : "SmartContractsTestNet";
                var text = scText;
                string path = "";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    path = homeDirectory + Path.DirectorySeparatorChar + "rbx" + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                }
                else
                {
                    if (Debugger.IsAttached)
                    {
                        path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "DBs" + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                    }
                    else
                    {
                        path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + MainFolder + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                    }
                }
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                var scName = scMain.SmartContractUID.Split(':');
                await File.AppendAllTextAsync(path + scName[0].ToString() + ".trlm", text);
            }
            catch (Exception ex)
            {
                NFTLogUtility.Log($"Failed to save smart contract locally: {scMain.SmartContractUID}. Error Message: {ex.ToString()}",
                "TokenizedBitcoin.SaveSCLocally(SmartContractMain scMain, string scText)");
            }
        }
    }
}
