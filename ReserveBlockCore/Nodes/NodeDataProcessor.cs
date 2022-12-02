using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;

namespace ReserveBlockCore.Nodes
{
    public class NodeDataProcessor
    {
        public static async Task ProcessData(string message, string data, string ipAddress)
        {
            if (message == null || message == "")
            {
                return;
            }
            else
            {
                if (message == "blk")
                {
                    var nextBlock = JsonConvert.DeserializeObject<Block>(data);

                    if(nextBlock != null)
                    {
                        if(nextBlock.ChainRefId != BlockchainData.ChainRef)
                        {                                    
                            var nextHeight = Globals.LastBlock.Height + 1;
                            var currentHeight = nextBlock.Height;

                            if (currentHeight < nextHeight)
                            {                                        
                                await BlockValidatorService.ValidationDelay();
                                var checkBlock = BlockchainData.GetBlockByHeight(currentHeight);

                                if (checkBlock != null)
                                {
                                    var localHash = checkBlock.Hash;
                                    var remoteHash = nextBlock.Hash;

                                    if (localHash != remoteHash)
                                    {
                                        Console.WriteLine("Possible block differ");
                                    }
                                }
                            }
                            else
                            {
                                if (Globals.BlocksDownloading == 0 && !BlockDownloadService.BlockDict.ContainsKey(currentHeight))
                                {
                                    BlockDownloadService.BlockDict[currentHeight] = (nextBlock, ipAddress);
                                    if (nextHeight == currentHeight)
                                        await BlockValidatorService.ValidateBlocks();
                                    if (nextHeight < currentHeight)                                            
                                        await BlockDownloadService.GetAllBlocks();                                                                                      
                                }
  
                            }
                        }
                                
                    }
                }
            }            
        }
    }
}
