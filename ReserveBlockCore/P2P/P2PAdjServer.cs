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
        
        #region Broadcast methods
        public override async Task OnConnectedAsync()
        {
            var peerIP = GetIP(Context);
            string connectionId = Context.ConnectionId;
            var httpContext = Context.GetHttpContext();
            if(httpContext != null)
            {
                var address = httpContext.Request.Headers["address"].ToString();
                var uName = httpContext.Request.Headers["uName"].ToString();
                var signature = httpContext.Request.Headers["signature"].ToString();

                var fortisPool = FortisPool.ToList();

                if (address != "" && uName != "" && signature != "")
                {
                    try
                    {
                        var stateAddress = StateData.GetSpecificAccountStateTrei(address);
                        if (address != null)
                        {
                            if (stateAddress.Balance >= 1000)
                            {
                                var verifySig = SignatureService.VerifySignature(address, address, signature);
                                if (verifySig != false)
                                {
                                
                                    var exist = fortisPool.Exists(x => x.ConnectionId == connectionId || x.Address == address);
                                    if (!exist)
                                    {
                                        FortisPool fortisPools = new FortisPool();
                                        fortisPools.IpAddress = peerIP;
                                        fortisPools.UniqueName = uName;
                                        fortisPools.ConnectDate = DateTime.UtcNow;
                                        fortisPools.Address = address;
                                        fortisPools.ConnectionId = connectionId;

                                        FortisPool.Add(fortisPools);
                                        Console.WriteLine("User Added! RBX Addr: " + address + " Unique Name: " + uName);
                                    }
                                    else
                                    {
                                        var validator = FortisPool.Where(x => x.Address == address || x.ConnectionId == connectionId).FirstOrDefault();
                                        if (validator != null)
                                        {
                                            validator.ConnectDate = DateTime.UtcNow;
                                            validator.Address = address;
                                            validator.ConnectionId = connectionId;
                                            validator.UniqueName = uName;
                                            Console.WriteLine("User Updated! RBX Addr: " + address + " Unique Name: " + uName);
                                        }
                                    }

                                    var fortisPoolStr = "";
                                    fortisPoolStr = JsonConvert.SerializeObject(FortisPool);
                                    await SendAdjMessageAll("fortisPool", fortisPoolStr);

                                    Console.WriteLine("Fortis Pool Sent");
                                    if (CurrentTaskQuestion == null)
                                    {
                                        await SendAdjMessageSingle("status", "Connected");
                                        CurrentTaskQuestion = await TaskQuestionUtility.CreateTaskQuestion("rndNum");
                                        Console.WriteLine("Task Created");
                                        var taskQuest = CurrentTaskQuestion;
                                        TaskQuestion nTaskQuestion = new TaskQuestion();
                                        nTaskQuestion.TaskType = taskQuest.TaskType;
                                        nTaskQuestion.BlockHeight = taskQuest.BlockHeight;
                                        string taskQuestionStr = "";
                                        taskQuestionStr = JsonConvert.SerializeObject(nTaskQuestion);
                                        await SendAdjMessageAll("task", taskQuestionStr);
                                        Console.WriteLine("Task Sent All");
                                    }
                                    else
                                    {
                                        var taskQuest = CurrentTaskQuestion;
                                        TaskQuestion nTaskQuestion = new TaskQuestion();
                                        nTaskQuestion.TaskType = taskQuest.TaskType;
                                        nTaskQuestion.BlockHeight = taskQuest.BlockHeight;
                                        string taskQuestionStr = "";
                                        taskQuestionStr = JsonConvert.SerializeObject(nTaskQuestion);
                                        await SendAdjMessageSingle("task", taskQuestionStr);
                                        Console.WriteLine("Task Sent Single");
                                    }

                                    
                                }
                            }
                            else
                            {
                                await SendAdjMessageSingle("status", "Connected, but your address signature failed to verify.");
                            }
                        }
                        else
                        {
                            await SendAdjMessageSingle("status", "Connected, but you do not have the minimum balance of 1000 RBX.");
                        }
                    
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error: " + ex.Message.ToString());
                    }
                }
                else
                {
                    await SendAdjMessageSingle("status", "Connected, but missing field(s). Address, Unique name, and Signature required.");
                }

                
            }

            
            await base.OnConnectedAsync();
        }
        public override async Task OnDisconnectedAsync(Exception? ex)
        {
            string connectionId = Context.ConnectionId;
            FortisPool.RemoveAll(x => x.ConnectionId == connectionId);
            var fortisPoolStr = "";
            fortisPoolStr = JsonConvert.SerializeObject(FortisPool);
            await SendAdjMessageAll("fortisPool", fortisPoolStr);
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

        #region Receive Block and Task Answer
        public async Task<bool> ReceiveTaskAnswer(TaskAnswer taskResult)
        {
            Console.WriteLine("Answer Received");
            if (Program.BlocksDownloading == false)
            {
                Console.WriteLine("Answer Received 2");
                if (Program.Adjudicate)
                {
                    Console.WriteLine("Answer Received 3");
                    var fortisPool = FortisPool.ToList();
                    if(fortisPool.Exists(x => x.Address == taskResult.Address))
                    {
                        if(taskResult.Block.Height == Program.BlockHeight + 1)
                        {
                            var taskAnswerList = TaskAnswerList.ToList();
                            var answerExist = taskAnswerList.Exists(x => x.Address == taskResult.Address);
                            if (!answerExist)
                            {
                                taskResult.SubmitTime = DateTime.UtcNow;
                                TaskAnswerList.Add(taskResult);
                                return true;
                                Console.WriteLine("Answer Received Success: True");
                            }
                        }
                    }
                }
            }
            Console.WriteLine("Fail");
            return false;
            
        }

        #endregion

        #region Get IP

        private static string GetIP(HubCallerContext context)
        {
            var feature = context.Features.Get<IHttpConnectionFeature>();
            var peerIP = feature.RemoteIpAddress.MapToIPv4().ToString();

            return peerIP;
        }

        #endregion
    }
}
