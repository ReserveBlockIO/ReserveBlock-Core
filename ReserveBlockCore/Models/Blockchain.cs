using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReserveBlockCore.Models
{
    public class Blockchain
    {
        public string ChainHash { get; set; }
        public List<Transaction> PendingTransactions { get; set; }
        public decimal MasterNodeReward { get; set; } = 15M;
        public decimal DataNodeReward { get; set; } = 25M;
        public virtual ICollection<Block> Blocks { get; set; }

        public int GetLatestBlock
        {
            get { return Blocks.Count() - 1; } 
        }

        public int GetNextBlockHeight
        {
            get { return Blocks.Count(); }
        }

    }
}
