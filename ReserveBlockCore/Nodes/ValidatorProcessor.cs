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
        public static async Task ProcessData(string message, string data, string ipAddress)
        {
            if (message == null || message == "")
            {
                return;
            }
            if (Globals.StopAllTimers == false && Globals.BlocksDownloading != 1) //this will prevent new blocks from coming in if flag. Normally only flagged when syncing chain.
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
                    await BlockValidatorService.ValidationDelay();
                    Globals.LastTaskResultTime = DateTime.Now;
                    var nextBlock = JsonConvert.DeserializeObject<Block>(data);
                    var nextHeight = Globals.LastBlock.Height + 1;
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
                        if (Globals.BlocksDownloading == 0 && !BlockDownloadService.BlockDict.ContainsKey(currentHeight))
                        {
                            BlockDownloadService.BlockDict[currentHeight] = (nextBlock, ipAddress);
                            if (nextHeight == currentHeight)
                                await BlockValidatorService.ValidateBlocks();
                            if (nextHeight < currentHeight)
                                await BlockDownloadService.GetAllBlocks();
                        }    
                    }

                    await BlockValidatorService.ValidationDelay();
                }
                if(message == "fortisPool")
                {
                    var fortisPool = JsonConvert.DeserializeObject<List<FortisPool>>(data);
                    if(fortisPool != null)
                    {
                        Globals.FortisPool = fortisPool;
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
                                            mempool.InsertSafe(transaction);
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
                                            mempool.DeleteManySafe(x => x.Hash == transaction.Hash);// tx has been crafted into block. Remove.
                                        }
                                        catch (Exception ex)
                                        {
                                            DbContext.Rollback();
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
            var fortisPool = Globals.FortisPool.ToList();
            taskAnswer.Address = Globals.ValidatorAddress;
            taskAnswer.Answer = num.ToString();
            var block = await BlockchainData.CraftNewBlock(Globals.ValidatorAddress, fortisPool.Count(), num.ToString());
            if(block != null)
            {
                taskAnswer.Block = block;
                await P2PClient.SendTaskAnswer(taskAnswer);
            }
            else
            {
                ValidatorLogUtility.Log("Failed to add block. Block was null", "ValidatorProcessor.RandomNumberTask()");
                Globals.LastTaskError = true;
            }

        }
        

    }
}
