using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System.Collections.Concurrent;

namespace ReserveBlockCore.P2P
{
    public class ConsensusServer : Hub
    {
        static ConsensusServer()
        {
            AdjPool = new ConcurrentDictionary<string, AdjPool>();
            Messages = new ConcurrentDictionary<(long Height, int MethodCode), ConcurrentDictionary<string, (string Message, string Signature)>>();
            Histories = new ConcurrentDictionary<(long Height, int MethodCode, string SendingAddress), ConcurrentDictionary<string, bool>>();

            var state = ConsensusState.ConsensusStateData.GetAll().FindOne(x => true);

            var CurrentHeight = Globals.LastBlock.Height + 1;
            if (state != null)
            {
                ConsenusStateSingelton = state;
                if (CurrentHeight == state.Height)
                {
                    UpdateState(CurrentHeight, state.MethodCode, (int)state.Status, state.RandomNumber, state.Salt);

                    var messages = Consensus.ConsensusData.GetAll().FindAll().ToArray();
                    var histories = ConsensusHistory.ConsensusHistoryData.GetAll().FindAll().ToArray();

                    foreach (var message in messages.GroupBy(x => (x.Height, x.MethodCode)))
                        Messages[message.Key] = new ConcurrentDictionary<string, (string Message, string Signature)>(message.ToDictionary(x => x.Address, x => (x.Message, x.Signature)));                                                        

                    foreach (var history in histories.GroupBy(x => (x.Height, x.MethodCode, x.SendingAddress)))
                        Histories[history.Key] = new ConcurrentDictionary<string, bool>(history.ToDictionary(x => x.MessageAddress, x => true));
                }
                else
                    UpdateState(CurrentHeight, 0, (int)ConsensusStatus.Processing);                
            }
            else
            {
                ConsensusState.ConsensusStateData.GetAll().InsertSafe(new ConsensusState { Height = CurrentHeight, Status = ConsensusStatus.Processing, MethodCode = 0 });
                ConsenusStateSingelton = ConsensusState.ConsensusStateData.GetAll().FindOne(x => true);
                UpdateState(CurrentHeight, 0, (int)ConsensusStatus.Processing);
            }            
        }

        public static ConcurrentDictionary<string, AdjPool> AdjPool;
        public static ConcurrentDictionary<(long Height, int MethodCode), ConcurrentDictionary<string, (string Message, string Signature)>> Messages;
        public static ConcurrentDictionary<(long Height, int MethodCode, string SendingAddress), ConcurrentDictionary<string, bool>> Histories;
        private static ConsensusState ConsenusStateSingelton;
        private static object MessageLock = new object();
        public override async Task OnConnectedAsync()
        {                       
            try
            {
                var peerIP = GetIP(Context);
                if(!Globals.ConsensusNodes.ContainsKey(peerIP))
                {
                    EndOnConnect(peerIP, peerIP + " attempted to connect as adjudicator", peerIP + " attempted to connect as adjudicator");
                    return;
                }
                                
                var httpContext = Context.GetHttpContext();
                if (httpContext == null)
                {
                    EndOnConnect(peerIP, "httpContext is null", "httpContext is null");
                    return;
                }

                var address = httpContext.Request.Headers["address"].ToString();                
                var signature = httpContext.Request.Headers["signature"].ToString();                

                
                var fortisPool = Globals.FortisPool.Values;
                if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(signature))
                {
                    EndOnConnect(peerIP,
                        "Connection Attempted, but missing field(s). Address, and Signature required. You are being disconnected.",
                        "Connected, but missing field(s). Address, and Signature required: " + address);
                    return;
                }

                var verifySig = SignatureService.VerifySignature(address, address, signature);
                if (!verifySig)
                {
                    EndOnConnect(peerIP,
                        "Connected, but your address signature failed to verify. You are being disconnected.",
                        "Connected, but your address signature failed to verify with Consensus: " + address);
                    return;
                }

                if(!AdjPool.TryGetValue(peerIP, out var Pool))
                {
                    Pool = new AdjPool
                    {
                        Address = address,
                    };
                    AdjPool[peerIP] = Pool;
                }

                if (Pool.Context?.ConnectionId != Context.ConnectionId)
                    Pool.Context?.Abort();
                
                Pool.Context = Context;
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError($"Unhandled exception has happend. Error : {ex.ToString()}", "ConsensusServer.OnConnectedAsync()");
            }

            await base.OnConnectedAsync();
        }

        private void EndOnConnect(string ipAddress, string adjMessage, string loggMessage)
        {            
            if (Globals.OptionalLogging == true)
            {
                LogUtility.Log(loggMessage, "Consensus Connection");
                LogUtility.Log($"IP: {ipAddress} ", "Consensus Connection");
            }

            Context?.Abort();
        }

