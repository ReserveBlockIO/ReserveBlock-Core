using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReserveBlockCore.Models
{
    internal class Peers
    {
        public long Id { get; set; }
        public Guid NodeId { get; set; }
        public string Connection { get; set; }
        public DateTime LastReach { get; set; }
        public string Version { get; set; }
        public string UserAgent { get; set; }
        public long BlockHeight { get; set; }
        public string SentData { get; set; }
        public string ReceivedData { get; set; }


    }
}
