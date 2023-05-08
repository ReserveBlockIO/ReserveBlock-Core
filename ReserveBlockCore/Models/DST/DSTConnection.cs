using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Models.DST
{
    public class DSTConnection
    {
        public string IPAddress { get; set; }
        public bool IsConnected { get { return GetConnectionStatus(); } }
        public long ConnectDate { get; set; }
        public long LastSentMessage { get; set; }
        public long LastReceiveMessage { get; set; }
        public bool KeepAliveStarted { get; set; }
        public bool AttemptReconnect { get; set; }
        public string ShopURL { get; set; }
        public string ConnectionId { get; set; }
        public Message InitialMessage { get; set; }
        public Message? LastMessageSent { get; set; }

        public bool GetConnectionStatus()
        {
            var currentTime = TimeUtil.GetTime();
            if (currentTime - LastReceiveMessage > 65)
                return false;

            return true;
        }
    }
}
