using LiteDB;
using ReserveBlockCore.Data;

namespace ReserveBlockCore.Services
{
    public class BlockQueueService
    {
        public static void UpdateMemBlocks()
        {
            var blockChain = BlockchainData.GetBlocks();
            var blocks = blockChain.Find(Query.All(Query.Descending)).ToList();

            Program.MemBlocks = blocks.Take(15).ToList();
        }

        public static void UpdateBlockQueue()
        {

        }
    }
}
