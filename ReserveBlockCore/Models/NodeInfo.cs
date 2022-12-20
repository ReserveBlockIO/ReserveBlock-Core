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
        public bool IsConnected { get { return Connection?.State == HubConnectionState.Connected; } }
        public bool IsValidator { get; set; }
        public bool IsAdjudicator { get; set; }

        private int ProcessQueueLock = 0;

        private ConcurrentQueue<(Func<CancellationToken, Task<object>> invokeFunc, Func<CancellationToken> ctFunc, Action<object> setResult)> invokeQueue =
            new ConcurrentQueue<(Func<CancellationToken, Task<object>> invokeFunc, Func<CancellationToken> ctFunc, Action<object> setResult)>();

        private async Task ProcessQueue()
        {
            if (Interlocked.Exchange(ref ProcessQueueLock, 1) == 1)
                return;

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
                            var Result = await RequestInfo.invokeFunc(token);                           
                            RequestInfo.setResult(Result);                            
                            Fail = false;
                        }
                        catch { }
                        if (Fail)
                            RequestInfo.setResult(default);
                    }
                }
                catch { }
            }

            Interlocked.Exchange(ref ProcessQueueLock, 0);
            if (invokeQueue.Count != 0)
                await ProcessQueue();
        }

        public async Task<T> InvokeAsync<T>(string method, object[] args, Func<CancellationToken> ctFunc)
        {
            var Source = new TaskCompletionSource<T>();
            var InvokeFunc = async (CancellationToken ct) => (object)(await Connection.InvokeCoreAsync<T>(method, args, ct));
            invokeQueue.Enqueue((InvokeFunc, ctFunc, (object x) => Source.SetResult((T)x)));
            _ = ProcessQueue();

            return await Source.Task;
        }
    }
}
