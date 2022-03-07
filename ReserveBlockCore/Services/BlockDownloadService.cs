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
                BlockQueueService.QueueProcessing = true;
                await BlockQueueService.ProcessBlockQueue();

                var myBlockHeight = BlockchainData.GetHeight();
                var difference = nHeight - myBlockHeight;
                try
                {
                    for (int i = 1; i <= difference; i++)
                    {
                        //call out to nodes and get blocks.
                        var nextBlockHeight = myBlockHeight + i;
                        var newBlocks = await P2PClient.GetBlock();

                        if (newBlocks.Count() > 0)
                        {
                            foreach (var block in newBlocks)
                            {
                                if (block != null)
                                {
                                    var blockResult = await BlockValidatorService.ValidateBlock(block, true);
                                    if(blockResult == false)
                                    {

                                    }
                                }
                            }


                        }
                    }
                    BlockQueueService.QueueProcessing = false;
                }
                catch (Exception ex)
                {
                    //Error
                    BlockQueueService.QueueProcessing = false;
                }

                return false; //we return false once complete to alert wallet it is done downloading bulk blocks
            }
            return false;
        }

        public static async Task BlockCollisionResolve(Block badBlock, Block goodBlock)
        {

        }

    }
}
