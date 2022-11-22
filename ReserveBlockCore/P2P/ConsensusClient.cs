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
                    node.Connection.DisposeAsync().ConfigureAwait(false).GetAwaiter().GetResult();                
            }
        }

        protected virtual async ValueTask DisposeAsyncCore()
        {
            foreach (var node in Globals.Nodes.Values)
                await node.Connection.DisposeAsync();
        }

        #endregion

        #region Consensus Code

        public static async Task<(string Address, string Message)[]> ConsensusRun(long height, int methodCode, string message, string signature, int timeToFinalize, CancellationToken ct)
        {
            var CurrentAddresses = Signer.CurrentSigningAddresses();
            var NumNodes = CurrentAddresses.Count;
            var Majority = NumNodes / 2 + 1;
            var Address = Globals.AdjudicateAccount.Address;
            var Peers = Globals.Nodes.Values.Where(x => x.Address != Address).ToArray();

            ConsensusServer.UpdateState(height, methodCode, (int)ConsensusStatus.Processing);
            if(!ConsensusServer.Messages.TryGetValue((height, methodCode), out var Message))
            {
                Message = new ConcurrentDictionary<string, (string Message, string Signature)>();
                ConsensusServer.Messages[(height, methodCode)] = Message;
            }

            Message[Globals.AdjudicateAccount.Address] = (message, signature);

            var messages = ConsensusServer.Messages[(height, methodCode)];
                        
            var ConsensusSource = CancellationTokenSource.CreateLinkedTokenSource(Globals.ConsensusTokenSource.Token);            
            foreach (var peer in Peers)
            {
                _ = PeerRequestLoop(height, methodCode, peer, CurrentAddresses, ConsensusSource);
            }
            try
            {
                await Task.Delay(timeToFinalize, ConsensusSource.Token);
            }
            catch { }
            while (messages.Count < Majority && !Globals.ConsensusTokenSource.IsCancellationRequested)
            {
                await Task.Delay(4);
            }
            
            ConsensusSource.Cancel();

            var SignatureSource = CancellationTokenSource.CreateLinkedTokenSource(Globals.ConsensusTokenSource.Token);
            var SignatureTasks = Peers.Select(node =>
            {
                var SignatureRequestFunc = () => node.Connection?.InvokeCoreAsync<string[]>("Signatures", args: new object?[] { height, methodCode }, ct)
                    ?? Task.FromResult((string[])null);
                return SignatureRequestFunc.RetryUntilSuccessOrCancel(x => x != null, 100, SignatureSource.Token);
            })
            .ToArray();

            await SignatureTasks.WhenAtLeast(x => x != null, Signer.Majority() - 1);
            SignatureSource.Cancel();

            var PeerSignatures = (await Task.WhenAll(SignatureTasks.Where(x => x.IsCompleted))).Where(x => x != null).ToArray();
            if (PeerSignatures.Length < Majority - 1)
                return null;

            var MySignatures = messages.Select(x => x.Key + ":" + x.Value.Signature).ToHashSet();
            if (PeerSignatures.Any(x => !MySignatures.SetEquals(x)))
                return null;

            return messages.Select(x => (x.Key, x.Value.Message)).ToArray();
        }

        public static async Task PeerRequestLoop(long height, int methodCode, NodeInfo peer, HashSet<string> addresses, CancellationTokenSource cts)
        {
            var messages = ConsensusServer.Messages[(height, methodCode)];
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
                    
                    var Response = await peer.Connection.InvokeCoreAsync<string>("Message", args: new object?[] { height, methodCode, MissingAddresses }, cts.Token);
                    if(Response != null)
                    {
                        var arr = Response.Split(':');
                        var (address, message, signature) = (arr[0], arr[1], arr[2]);
                        if(MissingAddresses.Contains(address) && SignatureService.VerifySignature(address, message, signature))
                            messages[address] = (message, signature);
                    }
                    MissingAddresses = addresses.Except(messages.Select(x => x.Key)).OrderBy(x => rnd.Next()).ToArray();
                }
                catch(Exception ex) {                    
                }
                await delay;
            }
            if (!MissingAddresses.Any())
                cts.Cancel();
        }

        #endregion


        #region Connect Adjudicator
        public static ConcurrentDictionary<string, bool> IsConnectingDict = new ConcurrentDictionary<string, bool>();
        public static async Task<bool> ConnectConsensusNode(string url, string address, string time, string uName, string signature)
        {
            var IPAddress = url.Replace("http://", "").Replace("/consensus", "").Replace(Globals.Port.ToString(), "").Replace(":", "");
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
                if (hubConnection?.State != HubConnectionState.Connected)
                    return false;

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
