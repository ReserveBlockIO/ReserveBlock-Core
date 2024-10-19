namespace ReserveBlockCore.Bitcoin.Models
{
    public class BTCTokenizeWithdrawalRaw
    {
        public string SmartContractUID { get; set; }
        public decimal Amount { get; set; }
        public string VFXAddress { get; set; }
        public string BTCToAddress { get; set; }
        public long Timestamp { get; set; }
        public string UniqueId { get; set; }
        public string VFXSignature { get; set; }
        public long ChosenFeeRate { get; set; }
        public bool IsTest { get; set; }
    }
}
