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

namespace ReserveBlockCore.P2P
{
    public class P2PAdjServer : Hub
    {
        #region Broadcast methods
        public override async Task OnConnectedAsync()
        {
            string lastArea = "";
            var startTime = DateTime.UtcNow;
            ConnectionHistory.ConnectionHistoryQueue conQueue = new ConnectionHistory.ConnectionHistoryQueue();
            try
            {
                var keepValConnected = true;
                var peerIP = GetIP(Context);
                bool logData = false;
                                
                var httpContext = Context.GetHttpContext();
                if (httpContext != null)
                {
                    var address = httpContext.Request.Headers["address"].ToString();
                    var uName = httpContext.Request.Headers["uName"].ToString();
                    var signature = httpContext.Request.Headers["signature"].ToString();
                    var walletVersion = httpContext.Request.Headers["walver"].ToString();

                    var walletVersionVerify = WalletVersionUtility.Verify(walletVersion);

                    var fortisPool = Globals.FortisPool.Values;
                    lastArea = "A";
                    if (!string.IsNullOrWhiteSpace(address) && !string.IsNullOrWhiteSpace(uName) && !string.IsNullOrWhiteSpace(signature) && walletVersionVerify)
                    {
                        try
                        {
                            conQueue.Address = address;
                            conQueue.IPAddress = peerIP;
                            
                            lastArea = "B";
                            var stateAddress = StateData.GetSpecificAccountStateTrei(address);
                            if (!string.IsNullOrWhiteSpace(address))
                            {
                                lastArea = "C";
                                if (stateAddress.Balance >= 1000)
                                {
                                    lastArea = "D";
                                    var verifySig = SignatureService.VerifySignature(address, address, signature);
                                    if (verifySig != false)
                                    {
                                        lastArea = "E";
                                        var valExist = fortisPool.Where(x => x.Address == address || x.IpAddress == peerIP).FirstOrDefault();
                                        if (valExist == null)
                                        {
                                            lastArea = "F";
                                            FortisPool fortisPools = new FortisPool();
                                            fortisPools.IpAddress = peerIP;
                                            fortisPools.UniqueName = uName;
                                            fortisPools.ConnectDate = DateTime.UtcNow;
                                            fortisPools.Address = address;                                            
                                            fortisPools.WalletVersion = walletVersion;
                                            fortisPools.Context = Context;

                                            Globals.FortisPool[(peerIP, address)] = fortisPools;

                                            //keepValConnected = true;                                            
                                            //Console.WriteLine("User Added! RBX Addr: " + address + " Unique Name: " + uName);
                                        }
                                        else
                                        {
                                            lastArea = "G";
                                            var validator = fortisPool.Where(x => x.Address == address || x.IpAddress == peerIP).FirstOrDefault();
                                            if (validator != null)
                                            {
                                                lastArea = "H";
                                                if (validator.Address != address)
                                                {
                                                    lastArea = "I";
                                                    DateTime? lastResponseTime = validator.LastAnswerSendDate != null ? validator.LastAnswerSendDate.Value.AddMinutes(15) : null;
                                                    if(lastResponseTime == null)
                                                    {
                                                        lastArea = "J";
                                                        var connectDate = validator.ConnectDate.AddMinutes(15);
                                                        if (connectDate >= DateTime.UtcNow)
                                                        {
                                                            lastArea = "K";
                                                            //Connection aborted
                                                            await SendAdjMessageSingle("status", "Disconnected. Connect DateTime >= Current DateTime");
                                                            keepValConnected = false;
                                                        }
                                                        else
                                                        {
                                                            lastArea = "L";
                                                            validator.ConnectDate = DateTime.UtcNow;
                                                            validator.Address = address;
                                                            validator.Context = Context;
                                                            validator.UniqueName = uName;
                                                            validator.IpAddress = peerIP;
                                                            validator.WalletVersion = walletVersion;
                                                            validator.LastAnswerSendDate = null;
                                                            //keepValConnected = true;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        lastArea = "M";
                                                        if (lastResponseTime >= DateTime.Now)
                                                        {
                                                            lastArea = "N";
                                                            //Connection aborted
                                                            await SendAdjMessageSingle("status", "Disconnected. Last Task Sent DateTime >= Current Last Task Sent DateTime");
                                                            keepValConnected = false;
                                                        }
                                                        else
                                                        {
                                                            lastArea = "O";
                                                            validator.ConnectDate = DateTime.UtcNow;
                                                            validator.Address = address;
                                                            validator.Context = Context;
                                                            validator.UniqueName = uName;
                                                            validator.IpAddress = peerIP;
                                                            validator.WalletVersion = walletVersion;
                                                            validator.LastAnswerSendDate = null;
                                                            //keepValConnected = true;
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    lastArea = "P";
                                                    if (validator.IpAddress == peerIP)
                                                    {
                                                        lastArea = "Q";
                                                        validator.ConnectDate = DateTime.UtcNow;
                                                        validator.Address = address;
                                                        validator.Context = Context;
                                                        validator.UniqueName = uName;
                                                        validator.IpAddress = peerIP;
                                                        validator.WalletVersion = walletVersion;
                                                        validator.LastAnswerSendDate = null;
                                                        //keepValConnected = true;
                                                    }
                                                }
                                                lastArea = "R";
                                                //ConsoleWriterService.Output($"User Updated! RBX Addr: {address} / Unique Name: {uName} / Peer IP: {peerIP}");
                                            }
                                            else
                                            {
                                                FortisPool fortisPools = new FortisPool();
                                                fortisPools.IpAddress = peerIP;
                                                fortisPools.UniqueName = uName;
                                                fortisPools.ConnectDate = DateTime.UtcNow;
                                                fortisPools.Address = address;
                                                fortisPools.Context = Context;
                                                fortisPools.WalletVersion = walletVersion;

                                                Globals.FortisPool[(peerIP, address)] = fortisPools;
                                                //keepValConnected = true;
                                            }
                                            lastArea = "S";
                                        }

                                        await SendAdjMessageSingle("status", $"Authenticated? {keepValConnected}");

                                        var fortisPoolStr = "";

                                        if (Globals.CurrentTaskQuestion == null)
                                        {
                                            lastArea = "T";
                                            conQueue.WasSuccess = true;
                                            await SendAdjMessageSingle("status", "Connected");
                                            Globals.CurrentTaskQuestion = await TaskQuestionUtility.CreateTaskQuestion("rndNum");
                                            ConsoleWriterService.Output("Task Created");
                                            var taskQuest = Globals.CurrentTaskQuestion;
                                            TaskQuestion nTaskQuestion = new TaskQuestion();
                                            nTaskQuestion.TaskType = taskQuest.TaskType;
                                            nTaskQuestion.BlockHeight = taskQuest.BlockHeight;
                                            string taskQuestionStr = "";
                                            taskQuestionStr = JsonConvert.SerializeObject(nTaskQuestion);
                                            await SendAdjMessageAll("task", taskQuestionStr);
                                            //Console.WriteLine("Task Sent All");
                                        }
                                        else
                                        {
                                            conQueue.WasSuccess = true;
                                            lastArea = "U";
                                            await SendAdjMessageSingle("status", "Connected");
                                            var taskQuest = Globals.CurrentTaskQuestion;
                                            TaskQuestion nTaskQuestion = new TaskQuestion();
                                            nTaskQuestion.TaskType = taskQuest.TaskType;
                                            nTaskQuestion.BlockHeight = taskQuest.BlockHeight;
                                            string taskQuestionStr = "";
                                            taskQuestionStr = JsonConvert.SerializeObject(nTaskQuestion);
                                            await SendAdjMessageSingle("task", taskQuestionStr);
                                            //Console.WriteLine("Task Sent Single");
                                        }
                                    }
                                    else
                                    {
                                        lastArea = "V";
                                        await SendAdjMessageSingle("status", "Connected, but your address signature failed to verify. You are being disconnected.");
                                        keepValConnected = false;
                                        if (Globals.OptionalLogging == true)
                                        {
                                            LogUtility.Log("Connected, but your address signature failed to verify with ADJ: " + address, "Adj Connection");
                                        }

                                    }
                                }
                                else
                                {
                                    lastArea = "W";
                                    await SendAdjMessageSingle("status", "Connected, but you do not have the minimum balance of 1000 RBX. You are being disconnected.");
                                    keepValConnected = false;
                                    if (Globals.OptionalLogging == true)
                                    {
                                        LogUtility.Log("Connected, but you do not have the minimum balance of 1000 RBX: " + address, "Adj Connection");
                                    }

                                }
                            }
                            else
                            {
                                lastArea = "X";
                                await SendAdjMessageSingle("status", "Connection Attempted, But failed to find the address in trie. You are being disconnected.");
                                keepValConnected = false;
                                if (Globals.OptionalLogging == true)
                                {
                                    LogUtility.Log("Connection Attempted, but missing field Address: " + address + " IP: " + peerIP, "Adj Connection");
                                }

                            }

                        }
                        catch (Exception ex)
                        {
                            lastArea = "Y";
                            DbContext.Rollback();
                            if (Globals.OptionalLogging == true)
                            {
                                ErrorLogUtility.LogError($"Exception thrown: Error: {ex.ToString()}", "Adj Connection");
                            }
                        }
                    }
                    else
                    {
                        lastArea = "Z";
                        await SendAdjMessageSingle("status", "Connection Attempted, but missing field(s). Address, Unique name, and Signature required. You are being disconnected.");
                        keepValConnected = false;
                        if (Globals.OptionalLogging == true)
                        {
                            LogUtility.Log("Connected, but missing field(s). Address, Unique name, and Signature required: " + address, "Adj Connection");
                        }
                    }
                }
                else
                {
                    lastArea = "1";
                    keepValConnected = false;
                    await SendAdjMessageSingle("status", "httpcontext was null");
                    if (Globals.OptionalLogging == true)
                    {
                        LogUtility.Log("Httpcontext was null.", "Adj Connection");
                    }
                }

                if (keepValConnected)
                {
                    if (Globals.FortisPool.TryGetFromKey1(peerIP, out var pool) && pool.Value.Context.ConnectionId != Context.ConnectionId)
                    {
                        pool.Value.Context.Abort();
                        pool.Value.Context = Context;
                    }
                }
                else
                {
                    Globals.FortisPool.TryRemoveFromKey1(peerIP, out var test);
                    Context.Abort();
                }

                if (Globals.OptionalLogging == true)
                {
                    LogUtility.Log($"Last Area Reached : '{lastArea}'. IP: {peerIP} ", "Adj Connection");
                }

                conQueue.ConnectionTime = (DateTime.UtcNow - startTime).Milliseconds;
                Globals.ConnectionHistoryDict.TryAdd(conQueue.Address, conQueue);
            }
            catch (Exception ex)
            {
                DbContext.Rollback();
                //Connection attempt failed with unhandled error.
                //if (Globals.OptionalLogging == true)
                //{
                //    ErrorLogUtility.LogError($"Unhandled exception has happend. Error : {ex.ToString()}", "P2PAdjServer.OnConnectedAsync()");
                //}
                //Going to produce all errors for now.
                ErrorLogUtility.LogError($"Unhandled exception has happend. Error : {ex.ToString()}", "P2PAdjServer.OnConnectedAsync()");
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? ex)
        {
            var peerIP = GetIP(Context);
            Globals.P2PPeerDict.TryRemove(peerIP, out var test);
            Globals.FortisPool.TryRemoveFromKey1(peerIP, out var test2);

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

        #region Receive Rand Num and Task Answer **NEW
        public async Task<TaskAnswerResult> ReceiveTaskAnswer_New(TaskNumberAnswer taskResult)
        {
            TaskAnswerResult taskAnsRes = new TaskAnswerResult();
            try
            {
                if(taskResult != null)
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
                        if (Globals.BlocksDownloading == 0)
                        {
                            if (Globals.Adjudicate)
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
            catch(Exception ex) 
            {
                ErrorLogUtility.LogError($"Error Processing Task - Error: {ex.ToString()}", "P2PAdjServer.ReceiveTaskAnswer_New()");
            }
            taskAnsRes.AnswerCode = 1337; // Unknown Error
            return taskAnsRes;
        }

        #endregion

        #region Receive Winning Task Block Answer **NEW
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
                            if (Globals.BlocksDownloading == 0)
                            {
                                if (Globals.Adjudicate)
                                {
                                    //This will result in users not getting their answers chosen if they are not in list.                                    
                                    if (Globals.FortisPool.TryGetFromKey1(ipAddress, out var Out))
                                    {
                                        (winningTask.Address, _) = Out;                                        
                                        if (Globals.TaskSelectedNumbers.TryGetValue(winningTask.Address, out var Winner))
                                        {
                                            if (winningTask.WinningBlock.Height == Globals.LastBlock.Height + 1 &&
                                        winningTask.VerifySecret == Globals.VerifySecret)
                                            {
                                                Globals.TaskWinnerDict[winningTask.Address] = winningTask;
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

        #region Receive Block and Task Answer **Deprecated
        public async Task<bool> ReceiveTaskAnswer(TaskAnswer taskResult)
        {
            try
            {
                if(taskResult != null)
                {
                    if(taskResult.Block != null)
                    {
                        if (taskResult.Block.Size > 1048576)
                            return false;

                        var ipAddress = GetIP(Context);
                        return await P2PServer.SignalRQueue(Context, (int)taskResult.Block.Size, async () =>
                        {
                            if (Globals.BlocksDownloading == 0)
                            {
                                if (Globals.Adjudicate)
                                {
                                    //This will result in users not getting their answers chosen if they are not in list.                                    
                                    if (Globals.FortisPool.TryGetFromKey1(ipAddress, out var Out))
                                    {
                                        (taskResult.Address, _) = Out;
                                        if (taskResult.Block.Height == Globals.LastBlock.Height + 1)
                                        {                                            
                                            if (!Globals.TaskAnswerDict.TryGetValue(taskResult.Address, out var Answer))
                                            {
                                                taskResult.SubmitTime = DateTime.UtcNow;
                                                Globals.TaskAnswerDict[taskResult.Address] = taskResult;
                                                return true;
                                            }
                                        }
                                        else
                                        {
                                            //RejectedTaskAnswerList.Add(taskResult);
                                            return false;
                                        }
                                    }
                                    else
                                    {
                                        //RejectedTaskAnswerList.Add(taskResult);
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
            catch(Exception ex)
            {
                return false;
            }
            
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
                    if (Globals.BlocksDownloading == 0)
                    {
                        if (Globals.Adjudicate)
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
                                                if (!Globals.BroadcastedTrxDict.TryGetValue(transaction.Hash, out var test))
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
