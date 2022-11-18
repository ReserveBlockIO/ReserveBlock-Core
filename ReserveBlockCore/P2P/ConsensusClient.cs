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

namespace ReserveBlockCore.P2P
{
    public class ConsensusClient : IAsyncDisposable, IDisposable
    {
        //private static async Task RemoveConsensusNode(ConsensusNodeInfo node)
        //{
        //    if (Globals.ConsensusNodes.TryRemove(node.IpAddress, out var test))
        //        await node.Connection.DisposeAsync();
        //}

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
        private static bool HasMajorityIntersectionSet()
        {
            var majority = Signer.Majority();
            return ConsensusServer.Histories.Values.Where(x => x.Count >= majority).Count() >= majority;
        }

        private static bool BestCase()
        {
            var NumNodes = Signer.NumSigners();
            return ConsensusServer.Histories.Values.Where(x => x.Count == NumNodes).Count() == NumNodes;
        }
        public static async Task<(string Address, string Message)[]> ConsensusRun(long height, int methodCode, string message, string signature, int timeToFinalize, CancellationToken ct)
        {
            var NumNodes = Signer.NumSigners;
            var Address = Globals.AdjudicateAccount.Address;
            var Peers = Globals.ConsensusNodes.Values.Where(x => x.Address != Address).ToArray();
            var Now = DateTime.Now;

            ConsensusServer.UpdateState(height, methodCode, (int)ConsensusStatus.Processing);
            if(!ConsensusServer.Messages.TryGetValue((height, methodCode), out var Message))
            {
                Message = new ConcurrentDictionary<string, (string Message, string Signature)>();
                ConsensusServer.Messages[(height, methodCode)] = Message;
            }

            Message[Globals.AdjudicateAccount.Address] = (message, signature);

            if (!ConsensusServer.Histories.TryGetValue((height, methodCode, Globals.AdjudicateAccount.Address), out var History))
            {
                History = new ConcurrentDictionary<string, bool>();
                ConsensusServer.Histories[(height, methodCode, Globals.AdjudicateAccount.Address)] = History;
            }

            History[Globals.AdjudicateAccount.Address] = true;            

            var ConsensusSource = CancellationTokenSource.CreateLinkedTokenSource(Globals.ConsensusTokenSource.Token);
            while (!BestCase() && !(HasMajorityIntersectionSet() && DateTime.Now < Now.AddMilliseconds(timeToFinalize)))
            {
                await ConsensusIteration(height, methodCode, Peers, ConsensusSource.Token);
            }
            ConsensusSource.Cancel();

            if (BestCase())
            {
                ConsensusServer.UpdateState(status: (int)ConsensusStatus.Done);
                if (ConsensusServer.Messages.TryGetValue((height, methodCode), out var BestResult))
                    return BestResult.Select(x => (x.Key, x.Value.Message)).ToArray();
            }
            else
                ConsensusServer.UpdateState(status: (int)ConsensusStatus.Finalizing);

            var FinalizingSource = CancellationTokenSource.CreateLinkedTokenSource(Globals.ConsensusTokenSource.Token);
            await Peers.Select(node =>
            {
                var IsFinalizingOrDoneFunc = () => Globals.ConsensusNodes[node.IpAddress].Connection?.InvokeCoreAsync<bool>("IsFinalizingOrDone", args: new object?[] { height, methodCode }, ct)
                    ?? Task.FromResult(false);
                return IsFinalizingOrDoneFunc.RetryUntilSuccessOrCancel(x => x, 5000, FinalizingSource.Token);
            })
            .ToArray()
            .WhenAtLeast(x => x, Signer.Majority() - 1);            
            FinalizingSource.Cancel();

            var FinalSource = CancellationTokenSource.CreateLinkedTokenSource(Globals.ConsensusTokenSource.Token);
            await ConsensusIteration(height, methodCode, Peers, FinalSource.Token);
            FinalSource.Cancel();

            ConsensusServer.UpdateState(status: (int)ConsensusStatus.Done);
            if (ConsensusServer.Messages.TryGetValue((height, methodCode), out var Result))
                return Result.Select(x => (x.Key, x.Value.Message)).ToArray();
            return default;
        }

        public static async Task ConsensusIteration(long height, int methodCode, ConsensusNodeInfo[] peers, CancellationToken ct)
        {
            var CurrentMessages = ConsensusServer.Messages.TryGetValue((height, methodCode), out var messages) ?
                messages.Select(x => (x.Key, x.Value.Message, x.Value.Signature)).ToArray() : new (string, string, string)[] { };

            var Requests = peers.Select(node =>
            {
                var NodeHistory = ConsensusServer.Histories.TryGetValue((height, methodCode, node.Address), out var history) ?
                    history.Keys.ToHashSet() : new HashSet<string>();
                var MessagesToSend = CurrentMessages.Where(x => !NodeHistory.Contains(x.Item1)).ToArray();
                var MessagesToSendString = JsonConvert.SerializeObject(MessagesToSend);
                var MessageFunc = () => Globals.ConsensusNodes[node.IpAddress].Connection?.InvokeCoreAsync<(string address, string message, string signature)[]>("Message", args: new object?[] { height, methodCode, MessagesToSendString }, ct)
                    ?? Task.FromResult(((string address, string message, string signature)[])null);
                return (node, MessageFunc.RetryUntilSuccessOrCancel(x => x != null, 5000, ct));
            })
            .ToArray();

            await Requests.Select(x => x.Item2).ToArray().WhenAtLeast(x => x != null, Signer.Majority() - 1);
            
            foreach (var request in Requests)
            {
                if (request.Item2.IsCompleted)
                {
                    var result = (await request.Item2).Where(x => SignatureService.VerifySignature(x.address, x.message, x.signature)).ToArray();

                    if (!ConsensusServer.Messages.TryGetValue((height, methodCode), out var Messages))
                    {
                        Messages = new ConcurrentDictionary<string, (string Message, string Signature)>();
                        ConsensusServer.Messages[(height, methodCode)] = Messages;                           
                    }

                    if (!ConsensusServer.Histories.TryGetValue((height, methodCode, request.node.Address), out var History))
                    {
                        History = new ConcurrentDictionary<string, bool>();
                        ConsensusServer.Histories[(height, methodCode, request.node.Address)] = History;
                    }

                    foreach (var item in result)
                    {
                        Messages[item.address] =  (item.message, item.signature);
                        History[item.address] = true;
                    }
                }
            }
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
                    return Globals.ConsensusNodes[IPAddress].IsConnected;

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

                Globals.ConsensusNodes[IPAddress].Connection = hubConnection;

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

        public static async Task<bool> GetBlock(long height, ConsensusNodeInfo node)
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
                        BlockDownloadService.BlockDict[height] = (Block, node.IpAddress);
                        return true;
                    }                        
                }
            }
            catch { }

            return false;
        }

        public static async Task<long> GetNodeHeight(ConsensusNodeInfo node)
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
