using Microsoft.AspNetCore.SignalR.Client;

namespace ReserveBlockCore.Models
{
    public class BeaconNodeInfo
    {
        public HubConnection Connection;
        public string IPAddress { get; set; }
        public DateTime ConnectDate { get; set; }
        public DateTime LastCallDate { get; set; }
        public bool Downloading { get; set; }
        public bool Uploading { get; set; }
        public Beacons Beacons { get; set; }
        public bool IsConnected { get { return Connection?.State == HubConnectionState.Connected; } }
    }
}
