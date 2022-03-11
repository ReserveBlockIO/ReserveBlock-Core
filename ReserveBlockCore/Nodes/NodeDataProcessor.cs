using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;

namespace ReserveBlockCore.Nodes
{
    public class NodeDataProcessor
    {
        public static async Task ProcessData(string message, string data)
        {
            if(Program.PeersConnecting == false)
            {
                if (message == null || message == "")
                {
                    return;
                }
                else
                {
                    if (Program.StopAllTimers == false && Program.BlockCrafting == false) //this will prevent new blocks from coming in if flag. Normally only flagged when syncing chain.
                    {
                        if (message == "tx")
                        {
                            var transaction = JsonConvert.DeserializeObject<Transaction>(data);
                            if (transaction != null)
                            {
                                var mempool = TransactionData.GetPool();
                                if (mempool.Count() != 0)
                                {
                                    var txFound = mempool.FindOne(x => x.Hash == transaction.Hash);
                                    if (txFound == null)
                                    {
                                        var dblspndChk = await TransactionData.DoubleSpendCheck(transaction);

                                        var txResult = TransactionValidatorService.VerifyTX(transaction); //sends tx to connected peers
                                        if (txResult == true && dblspndChk == false)
                                        {
                                            P2PServer.TxRebroadcastDict.Add(transaction.Hash, 1);
                                            mempool.Insert(transaction);
                                            P2PClient.SendTXMempool(transaction);
                                        }
                                    }
                                    else
                                    {
                                        
                                    }
                                }
                                else
                                {
                                    var dblspndChk = await TransactionData.DoubleSpendCheck(transaction);

                                    var txResult = TransactionValidatorService.VerifyTX(transaction);
                                    if (txResult == true && dblspndChk == false)
                                    {
                                        P2PServer.TxRebroadcastDict.Add(transaction.Hash, 1);
                                        mempool.Insert(transaction);
                                        P2PClient.SendTXMempool(transaction);
                                    }
                                }
                            }

                        }

                        if (message == "val")
                        {
                            await P2PClient.GetMasternodes();
                        }

                        if (message == "blk")
                        {
                            await BlockQueueService.ProcessBlockQueue();

                            var nextBlock = JsonConvert.DeserializeObject<Block>(data);
                            var nextHeight = BlockchainData.GetHeight() + 1;
                            var currentHeight = nextBlock.Height;

                            if (currentHeight < nextHeight)
                            {
                                //already have block
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
                                if (nextHeight == currentHeight)
                                {
                                    var broadcast = await BlockQueueService.AddBlock(nextBlock);

                                    if (broadcast == true)
                                    {
                                        Console.WriteLine("Block was added from: " + nextBlock.Validator);
                                        P2PClient.BroadcastBlock(nextBlock);//broadcast out to your connected nodes
                                    }
                                }
                                else
                                {
                                    // means we need to download some blocks
                                    //Check to make sure blocks aren't already being downloaded, so we don't downloaded them multiple times
                                    if (Program.BlocksDownloading == false)
                                    {
                                        Program.BlocksDownloading = true; 
                                        var setDownload = await BlockDownloadService.GetAllBlocks(currentHeight);
                                        Program.BlocksDownloading = setDownload;
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
