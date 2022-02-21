using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;

namespace ReserveBlockCore.Services
{
    public class BlockDownloadService
    {
        public static async Task<bool> GetAllBlocks(long nHeight)
        {
            var myBlockHeight = BlockchainData.GetHeight();
            var difference = nHeight - myBlockHeight;

            for(int i = 1; i <= difference; i++)
            {
                //call out to nodes and get blocks.
                var nextBlock = myBlockHeight + i;
                var newBlock = await P2PClient.GetBlock();

                if(newBlock != null)
                {
                    //await BlockValidatorService.ValidateBlock(newBlock);
                }
            }

            return false; //we return false once complete to alert wallet it is done downloading bulk blocks
        }
    }
}
