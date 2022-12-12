using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using static ReserveBlockCore.Models.ConnectionHistory;

namespace ReserveBlockCore.P2P
{
    public class P2PAdjServer : Hub
    {
        #region Broadcast methods
        public override async Task OnConnectedAsync()
        {
            string lastArea = "";
            string peerIP = "";
            var startTime = DateTime.UtcNow;
            ConnectionHistory.ConnectionHistoryQueue conQueue = new ConnectionHistory.ConnectionHistoryQueue();
            try
            {
                peerIP = GetIP(Context);
                var httpContext = Context.GetHttpContext();
                if(httpContext == null)
                {                    
                    await EndOnConnect(peerIP, "1", startTime, conQueue, "httpcontext was null", "httpcontext was null");
                    return;
                }

                var address = httpContext.Request.Headers["address"].ToString();
                var time = httpContext.Request.Headers["time"].ToString();
                var uName = httpContext.Request.Headers["uName"].ToString();
                var signature = httpContext.Request.Headers["signature"].ToString();
                var walletVersion = httpContext.Request.Headers["walver"].ToString();

                var SignedMessage = address;
                if (Globals.LastBlock.Height >= Globals.BlockLock)
                {
                    SignedMessage = address + ":" + time;
                    if (TimeUtil.GetTime() - long.Parse(time) > 30000000)
                    {
                        await EndOnConnect(peerIP, "20", startTime, conQueue, "Signature Bad time.", "Signature Bad time.");
                        return;
                    }
                }

                conQueue.Address = address;
                conQueue.IPAddress = peerIP;

                var walletVersionVerify = WalletVersionUtility.Verify(walletVersion);

                var fortisPool = Globals.FortisPool.Values;                
                if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(uName) || string.IsNullOrWhiteSpace(signature) || !walletVersionVerify) 
                {
                    await EndOnConnect(peerIP, "Z", startTime, conQueue,
                        "Connection Attempted, but missing field(s). Address, Unique name, and Signature required. You are being disconnected.",
                        "Connected, but missing field(s). Address, Unique name, and Signature required: " + address);
                    return;
                }
                
                var stateAddress = StateData.GetSpecificAccountStateTrei(address);
                if(stateAddress == null)
                {
                    await EndOnConnect(peerIP, "X", startTime, conQueue,
                        "Connection Attempted, But failed to find the address in trie. You are being disconnected.",
                        "Connection Attempted, but missing field Address: " + address + " IP: " + peerIP);
                    return;                    
                }

                if(stateAddress.Balance < 1000)
                {
                    await EndOnConnect(peerIP, "W", startTime, conQueue,
                        "Connected, but you do not have the minimum balance of 1000 RBX. You are being disconnected.",
                        "Connected, but you do not have the minimum balance of 1000 RBX: " + address);
                    return;
                }

                var verifySig = SignatureService.VerifySignature(address, SignedMessage, signature);
                if(!verifySig)
                {
                    await EndOnConnect(peerIP, "V", startTime, conQueue,
                        "Connected, but your address signature failed to verify. You are being disconnected.",
                        "Connected, but your address signature failed to verify with ADJ: " + address);
                    return;
                }

                var fortisPools = new FortisPool();
                fortisPools.IpAddress = peerIP;
                fortisPools.UniqueName = uName;
                fortisPools.ConnectDate = DateTime.UtcNow;
                fortisPools.Address = address;
                fortisPools.Context = Context;
                fortisPools.WalletVersion = walletVersion;

                UpdateFortisPool(fortisPools);

                await SendAdjMessageSingle("status", $"Authenticated? True");
                conQueue.WasSuccess = true;
                lastArea = "U";
                await SendAdjMessageSingle("status", "Connected");
                TaskQuestion nTaskQuestion = new TaskQuestion();
                nTaskQuestion.TaskType = "rndNum";
                nTaskQuestion.BlockHeight = Globals.LastBlock.Height + 1;
                string taskQuestionStr = "";
                taskQuestionStr = JsonConvert.SerializeObject(nTaskQuestion);
                await SendAdjMessageSingle("task", taskQuestionStr);

                lastArea = "A";
                if (Globals.OptionalLogging == true)                
                    LogUtility.Log($"Last Area Reached : '{lastArea}'. IP: {peerIP} ", "Adj Connection");                

                conQueue.ConnectionTime = (DateTime.UtcNow - startTime).Milliseconds;
                Globals.ConnectionHistoryDict.TryAdd(conQueue.Address, conQueue);
            }
            catch (Exception ex)
            {
                Globals.FortisPool.TryRemoveFromKey1(peerIP, out _);
                Context?.Abort();
                ErrorLogUtility.LogError($"Unhandled exception has happend. Error : {ex.ToString()}", "P2PAdjServer.OnConnectedAsync()");
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? ex)
        {
            var peerIP = GetIP(Context);
            Globals.P2PPeerDict.TryRemove(peerIP, out _);
            Globals.FortisPool.TryRemoveFromKey1(peerIP, out _);
            Context?.Abort();

            await base.OnDisconnectedAsync(ex);
        }

