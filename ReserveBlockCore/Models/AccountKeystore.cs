using ReserveBlockCore.Data;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Models
{
    public class AccountKeystore
    {
        public long Id { get; set; }
        public string Address { get; set; }
        public string PrivateKeyEncryptionKey { get; set; }

        public static LiteDB.ILiteCollection<AccountKeystore> GetAccountKeystore()
        {
            try
            {
                var accountKS = DbContext.DB_Wallet.GetCollection<AccountKeystore>(DbContext.RSRV_ACCOUNT_KEYSTORE);                
                return accountKS;
            }
            catch (Exception ex)
            {
                DbContext.Rollback();
                ErrorLogUtility.LogError(ex.ToString(), "AccountKeystore.GetAccountKeystore()");
                return null;
            }
        }

        public static string SaveAccountKeystore(string key, string address)
        {
            var adnr = GetAccountKeystore();
            if (adnr == null)
            {
                ErrorLogUtility.LogError("GetAccountKeystore() returned a null value.", "AccountKeystore.GetAccountKeystore()");
            }
            else
            {
                //encrypt key and store against address
            }

            return "Error Saving Account Keystore";

        }
    }
}
