namespace ReserveBlockCore.Bitcoin.Models
{
    public class CoinInput
    {
        public string ScriptPubKey { get; set; }
        public string RedeemScript { get; set; }
        public string TxHash { get; set; }
        public int Vout { get; set; }
        public decimal Money { get; set; }
    }
}
