using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using Spectre.Console;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace ReserveBlockCore.Services
{
    public class BlockDownloadService
    {
        public static ConcurrentDictionary<long, (Block block, string IPAddress)> BlockDict = 
            new ConcurrentDictionary<long, (Block, string)>();

        public const int MaxDownloadBuffer = 52428800;

        public static async Task<bool> GetAllBlocks()
        {                        
            if (Interlocked.Exchange(ref Globals.BlocksDownloading, 1) != 0)
            {
                await Task.Delay(1000);
                return false;
            }

            try
            {                
                var (_, MaxHeight) = await P2PClient.GetCurrentHeight();
                while (Globals.LastBlock.Height < MaxHeight)
                {                                                   
                    var coolDownTime = DateTime.Now;
                    var taskDict = new ConcurrentDictionary<long, (Task<Block> task, string ipAddress)>();
                    var heightToDownload = Globals.LastBlock.Height + 1;
                    
                    foreach (var node in Globals.Nodes.Values)
                    {
                        if (heightToDownload > MaxHeight)
                            break;
                        taskDict[heightToDownload] = (P2PClient.GetBlock(heightToDownload, node), node.NodeIP);
                        heightToDownload++;
                    }

                    while (taskDict.Any())
                    {
                        var completedTask = await Task.WhenAny(taskDict.Values.Select(x => x.task));
                        var result = await completedTask;

                        if (result == null)
                        {
                            var badTasks = taskDict.Where(x => x.Value.task.Id == completedTask.Id &&
                                x.Value.task.IsCompleted).ToArray();

                            foreach (var badTask in badTasks)
                                taskDict.TryRemove(badTask.Key, out var test);

                            heightToDownload = Math.Min(heightToDownload, badTasks.Min(x => x.Key));
                        }
                        else
                        {
                            var resultHeight = result.Height;
                            var (_, ipAddress) = taskDict[resultHeight];
                            BlockDict[resultHeight] = (result, ipAddress);
                            taskDict.TryRemove(resultHeight, out var test2);
                            _ = BlockValidatorService.ValidateBlocks();
                        }

                        _ = P2PClient.DropLowBandwidthPeers();
                        var AvailableNode = Globals.Nodes.Where(x => x.Value.IsSendingBlock == 0).FirstOrDefault().Value;
                        if (AvailableNode != null)
                        {
                            var DownloadBuffer = BlockDict.AsParallel().Sum(x => x.Value.block.Size);
                            if (DownloadBuffer > MaxDownloadBuffer)
                            {
                                if ((DateTime.Now - coolDownTime).Seconds > 30 && taskDict.Keys.Any())
                                {
                                    var staleHeight = taskDict.Keys.Min();
                                    var staleTask = taskDict[staleHeight];
                                    if (Globals.Nodes.TryRemove(staleTask.ipAddress, out var staleNode))
                                        _ = staleNode.Connection.DisposeAsync();
                                    taskDict.TryRemove(staleHeight, out var test4);
                                    staleTask.task.Dispose();
                                    heightToDownload = Math.Min(heightToDownload, staleHeight);
                                    coolDownTime = DateTime.Now;
                                }
                            }
                            else
                            {
                                var nextHeightToValidate = Globals.LastBlock.Height + 1;
                                if (!BlockDict.ContainsKey(nextHeightToValidate) && !taskDict.ContainsKey(nextHeightToValidate))
                                    heightToDownload = nextHeightToValidate;
                                while (taskDict.ContainsKey(heightToDownload))
                                    heightToDownload++;
                                if (heightToDownload > MaxHeight)
                                    continue;
                                taskDict[heightToDownload] = (P2PClient.GetBlock(heightToDownload, AvailableNode),
                                    AvailableNode.NodeIP);
                            }
                        }

                    }

                    (_, MaxHeight) = await P2PClient.GetCurrentHeight();
                }
            }
            catch (Exception ex)
            {
                //Error
                if (Globals.PrintConsoleErrors == true)
                {
                    Console.WriteLine("Failure in GetAllBlocks Method");
                    Console.WriteLine(ex.Message);
                }
            }
            finally
            {
                Interlocked.Exchange(ref Globals.BlocksDownloading, 0);
            }

            return false;
        }

        public static async Task BlockCollisionResolve(Block badBlock, Block goodBlock)
        {

        }

    }
}
