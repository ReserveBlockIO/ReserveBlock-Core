using ReserveBlockCore.Extensions;
using ReserveBlockCore.Data;

namespace ReserveBlockCore.Models
{
    public class AccountStateTrei
    {
        public long Id { get; set; }
        public string Key { get; set; }
        public string? RecoveryAccount { get; set; }
        public long Nonce { get; set; }
        public decimal Balance { get; set; }
        public decimal LockedBalance { get; set; }
        public string StateRoot { get; set; }
        public string CodeHash { get; set; }
        public List<TokenAccount> TokenAccounts { get; set; }

        public static decimal GetAccountBalance(string address)
        {
            var balance = 0.00M;
            var accounts = DbContext.DB_AccountStateTrei.GetCollection<AccountStateTrei>(DbContext.RSRV_ASTATE_TREI);
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
            var accounts = DbContext.DB_AccountStateTrei.GetCollection<AccountStateTrei>(DbContext.RSRV_ASTATE_TREI);
            var account = accounts.FindOne(x => x.Key == address);
            if (account != null)
            {
                nonce = account.Nonce;
            }

            return nonce;
        }
    }
}
