namespace ReserveBlockCore.Models
{
    public class StateTreiAuditData
    {
        public StateRecordStatus StateRecordType { get; set; }
        public decimal? OldValue { get; set; }
        public decimal NewValue { get; set; }
        public long Nonce { get; set; }
        public long NextNonce { get; set; }
        public string Address { get; set; }
        public string StateRoot { get; set; }

        public enum StateRecordStatus
        {
            Insert,
            Update
        }
    }
}
