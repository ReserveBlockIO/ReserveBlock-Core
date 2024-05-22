namespace ReserveBlockCore.Bitcoin.Models
{
    public class BTCTokenizeTransaction
    {
        public string SCUID { get; set; }
        public string? FromAddress { get; set; }
        public string ToAddress { get; set; }
        public decimal Amount { get; set; }
    }
}
