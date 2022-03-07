using LiteDB;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;

namespace ReserveBlockCore.Services
{
    public class BlockQueueService
    {
        public static bool QueueProcessing = false;

        public static void UpdateMemBlocks()
        {
            var blockChain = BlockchainData.GetBlocks();
            var blocks = blockChain.Find(Query.All(Query.Descending)).ToList();

            Program.MemBlocks = blocks.Take(15).ToList();
        }

        public static async Task ProcessBlockQueue()
        {
            if(!QueueProcessing)
            {
                QueueProcessing = true;
                var blockQueue = BlockchainData.GetBlockQueue();
                var blockQueueAll = blockQueue.FindAll();
                if (blockQueueAll.Count() > 0)
                {
                    var queueOrdered = blockQueueAll.OrderBy(x => x.Height).ThenBy(x => x.Timestamp).ToList();
                    foreach (var block in queueOrdered)
                    {
                        var blockchain = BlockchainData.GetBlocks();
                        var findBlock = blockchain.FindOne(x => x.Height == block.Height);
                        if (findBlock == null)
                        {
                            await BlockValidatorService.ValidateBlock(block);//insert into blockchain
                            blockQueue.DeleteMany(x => x.Height == block.Height);//delete from queue
                            
                        }
                        else
                        {
                            blockQueue.DeleteMany(x => x.Height == block.Height); //delete from queue
                        }
                    }
                }

                QueueProcessing = false;
            }
        }

        public static async Task<bool> AddBlock(Block block)
        {
            var blockQueue = BlockchainData.GetBlockQueue();
            var blockchain = BlockchainData.GetBlocks();
            bool BroadcastBlock = false;

            var blockchainBlock = blockchain.FindOne(x => x.Height == block.Height);
            if(blockchainBlock == null)
            {
                var blockQueueBlock = blockQueue.FindOne(x => x.Height == block.Height);
                if(blockQueueBlock == null)
                {
                    BroadcastBlock = true;
                    blockQueue.Insert(block);
                    try
                    {
                        await P2PClient.BroadcastBlock(block);
                    }
                    catch (Exception ex)
                    {

                    }
                }
            }

            return BroadcastBlock;
        }
    }
}
