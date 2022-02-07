namespace ReserveBlockCore.Models
{
    public class AccountStateTrei
    {
        public string Key { get; set; }
        public long Nonce { get; set; }
        public decimal Balance { get; set; }
        public string StateRoot { get; set; }
        public string CodeHash { get; set; }
    }
}
