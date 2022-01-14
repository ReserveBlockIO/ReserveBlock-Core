using ReserveBlockCore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReserveBlockCore.Data
{
    internal class BlockchainData
    {
        public IList<Transaction> PendingTransactions = new List<Transaction>();
        public Blockchain Chain { get; set; }

        public Block CreateGenesisBlock()
        {
            Block block = new Block
            {
                BlockHeight = 0,
                BlockReward = 0,
                PrevHash = "Genesis Block",
                Hash = "",
                Timestamp = DateTime.Now.Ticks,
                ChainHash = "",
                ChainRev = "alpha_rev1",
                NodeValidatorId = "Alpha Node",
                ValidatorKey = "*1337*"
            };
            
            //Need to add initial transactions

            return block;
        }

        public void AddGenesisBlock()
        {
            //add block to chain.
        }
    }
}
