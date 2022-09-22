using ReserveBlockCore.Data;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Models
{
    public class Keystore
    {
        public int Id { get; set; }
        public string PrivateKey { set; get; }
        public string PublicKey { set; get; }
        public string Address { get; set; }
        public string Key { get; set; }

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
                ErrorLogUtility.LogError(ex.Message, "Keystore.GetKeystore()");
                return null;
            }

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

        public static async Task GenerateKeystoreAddresses()
        {
            List<Keystore> keystoreList = new List<Keystore>();

            var accounts = AccountData.GetAccounts();
            var accountList = accounts.FindAll().ToList();
            var amount = 1000 - accountList.Count;
            if(amount > 0)
            {
                //Create 1000 less current account keystore addresses
                for(var i = 0; i < amount; i++)
                {
                    //generating addresses, then encrypt private key and save to account db
                    var account = AccountData.CreateNewAccount(true);
                    var keystore = await WalletEncryptionService.EncryptWallet(account);
                    if(keystore != null)
                    {
                        keystoreList.Add(keystore);
                    }
                    
                }
            }

            //Update current accounts to have private keys encrypted.
            foreach(var account in accountList)
            {
                var keystore = await WalletEncryptionService.EncryptWallet(account, true);
                if (keystore != null)
                {
                    keystoreList.Add(keystore);
                }
            }

            var keystores = GetKeystore();
            if(keystores != null)
            {
                keystores.InsertBulkSafe(keystoreList);
            }
        }
    }
}
