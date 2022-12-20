using Microsoft.AspNetCore.SignalR.Client;
using ReserveBlockCore.Utilities;
using System;
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

        public readonly SemaphoreSlim APILock = new SemaphoreSlim(1, 1);
        public bool IsConnected { get { return Connection?.State == HubConnectionState.Connected; } }
        public async Task<T> InvokeAsync<T>(string method, object[] args = null, CancellationToken ct = default)
        {
            try
            {
                await APILock.WaitAsync();
                var delay = Task.Delay(1000);
                var Result = await Connection.InvokeCoreAsync<T>(method, args ?? Array.Empty<object>(), ct);
                await delay;
                return Result;
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError($"Unknown Error: {ex.ToString()}", "AdjNodeInfo.InvokeAsync()");
            }
            finally
            {
                try { APILock.Release(); } catch { }
            }

            return default;
        }
    }
}
