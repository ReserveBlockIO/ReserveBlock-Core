using ReserveBlockCore.Utilities;
using System.Net;
using System.Net.Sockets;

namespace ReserveBlockCore.Models.DST
{
    public class ShopConnection
    {
        public UdpClient UdpClient { get; set; }
        public IPEndPoint EndPoint { get; set; }
        public CancellationTokenSource ShopToken { get; set; }
        public string ShopURL { get; set; }
        public bool IsConnected { get { return GetConnectionStatus(); } }
        public bool KeepAliveStarted { get; set; }
        public string IPAddress { get; set; }
        public long ConnectDate { get; set; }
        public long LastSentMessage { get; set; }
        public long LastReceiveMessage { get; set; }
        public Message InitialMessage { get; set; }

        public bool GetConnectionStatus()
        {
            var currentTime = TimeUtil.GetTime();
            if (currentTime - LastReceiveMessage > 65)
                return false;

            return true;
        }
    }
}
