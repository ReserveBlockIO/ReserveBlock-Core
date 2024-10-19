namespace ReserveBlockCore.Bitcoin.Models
{
    public class TokenizedWithdrawals
    {
        public string RequestorAddress { get; set; }
        public long OriginalRequestTime { get;set; }
        public string OriginalSignature { get; set; }
        public string OriginalUniqueId { get; set; }    
        public long Timestamp { get; set; }
        public string SmartContractUID { get; set; }
        public decimal Amount { get; set; }
        public WithdrawalRequestType WithdrawalRequestType { get; set; }
        public string TransactionHex { get; set; }
        public string ArbiterUniqueId { get; set; }
        public bool IsCompleted { get; set; }
    }

    public enum WithdrawalRequestType
    {
        Arbiter,
        Owner
    }
}
