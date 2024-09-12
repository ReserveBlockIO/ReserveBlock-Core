using Microsoft.AspNetCore.SignalR;

namespace ReserveBlockCore.Models
{
    public class FortisPool
    {
        public string Address { get; set; }
        public string UniqueName { get; set; }        
        public string IpAddress { get; set; }
        public string WalletVersion { get; set; }
        public DateTime ConnectDate { get; set; }
        public DateTime? LastAnswerSendDate { get; set; }
        public HubCallerContext? Context { get; set; }
    }
}
