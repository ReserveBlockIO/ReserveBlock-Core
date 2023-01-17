namespace ReserveBlockCore.Models
{
    public class AdjBench
    {
        public int Id { get; set; }
        public string IPAddress { get; set; }
        public string RBXAddress { get; set; }
        public long TimeEntered { get; set; }
        public long TimeEligibleForConsensus { get; set; }
        public bool PulledFromBench { get; set; }
        public string TXVoteHash { get; set; }
        public string TopicUID { get; set; }
    }
}
