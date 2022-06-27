using LiteDB;
using Newtonsoft.Json;
using ReserveBlockCore.Models;
using ReserveBlockCore.Utilities;
using ReserveBlockCore.P2P;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReserveBlockCore.Services;
using System.Numerics;
using ReserveBlockCore.EllipticCurve;
using System.Globalization;

namespace ReserveBlockCore.Data
{
    internal class BlockchainData
    {
        public IList<Transaction> PendingTransactions = new List<Transaction>();
        public Blockchain Chain { get; set; }
        public static string ChainRef { get; set; }

        public static int BlockVersion { get; set; }
        internal static async Task InitializeChain()
        {
            await StartupService.DownloadBlocks();

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

                // clear mempool
                trxPool.DeleteAll();
            }
        }
        //Method needing validator functions still.
        public static async Task<Block?> CraftNewBlock(string validator, int totalVals, string valAnswer)
        {
            try
            {
                await BlockQueueService.ProcessBlockQueue();

                var startCraftTimer = DateTime.UtcNow;
                var validatorAccount = AccountData.GetSingleAccount(validator);

                if (validatorAccount == null)
                {
                    return null;
                }

                //Get tx's from Mempool
                var processedTxPool = TransactionData.ProcessTxPool();
                var txPool = TransactionData.GetPool();

                var lastBlock = Program.LastBlock;
                var height = lastBlock.Height + 1;

                //Need to get master node validator.
                var timestamp = TimeUtil.GetTime();
                var transactionList = new List<Transaction>();

                //var coinbase_tx = new Transaction
                //{
                //    Amount = 0,
                //    ToAddress = validator,
                //    Fee = 0.00M,
                //    Timestamp = timestamp,
                //    FromAddress = "Coinbase_TrxFees",
                //    TransactionType = TransactionType.TX
                //};

                var coinbase_tx2 = new Transaction
                {
                    Amount = GetBlockReward(),
                    ToAddress = validator,
                    Fee = 0.00M,
                    Timestamp = timestamp,
                    FromAddress = "Coinbase_BlkRwd",
                    TransactionType = TransactionType.TX
                };

                if (processedTxPool.Count() > 0)
                {
                    //commenting these out to test burning of fee.
                    //coinbase_tx.Amount = GetTotalFees(processedTxPool);
                    //coinbase_tx.Build();
                    coinbase_tx2.Build();

                    //transactionList.Add(coinbase_tx);
                    transactionList.Add(coinbase_tx2);

                    transactionList.AddRange(processedTxPool);

                    //need to only delete processed mempool tx's in event new ones get added while creating block.
                    //delete after block is added, so they can't  be re-added before block is over.
                    foreach (var tx in processedTxPool)
                    {
                        var txRec = txPool.FindOne(x => x.Hash == tx.Hash);
                        if (txRec != null)
                        {
                            //txPool.DeleteMany(x => x.Hash == tx.Hash);
                        }
                    }
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
                    Validator = validator,
                    ChainRefId = ChainRef,
                    TotalValidators = totalVals,
                    ValidatorAnswer = valAnswer
                };
                block.Build();

                BigInteger b1 = BigInteger.Parse(validatorAccount.PrivateKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
                PrivateKey privateKey = new PrivateKey("secp256k1", b1);

                //Add validator signature
                block.ValidatorSignature = SignatureService.CreateSignature(block.Hash, privateKey, validatorAccount.PublicKey);

                //block size
                var str = JsonConvert.SerializeObject(block);
                block.Size = str.Length;

                // get craft time    
                var endTimer = DateTime.UtcNow;
                var buildTime = endTimer - startCraftTimer;
                block.BCraftTime = buildTime.Milliseconds;


                var blockValResult = await BlockValidatorService.ValidateBlockForTask(block);

                if (blockValResult == true)
                {
                    return block;
                }

                
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.Message, "BlockchainData.CraftNewBlock(string validator)");
            }
            // start craft time
            return null;
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

        public static ILiteCollection<Block> GetBlocks()
        {
            try
            {
                var blocks = DbContext.DB.GetCollection<Block>(DbContext.RSRV_BLOCKS);
                blocks.EnsureIndex(x => x.Height);
                return blocks;
            }
            catch(Exception ex)
            {
                ErrorLogUtility.LogError(ex.Message, "BlockchainData.GetBlocks()");
                return null;
            }
            
        }
        public static ILiteCollection<Block> GetBlockQueue()
        {
            var blocks = DbContext.DB_Queue.GetCollection<Block>(DbContext.RSRV_BLOCK_QUEUE);
            blocks.EnsureIndex(x => x.Height);
            return blocks;
        }
        public static Block GetGenesisBlock()
        {
            var block = GetBlocks().FindAll().FirstOrDefault();
            return block;
        }
        public static Block GetBlockByHeight(long height)
        {
            var blocks = DbContext.DB.GetCollection<Block>(DbContext.RSRV_BLOCKS);
            blocks.EnsureIndex(x => x.Height); 
            var block = blocks.FindOne(x => x.Height == height);
            return block;
        }

        public static Block GetBlockByHash(string hash)
        {
            var blocks = DbContext.DB.GetCollection<Block>(DbContext.RSRV_BLOCKS);
            blocks.EnsureIndex(x => x.Height); 
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
            if(lastBlock != null)
            {
                return lastBlock.Height;
            }
            return -1;
            
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
            blocks.EnsureIndex(x => x.Height);
            //only input block if null
            var blockCheck = blocks.FindOne(x => x.Height == block.Height);
            if (blockCheck == null)
            {
                blocks.Insert(block);

                //Update in memory fields.
                Program.LastBlock = block;
                Program.BlockHeight = block.Height;
            }
            else
            {
                var blockList = blocks.Find(Query.All(Query.Descending)).ToList();
                var eBlock = blockList.Where(x => x.Height == block.Height).FirstOrDefault();
                if (eBlock == null)
                {
                    //database corrupt
                    Program.DatabaseCorruptionDetected = true;
                    //DbContext.DeleteCorruptDb();
                }
            }
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
            Console.WriteLine(" * Chain Reference.: {0}", ChainRef);
            Console.WriteLine(" * Block Height....: {0}", block.Height);
            Console.WriteLine(" * Version         : {0}", block.Version);
            Console.WriteLine(" * Previous Hash...: {0}", block.PrevHash);
            Console.WriteLine(" * Hash            : {0}", block.Hash);
            Console.WriteLine(" * Merkle Hash.....: {0}", block.MerkleRoot);
            Console.WriteLine(" * State Hash......: {0}",  block.StateRoot);
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
