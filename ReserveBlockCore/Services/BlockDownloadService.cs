using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;

namespace ReserveBlockCore.Services
{
    public class BlockDownloadService
    {
        public static async Task<bool> GetAllBlocks(long nHeight)
        {
            if(Program.PeersConnecting == false)
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
                            foreach (var block in newBlock)
                            {
                                if (block != null)
                                {
                                    await BlockValidatorService.ValidateBlock(block, true);
                                }
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
            return false;
        }
    }
}
