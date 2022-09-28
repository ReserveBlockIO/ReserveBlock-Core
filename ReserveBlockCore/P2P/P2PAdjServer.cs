﻿using Microsoft.AspNetCore.Http.Features;
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
            try
            {
                var keepValConnected = false;
                var peerIP = GetIP(Context);

                string connectionId = Context.ConnectionId;
                var httpContext = Context.GetHttpContext();
                if (httpContext != null)
                {
                    var address = httpContext.Request.Headers["address"].ToString();
                    var uName = httpContext.Request.Headers["uName"].ToString();
                    var signature = httpContext.Request.Headers["signature"].ToString();
                    var walletVersion = httpContext.Request.Headers["walver"].ToString();

                    var walletVersionVerify = WalletVersionUtility.Verify(walletVersion);

                    var fortisPool = Globals.FortisPool.ToList();

                    if (!string.IsNullOrWhiteSpace(address) && !string.IsNullOrWhiteSpace(uName) && !string.IsNullOrWhiteSpace(signature) && walletVersionVerify)
                    {
                        try
                        {
                            var stateAddress = StateData.GetSpecificAccountStateTrei(address);
                            if (!string.IsNullOrWhiteSpace(address))
                            {
                                if (stateAddress.Balance >= 1000)
                                {
                                    var verifySig = SignatureService.VerifySignature(address, address, signature);
                                    if (verifySig != false)
                                    {

                                        var valExist = fortisPool.Where(x => x.Address == address || x.IpAddress == peerIP).FirstOrDefault();
                                        if (valExist == null)
                                        {
                                            FortisPool fortisPools = new FortisPool();
                                            fortisPools.IpAddress = peerIP;
                                            fortisPools.UniqueName = uName;
                                            fortisPools.ConnectDate = DateTime.UtcNow;
                                            fortisPools.Address = address;
                                            fortisPools.ConnectionId = connectionId;
                                            fortisPools.WalletVersion = walletVersion;

                                            Globals.FortisPool.Add(fortisPools);
                                            keepValConnected = true;                                            
                                            //Console.WriteLine("User Added! RBX Addr: " + address + " Unique Name: " + uName);
                                        }
                                        else
                                        {
                                            var validator = Globals.FortisPool.Where(x => x.Address == address || x.IpAddress == peerIP).FirstOrDefault();
                                            if (validator != null)
                                            {
                                                validator.ConnectDate = DateTime.UtcNow;
                                                validator.Address = address;
                                                validator.ConnectionId = connectionId;
                                                validator.UniqueName = uName;
                                                validator.IpAddress = peerIP;
                                                validator.WalletVersion = walletVersion;
                                                keepValConnected = true;
                                                ConsoleWriterService.Output($"User Updated! RBX Addr: {address} / Unique Name: {uName} / Peer IP: {peerIP}");
                                            }
                                            else
                                            {
                                                FortisPool fortisPools = new FortisPool();
                                                fortisPools.IpAddress = peerIP;
                                                fortisPools.UniqueName = uName;
                                                fortisPools.ConnectDate = DateTime.UtcNow;
                                                fortisPools.Address = address;
                                                fortisPools.ConnectionId = connectionId;
                                                fortisPools.WalletVersion = walletVersion;

                                                Globals.FortisPool.Add(fortisPools);                                                
                                                keepValConnected = true;
                                            }
                                        }

                                        var fortisPoolStr = "";

                                        if (Globals.CurrentTaskQuestion == null)
                                        {
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
                                }
                                else
                                {
                                    await SendAdjMessageSingle("status", "Connected, but your address signature failed to verify. You are being disconnected.");
                                    Context.Abort();
                                    if (Globals.OptionalLogging == true)
                                    {
                                        LogUtility.Log("Connected, but your address signature failed to verify with ADJ: " + address, "Adj Connection");
                                    }

                                }
                            }
                            else
                            {
                                await SendAdjMessageSingle("status", "Connected, but you do not have the minimum balance of 1000 RBX. You are being disconnected.");
                                Context.Abort();
                                if (Globals.OptionalLogging == true)
                                {
                                    LogUtility.Log("Connected, but you do not have the minimum balance of 1000 RBX: " + address, "Adj Connection");
                                }

                            }

                        }
                        catch (Exception ex)
                        {
                            DbContext.Rollback();
                            //Console.WriteLine("Error: " + ex.Message.ToString());
                            //Console.WriteLine("Error: " + (ex.StackTrace != null ? ex.StackTrace.ToString() : "No Stack Trace"));
                        }
                    }
                    else
                    {
                        await SendAdjMessageSingle("status", "Connection Attempted, but missing field(s). Address, Unique name, and Signature required. You are being disconnected.");
                        Context.Abort();
                        if (Globals.OptionalLogging == true)
                        {
                            LogUtility.Log("Connected, but missing field(s). Address, Unique name, and Signature required: " + address, "Adj Connection");
                        }
                    }
                }

                if (keepValConnected)
                {
                    if (Globals.AdjPeerList.TryGetValue(peerIP, out var context) && context.ConnectionId != Context.ConnectionId)
                        context.Abort();

                    Globals.AdjPeerList[peerIP] = Context;
                }
                else
                    Context.Abort();

            }
            catch (Exception ex)
            {
                DbContext.Rollback();
                //Connection attempt failed with unhandled error.
                if (Globals.OptionalLogging == true)
                {
                    ErrorLogUtility.LogError($"Unhandled exception has happend. Error : {ex.Message}", "Adj Connection");
                }
            }
            
            
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? ex)
        {
            var peerIP = GetIP(Context);
            Globals.P2PPeerList.TryRemove(peerIP, out var test);

            string connectionId = Context.ConnectionId;
            //FortisPool.RemoveAll(x => x.ConnectionId == connectionId);            
            var fortisPoolStr = "";
            //fortisPoolStr = JsonConvert.SerializeObject(FortisPool);
            //await SendAdjMessageAll("fortisPool", fortisPoolStr);
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
            var peerCount = Globals.AdjPeerList.Count;
            return peerCount;
        }

        #endregion

        #region Receive Block and Task Answer **NEW
        public async Task<bool> ReceiveTaskAnswer_New(TaskNumberAnswer taskResult)
        {
            var answerSize = JsonConvert.SerializeObject(taskResult).Length;
            return await P2PServer.SignalRQueue(Context, answerSize, async () =>
            {
                if (Globals.BlocksDownloading == 0)
                {
                    if (Globals.Adjudicate)
                    {
                        //This will result in users not getting their answers chosen if they are not in list.
                        var fortisPool = Globals.FortisPool.ToList();
                        if (fortisPool.Exists(x => x.Address == taskResult.Address))
                        {
                            if (taskResult.NextBlockHeight == Globals.LastBlock.Height + 1)
                            {
                                var taskAnswerList = Globals.TaskAnswerList_New.ToList();
                                var answerExist = taskAnswerList.Exists(x => x.Address == taskResult.Address);
                                if (!answerExist)
                                {
                                    taskResult.SubmitTime = DateTime.Now;
                                    Globals.TaskAnswerList_New.Add(taskResult);
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

        #endregion

        #region Receive Winning Task Block Answer **NEW
        public async Task<bool> ReceiveWinningTaskBlock(TaskWinner winningTask)
        {
            if (winningTask.WinningBlock.Size > 1048576)
                return false;
            //return await P2PServer.SignalRQueue(Context, (int)winningTask.WinningBlock.Size, async () =>
            //{
                if (Globals.BlocksDownloading == 0)
                {
                    if (Globals.Adjudicate)
                    {
                        //This will result in users not getting their answers chosen if they are not in list.
                        var fortisPool = Globals.FortisPool.ToList();
                        if (fortisPool.Exists(x => x.Address == winningTask.Address))
                        {
                            //if(true)
                            var exist = Globals.TaskSelectedNumbers.Exists(x => x.Address == winningTask.Address);
                            if(exist)
                            {
                                if (winningTask.WinningBlock.Height == Globals.LastBlock.Height + 1 &&
                            winningTask.VerifySecret == Globals.VerifySecret)
                                {
                                    Globals.TaskWinnerList.Add(winningTask);
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
            //});
        }

        #endregion

        #region Receive Block and Task Answer **Deprecated
        public async Task<bool> ReceiveTaskAnswer(TaskAnswer taskResult)
        {
            if (taskResult.Block.Size > 1048576)
                return false;
            return await P2PServer.SignalRQueue(Context, (int)taskResult.Block.Size, async () =>
            {
                if (Globals.BlocksDownloading == 0)
                {
                    if (Globals.Adjudicate)
                    {
                        //This will result in users not getting their answers chosen if they are not in list.
                        var fortisPool = Globals.FortisPool.ToList();
                        if (fortisPool.Exists(x => x.Address == taskResult.Address))
                        {
                            if (taskResult.Block.Height == Globals.LastBlock.Height + 1)
                            {
                                var taskAnswerList = Globals.TaskAnswerList.ToList();
                                var answerExist = taskAnswerList.Exists(x => x.Address == taskResult.Address);
                                if (!answerExist)
                                {
                                    taskResult.SubmitTime = DateTime.UtcNow;
                                    Globals.TaskAnswerList.Add(taskResult);                                    
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

        #endregion

        #region Receive TX to relay

        public async Task<bool> ReceiveTX(Transaction transaction)
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
                                                Globals.BroadcastedTrxList.Add(transaction);
                                                output = true;
                                            }
                                        }

                                    }
                                    else
                                    {

                                        var isCraftedIntoBlock = await TransactionData.HasTxBeenCraftedIntoBlock(transaction);
                                        if (!isCraftedIntoBlock)
                                        {
                                            if (!Globals.BroadcastedTrxList.Exists(x => x.Hash == transaction.Hash))
                                            {
                                                var txOutput = "";
                                                txOutput = JsonConvert.SerializeObject(transaction);
                                                await SendAdjMessageAll("tx", txOutput);
                                                Globals.BroadcastedTrxList.Add(transaction);
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

        #endregion

        #region Get IP

        private static string GetIP(HubCallerContext context)
        {
            var peerIP = "NA";
            var feature = context.Features.Get<IHttpConnectionFeature>();
            if(feature != null)
            {
                if(feature.RemoteIpAddress != null)
                {
                    peerIP = feature.RemoteIpAddress.MapToIPv4().ToString();
                }
            }

            return peerIP;
        }

        #endregion
    }
}
