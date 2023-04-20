using System.Net;

namespace ReserveBlockCore.Models.DST
{
    public class AssetSendData
    {
        public string UniqueId { get; set; } 
        public string AssetName { get; set; }
        public int AckNumber { get; set; }
        public IPEndPoint EndPoint { get; set; }
    }
}
