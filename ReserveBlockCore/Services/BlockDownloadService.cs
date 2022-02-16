using ReserveBlockCore.Data;
using ReserveBlockCore.Models;

namespace ReserveBlockCore.Services
{
    public class BlockDownloadService
    {
        public static async Task<bool> GetAllBlocks(long nHeight)
        {
            bool result = false;

            var myBlockHeight = BlockchainData.GetHeight();
            var difference = nHeight - myBlockHeight;

            for(int i = 1; i <= difference; i++)
            {
                //call out to nodes and get blocks.
                var nextBlock = myBlockHeight + i;

            }

            return false;
        }
    }
}