        private async Task SendAdjMessageSingle(string message, string data)
        {
            await Clients.Caller.SendAsync("GetAdjMessage", message, data);
        }

        private async Task SendAdjMessageAll(string message, string data)
        {
            await Clients.All.SendAsync("GetAdjMessage", message, data);
        }

        private async Task EndOnConnect(string ipAddress, string lastArea, DateTime startTime, ConnectionHistoryQueue queue, 
            string adjMessage, string loggMessage)
        {            
            await SendAdjMessageSingle("status", adjMessage);
            if (Globals.OptionalLogging == true)
            {
                LogUtility.Log(loggMessage, "Adj Connection");
                LogUtility.Log($"Last Area Reached : '{lastArea}'. IP: {ipAddress} ", "Adj Connection");
            }


            queue.ConnectionTime = (DateTime.UtcNow - startTime).Milliseconds;
            Globals.ConnectionHistoryDict.TryAdd(queue.Address, queue);
            Context?.Abort();
        }

        private static void UpdateFortisPool(FortisPool pool)
        {
            var hasIpPool = Globals.FortisPool.TryGetFromKey1(pool.IpAddress, out var ipPool);
            var hasAddressPool = Globals.FortisPool.TryGetFromKey2(pool.Address, out var addressPool);

            if (hasIpPool && ipPool.Value.Context.ConnectionId != pool.Context.ConnectionId)
                ipPool.Value.Context.Abort();

            if (hasAddressPool && addressPool.Value.Context.ConnectionId != pool.Context.ConnectionId)
                addressPool.Value.Context.Abort();

            Globals.FortisPool[(pool.IpAddress, pool.Address)] = pool;
        }

        #endregion

        #region Get Connected Val Count

        public static async Task<int> GetConnectedValCount()
        {
            try
            {
                var peerCount = Globals.FortisPool.Count;
                return peerCount;
            }
            catch { }

            return -1;
        }

        #endregion

        #region Receive Rand Num and Task Answer V3
        public async Task<TaskAnswerResult> ReceiveTaskAnswerV3(string request)
        {
            var taskAnsRes = new TaskAnswerResult();
            var taskResult = request?.Split(':');
            if (taskResult == null || taskResult.Length != 2)
            {
                taskAnsRes.AnswerCode = 5; // Task answer was null. Should not be possible.
                return taskAnsRes;
            }

            if (Globals.AdjudicateAccount == null)
            {
                taskAnsRes.AnswerCode = 4; //adjudicator is still booting up
                return taskAnsRes;
            }

            try
            {
                var (Answer, Signature) = (taskResult[0], taskResult[1]);
                var answerSize = Answer.Length + Signature.Length;
                var ipAddress = GetIP(Context);
                return  await P2PServer.SignalRQueue(Context, answerSize, async () =>
                {
                    //This will result in users not getting their answers chosen if they are not in list.
                    var fortisPool = Globals.FortisPool.Values;
                    if (Globals.FortisPool.TryGetFromKey1(ipAddress, out var Pool))
                    {                            
                        var NextHeight = Globals.LastBlock.Height + 1;
                        if (!SignatureService.VerifySignature(Pool.Key2, NextHeight + ":" + Answer, Signature))
                        {
                            NextHeight++;
                            if (!SignatureService.VerifySignature(Pool.Key2, NextHeight + ":" + Answer, Signature))
                            {
                                taskAnsRes.AnswerCode = 6;
                                return taskAnsRes;
                            }
                        }

                        if (!Globals.TaskAnswerDictV3.TryAdd((Pool.Key2, NextHeight), (ipAddress, Pool.Key2, int.Parse(Answer), Signature)))
                        {
                            taskAnsRes.AnswerAccepted = true;
                            taskAnsRes.AnswerCode = 0;
                            return taskAnsRes;
                        }

                        taskAnsRes.AnswerCode = 7; // Answer was already submitted
                        return taskAnsRes;
                    }

                    Context.Abort();
                    taskAnsRes.AnswerCode = 3; //address is not pressent in the fortis pool
                    return taskAnsRes;
                });

            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError($"Error Processing Task - Error: {ex.ToString()}", "P2PAdjServer.ReceiveTaskAnswerV3()");
            }
            taskAnsRes.AnswerCode = 1337; // Unknown Error
            return taskAnsRes;
        }

