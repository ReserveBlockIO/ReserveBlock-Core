using ReserveBlockCore.Extensions;
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
                Interlocked.Exchange(ref ReadyToFinalize, 0);
                ConsensusServer.UpdateState(methodCode: methodCode);
                var Address = Globals.AdjudicateAccount.Address;
                var Peers = Globals.Nodes.Values.Where(x => x.Address != Address).ToArray();
                long Height = -1;                
                int Majority = -1;
                ConcurrentDictionary<string, (string Message, string Signature)> Messages = null;
                ConcurrentDictionary<string, (string Hash, string Signature)> Hashes = null;
                SendMethodCode(Peers, methodCode, false);
                var DelayTask = Task.Delay(timeToFinalize);
                while (true)
                {
                    Height = Globals.LastBlock.Height + 1;                                    
                    Peers = Globals.Nodes.Values.Where(x => x.Address != Address).ToArray();                    

                    var CurrentAddresses = Signer.CurrentSigningAddresses();
                    var NumNodes = CurrentAddresses.Count;
                    Majority = NumNodes / 2 + 1;

                    Messages = new ConcurrentDictionary<string, (string Message, string Signature)>();
                    ConsensusServer.Messages.Clear();
                    ConsensusServer.Messages[(Height, methodCode)] = Messages;
                    Messages[Globals.AdjudicateAccount.Address] = (message, signature);

                    Hashes = new ConcurrentDictionary<string, (string Hash, string Signature)>();
                    ConsensusServer.Hashes.Clear();
                    ConsensusServer.Hashes[(Height, methodCode)] = Hashes;

                    var ConsensusSource = new CancellationTokenSource();                    
                    _ = PeerRequestLoop(methodCode, Peers, CurrentAddresses, ConsensusSource);

                    //while (Messages.Count < Majority && Height == Globals.LastBlock.Height + 1 && (runType == RunType.Initial ||
                    //                        Peers.Where(x => x.NodeHeight + 1 == Height && (x.IsConnected || TimeUtil.GetMillisecondTime() - x.LastMethodCodeTime < 3000) && (x.MethodCode == methodCode || (x.MethodCode == methodCode - 1 && x.IsFinalized))).Count() >= Majority - 1))

                    while (Messages.Count < Majority && Height == Globals.LastBlock.Height + 1)
                    {
                        await Task.Delay(4);
                    }

                    if (Height != Globals.LastBlock.Height + 1)
                    {
                        ConsensusSource.Cancel();
                        continue;
                    }

                    if (Messages.Count >= Majority)
                    {
                        await Task.Delay(1000);
                        Interlocked.Exchange(ref ReadyToFinalize, 1);
                        ConsensusSource.Cancel();
                        break;
                    }

                    ConsensusSource.Cancel();
                    return null;                    
                }

                var HashSource = new CancellationTokenSource();
                var signers = Signer.CurrentSigningAddresses();
                _ = PeerHashRequestLoop(methodCode, Peers, signers, HashSource);

                var MinPass = signers.Count / 2;
                (string Hash, string Signature) MyHash;
                while (!Hashes.TryGetValue(Globals.AdjudicateAccount.Address, out MyHash))
                    await Task.Delay(4);
                
                while (true) //Peers.Where(x => x.NodeHeight + 1 == Height && (x.IsConnected || TimeUtil.GetMillisecondTime() - x.LastMethodCodeTime < 3000) && (x.MethodCode == methodCode || (x.MethodCode == methodCode - 1 && x.IsFinalized))).Count() >= Majority - 1)
                {
                    var CurrentHashes = Hashes.Values.ToArray();
                    var NumMatches = CurrentHashes.Where(x => x.Hash == MyHash.Hash).Count();
                    if (NumMatches >= MinPass || Globals.Nodes.Values.Any(x => x.NodeHeight + 1 == Height && x.MethodCode == methodCode + 1))
                    {
                        HashSource.Cancel();
                        return Messages.Select(x => (x.Key, x.Value.Message)).ToArray();
                    }

                    if (CurrentHashes.Length - NumMatches > MinPass || Height != Globals.LastBlock.Height + 1)
                    {
                        HashSource.Cancel();
                        return null;
                    }

                    await Task.Delay(4);
                }

                HashSource.Cancel();
            }
            catch(Exception ex)
            {
            }            
            return null;
        }       

        public static void SendMethodCode(NodeInfo[] peers, int methodCode, bool isFinalized)
        {
            var Now = TimeUtil.GetMillisecondTime();            
            _ = Task.WhenAll(peers.Select(node =>
            {
                var Source = new CancellationTokenSource(1000);
                var SendMethodCodeFunc = () => node.InvokeAsync<bool>("SendMethodCode", args: new object?[] { Globals.LastBlock.Height, methodCode, isFinalized }, Source.Token)
                    ?? Task.FromResult(false);
                return SendMethodCodeFunc.RetryUntilSuccessOrCancel(x => x || TimeUtil.GetMillisecondTime() - Now > 2000, 100, default);
            }));
        }

        public static async Task PeerRequestLoop(int methodCode, NodeInfo[] peers, HashSet<string> addresses, CancellationTokenSource cts)
        {
            var messages = ConsensusServer.Messages[(Globals.LastBlock.Height + 1, methodCode)];
            var rnd = new Random();
            var taskDict = new ConcurrentDictionary<string, Task<string>>();
            var MissingAddresses = addresses.Except(messages.Select(x => x.Key)).OrderBy(x => rnd.Next()).ToArray();

            do
            {
                try
                {
                    var RecentPeers = peers.Where(x => x.IsConnected && x.NodeHeight == Globals.LastBlock.Height &&
                        x.MethodCode == methodCode && !taskDict.ContainsKey(x.NodeIP)).ToArray();

                    var Source = new CancellationTokenSource(1000);
                    for (var i = 0; i < RecentPeers.Length; i++)
                    {
                        var peer = RecentPeers[i];
                        taskDict[peer.NodeIP] = peer.InvokeAsync<string>("Message", args: new object?[] { Globals.LastBlock.Height + 1, methodCode, MissingAddresses.Rotate(i * MissingAddresses.Length / RecentPeers.Length) }, Source.Token);
                    }
                    
                    await Task.WhenAny(taskDict.Values);

                    var CompletedTasks = taskDict.Where(x => x.Value.IsCompleted).ToArray();
                    foreach(var completedTask in CompletedTasks)
                    {
                        try
                        {
                            var Response = await completedTask.Value;
                            if (Response != null)
                            {
                                var arr = Response.Split(";:;");
                                var (address, message, signature) = (arr[0], arr[1].Replace("::", ":"), arr[2]);
                                if (MissingAddresses.Contains(address) && SignatureService.VerifySignature(address, message, signature))
                                    messages[address] = (message, signature);
                                MissingAddresses = addresses.Except(messages.Select(x => x.Key)).OrderBy(x => rnd.Next()).ToArray();
                            }
                        }
                        catch { }

                        taskDict.TryRemove(completedTask.Key, out _);
                    }                    
                }
                catch { }

            await Task.Delay(10);
            } while (!cts.IsCancellationRequested && MissingAddresses.Any());

            await cts.Token.WhenCanceled();
            if (ReadyToFinalize == 1)
            {
                ConsensusServer.UpdateState(status: (int)ConsensusStatus.Finalized);
                var Height = Globals.LastBlock.Height + 1;
                var Messages = ConsensusServer.Messages[(Height, methodCode)];                
                var MyHash = Ecdsa.sha256(string.Join("", Messages.OrderBy(x => x.Key).Select(x => Ecdsa.sha256(x.Value.Message))));
                var Signature = SignatureService.AdjudicatorSignature(MyHash);
                var HashDict = ConsensusServer.Hashes[(Height, methodCode)];
                HashDict[Globals.AdjudicateAccount.Address] = (MyHash, Signature);                
                SendMethodCode(peers, methodCode, true);
            }
        }

        public static async Task PeerHashRequestLoop(int methodCode, NodeInfo[] peers, HashSet<string> addresses, CancellationTokenSource cts)
        {
            var hashes = ConsensusServer.Hashes[(Globals.LastBlock.Height + 1, methodCode)];
            var rnd = new Random();
            var taskDict = new ConcurrentDictionary<string, Task<string>>();
            var MissingAddresses = addresses.Except(hashes.Select(x => x.Key)).OrderBy(x => rnd.Next()).ToArray();

            do
            {
                try
                {
                    var RecentPeers = peers.Where(x => x.IsConnected && x.NodeHeight == Globals.LastBlock.Height &&
                        x.MethodCode == methodCode && !taskDict.ContainsKey(x.NodeIP)).ToArray();

                    var Source = new CancellationTokenSource(1000);
                    for (var i = 0; i < RecentPeers.Length; i++)
                    {
                        var peer = RecentPeers[i];
                        taskDict[peer.NodeIP] = peer.InvokeAsync<string>("Hash", args: new object?[] { Globals.LastBlock.Height + 1, methodCode, MissingAddresses.Rotate(i * MissingAddresses.Length / RecentPeers.Length) }, Source.Token);
                    }

                    await Task.WhenAny(taskDict.Values);

                    var CompletedTasks = taskDict.Where(x => x.Value.IsCompleted).ToArray();
                    foreach (var completedTask in CompletedTasks)
                    {
                        try
                        {
                            var Response = await completedTask.Value;
                            if (Response != null)
                            {
                                var arr = Response.Split(":");
                                var (address, hash, signature) = (arr[0], arr[1], arr[2]);
                                if (MissingAddresses.Contains(address) && SignatureService.VerifySignature(address, hash, signature))
                                    hashes[address] = (hash, signature);
                                MissingAddresses = addresses.Except(hashes.Select(x => x.Key)).OrderBy(x => rnd.Next()).ToArray();
                            }
                        }
                        catch { }

                        taskDict.TryRemove(completedTask.Key, out _);
                    }                    
                }
                catch { }

            await Task.Delay(10);
            } while (!cts.IsCancellationRequested && MissingAddresses.Any());            
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
            catch { }

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
            catch { }
            return default;
        }
    }
}
