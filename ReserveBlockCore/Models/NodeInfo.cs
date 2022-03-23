using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReserveBlockCore.Models
{
    internal class NodeInfo
    {
        public string? NodeIP { get; set; } 
        public long NodeHeight { get; set; }
        public int NodeLatency { get; set; }
        public DateTime? NodeLastChecked { get; set; }
        
    }
}
