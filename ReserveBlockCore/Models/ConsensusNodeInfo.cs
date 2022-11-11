using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReserveBlockCore.Models
{
    public class ConsensusNodeInfo
    {
        public HubConnection Connection;
        public string Address { get; set; }
        public string IpAddress { get; set; }     
        public bool IsConnected { get { return Connection?.State == HubConnectionState.Connected; } }
    }
}
