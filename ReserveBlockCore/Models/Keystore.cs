using ReserveBlockCore.Data;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using Spectre.Console;

namespace ReserveBlockCore.Models
{
    public class Keystore
    {
        public int Id { get; set; }
        public string PrivateKey { set; get; }
        public string PublicKey { set; get; }
        public string Address { get; set; }
        public string Key { get; set; }
        public bool IsUsed { get; set; }

        #region GetKeystore()
        public static LiteDB.ILiteCollection<Keystore>? GetKeystore()
        {
            try
            {
                var keystore = DbContext.DB_Keystore.GetCollection<Keystore>(DbContext.RSRV_KEYSTORE);
                return keystore;
            }
            catch (Exception ex)
            {
                DbContext.Rollback();
                ErrorLogUtility.LogError(ex.ToString(), "Keystore.GetKeystore()");
                return null;
            }

        }
        #endregion

        #region Keystore Count Check
        public static async Task<int> KeystoreCheck()
        {
            var keystores = GetKeystore();
            var availKeys = keystores.Find(x => x.IsUsed == false).ToList().Count();

            return availKeys;
        }
        #endregion

        #region Get Unused Keystore
        public static async Task<Account>? GetNextKeystore()
        {
            var keystores = GetKeystore();
            if(keystores != null)
            {
                var keystore = keystores.FindOne(x => x.IsUsed == false);
                var accounts = AccountData.GetAccounts();
                if(accounts != null)
                {
                    var check = accounts.FindOne(x => x.Address == keystore.Address);
                    if(check == null)
                    {
                        Account account = new Account {
                            Address = keystore.Address,
                            Balance = 0.00M,
                            PrivateKey = keystore.PrivateKey,
                            PublicKey = keystore.PublicKey
                        };

                        accounts.InsertSafe(account);
                        keystore.IsUsed = true;
                        keystores.UpdateSafe(keystore);

                        return account;
                    }
                }
            }

            return null;
            
        }
       
        #endregion

        #region SaveKeystore(Keystore keystoreData)
        public static string SaveKeystore(Keystore keystoreData)
        {
            var keystore = GetKeystore();
            if (keystore == null)
            {
                ErrorLogUtility.LogError("GetKeystore() returned a null value.", "Keystore.GetKeystore()");
            }
            else
            {
                var keystoreRecData = keystore.FindOne(x => x.Address == keystoreData.Address);
                if (keystoreRecData != null)
                {
                    return "Record Already Exist.";
                }
                else
                {
                    keystore.InsertSafe(keystoreData);
                }
            }

            return "Error Saving Keystore";

        }
        #endregion

        #region BulkSaveKeystore(List<Keystore> keystoreDataList)
        public static string BulkSaveKeystore(List<Keystore> keystoreDataList)
        {
            var keystore = GetKeystore();
            if (keystore == null)
            {
                ErrorLogUtility.LogError("GetKeystore() returned a null value.", "Keystore.BulkSaveKeystore()");
            }
            else
            {
                keystore.InsertBulkSafe(keystoreDataList);
            }
            return "Error Saving Keystore";
        }
        #endregion

        #region DeleteKeystore(string address)
        public static void DeleteKeystore(string address)
        {
            var keystore = GetKeystore();
            if (keystore == null)
            {
                ErrorLogUtility.LogError("GetKeystore() returned a null value.", "Keystore.DeleteKeystore()");
            }
            else
            {
                keystore.DeleteManySafe(x => x.Address == address);
            }
        }
        #endregion

        #region Generate Keystore Addresses

        public static async Task GenerateKeystoreAddresses(bool convertCurrentAddr = true)
        {
            List<Keystore> keystoreList = new List<Keystore>();

            var accounts = AccountData.GetAccounts();
            var accountList = accounts.FindAll().ToList();
            var amount = 1000;
            var progress = 0.00M;
            //Create 1000 keystore addresses
            await AnsiConsole.Progress()
                .StartAsync(async ctx =>
                {
                    var task1 = ctx.AddTask("[green]Generating Encrypted Keystore[/]");
                    var task2 = ctx.AddTask("[green]Encrypting Current Keys[/]");
                       
                    while (!ctx.IsFinished)
                    {
                        for (var i = 0; i < amount; i++)
                        {
                            //generating addresses, then encrypt private key and save to account db
                            var account = AccountData.CreateNewAccount(true);
                            var keystore = await WalletEncryptionService.EncryptWallet(account);
                            if (keystore != null)
                            {
                                keystoreList.Add(keystore);
                                progress += 0.1M;
                                task1.Increment(0.1);
                            }
                        }
                        task1.Increment(100);

                        if (convertCurrentAddr)
                        {
                            var accountListCount = accountList.Count();
                            double incr = 100 / accountListCount;
                            //Update current accounts to have private keys encrypted.
                            foreach (var account in accountList)
                            {
                                var keystore = await WalletEncryptionService.EncryptWallet(account, true);
                                if (keystore != null)
                                {
                                    keystoreList.Add(keystore);
                                    task2.Increment(incr);
                                }
                            }
                            task2.Increment(100);
                        }
                        else
                        {
                            task2.Increment(100);
                        }
                    }
                });

            var keystores = GetKeystore();
            if(keystores != null)
            {
                keystores.InsertBulkSafe(keystoreList);
            }
        }

        #endregion
    }
}
