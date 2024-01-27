namespace ReserveBlockCore.Models
{
    public class NetworkValidator
    {
        public string IPAddress { get; set; }
        public string Address { get; set; }
        public string UniqueName { get; set; }
        public string PublicKey { get; set; }
        public string Signature { get; set; }
        public long BlockStart { get; set; } //make them wait 144 blocks = 1 hour
        public long LastBlockProof { get; set; }
    }
}
