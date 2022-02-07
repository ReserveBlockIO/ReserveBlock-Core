using LiteDB;
using ReserveBlockCore.Data;

namespace ReserveBlockCore.Models
{
    public class AccountStateTrei
    {
        public string Key { get; set; }
        public long Nonce { get; set; }
        public decimal Balance { get; set; }
        public string StateRoot { get; set; }
        public string CodeHash { get; set; }

        public static decimal GetAccountBalance(string address)
        {
            var balance = 0.00M;
            var accounts = DbContext.DB.GetCollection<AccountStateTrei>(DbContext.RSRV_ASTATE_TREI);
            var account = accounts.FindOne(x => x.Key == address);
            if(account != null)
            {
                balance = account.Balance;
            }    

            return balance;
        }

        public static long GetNextNonce(string address)
        {
            long nonce = 0;
            var accounts = DbContext.DB.GetCollection<AccountStateTrei>(DbContext.RSRV_ASTATE_TREI);
            var account = accounts.FindOne(x => x.Key == address);
            if (account != null)
            {
                nonce = account.Nonce;
            }

            return nonce;
        }
    }
}
