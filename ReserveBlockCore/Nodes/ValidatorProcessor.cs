using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Nodes
{
    public class ValidatorProcessor
    {
        public static async Task ProcessData(string message, string data)
        {
            if (Program.PeersConnecting == false)
            {
                if (message == null || message == "")
                {
                    return;
                }
                if (Program.StopAllTimers == false && Program.BlockCrafting == false && Program.BlocksDownloading != true) //this will prevent new blocks from coming in if flag. Normally only flagged when syncing chain.
                {
                    if(message == "task")
                    {
                        var taskQuestion = JsonConvert.DeserializeObject<TaskQuestion>(data);
                        switch(taskQuestion.TaskType)
                        {
                            case "rndNum":
                                RandomNumberTask();
                                break;
                        }
                    }
                    if(message == "taskResult")
                    {
                        await BlockQueueService.ProcessBlockQueue();

                        var nextBlock = JsonConvert.DeserializeObject<Block>(data);
                        var nextHeight = Program.BlockHeight + 1;
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
                                }
                            }
                            if (nextHeight < currentHeight)
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

                        await BlockQueueService.ProcessBlockQueue();
                    }
                    if(message == "fortisPool")
                    {
                        var fortisPool = JsonConvert.DeserializeObject<List<FortisPool>>(data);
                        if(fortisPool != null)
                        {
                            P2PAdjServer.FortisPool = fortisPool;
                        }
                    }

                }

            }
        }

        private static async void RandomNumberTask()
        {
            var taskAnswer = new TaskAnswer();
            var num = TaskQuestionUtility.GenerateRandomNumber();
            var fortisPool = P2PAdjServer.FortisPool.ToList();
            taskAnswer.Address = Program.ValidatorAddress;
            taskAnswer.Answer = num.ToString();
            var block = await BlockchainData.CraftNewBlock(Program.ValidatorAddress, fortisPool.Count(), num.ToString());
            if(block != null)
            {
                taskAnswer.Block = block;
                await P2PClient.SendTaskAnswer(taskAnswer);
            }

        }
        

    }
}
