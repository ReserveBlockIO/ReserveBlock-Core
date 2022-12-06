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

        public static async Task<(string Address, string Message)[]> ConsensusRun(string message, string signature, int timeToFinalize, CancellationToken ct, RunType runType)
        {
            try
            {
                var Height = Globals.LastBlock.Height + 1;
                var methodCode = ConsensusServer.GetState().MethodCode;                
                var Address = Globals.AdjudicateAccount.Address;
                var Peers = Globals.Nodes.Values.Where(x => x.Address != Address).ToArray();
                var CurrentTime = TimeUtil.GetMillisecondTime();

                var CurrentAddresses = Signer.CurrentSigningAddresses();
                var NumNodes = CurrentAddresses.Count;
                var Majority = NumNodes / 2 + 1;
                               
                var Messages = new ConcurrentDictionary<string, (string Message, string Signature)>();
                ConsensusServer.Messages.Clear();
                ConsensusServer.Messages[(Height, methodCode)] = Messages;
                Messages[Globals.AdjudicateAccount.Address] = (message, signature);

                var ConsensusSource = CancellationTokenSource.CreateLinkedTokenSource(Globals.ConsensusTokenSource.Token);
                foreach (var peer in Peers)
                {
                    _ = PeerRequestLoop(methodCode, peer, CurrentAddresses, ConsensusSource);
                }

                if(runType == RunType.Initial)
                {
                    while (Messages.Count < Majority)
                    {
                        try
                        {
                            await Task.Delay(4, Globals.ConsensusTokenSource.Token);
                        }
                        catch { }
                    }
                }

                try
                {
                    await Task.Delay(timeToFinalize, ConsensusSource.Token);
                }
                catch { }

                ConsensusSource.Cancel();
                if (Messages.Count < Majority)
                    return null;
                
                ConsensusServer.UpdateState(status: (int)ConsensusStatus.Finalized);
                var HashSource = CancellationTokenSource.CreateLinkedTokenSource(Globals.ConsensusTokenSource.Token);
                var Now = TimeUtil.GetMillisecondTime();

                var HashTasks = Peers.Select(node =>
                {
                    var HashRequestFunc = () => node.Connection?.InvokeCoreAsync<string[]>("Hashes", args: new object?[] { Height, methodCode }, HashSource.Token)
                        ?? Task.FromResult((string[])null);
                    return HashRequestFunc.RetryUntilSuccessOrCancel(x => x != null || (TimeUtil.GetMillisecondTime() - Now) > 1000 || ConsensusServer.GetState().MethodCode != methodCode, 100, HashSource.Token);
                })
                .ToArray();

                await HashTasks.WhenAtLeast(x => x != null || (TimeUtil.GetMillisecondTime() - Now) > 1000 || ConsensusServer.GetState().MethodCode != methodCode, Signer.Majority() - 1);                
                HashSource.Cancel();
                if (Globals.ConsensusTokenSource.IsCancellationRequested)
                    return null;

                if (runType != RunType.Last && ConsensusServer.GetState().MethodCode != methodCode)
                {
                    SendMethodCode(Peers, methodCode);
                    return Messages.Select(x => (x.Key, x.Value.Message)).ToArray();
                }

                var PeerHashes = (await Task.WhenAll(HashTasks.Where(x => x.IsCompleted))).Where(x => x != null).ToArray();
                if (PeerHashes.Length < Majority - 1)
                    return null;

                var MyHashes = Messages.Select(x => x.Key + ":" + Ecdsa.sha256(x.Value.Message)).ToHashSet();
                if (PeerHashes.Any(x => !MyHashes.SetEquals(x)))
                    return null;

                if (runType != RunType.Last)
                {
                    ConsensusServer.IncrementMethodCode(methodCode);
                    SendMethodCode(Peers, methodCode + 1);
                }
                return Messages.Select(x => (x.Key, x.Value.Message)).ToArray();
            }
            catch(Exception ex)
            {
            }
            return null;
        }

        public static void SendMethodCode(NodeInfo[] peers, int methodCode)
        {
            var Now = TimeUtil.GetMillisecondTime();
            var Height = Globals.LastBlock.Height + 1;
            _ = Task.WhenAll(peers.Select(node =>
            {
                var SendMethodCodeFunc = () => node.Connection?.InvokeCoreAsync<bool>("SendMethodCode", args: new object?[] { Height, methodCode }, default)
                    ?? Task.FromResult(false);
                return SendMethodCodeFunc.RetryUntilSuccessOrCancel(x => x || TimeUtil.GetMillisecondTime() - Now > 2000, 100, default);
            }));
        }

        public static async Task PeerRequestLoop(int methodCode, NodeInfo peer, HashSet<string> addresses, CancellationTokenSource cts)
        {
            var messages = ConsensusServer.Messages[(Globals.LastBlock.Height + 1, methodCode)];
            var rnd = new Random();
            var MissingAddresses = addresses.Except(messages.Select(x => x.Key)).OrderBy(x => rnd.Next()).ToArray();            
            while (!cts.IsCancellationRequested && MissingAddresses.Any())
            {
                var delay = Task.Delay(100);
                try
                {
                    if (!peer.IsConnected)
                    {
                        await delay;
                        continue;
                    }
                    
                    var Response = await peer.Connection.InvokeCoreAsync<string>("Message", args: new object?[] { Globals.LastBlock.Height + 1, methodCode, MissingAddresses }, cts.Token);
                    if(Response != null)
                    {                        
                        var arr = Response.Split(";:;");
                        var (address, message, signature) = (arr[0], arr[1].Replace("::", ":"), arr[2]);
                        if(MissingAddresses.Contains(address) && SignatureService.VerifySignature(address, message, signature))
                            messages[address] = (message, signature);
                    }
                    MissingAddresses = addresses.Except(messages.Select(x => x.Key)).OrderBy(x => rnd.Next()).ToArray();
                }
                catch(Exception ex) {                    
                }
                await delay;
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
