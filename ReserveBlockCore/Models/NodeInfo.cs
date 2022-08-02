using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReserveBlockCore.Models
{
    public class NodeInfo
    {
        public HubConnection Connection;
        public string NodeIP { get; set; } 
        public long NodeHeight { get; set; }
        public int NodeLatency { get; set; }
        public DateTime? NodeLastChecked { get; set; }
        
    }
}
