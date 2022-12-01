using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System;
using System.Collections.Concurrent;

namespace ReserveBlockCore.P2P
{
    public class ConsensusServer : P2PServer
    {
        static ConsensusServer()
        {
            AdjPool = new ConcurrentDictionary<string, AdjPool>();
            Messages = new ConcurrentDictionary<(long Height, int MethodCode), ConcurrentDictionary<string, (string Message, string Signature)>>();            
            ConsenusStateSingelton = new ConsensusState();
        }

        public static ConcurrentDictionary<string, AdjPool> AdjPool;
        public static ConcurrentDictionary<(long Height, int MethodCode), ConcurrentDictionary<string, (string Message, string Signature)>> Messages;
        private static ConsensusState ConsenusStateSingelton;
        private static object UpdateLock = new object();
        public override async Task OnConnectedAsync()
        {                       
            try
            {
                var peerIP = GetIP(Context);
                if(!Globals.Nodes.ContainsKey(peerIP))
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
                var time = httpContext.Request.Headers["time"].ToString();
                var signature = httpContext.Request.Headers["signature"].ToString();

                if (TimeUtil.GetTime() - long.Parse(time) > 30000000)
                {
                    EndOnConnect(peerIP, "Signature Bad time.", "Signature Bad time.");
                    return;
                }

                var fortisPool = Globals.FortisPool.Values;
                if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(signature))
                {
                    EndOnConnect(peerIP,
                        "Connection Attempted, but missing field(s). Address, and Signature required. You are being disconnected.",
                        "Connected, but missing field(s). Address, and Signature required: " + address);
                    return;
                }

                var verifySig = SignatureService.VerifySignature(address, address + ":" + time, signature);
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

        public static void UpdateState(int methodCode = -1, int status = -1, int randomNumber = -1)
        {
            lock (UpdateLock)
            {
                if (status != -1)
                    ConsenusStateSingelton.Status = (ConsensusStatus)status;
                if (methodCode != -1)
                    ConsenusStateSingelton.MethodCode = methodCode;
                if (randomNumber != -1)
                    ConsenusStateSingelton.RandomNumber = randomNumber;
            }
        }

        public bool StartRuns(long height)
        {
            if (Globals.LastBlock.Height + 1 == height && (int)ConsenusStateSingelton.MethodCode == -100)
                UpdateState(methodCode: 0);
            return true;
        }

        public static (int MethodCode, ConsensusStatus Status, int Answer) GetState()
        {
            if (ConsenusStateSingelton == null)
                return (0, ConsensusStatus.Processing, -1);
            return (ConsenusStateSingelton.MethodCode, ConsenusStateSingelton.Status, ConsenusStateSingelton.RandomNumber);
        }

        public int MethodCode(long height)
        {
            var ip = GetIP(Context);
            if (!Globals.Nodes.TryGetValue(ip, out var Pool))
            {
                Context?.Abort();
                return -1;
            }

            if (height != Globals.LastBlock.Height + 1)
                return -1;

            return ConsenusStateSingelton.MethodCode;
        }

        public string Message(long height, int methodCode, string[] addresses)
        {
            try
            {
                var ip = GetIP(Context);
                if (!Globals.Nodes.TryGetValue(ip, out var Pool))
                {
                    Context?.Abort();
                    return null;
                }

                if (!Messages.TryGetValue((height, methodCode), out var messages))
                    return null;

                foreach (var address in addresses)
                {
                    if (messages.TryGetValue(address, out var Value))
                        return address + ";:;" + Value.Message.Replace(":", "::") + ";:;" + Value.Signature;
                }               
            }
            catch(Exception ex)
            {
                try
                {                    
                    ErrorLogUtility.LogError($"Unhandled exception has happend. Error : {ex.ToString()}", "ConsensusServer.Message()");
                }
                catch { }
            }
                       
            return null;
        }

        public string[] Hashes(long height, int methodCode)
        {
            try
            {               
                var ip = GetIP(Context);
                if (!Globals.Nodes.TryGetValue(ip, out var Pool))
                {
                    Context?.Abort();
                    return null;
                }

                if (Globals.LastBlock.Height + 1 != height || ConsenusStateSingelton.MethodCode != methodCode ||
                    ConsenusStateSingelton.Status != ConsensusStatus.Finalized)
                    return null;

                if (!Messages.TryGetValue((height, methodCode), out var messages))
                    return null;
                
                return messages.Select(x => x.Key + ":" + Ecdsa.sha256(x.Value.Message)).ToArray();
            }
            catch (Exception ex)
            {
                try
                {
                    ErrorLogUtility.LogError($"Unhandled exception has happend. Error : {ex.ToString()}", "ConsensusServer.Message()");
                }
                catch { }
            }

            return null;
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
