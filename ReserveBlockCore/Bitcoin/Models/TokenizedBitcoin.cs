using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.Utilities;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ReserveBlockCore.Bitcoin.Models
{
    public class TokenizedBitcoin
    {
        #region Variables
        public long Id { get; set; }
        public string SmartContractUID { get; set; }
        public string RBXAddress { get; set; }
        public string? DepositAddress { get; set; }
        public decimal Balance { get; set; }
        public decimal MyBalance { get { return GetMyBalance(RBXAddress, SmartContractUID); } }
        public string TokenName { get; set; }
        public string TokenDescription { get; set; }
        public long SmartContractMainId { get; set; }
        public bool IsPublished { get; set; }
        public bool TokenHasBecomeInsolvent { get; set; }

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
                var sc = scs.FindOne(x => x.DepositAddress == address);
                if (sc != null)
                {
                    sc.Balance = balance;
                    scs.UpdateSafe(sc);
                }
            }
        }

        public static async Task FlagInsolvent(string address)
        {
            var scs = GetDb();
            if (scs != null)
            {
                var sc = scs.FindOne(x => x.DepositAddress == address);
                if (sc != null)
                {
                    sc.TokenHasBecomeInsolvent = !sc.TokenHasBecomeInsolvent;
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
                tokenList = scs.Find(x => x.IsPublished && x.DepositAddress == null).ToList();
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

        public static async Task<TokenizedBitcoin?> GetTokenizedBitcoin(string scUID)
        {
            var scs = GetDb();
            if (scs != null)
            {
                var sc = scs.FindOne(x => x.SmartContractUID == scUID);
                if (sc != null)
                {
                    return sc;
                }
            }

            return null;
        }

        public static async Task SaveSmartContract(SmartContractMain scMain, string? scText = null, string? rbxAddress = null)
        {
            var scs = GetDb();

            var exist = scs.FindOne(x => x.SmartContractUID == scMain.SmartContractUID);

            if (exist == null)
            {
                if(scMain.Features != null)
                {
                    var tknzFeature = scMain.Features.Where(x => x.FeatureName == FeatureName.Tokenization).Select(x => x.FeatureFeatures).FirstOrDefault();
                    if (tknzFeature != null)
                    {
                        var tknz = (TokenizationFeature)tknzFeature;
                        if(tknz != null)
                        {
                            TokenizedBitcoin tokenizedBitcoin = new TokenizedBitcoin
                            {
                                DepositAddress = tknz.DepositAddress,
                                IsPublished = false,
                                RBXAddress = rbxAddress == null ? scMain.MinterAddress : rbxAddress,
                                SmartContractMainId = scMain.Id,
                                SmartContractUID = scMain.SmartContractUID,
                                TokenDescription = scMain.Description,
                                TokenName = scMain.Name,
                            };
                            scs.InsertSafe(tokenizedBitcoin);
                        }
                        else
                        {
                            SCLogUtility.Log("Failed to read tokenization feature, but it was found", "TokenizedBitcoin.SaveSmartContract()");
                        }
                    }
                    else
                    {
                        SCLogUtility.Log("No tokenization features found on SC", "TokenizedBitcoin.SaveSmartContract()");
                    }
                }
                else
                {
                    SCLogUtility.Log("No features found on SC", "TokenizedBitcoin.SaveSmartContract()");
                }
            }
            if (scText != null)
            {
                SaveSCLocaly(scMain, scText);
            }

        }
        public static void DeleteSmartContract(string scUID)
        {
            try
            {
                var scs = GetDb();

                scs.DeleteManySafe(x => x.SmartContractUID == scUID);
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.ToString(), "TokenizedBitcoin.DeleteSmartContract()");
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
                SCLogUtility.Log($"Failed to save smart contract locally: {scMain.SmartContractUID}. Error Message: {ex.ToString()}",
                "TokenizedBitcoin.SaveSCLocally(SmartContractMain scMain, string scText)");
            }
        }

        private decimal GetMyBalance(string vfxAddress, string scUID)
        {
            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);

            if (scStateTreiRec == null)
                return 0.0M;

            var balances = scStateTreiRec.SCStateTreiTokenizationTXes?.Where(x => x.FromAddress == vfxAddress || x.ToAddress == vfxAddress).ToList();

            if (scStateTreiRec.OwnerAddress == vfxAddress)
            {
                if(balances != null)
                {
                    var balance = balances.Sum(x => x.Amount);
                    var finalBalance = Balance - balance;

                    return finalBalance;
                }

                return Balance;
            }
            else
            {
                if(balances != null)
                {
                    var balance = balances.Sum(x => x.Amount);
                    return balance;
                }

                return 0.0M;
            }
        }
    }
}
