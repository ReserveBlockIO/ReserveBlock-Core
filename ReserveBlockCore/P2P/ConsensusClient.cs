﻿using ReserveBlockCore.Extensions;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using ReserveBlockCore.Beacon;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Models;
using ReserveBlockCore.Nodes;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using ReserveBlockCore.Extensions;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Reflection.Metadata;

namespace ReserveBlockCore.P2P
{
    public class ConsensusClient : IAsyncDisposable, IDisposable
    {
        #region Hub Dispose
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);

            Dispose(disposing: false);
            #pragma warning disable CA1816
            GC.SuppressFinalize(this);
            #pragma warning restore CA1816
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach(var node in Globals.Nodes.Values)
                    if(node.Connection != null)
                        node.Connection.DisposeAsync().ConfigureAwait(false).GetAwaiter().GetResult();                
            }
        }

        protected virtual async ValueTask DisposeAsyncCore()
        {
            foreach (var node in Globals.Nodes.Values)
                if(node.Connection != null)
                    await node.Connection.DisposeAsync();
        }

        #endregion

        #region Consensus Code

        public enum RunType
        {
            Initial,
            Middle,
            Last
        }

        private static int ReadyToFinalize = 0;

        public static async Task<(string Address, string Message)[]> ConsensusRun(int methodCode, string message, string signature, int timeToFinalize, RunType runType)
        {
            try
            {                
                LogUtility.LogQueue(methodCode + " " + message, "ConsensusRun");
                Interlocked.Exchange(ref ReadyToFinalize, 0);
                ConsensusServer.UpdateState(methodCode: methodCode, status: (int)ConsensusStatus.Processing);
                var Address = Globals.AdjudicateAccount.Address;
                var Peers = Globals.Nodes.Values.Where(x => x.Address != Address).ToArray();
                long Height = -1;                
                int Majority = -1;
                ConcurrentDictionary<string, (string Message, string Signature)> Messages = null;
                ConcurrentDictionary<string, (string Hash, string Signature)> Hashes = null;                               
                while (true)
                {
                    Height = Globals.LastBlock.Height + 1;                                    
                    Peers = Globals.Nodes.Values.Where(x => x.Address != Address).ToArray();

                    var CurrentAddresses = Globals.Signers.Keys.ToHashSet();
                    var NumNodes = CurrentAddresses.Count;
                    Majority = NumNodes / 2 + 1;
                    
                    var MessageKeysToKeep = ConsensusServer.Messages.Where(x => (x.Key.Height == Height && x.Key.MethodCode == methodCode) ||
                        (x.Key.Height == Height && x.Key.MethodCode == methodCode - 1))
                        .Select(x => x.Key).ToHashSet();
                    foreach(var key in ConsensusServer.Messages.Keys.Where(x => !MessageKeysToKeep.Contains(x)))
                    {
                        ConsensusServer.Messages.TryRemove(key, out _);
                    }

                    Messages = ConsensusServer.Messages.GetOrAdd((Height, methodCode), new ConcurrentDictionary<string, (string Message, string Signature)>());                    
                    Messages[Globals.AdjudicateAccount.Address] = (message, signature);

                    var HashKeysToKeep = ConsensusServer.Hashes.Where(x => (x.Key.Height == Height && x.Key.MethodCode == methodCode) ||
                        (x.Key.Height == Height && x.Key.MethodCode == methodCode - 1))
                        .Select(x => x.Key).ToHashSet();
                    foreach (var key in ConsensusServer.Hashes.Keys.Where(x => !HashKeysToKeep.Contains(x)))
                    {
                        ConsensusServer.Hashes.TryRemove(key, out _);
                    }

                    Hashes = ConsensusServer.Hashes.GetOrAdd((Height, methodCode), new ConcurrentDictionary<string, (string Hash, string Signature)>());                    

                    var ConsensusSource = new CancellationTokenSource();
                    _ = MessageRequests(methodCode, Peers, CurrentAddresses.ToArray(), ConsensusSource);
                                        
                    var WaitForAddresses = AddressesToWaitFor(Height, methodCode, 3000);                    
                    while (Height == Globals.LastBlock.Height + 1)
                    {
                        var RemainingAddressCount = !ConsensusSource.IsCancellationRequested ? WaitForAddresses.Except(Messages.Select(x => x.Key)).Count() : 0;
                        LogUtility.LogQueue(Messages.Count + " " + RemainingAddressCount, "ConsensusRun First loop");
                        if ((runType != RunType.Initial && Messages.Count + RemainingAddressCount < Majority) || 
                            (RemainingAddressCount == 0 && Messages.Count >= Majority))
                            break;
                        
                        await Task.Delay(20);
                        WaitForAddresses = AddressesToWaitFor(Height, methodCode, 3000);
                    }

                    if (Height != Globals.LastBlock.Height + 1)
                    {
                        ConsensusSource.Cancel();                        
                        continue;
                    }

                    if (Messages.Count >= Majority)
                    {                        
                        ConsensusSource.Cancel();
                        break;
                    }

                    ConsensusSource.Cancel();
                    LogUtility.LogQueue(Height + " " + methodCode, "end of first loop");                    
                    return null;                    
                }

                while (ReadyToFinalize != 1)
                    await Task.Delay(20);

                var FinalizedMessages = Messages.OrderBy(x => x.Key).ToArray();

                ConsensusServer.UpdateState(status: (int)ConsensusStatus.Finalized);                                
                var MyHash = Ecdsa.sha256(string.Join("", FinalizedMessages.Select(x => Ecdsa.sha256(x.Value.Message))));
                var Signature = SignatureService.AdjudicatorSignature(MyHash);

                Hashes[Globals.AdjudicateAccount.Address] = (MyHash, Signature);                
                
                var HashSource = new CancellationTokenSource();                
                _ = HashRequests(methodCode, Peers, Globals.Signers.Keys.ToArray(), HashSource);
                                
                var HashAddressesToWaitFor = AddressesToWaitFor(Height, methodCode, 3000);                
                while (Height == Globals.LastBlock.Height + 1)
                {                    
                    var CurrentHashes = Hashes.ToArray();
                    var CurrentMatchAddresses = CurrentHashes.Where(x => x.Value.Hash == MyHash).Select(x => x.Key).ToArray();
                    var NumMatches = CurrentMatchAddresses.Length;
                    if (NumMatches >= Majority)
                    {
                        HashSource.Cancel();
                        while(true)
                        {
                            var Now = TimeUtil.GetMillisecondTime();
                            var AnyNodesToWaitFor = Globals.Nodes.Values.Where(x => Now - x.LastMethodCodeTime < 3000 &&
                                ((x.MethodCode == methodCode && !x.IsFinalized) || (x.MethodCode == methodCode - 1 && x.IsFinalized))).Any();                                                               
                            LogUtility.LogQueue(AnyNodesToWaitFor.ToString(), "ConsensusRun inner success loop");
                            if (AnyNodesToWaitFor)
                            {
                                await Task.Delay(20);
                                continue;
                            }
                            break;
                        }
                        LogUtility.LogQueue(Height + " " + methodCode, "good return");
                        return FinalizedMessages.Select(x => (x.Key, x.Value.Message)).ToArray();
                    }
                                        
                    HashAddressesToWaitFor = AddressesToWaitFor(Height, methodCode, 3000);
                    var RemainingAddressCount = !HashSource.IsCancellationRequested ? HashAddressesToWaitFor.Except(CurrentHashes.Select(x => x.Key)).Count() : 0;
                    LogUtility.LogQueue(NumMatches + " " + RemainingAddressCount, "ConsensusRun fail check");
                    if (NumMatches + RemainingAddressCount < Majority)
                    {
                        HashSource.Cancel();
                        LogUtility.LogQueue(Height + " " + methodCode, "hash fail");                        
                        return null;
                    }
                    
                    await Task.Delay(20);                    
                }

                HashSource.Cancel();
            }
            catch(Exception ex)
            {
                ErrorLogUtility.LogError(ex.ToString(), "ConsensusRun");
            }
            LogUtility.LogQueue((Globals.LastBlock.Height + 1) + " " + methodCode, "exception thrown at end");            
            return null;
        }

        public static HashSet<string> AddressesToWaitFor(long height, int methodCode, int wait)
        {
            var Now = TimeUtil.GetMillisecondTime();            
            return Globals.Nodes.Values.Where(x => Now - x.LastMethodCodeTime < wait && ((x.NodeHeight + 2 == height) ||
                (x.NodeHeight + 1 == height && (x.MethodCode == methodCode || (x.MethodCode == methodCode + 1 && !x.IsFinalized) || (x.MethodCode == methodCode - 1 && x.IsFinalized)))))
                .Select(x => x.Address).ToHashSet();
        }

        public static string[] RotateFrom(string[] arr, string elem)
        {
            var Index = arr.Select((x, i) => (x, i)).Where(x => x.x == elem).Select(x => (int?)x.i).FirstOrDefault() ?? -1;
            if (Index == -1)
                return arr;
            return arr.Skip(Index).Concat(arr.Take(Index)).ToArray();
        }
        public static async Task MessageRequests(int methodCode, NodeInfo[] peers, string[] addresses, CancellationTokenSource cts)
        {
            var currentHeight = Globals.LastBlock.Height;
            var messages = ConsensusServer.Messages[(currentHeight + 1, methodCode)];                          
            var MyMessage = messages[Globals.AdjudicateAccount.Address];
            var ToSend = MyMessage.Message.Replace(":", "::") + ";:;" + MyMessage.Signature;

            for (var i = 0; i < peers.Length; i++)
            {
                var peer = peers[i];                
                _ = MessageRequest(peer, ToSend, currentHeight, methodCode, messages, addresses, cts);
            }

            await cts.Token.WhenCanceled();
            Interlocked.Exchange(ref ReadyToFinalize, 1);
        }

        private static async Task MessageRequest(NodeInfo peer, string toSend, long currentHeight, int methodCode,
            ConcurrentDictionary<string, (string Message, string Signature)> messages, string[] addresses, CancellationTokenSource cts)
        {
            var SentMessage = false;
            addresses = RotateFrom(addresses, peer.Address);

            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var MessageToSend = SentMessage ? null : toSend;
                    var RemainingAddresses = addresses.Where(x => !messages.Keys.Contains(x)).ToArray();

                    if (RemainingAddresses.Length == 0 || ConsensusServer.GetState().MethodCode != methodCode || Globals.LastBlock.Height != currentHeight)
                    {
                        cts.Cancel();
                        break;
                    }

                    if (!peer.IsConnected)
                    {
                        await Task.Delay(20);
                        continue;
                    }

                    ConsensusServer.UpdateConsensusDump(peer.NodeIP, "BeforeRequestMessage", toSend + " " + (currentHeight + 1) + " " + methodCode + " (" + string.Join(",", messages.Select(x => x.Key + " " + x.Value.Message)) + ") ", null);
                    LogUtility.LogQueue(methodCode + " " + MessageToSend, "Before Message");                    
                    var Now = TimeUtil.GetMillisecondTime();
                    var Response = await peer.InvokeAsync<string>("Message", new object?[] { currentHeight + 1, methodCode, RemainingAddresses, MessageToSend },
                        () => CancellationTokenSource.CreateLinkedTokenSource(cts.Token, new CancellationTokenSource(3000).Token).Token);
                    LogUtility.LogQueue(Response, "After Message");
                    ConsensusServer.UpdateConsensusDump(peer.NodeIP, "AfterRequestMessage", null, Response);

                    if (Response != null)
                    {
                        var PrefixSplit = Response.Split(new[] { '|' }, 2);
                        var Prefix = PrefixSplit[0].Split(':');
                        if (Now > peer.LastMethodCodeTime)
                        {
                            lock (ConsensusServer.UpdateNodeLock)
                            {
                                peer.LastMethodCodeTime = Now;
                                peer.NodeHeight = long.Parse(Prefix[0]);
                                peer.MethodCode = int.Parse(Prefix[1]);
                                peer.IsFinalized = Prefix[2] == "1";
                            }
                        }

                        if (peer.NodeHeight > currentHeight)
                        {
                            cts.Cancel();
                            LogUtility.LogQueue(currentHeight + " " + methodCode, "Message height cancel");
                            break;
                        }

                        if (PrefixSplit.Length == 2 && ConsensusServer.GetState().Status == ConsensusStatus.Processing)
                        {
                            var arr = PrefixSplit[1].Split(";:;");
                            var (address, message, signature) = (arr[0], arr[1].Replace("::", ":"), arr[2]);
                            if (SignatureService.VerifySignature(address, message, signature))
                                messages[address] = (message, signature);
                            else
                                LogUtility.LogQueue(address + " " + message + " " + signature, "Message signature failure");
                        }
                        else if (peer.MethodCode > methodCode)
                        {
                            LogUtility.LogQueue(currentHeight + " " + methodCode, "Message wait");
                            await Task.Delay(500);
                        }
                    }

                    SentMessage = true;
                }
                catch (TaskCanceledException ex)
                { }
                catch (Exception ex)
                {
                    ErrorLogUtility.LogError(ex.ToString(), "PeerRequestLoop inner catch");
                }

                await Task.Delay(100);
            }
        }

        public static async Task HashRequests(int methodCode, NodeInfo[] peers, string[] addresses, CancellationTokenSource cts)
        {
            var currentHeight = Globals.LastBlock.Height;
            var hashes = ConsensusServer.Hashes[(currentHeight + 1, methodCode)];
            var MyHash = hashes[Globals.AdjudicateAccount.Address];
            var ToSend = MyHash.Hash + ":" + MyHash.Signature;

            for (var i = 0; i < peers.Length; i++)
            {
                var peer = peers[i];
                _ = HashRequest(peer, ToSend, currentHeight, methodCode, hashes, addresses, cts);
            }           
        }

        private static async Task HashRequest(NodeInfo peer, string toSend, long currentHeight, int methodCode,
    ConcurrentDictionary<string, (string Hash, string Signature)> hashes, string[] addresses, CancellationTokenSource cts)
        {
            var SentHash = false;
            addresses = RotateFrom(addresses, peer.Address);

            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var HashToSend = SentHash ? null : toSend;
                    var RemainingAddresses = addresses.Where(x => !hashes.Keys.Contains(x)).ToArray();

                    if (RemainingAddresses.Length == 0 || ConsensusServer.GetState().MethodCode != methodCode || Globals.LastBlock.Height != currentHeight)
                    {
                        cts.Cancel();
                        break;
                    }

                    if (!peer.IsConnected)
                    {
                        await Task.Delay(20);
                        continue;
                    }

                    LogUtility.LogQueue(methodCode + " " + HashToSend, "Before Hash");
                    ConsensusServer.UpdateConsensusDump(peer.NodeIP, "BeforeRequestHash", toSend + " " + (currentHeight + 1) + " " + methodCode + " (" + string.Join(",", hashes.Select(x => x.Key + " " + x.Value.Hash)) + ") ", null);                    
                    var Now = TimeUtil.GetMillisecondTime();
                    var Response = await peer.InvokeAsync<string>("Hash", new object?[] { currentHeight + 1, methodCode, RemainingAddresses, HashToSend }, 
                        () => CancellationTokenSource.CreateLinkedTokenSource(cts.Token, new CancellationTokenSource(1000).Token).Token);
                    LogUtility.LogQueue(Response, "After Hash");
                    ConsensusServer.UpdateConsensusDump(peer.NodeIP, "AfterRequestHash", null, Response);

                    if (Response != null)
                    {
                        var PrefixSplit = Response.Split(new[] { '|' }, 2);
                        var Prefix = PrefixSplit[0].Split(':');
                        if (Now > peer.LastMethodCodeTime)
                        {
                            lock (ConsensusServer.UpdateNodeLock)
                            {
                                peer.LastMethodCodeTime = Now;
                                peer.NodeHeight = long.Parse(Prefix[0]);
                                peer.MethodCode = int.Parse(Prefix[1]);
                                peer.IsFinalized = Prefix[2] == "1";
                            }
                        }

                        if (peer.NodeHeight > currentHeight)
                        {
                            cts.Cancel();
                            LogUtility.LogQueue(currentHeight + " " + methodCode, "Hash height cancel");
                            break;
                        }

                        if (PrefixSplit.Length == 2)
                        {
                            var arr = PrefixSplit[1].Split(":");
                            var (address, hash, signature) = (arr[0], arr[1], arr[2]);
                            if (SignatureService.VerifySignature(address, hash, signature))
                                hashes[address] = (hash, signature);
                            else
                                LogUtility.LogQueue(address + " " + hash + " " + signature, "Hash signature failure");
                        }
                        else if(peer.MethodCode > methodCode)
                        {
                            await Task.Delay(500);
                        }
                    }

                    SentHash = true;
                }
                catch (TaskCanceledException ex)
                { }
                catch (Exception ex)
                {
                    ErrorLogUtility.LogError(ex.ToString(), "HashRequestLoop inner catch");
                }

                await Task.Delay(100);
            }
        }

        #endregion


        #region Connect Adjudicator
        public static ConcurrentDictionary<string, bool> IsConnectingDict = new ConcurrentDictionary<string, bool>();
        public static async Task<bool> ConnectConsensusNode(string url, string address, string time, string uName, string signature)
        {
            var IPAddress = GetPathUtility.IPFromURL(url);
            try
            {               
                if (!IsConnectingDict.TryAdd(IPAddress, true))
                    return Globals.Nodes[IPAddress].IsConnected;

                var hubConnection = new HubConnectionBuilder()                
                .WithUrl(url, options => {
                    options.Headers.Add("address", address);
                    options.Headers.Add("time", time);
                    options.Headers.Add("uName", uName);
                    options.Headers.Add("signature", signature);
                    options.Headers.Add("walver", Globals.CLIVersion);
                })                
                .Build();

                LogUtility.Log("Connecting to Consensus Node", "ConnectConsensusNode()");

                
                hubConnection.Reconnecting += (sender) =>
                {
                    LogUtility.Log("Reconnecting to Adjudicator", "ConnectConsensusNode()");
                    Console.WriteLine("[" + DateTime.Now.ToString() + "] Connection to adjudicator lost. Attempting to Reconnect.");
                    return Task.CompletedTask;
                };

                hubConnection.Reconnected += (sender) =>
                {
                    LogUtility.Log("Success! Reconnected to Adjudicator", "ConnectConsensusNode()");
                    Console.WriteLine("[" + DateTime.Now.ToString() + "] Connection to adjudicator has been restored.");
                    return Task.CompletedTask;
                };

                hubConnection.Closed += (sender) =>
                {
                    LogUtility.Log("Closed to Adjudicator", "ConnectConsensusNode()");
                    Console.WriteLine("[" + DateTime.Now.ToString() + "] Connection to adjudicator has been closed.");
                    return Task.CompletedTask;
                };                

                await hubConnection.StartAsync().WaitAsync(new TimeSpan(0, 0, 8));

                var node = Globals.Nodes[IPAddress];
                (node.NodeHeight, node.NodeLastChecked, node.NodeLatency) = await P2PClient.GetNodeHeight(hubConnection);
                Globals.Nodes[IPAddress].Connection = hubConnection;

                return true;
            }
            catch (Exception ex)
            {
                ValidatorLogUtility.Log("Failed! Connecting to Adjudicator: Reason - " + ex.ToString(), "ConnectAdjudicator()");
            }            
            finally
            {
                IsConnectingDict.TryRemove(IPAddress, out _);
            }

            return false;
        }

        #endregion

        public static async Task<bool> GetBlock(long height, NodeInfo node)
        {
            var startTime = DateTime.Now;
            long blockSize = 0;
            Block Block = null;
            try
            {
                var source = new CancellationTokenSource(10000);
                Block = await node.Connection.InvokeCoreAsync<Block>("SendBlock", args: new object?[] { height - 1 }, source.Token);
                if (Block != null)
                {
                    blockSize = Block.Size;
                    if (Block.Height == height)
                    {
                        BlockDownloadService.BlockDict[height] = (Block, node.NodeIP);
                        return true;
                    }                        
                }
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError($"Unknown Error: {ex.ToString()}", "ConsensusClient.GetBlock()");
            }

            return false;
        }

        public static async Task<long> GetNodeHeight(NodeInfo node)
        {
            try
            {
                if (!node.IsConnected)
                    return default;
                using (var Source = new CancellationTokenSource(2000))
                    return await node.Connection.InvokeAsync<long>("SendBlockHeight", Source.Token);
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError($"Unknown Error: {ex.ToString()}", "ConsensusClient.GetNodeHeight()");
            }
            return default;
        }
    }
}
