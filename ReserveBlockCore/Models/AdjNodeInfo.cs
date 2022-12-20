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
    public class AdjNodeInfo
    {
        public HubConnection Connection;
        public string Address { get; set; }
        public string IpAddress { get; set; }
        public bool LastWinningTaskError { get; set; }
        public DateTime LastWinningTaskSentTime { get; set; }
        public long LastWinningTaskBlockHeight { get; set; }
        public long LastSentBlockHeight { get; set; }        
        public DateTime? AdjudicatorConnectDate { get; set; }
        public DateTime? LastTaskSentTime { get; set; }        
        public DateTime? LastTaskResultTime { get; set; }
        public long LastTaskBlockHeight { get; set; }
        public bool LastTaskError { get; set; }
        public int LastTaskErrorCount { get; set; }                
        public bool IsConnected { get { return Connection?.State == HubConnectionState.Connected; } }

        private int ProcessQueueLock = 0;

        private ConcurrentQueue<(string method, object[] args, Func<CancellationToken> ctFunc, TaskCompletionSource<string> source)> invokeQueue =
            new ConcurrentQueue<(string method, object[] args, Func<CancellationToken> ctFunc, TaskCompletionSource<string> source)>();

        private async Task ProcessQueue()
        {
            if (Interlocked.Exchange(ref ProcessQueueLock, 1) == 1)
                return;

            while (invokeQueue.Count != 0)
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
                        if (Fail)
                            RequestInfo.source.SetResult(null);
                    }
                }
                catch { }
            }

            Interlocked.Exchange(ref ProcessQueueLock, 0);
            if (invokeQueue.Count != 0)
                await ProcessQueue();
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
