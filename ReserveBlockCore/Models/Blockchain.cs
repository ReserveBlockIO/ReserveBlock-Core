using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReserveBlockCore.Models
{
    public class Blockchain
    {
        public List<Block> Chain { get; set; }
        public int Difficulty { get; set; } = 2; //
        public List<Transaction> PendingTransactions { get; set; }
        public decimal MasterNodeReward { get; set; } = 50M;
        public decimal DataNodeReward { get; set; } = 100M;

    }
}
