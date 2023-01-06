using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System;

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

            Globals.AdjNodes.TryGetValue(ipAddress, out var node);
            if(message == "task")
            {
                var taskQuestion = JsonConvert.DeserializeObject<TaskQuestion>(data);
                switch(taskQuestion.TaskType)
                {
                    case "rndNum":
                        if(Globals.LastBlock.Height >= Globals.BlockLock)
                        {
                            RandomNumberTaskV3(taskQuestion.BlockHeight);
                        }
                        else
                        {
                            RandomNumberTask_New(taskQuestion.BlockHeight);                                
                        }
                        break;
                }
            }

            if (message == "sendWinningBlock")
            {
                var verifySecret = data != null ? data : "Empty";
                var taskWin = new TaskWinner();
                var fortisPool = Globals.FortisPool.Values;
                var answer = Globals.CurrentTaskNumberAnswerV3.Signature != null ? Globals.CurrentTaskNumberAnswerV3.Answer.ToString() 
                    : Globals.CurrentTaskNumberAnswerV2.Item2?.Answer;
                
                if ((DateTime.Now - node.LastWinningTaskRequestTime).Seconds < 4)
                    return;

                if (Globals.LastBlock.Height + 1 != Globals.CurrentWinner?.WinningBlock?.Height || verifySecret != Globals.CurrentWinner?.VerifySecret)
                {
                    if (answer != null)
                    {
                        var block = await BlockchainData.CraftNewBlock_New(Globals.ValidatorAddress, fortisPool.Count(), answer);
                        if (block != null)
                        {
                            var blockString = JsonConvert.SerializeObject(block);
                            taskWin.VerifySecret = verifySecret;
                            taskWin.Address = Globals.ValidatorAddress;
                            taskWin.WinningBlock = block;
                            Globals.CurrentWinner = taskWin;
                            node.LastWinningTaskRequestTime = DateTime.Now;
                            if (block.Height > Globals.BlockLock)
                                await P2PClient.SendWinningTaskV3(node, blockString, block.Height);
                            else
                                await P2PClient.SendWinningTask_New(taskWin);
                        }
                        else
                        {
                            ValidatorLogUtility.Log("Failed to add block. Block was null", "ValidatorProcessor.ProcessData() - sendWinningBlock");
                            node?.AsParamater(x => x.LastTaskError = true);
                        }
                    }
                }
                else
                {
                    if (Globals.CurrentWinner?.WinningBlock.Height > Globals.BlockLock)
                    {
                        node.LastWinningTaskRequestTime = DateTime.Now;
                        var blockString = JsonConvert.SerializeObject(Globals.CurrentWinner.WinningBlock);
                        await P2PClient.SendWinningTaskV3(node, blockString, Globals.CurrentWinner.WinningBlock.Height);
                    }
                    else
                        await P2PClient.SendWinningTask_New(Globals.CurrentWinner);
                }
            }

            if(message == "taskResult")
            {
                await BlockValidatorService.ValidationDelay();
                node?.AsParamater(x => x.LastTaskResultTime = DateTime.Now);
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
                    if (!BlockDownloadService.BlockDict.ContainsKey(currentHeight))
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
                try
                {
                    var fortisPool = JsonConvert.DeserializeObject<List<FortisPool>>(data);
                    if (fortisPool != null)
                    {
                        foreach (var pool in fortisPool)
                            Globals.FortisPool[(pool.IpAddress, pool.Address)] = pool;
                    }
                }
                catch(Exception ex)
                {
                    ErrorLogUtility.LogError($"Error getting Masternodes (Fortis Pool). Error: {ex.ToString}", "ValidatorProcessor.ProcessData()");
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
                                if (txResult.Item1 == true)
                                {
                                    var dblspndChk = await TransactionData.DoubleSpendReplayCheck(transaction);
                                    var isCraftedIntoBlock = await TransactionData.HasTxBeenCraftedIntoBlock(transaction);
                                    var rating = await TransactionRatingService.GetTransactionRating(transaction);
                                    transaction.TransactionRating = rating;

                                    if (dblspndChk == false && isCraftedIntoBlock == false && rating != TransactionRating.F)
                                    {
                                        mempool.InsertSafe(transaction);
                                    }
                                }

                            }
                            else
                            {
                                var isCraftedIntoBlock = await TransactionData.HasTxBeenCraftedIntoBlock(transaction);
                                if (isCraftedIntoBlock)
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
                            if (txResult.Item1 == true)
                            {
                                var dblspndChk = await TransactionData.DoubleSpendReplayCheck(transaction);
                                var isCraftedIntoBlock = await TransactionData.HasTxBeenCraftedIntoBlock(transaction);
                                var rating = await TransactionRatingService.GetTransactionRating(transaction);
                                transaction.TransactionRating = rating;

                                if (dblspndChk == false && isCraftedIntoBlock == false && rating != TransactionRating.F)
                                {
                                    mempool.InsertSafe(transaction);
                                }
                            }
                        }
                    }

                }
            }                       
        }

        public static async void RandomNumberTaskV3(long blockHeight)
        {
            if (string.IsNullOrWhiteSpace(Globals.ValidatorAddress))
                return;

            while (Globals.LastBlock.Height + 1 != blockHeight)
            {                
                await BlockDownloadService.GetAllBlocks();
            }

            if ((DateTime.Now - Globals.CurrentTaskNumberAnswerV3.Time).Seconds < 4)
            {
                return;
            }

            if (Globals.CurrentTaskNumberAnswerV3.Height != blockHeight)
            {
                var num = TaskQuestionUtility.GenerateRandomNumber(blockHeight);                
                var Signature = SignatureService.ValidatorSignature(blockHeight + ":" + num);
                Globals.CurrentTaskNumberAnswerV3 = (blockHeight, num, Signature, DateTime.Now);
            }

            await P2PClient.SendTaskAnswerV3(Globals.CurrentTaskNumberAnswerV3.Answer + ":" + Globals.CurrentTaskNumberAnswerV3.Signature);
        }

        public static async void RandomNumberTask_New(long blockHeight)
        {
            if (string.IsNullOrWhiteSpace(Globals.ValidatorAddress))
                return;

            var nextBlock = Globals.LastBlock.Height + 1;
            if (nextBlock != blockHeight)
            {
                //download blocks
                await BlockDownloadService.GetAllBlocks();
            }

            var taskAnswer = new TaskNumberAnswerV2();
            if (Globals.CurrentTaskNumberAnswerV2.Item1 != blockHeight)
            {
                var num = TaskQuestionUtility.GenerateRandomNumber(blockHeight);
                taskAnswer.Address = Globals.ValidatorAddress;
                taskAnswer.Answer = num.ToString();
                taskAnswer.SubmitTime = DateTime.Now;
                taskAnswer.NextBlockHeight = blockHeight;
                Globals.CurrentTaskNumberAnswerV2 = (blockHeight, taskAnswer, DateTime.Now);
            }
            else if((DateTime.Now - Globals.CurrentTaskNumberAnswerV2.Item3).Seconds < 5)
            {
                return;
            }
            else
                taskAnswer = Globals.CurrentTaskNumberAnswerV2.Item2;
            await P2PClient.SendTaskAnswer_New(taskAnswer);

        }
    }
}
