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
                        P2PClient.LastTaskSentTime = DateTime.Now;
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
                        P2PClient.LastTaskResultTime = DateTime.Now;
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
                                    P2PClient.LastTaskBlockHeight = nextBlock.Height;
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

                    if(message == "tx")
                    {
                        var transaction = JsonConvert.DeserializeObject<Transaction>(data);
                        if (transaction != null)
                        {
                            var isTxStale = await TransactionData.IsTxTimestampStale(transaction);
                            if (!isTxStale)
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
                                                mempool.Insert(transaction);
                                            }
                                        }

                                    }
                                    else
                                    {

                                        var isCraftedIntoBlock = await TransactionData.HasTxBeenCraftedIntoBlock(transaction);
                                        if (!isCraftedIntoBlock)
                                        {

                                        }
                                        else
                                        {
                                            try
                                            {
                                                mempool.DeleteMany(x => x.Hash == transaction.Hash);// tx has been crafted into block. Remove.
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
                                            mempool.Insert(transaction);
                                        }
                                    }
                                }
                            }

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