        public static void UpdateState(long height = -1, int methodCode = -1, int status = -1, int randomNumber = -1, string salt = null)
        {
            if(height != -1)
                ConsenusStateSingelton.Height = height;
            if (status != -1)
                ConsenusStateSingelton.Status = (ConsensusStatus)status;
            if (methodCode != -1)
                ConsenusStateSingelton.MethodCode = methodCode;
            if (randomNumber != -1)
                ConsenusStateSingelton.RandomNumber = randomNumber;
            if (salt != null)
                ConsenusStateSingelton.Salt = salt;            

            var states = ConsensusState.ConsensusStateData.GetAll();
            states.Update(ConsenusStateSingelton);
        }

        public static (long Height, int MethodCode, ConsensusStatus Status, int Answer, string salt) GetState()
        {
            if (ConsenusStateSingelton == null)
                return (-1, 0, ConsensusStatus.Processing, -1, null);
            return (ConsenusStateSingelton.Height, ConsenusStateSingelton.MethodCode, ConsenusStateSingelton.Status, ConsenusStateSingelton.RandomNumber, ConsenusStateSingelton.Salt);
        }

        public (string address, string message, string signature)[] Message(long height, int methodCode, (string address, string message, string signature)[] request)
        {
            try
            {
                var ip = GetIP(Context);
                if (!Globals.ConsensusNodes.TryGetValue(ip, out var Pool))
                {
                    Context?.Abort();
                    return default;
                }

                var currentHeight = ConsenusStateSingelton.Height;
                if (height < currentHeight)
                    return default;
                
                var db = Consensus.ConsensusData.GetAll();
                var history = ConsensusHistory.ConsensusHistoryData.GetAll();
                var FilteredRequest = request.Where(x => SignatureService.VerifySignature(x.address, x.message, x.signature)).ToArray();
                lock (MessageLock)
                {
                    DbContext.DB_Consensus.BeginTrans();
                    foreach (var message in FilteredRequest)
                    {
                        if(Messages.TryGetValue((height, methodCode), out var Message))
                        {
                            if(Message.TryAdd(message.address, (message.message, message.signature)))
                                db.InsertSafe(new Consensus { Address = message.address, Height = height, Message = message.message, MethodCode = methodCode, Signature = message.signature });
                        }
                        else
                        {
                            Messages[(height, methodCode)] = new ConcurrentDictionary<string, (string Message, string Signature)> { 
                                [message.address] = (message.message, message.signature)
                            };                            
                            db.InsertSafe(new Consensus { Address = message.address, Height = height, Message = message.message, MethodCode = methodCode, Signature = message.signature });
                        }
                        
                        if(Histories.TryGetValue((height, methodCode, Pool.Address), out var History))
                        {
                            if(History.TryAdd(message.address, true))
                                history.InsertSafe(new ConsensusHistory { Height = height, MethodCode = methodCode, MessageAddress = message.address, SendingAddress = Pool.Address });
                        }
                        else
                        {
                            Histories[(height, methodCode, Pool.Address)] = new ConcurrentDictionary<string, bool> { 
                                [message.address] = true
                            };
                            history.InsertSafe(new ConsensusHistory { Height = height, MethodCode = methodCode, MessageAddress = message.address, SendingAddress = Pool.Address });
                        }                                                    
                    }

                    DbContext.DB_Consensus.Commit();
                }

                if (height > currentHeight)
                    return default;

                if(!Histories.TryGetValue((height, methodCode, Pool.Address), out var UpdatedHistory))
                    UpdatedHistory = new ConcurrentDictionary<string, bool>();
                return Messages[(currentHeight, methodCode)].Select(x => (x.Key, x.Value.Message, x.Value.Signature)).ToArray();
            }
            catch(Exception ex)
            {
                try
                {
                    DbContext.DB_Consensus.Rollback();
                    ErrorLogUtility.LogError($"Unhandled exception has happend. Error : {ex.ToString()}", "ConsensusServer.Message()");
                }
                catch { }
            }
                       
            return default;
        }

        public bool IsFinalizingOrDone(long height, int methodCode)
        {
            var Height = ConsenusStateSingelton.Height;
            if (height > Height)
                return false;
            if (height < Height)
                return true;
            if (methodCode > ConsenusStateSingelton.MethodCode)
                return false;
            if (methodCode < ConsenusStateSingelton.MethodCode)
                return true;

            return ConsenusStateSingelton.Status != ConsensusStatus.Processing;
        }

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
    }
}
