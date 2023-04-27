namespace ReserveBlockCore.Models.DST
{
    public class DSTConnection
    {
        public string IPAddress { get; set; }
        public bool IsConnected { get; set; }
        public long ConnectDate { get; set; }
        public long LastSentMessage { get; set; }
        public long LastReceiveMessage { get; set; }
        public string ShopURL { get; set; }
        public Message InitialMessage { get; set; }
    }
}
