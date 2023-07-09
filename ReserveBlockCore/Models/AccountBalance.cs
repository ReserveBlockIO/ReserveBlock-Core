namespace ReserveBlockCore.Models
{
    public class AccountBalance
    {
        public string Address { get; set; }
        public decimal RBXBalance { get; set; }
        public decimal RBXLockedBalance { get; set; }
        public List<TokenAccount> TokenAccounts { get; set; }
    }
}
