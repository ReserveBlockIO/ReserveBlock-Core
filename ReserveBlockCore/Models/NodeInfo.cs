using Microsoft.AspNetCore.SignalR.Client;
using ReserveBlockCore.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReserveBlockCore.Models
{
    public class NodeInfo
    {
        public HubConnection Connection;
        public string NodeIP { get; set; } 

        public string Address { get; set; }

        public long NodeHeight { get; set; }

        public int MethodCode = -100;

        public bool IsFinalized = false;

        public long LastMethodCodeTime { get; set; }
        public int NodeLatency { get; set; }
        public DateTime? NodeLastChecked { get; set; }

        public int IsSendingBlock;
        
        public long SendingBlockTime;
        
        public long TotalDataSent;

        public long PreviousReceiveTime;

        public long SecondPreviousReceiveTime;

        public readonly SemaphoreSlim APILock = new SemaphoreSlim(1, 1);

        public bool IsConnected { get { return Connection?.State == HubConnectionState.Connected; } }
        public bool IsValidator { get; set; }
        public bool IsAdjudicator { get; set; }

        private ConcurrentQueue<(string method, object[] args, Func<CancellationToken> ctFunc, TaskCompletionSource<string> source)> invokeQueue =
            new ConcurrentQueue<(string method, object[] args, Func<CancellationToken> ctFunc, TaskCompletionSource<string> source)>();

        private async Task ProcessQueue()
        {
            while(invokeQueue.Count != 0)
            {
                try
                {
                    if (invokeQueue.TryDequeue(out var RequestInfo))
                    {
                        var Fail = true;
                        try
                        {
                            var token = RequestInfo.ctFunc();
                            var Result = await Connection.InvokeCoreAsync<string>(RequestInfo.method, RequestInfo.args, token);
                            RequestInfo.source.SetResult(Result);
                            Fail = false;
                        }
                        catch { }
                        if(Fail)
                            RequestInfo.source.SetResult(null);
                    }
                }
                catch { }
            }
        }

        public async Task<string> InvokeAsync(string method, object[] args, Func<CancellationToken> ctFunc)
        {
            var Source = new TaskCompletionSource<string>();
            invokeQueue.Enqueue((method, args, ctFunc, Source));
            _ = ProcessQueue();

            return await Source.Task;
        }
    }
}