        #endregion

        #region Receive Winning Task Block Answer V2
        public async Task<bool> ReceiveWinningTaskBlock(TaskWinner winningTask)
        {
            try
            {
                if(winningTask != null)
                {
                    if(winningTask.WinningBlock != null)
                    {
                        if (winningTask.WinningBlock.Size > 1048576)
                            return false;

                        var ipAddress = GetIP(Context);
                        return await P2PServer.SignalRQueue(Context, (int)winningTask.WinningBlock.Size, async () =>
                        {
                            if (Globals.BlocksDownloadSlim.CurrentCount != 0)
                            {
                                if (Globals.AdjudicateAccount != null)
                                {
                                    //This will result in users not getting their answers chosen if they are not in list.                                    
                                    if (Globals.FortisPool.TryGetFromKey1(ipAddress, out var Out))
                                    {
                                        (winningTask.Address, _) = Out;                                        
                                        if (Globals.TaskSelectedNumbersV2.TryGetValue(winningTask.Address, out var Winner))
                                        {
                                            if (winningTask.WinningBlock.Height == Globals.LastBlock.Height + 1 &&
                                        winningTask.VerifySecret == Globals.VerifySecret)
                                            {
                                                Globals.TaskWinnerDictV2[winningTask.Address] = winningTask;
                                                return true;
                                            }
                                            else
                                            {
                                                return false;
                                            }
                                        }
                                        else
                                        {
                                            return false;
                                        }
                                    }
                                    else
                                    {
                                        return false;
                                    }
                                }
                            }
                            return false;
                        });
                    }
                }

                return false;
            }
            catch { }
            return false;
        }

        #endregion

        #region Receive Winning Task Block Answer V3
        public async Task<bool> ReceiveWinningBlockV3(Block block)
        {
            try
            {
                if (block == null || block.Size > 1048576 || Globals.AdjudicateAccount == null || block.Height != Globals.LastBlock.Height + 1)
                    return false;


                var ipAddress = GetIP(Context);
                return await P2PServer.SignalRQueue(Context, (int)block.Size, async () =>
                {
                    if (!Globals.FortisPool.TryGetFromKey1(ipAddress, out var Pool))
                        return false;

                    var RBXAddress = Pool.Key2;
                    if(!Globals.TaskSelectedNumbersV3.ContainsKey((RBXAddress, block.Height)))
                    {                       
                        return false;
                    }                    

                    if (SignatureService.VerifySignature(RBXAddress, block.Hash, block.ValidatorSignature)
                        && RBXAddress == block.Validator && Globals.TaskWinnerDictV3.TryAdd((RBXAddress, block.Height), block))
                    {
                        return true;
                    }

                    return false;
                });
            }
            catch { }
            return false;
        }

        #endregion

        #region Receive Block and Task Answer V2

        public async Task<TaskAnswerResult> ReceiveTaskAnswer_New(TaskNumberAnswerV2 taskResult)
        {
            TaskAnswerResult taskAnsRes = new TaskAnswerResult();
            try
            {
                if (taskResult != null)
                {
                    var answerSize = JsonConvert.SerializeObject(taskResult).Length;
                    if (answerSize > 1048576)
                    {
                        taskAnsRes.AnswerCode = 1; //Answer too large
                        return taskAnsRes;
                    }

                    var ipAddress = GetIP(Context);
                    return await P2PServer.SignalRQueue(Context, answerSize, async () =>
                    {
                        if (Globals.BlocksDownloadSlim.CurrentCount != 0)
                        {
                            if (Globals.AdjudicateAccount != null)
                            {
                                //This will result in users not getting their answers chosen if they are not in list.
                                var fortisPool = Globals.FortisPool.Values;
                                if (Globals.FortisPool.TryGetFromKey1(ipAddress, out var Out))
                                {
                                    (taskResult.Address, _) = Out;
                                    if (taskResult.NextBlockHeight == Globals.LastBlock.Height + 1)
                                    {
                                        if (!Globals.TaskAnswerDict_New.TryGetValue(taskResult.Address, out var Answer))
                                        {
                                            taskResult.SubmitTime = DateTime.Now;
                                            Globals.TaskAnswerDict_New[taskResult.Address] = taskResult;
                                            taskAnsRes.AnswerAccepted = true;
                                            taskAnsRes.AnswerCode = 0;
                                            return taskAnsRes;
                                        }
                                    }
                                    else
                                    {
                                        var nextBlockHeight = Globals.LastBlock.Height + 1;
                                        taskAnsRes.AnswerCode = 2; //Answers block height did not match the adjudicators next block height
                                        return taskAnsRes;
                                    }
                                }
                                else
                                {
                                    taskAnsRes.AnswerCode = 3; //address is not pressent in the fortis pool
                                    return taskAnsRes;
                                }
                            }
                        }
                        taskAnsRes.AnswerCode = 4; //adjudicator is still booting up
                        return taskAnsRes;
                    });
                }
                taskAnsRes.AnswerCode = 5; // Task answer was null. Should not be possible.

                return taskAnsRes;
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError($"Error Processing Task - Error: {ex.ToString()}", "P2PAdjServer.ReceiveTaskAnswer_New()");
            }
            taskAnsRes.AnswerCode = 1337; // Unknown Error
            return taskAnsRes;
        }

