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
            try
            {
                for (int i = 1; i <= difference; i++)
                {
                    //call out to nodes and get blocks.
                    var nextBlockHeight = myBlockHeight + i;
                    var newBlock = await P2PClient.GetBlock();

                    if (newBlock.Count() > 0)
                    {
                        var nextBlock = newBlock.Where(x => x.Height == nextBlockHeight).FirstOrDefault();
                        if (nextBlock != null)
                        {
                            await BlockValidatorService.ValidateBlock(nextBlock);
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                //Error
            }

            return false; //we return false once complete to alert wallet it is done downloading bulk blocks
        }
    }
}
