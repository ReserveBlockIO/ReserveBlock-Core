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
                if (Globals.StopAllTimers == false && Globals.BlocksDownloading != 1) //this will prevent new blocks from coming in if flag. Normally only flagged when syncing chain.
                {
                    if (message == "tx")
                    {
                        var transaction = JsonConvert.DeserializeObject<Transaction>(data);
                        if (transaction != null)
                        {
                            var isTxStale = await TransactionData.IsTxTimestampStale(transaction);
                            if(!isTxStale)
                            {
                                var mempool = TransactionData.GetPool();
                                if (mempool.Count() != 0)
                                {
                                    var txFound = mempool.FindOne(x => x.Hash == transaction.Hash);
                                    if (txFound == null)
                                    {

                                        var txResult = await TransactionValidatorService.VerifyTX(transaction);
                                        if (txResult == true)
                                        {
                                            var dblspndChk = await TransactionData.DoubleSpendCheck(transaction);
                                            var isCraftedIntoBlock = await TransactionData.HasTxBeenCraftedIntoBlock(transaction);

                                            if (dblspndChk == false && isCraftedIntoBlock == false)
                                            {
                                                mempool.InsertSafe(transaction);
                                                P2PClient.SendTXMempool(transaction);
                                            }
                                        }

                                    }
                                    else
                                    {

                                        var isCraftedIntoBlock = await TransactionData.HasTxBeenCraftedIntoBlock(transaction);
                                        if (!isCraftedIntoBlock)
                                        {
                                            //P2PClient.SendTXMempool(transaction); // send to everyone I am connected too (out connects)
                                        }
                                        else
                                        {
                                            try
                                            {
                                                mempool.DeleteManySafe(x => x.Hash == transaction.Hash);// tx has been crafted into block. Remove.
                                            }
                                            catch (Exception ex)
                                            {
                                                //delete failed
                                            }
                                        }                                            
                                    }
                                }
                                else
                                {

                                    var txResult = await TransactionValidatorService.VerifyTX(transaction);
                                    if (txResult == true)
                                    {
                                        var dblspndChk = await TransactionData.DoubleSpendCheck(transaction);
                                        var isCraftedIntoBlock = await TransactionData.HasTxBeenCraftedIntoBlock(transaction);

                                        if (dblspndChk == false && isCraftedIntoBlock == false)
                                        {
                                            mempool.InsertSafe(transaction);
                                            P2PClient.SendTXMempool(transaction);
                                        }
                                    }
                                }
                            }
                                
                        }

                    }

                    if (message == "val")
                    {

                    }

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
}
