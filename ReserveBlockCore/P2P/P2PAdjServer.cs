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
        public static List<FortisPool> FortisPool = new List<FortisPool>();

        public static TaskQuestion? CurrentTaskQuestion = null;

        public static List<TaskAnswer> TaskAnswerList = new List<TaskAnswer>();
        public static List<TaskAnswer> RejectedTaskAnswerList = new List<TaskAnswer>();
        public static List<Transaction> BroadcastedTrxList = new List<Transaction>();

        public static int ValConnectedCount = 0;

        #region Broadcast methods
        public override async Task OnConnectedAsync()
        {
            try
            {
                var peerIP = GetIP(Context);
                if (PeerList.TryGetValue(peerIP, out var context) && context.ConnectionId != Context.ConnectionId)
                    context.Abort();

                PeerList[peerIP] = Context;

                string connectionId = Context.ConnectionId;
                var httpContext = Context.GetHttpContext();
                if (httpContext != null)
                {
                    var address = httpContext.Request.Headers["address"].ToString();
                    var uName = httpContext.Request.Headers["uName"].ToString();
                    var signature = httpContext.Request.Headers["signature"].ToString();
                    var walletVersion = httpContext.Request.Headers["walver"].ToString();

                    var walletVersionVerify = WalletVersionUtility.Verify(walletVersion);

                    var fortisPool = FortisPool.ToList();

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

                                            FortisPool.Add(fortisPools);
                                            ValConnectedCount++;
                                            //Console.WriteLine("User Added! RBX Addr: " + address + " Unique Name: " + uName);
                                        }
                                        else
                                        {
                                            var validator = FortisPool.Where(x => x.Address == address || x.IpAddress == peerIP).FirstOrDefault();
                                            if (validator != null)
                                            {
                                                validator.ConnectDate = DateTime.UtcNow;
                                                validator.Address = address;
                                                validator.ConnectionId = connectionId;
                                                validator.UniqueName = uName;
                                                validator.IpAddress = peerIP;
                                                validator.WalletVersion = walletVersion;
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

                                                FortisPool.Add(fortisPools);
                                                ValConnectedCount++;
                                            }
                                        }

                                        var fortisPoolStr = "";

                                        if (CurrentTaskQuestion == null)
                                        {
                                            await SendAdjMessageSingle("status", "Connected");
                                            CurrentTaskQuestion = await TaskQuestionUtility.CreateTaskQuestion("rndNum");
                                            ConsoleWriterService.Output("Task Created");
                                            var taskQuest = CurrentTaskQuestion;
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
                                            var taskQuest = CurrentTaskQuestion;
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
                                    if (Program.OptionalLogging == true)
                                    {
                                        LogUtility.Log("Connected, but your address signature failed to verify with ADJ: " + address, "Adj Connection");
                                    }

                                }
                            }
                            else
                            {
                                await SendAdjMessageSingle("status", "Connected, but you do not have the minimum balance of 1000 RBX. You are being disconnected.");
                                Context.Abort();
                                if (Program.OptionalLogging == true)
                                {
                                    LogUtility.Log("Connected, but you do not have the minimum balance of 1000 RBX: " + address, "Adj Connection");
                                }

                            }

                        }
                        catch (Exception ex)
                        {
                            //Console.WriteLine("Error: " + ex.Message.ToString());
                            //Console.WriteLine("Error: " + (ex.StackTrace != null ? ex.StackTrace.ToString() : "No Stack Trace"));
                        }
                    }
                    else
                    {
                        await SendAdjMessageSingle("status", "Connection Attempted, but missing field(s). Address, Unique name, and Signature required. You are being disconnected.");
                        Context.Abort();
                        if (Program.OptionalLogging == true)
                        {
                            LogUtility.Log("Connected, but missing field(s). Address, Unique name, and Signature required: " + address, "Adj Connection");
                        }
                    }

                }

            }
            catch (Exception ex)
            {
                //Connection attempt failed with unhandled error.
                if (Program.OptionalLogging == true)
                {
                    ErrorLogUtility.LogError($"Unhandled exception has happend. Error : {ex.Message}", "Adj Connection");
                }
            }
            
            
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? ex)
        {
            string connectionId = Context.ConnectionId;
            //FortisPool.RemoveAll(x => x.ConnectionId == connectionId);
            ValConnectedCount--;
            var fortisPoolStr = "";
            //fortisPoolStr = JsonConvert.SerializeObject(FortisPool);
            //await SendAdjMessageAll("fortisPool", fortisPoolStr);
            await base.OnDisconnectedAsync(ex);
        }

        public async Task SendAdjMessageSingle(string message, string data)
        {
            await Clients.Caller.SendAsync("GetAdjMessage", message, data);
        }

        public async Task SendAdjMessageAll(string message, string data)
        {
            await Clients.All.SendAsync("GetAdjMessage", message, data);
        }


        #endregion

        #region Get Connected Val Count

        public static async Task<int> GetConnectedValCount()
        {
            var peerCount = ValConnectedCount;
            return peerCount;
        }

        #endregion

        #region Receive Block and Task Answer
        public async Task<bool> ReceiveTaskAnswer(TaskAnswer taskResult)
        {
            if (Program.BlocksDownloading == 0)
            {
                if (Program.Adjudicate)
                {
                    //This will result in users not getting their answers chosen if they are not in list.
                    var fortisPool = FortisPool.ToList();
                    if(fortisPool.Exists(x => x.Address == taskResult.Address))
                    {
                        if(taskResult.Block.Height == Program.LastBlock.Height + 1)
                        {
                            var taskAnswerList = TaskAnswerList.ToList();
                            var answerExist = taskAnswerList.Exists(x => x.Address == taskResult.Address);
                            if (!answerExist)
                            {
                                taskResult.SubmitTime = DateTime.UtcNow;
                                TaskAnswerList.Add(taskResult);
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
            
        }

        #endregion

        #region Receive TX to relay

        public async Task<bool> ReceiveTX(Transaction transaction)
        {
            bool output = false;
            if (Program.BlocksDownloading == 0)
            {
                if (Program.Adjudicate)
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
                                        var dblspndChk = await TransactionData.DoubleSpendCheck(transaction);
                                        var isCraftedIntoBlock = await TransactionData.HasTxBeenCraftedIntoBlock(transaction);

                                        if (dblspndChk == false && isCraftedIntoBlock == false)
                                        {
                                            mempool.InsertSafe(transaction);
                                            var txOutput = "";
                                            txOutput = JsonConvert.SerializeObject(transaction);
                                            await SendAdjMessageAll("tx", txOutput);//sends messages to all in fortis pool
                                            BroadcastedTrxList.Add(transaction);
                                            output = true;
                                        }
                                    }

                                }
                                else
                                {

                                    var isCraftedIntoBlock = await TransactionData.HasTxBeenCraftedIntoBlock(transaction);
                                    if (!isCraftedIntoBlock)
                                    {
                                        if(!BroadcastedTrxList.Exists(x => x.Hash == transaction.Hash))
                                        {
                                            var txOutput = "";
                                            txOutput = JsonConvert.SerializeObject(transaction);
                                            await SendAdjMessageAll("tx", txOutput);
                                            BroadcastedTrxList.Add(transaction);
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
                                    var dblspndChk = await TransactionData.DoubleSpendCheck(transaction);
                                    var isCraftedIntoBlock = await TransactionData.HasTxBeenCraftedIntoBlock(transaction);

                                    if (dblspndChk == false && isCraftedIntoBlock == false)
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
