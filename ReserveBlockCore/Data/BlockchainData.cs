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

                //Updates state trei
                StateData.CreateGenesisWorldTrei(block);

                // move the processed mempool tx(s) into Finalized table
                transactions.ForEach(x => { Transaction.Add(x); });
                // clear mempool
                trxPool.DeleteAll();
            }
        }

        public static decimal GetBlockReward()
        {
            var BlockReward = HalvingUtility.GetBlockReward(); 
            return BlockReward;
        }
        public static List<Transaction> GiveOtherInfos(List<Transaction> trxs, long height)
        {
            foreach (var trx in trxs)
            {
                trx.Height = height;
            }
            return trxs;
        }
        //Method needing validator functions still.
        public static void CraftNewBlock(string validator)
        {
            // start craft time
            var startCraftTimer = DateTime.UtcNow;

            //Get tx's from Mempool
            var processedTxPool = TransactionData.ProcessTxPool();
            var txPool = TransactionData.GetPool();

            var lastBlock = GetLastBlock();
            var height = lastBlock.Height + 1;
            var timestamp = TimeUtil.GetTime();
            //Need to get master node validator.

            var transactionList = new List<Transaction>();

            var coinbase_tx = new Transaction
            {
                Amount = 0,
                ToAddress = validator,
                Fee = 0.00M,
                Timestamp = timestamp,
                FromAddress = "Coinbase_TrxFees",
            };

            var coinbase_tx2 = new Transaction
            {
                Amount = GetBlockReward(),
                ToAddress = validator,
                Fee = 0.00M,
                Timestamp = timestamp,
                FromAddress = "Coinbase_BlkRwd",
            };

            //this is just for testing. Validator will always get reward regardless of tx count. 
            if (processedTxPool.Count() > 0)
            {
                coinbase_tx.Amount = GetTotalFees(processedTxPool);
                coinbase_tx.Build();
                coinbase_tx2.Build();

                transactionList.Add(coinbase_tx);
                transactionList.Add(coinbase_tx2);
                
                transactionList.AddRange(processedTxPool);

                txPool.DeleteAll();
            }
            else
            {
                coinbase_tx2.Build();
                transactionList.Add(coinbase_tx2);
            }

            var block = new Block
            {
                Height = height,
                Timestamp = timestamp,
                Transactions = GiveOtherInfos(transactionList, height),
                Validator = validator
            };
            block.Build();


            //block size
            var str = JsonConvert.SerializeObject(block);
            block.Size = str.Length;

            // get craft time    
            var endTimer = DateTime.UtcNow;
            var buildTime = endTimer - startCraftTimer;
            block.BCraftTime = buildTime.Milliseconds;

            //validates the coinbase tx's
            var blockValResult = ValidateBlock(block);

            if(blockValResult == true)
            {
                AddBlock(block);
                //Update World Trei with new State Root
                WorldTrei.UpdateWorldTrei(block);
                //Need to publish block to known nodes. 
                PrintBlock(block);

                //This might be double redundant. Possibly fix.
                foreach (var tx in transactionList)
                {
                    Transaction.Add(tx);
                }
            }
            else
            {
                Console.WriteLine("Error! Block was not validated.");
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
        public static bool ValidateBlock(Block block)
        {
            bool result = false;

            var txList = block.Transactions.ToList();

            foreach(var tx in txList)
            {
                if (tx.FromAddress == "Coinbase_TrxFees")
                {
                    //validating fees to ensure block is not malformed.
                    result = tx.Amount == txList.Sum(y => y.Fee) ? true : false;
                    if (result == false)
                    {
                        break;
                    }
                }
                if (tx.FromAddress == "Coinbase_BlkRwd")
                {
                    //validating block reward to ensure block is not malformed.
                    result = tx.Amount == GetBlockReward() ? true : false;
                    if (result == false)
                    {
                        break;
                    }
                }
            }
            
            return result;
        }
        public static void AddBlock(Block block)
        {
            var blocks = GetBlocks();
            blocks.Insert(block);
        }
        private static decimal GetTotalFees(List<Transaction> txs)
        {
            var totFee = txs.AsEnumerable().Sum(x => x.Fee);
            return totFee;
        }

        public static IEnumerable<Block> GetBlocksByValidator(string address)
        {

            var blocks = DbContext.DB.GetCollection<Block>(DbContext.RSRV_BLOCKS);
            blocks.EnsureIndex(x => x.Validator);
            var query = blocks.Query()
                .OrderByDescending(x => x.Height)
                .Where(x => x.Validator == address)
                .Limit(20).ToList();
            return query;
        }

            public static void PrintBlock(Block block)
        {
            Console.WriteLine("\n===========\nBlock Info:");
            Console.WriteLine(" * Block Height....: {0}", block.Height);
            Console.WriteLine(" * Version         : {0}", block.Version);
            Console.WriteLine(" * Previous Hash...: {0}", block.PrevHash);
            Console.WriteLine(" * Hash            : {0}", block.Hash);
            Console.WriteLine(" * Merkle Hash.....: {0}", block.MerkleRoot);
            Console.WriteLine(" * State Hash.....: {0}", block.StateRoot);
            Console.WriteLine(" * Timestamp       : {0}", TimeUtil.ToDateTime(block.Timestamp));
            Console.WriteLine(" * Chain Validator : {0}", block.Validator);

            Console.WriteLine(" * Number Of Tx(s) : {0}", block.NumOfTx);
            Console.WriteLine(" * Amout...........: {0}", block.TotalAmount);
            Console.WriteLine(" * Reward          : {0}", block.TotalReward);
            Console.WriteLine(" * Size............: {0}", block.Size);
            Console.WriteLine(" * Craft Time      : {0}", block.BCraftTime);


        }
    }
}
