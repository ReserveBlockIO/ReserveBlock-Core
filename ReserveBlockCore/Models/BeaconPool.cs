namespace ReserveBlockCore.Models
{
    public class BeaconPool
    {
        public string Reference { get; set; }
        public string ConnectionId { get; set; }
        public string IpAddress { get; set; }
        public string WalletVersion { get; set; }
        public DateTime ConnectDate { get; set; }
    }
}
