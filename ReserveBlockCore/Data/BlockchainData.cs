using LiteDB;
using Newtonsoft.Json;
using ReserveBlockCore.Models;
using ReserveBlockCore.Utilities;
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

        internal static void InitializeChain()
        {
            var blocks = BlockData.GetBlocks();
            if (blocks.Count() < 1)
            {
                var genesisTime = DateTime.UtcNow;
                TransactionData.CreateGenesisTransction();

                //Get all transaction in pool. This can be used to create multiple accounts to receive funds at start of chain
                var trxPool = TransactionData.GetPool();
                var transactions = trxPool.FindAll().ToList();

                var block = BlockData.CreateGenesisBlock(transactions);

                // add the genesis block to our new blockchain
                AddBlock(block);

                // move the processed mempool tx(s) into Finalized table
                transactions.ForEach(x => { Transaction.Add(x); });
                // clear mempool
                trxPool.DeleteAll();
            }
        }
        public static ILiteCollection<Block> GetBlocks()
        {
            var blocks = DbContext.DB.GetCollection<Block>(DbContext.RSRV_BLOCKS);
            blocks.EnsureIndex(x => x.Height);
            return blocks;
        }
        public static Block GetGenesisBlock()
        {
            var block = GetBlocks().FindAll().FirstOrDefault();
            return block;
        }
        public static Block GetBlockByHeight(int height)
        {
            var blocks = DbContext.DB.GetCollection<Block>(DbContext.RSRV_BLOCKS);
            blocks.EnsureIndex(x => x.Height); ;
            var block = blocks.FindOne(x => x.Height == height);
            return block;
        }

        public static Block GetBlockByHash(string hash)
        {
            var blocks = DbContext.DB.GetCollection<Block>(DbContext.RSRV_BLOCKS);
            blocks.EnsureIndex(x => x.Height); ;
            var block = blocks.FindOne(x => x.Hash == hash);
            return block;
        }
        public static Block GetLastBlock()
        {
            var blockchain = GetBlocks();
            var block = blockchain.FindOne(Query.All(Query.Descending));
            return block;
        }

        public static long GetHeight()
        {
            var lastBlock = GetLastBlock();
            return lastBlock.Height;
        }
        public static void AddBlock(Block block)
        {
            var blocks = GetBlocks();
            blocks.Insert(block);
        }
        private static float GetTotalFees(IList<Transaction> txs)
        {
            var totFee = txs.AsEnumerable().Sum(x => x.Fee);
            return (float)totFee;
        }

        public static void CraftNewBlock()
        {

        }
        public static void PrintBlock(Block block)
        {
            Console.WriteLine("\n===========\nNew Block created");
            Console.WriteLine(" * Block Height....: {0}", block.Height);
            Console.WriteLine(" * Version         : {0}", block.Version);
            Console.WriteLine(" * Previous Hash...: {0}", block.PrevHash);
            Console.WriteLine(" * Hash            : {0}", block.Hash);
            Console.WriteLine(" * Merkle Hash.....: {0}", block.MerkleRoot);
            Console.WriteLine(" * Timestamp       : {0}", TimeUtil.ToDateTime(block.Timestamp));
            Console.WriteLine(" * Difficulty......: {0}", block.Difficulty);
            Console.WriteLine(" * Chain Validator : {0}", block.Validator);

            Console.WriteLine(" * Number Of Tx(s) : {0}", block.NumOfTx);
            Console.WriteLine(" * Amout...........: {0}", block.TotalAmount);
            Console.WriteLine(" * Reward          : {0}", block.TotalReward);
            Console.WriteLine(" * Size............: {0}", block.Size);
            Console.WriteLine(" * Craft Time      : {0}", block.BCraftTime);


        }
    }
}