        #endregion

        #region Receive TX to relay
        public async Task<bool> ReceiveTX(Transaction transaction)
        {
            try
            {
                return await P2PServer.SignalRQueue(Context, (transaction.Data?.Length ?? 0) + 1028, async () =>
                {
                    bool output = false;
                    if (Globals.BlocksDownloadSlim.CurrentCount != 0)
                    {
                        if (Globals.AdjudicateAccount != null)
                        {
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
                                                var dblspndChk = await TransactionData.DoubleSpendReplayCheck(transaction);
                                                var isCraftedIntoBlock = await TransactionData.HasTxBeenCraftedIntoBlock(transaction);
                                                var rating = await TransactionRatingService.GetTransactionRating(transaction);
                                                transaction.TransactionRating = rating;

                                                if (dblspndChk == false && isCraftedIntoBlock == false && rating != TransactionRating.F)
                                                {
                                                    mempool.InsertSafe(transaction);
                                                    var txOutput = "";
                                                    txOutput = JsonConvert.SerializeObject(transaction);
                                                    await SendAdjMessageAll("tx", txOutput);//sends messages to all in fortis pool
                                                    Globals.BroadcastedTrxDict[transaction.Hash] = transaction;
                                                    output = true;
                                                }
                                            }

                                        }
                                        else
                                        {

                                            var isCraftedIntoBlock = await TransactionData.HasTxBeenCraftedIntoBlock(transaction);
                                            if (!isCraftedIntoBlock)
                                            {
                                                if (!Globals.BroadcastedTrxDict.TryGetValue(transaction.Hash, out _))
                                                {
                                                    var txOutput = "";
                                                    txOutput = JsonConvert.SerializeObject(transaction);
                                                    await SendAdjMessageAll("tx", txOutput);
                                                    Globals.BroadcastedTrxDict[transaction.Hash] = transaction;
                                                }
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
                                            var dblspndChk = await TransactionData.DoubleSpendReplayCheck(transaction);
                                            var isCraftedIntoBlock = await TransactionData.HasTxBeenCraftedIntoBlock(transaction);
                                            var rating = await TransactionRatingService.GetTransactionRating(transaction);
                                            transaction.TransactionRating = rating;

                                            if (dblspndChk == false && isCraftedIntoBlock == false && rating != TransactionRating.F)
                                            {
                                                mempool.InsertSafe(transaction);
                                                var txOutput = "";
                                                txOutput = JsonConvert.SerializeObject(transaction);
                                                await SendAdjMessageAll("tx", txOutput);//sends messages to all in fortis pool
                                                output = true;
                                            }
                                        }
                                    }
                                }

                            }
                        }
                    }

                    return output;
                });
            }
            catch { } //incorrect TX received

            return false;
        }

        #endregion

        #region Get IP

        private static string GetIP(HubCallerContext context)
        {
            try
            {
                var peerIP = "NA";
                var feature = context.Features.Get<IHttpConnectionFeature>();
                if (feature != null)
                {
                    if (feature.RemoteIpAddress != null)
                    {
                        peerIP = feature.RemoteIpAddress.MapToIPv4().ToString();
                    }
                }

                return peerIP;
            }
            catch { }

            return "0.0.0.0";
        }

        #endregion
    }
}
