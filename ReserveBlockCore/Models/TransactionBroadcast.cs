namespace ReserveBlockCore.Models
{
    public class TransactionBroadcast
    {
        public string Hash { get; set; }
        public bool IsBroadcastedToAdj { get; set; }
        public bool IsBroadcastedToVal { get; set; }
        public int  RebroadcastCount { get; set; }
        public Transaction Transaction { get; set; }
    }
}
