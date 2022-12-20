using Microsoft.AspNetCore.SignalR.Client;
using ReserveBlockCore.Utilities;
using System;
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

        public async Task<T> InvokeAsync<T>(string method, object[] args = null, CancellationToken ct = default)
        {
            try
            {
                await APILock.WaitAsync();
                var delay = Globals.AdjudicateAccount != null ? Task.CompletedTask : Task.Delay(1000);
                var Result = await Connection.InvokeCoreAsync<T>(method, args ?? Array.Empty<object>(), ct);
                await delay;
                return Result;
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError($"Unknown Error: {ex.ToString()}", "NodeInfo.InvokeAsync()");
            }
            finally
            {
                try { APILock.Release(); } catch { }
            }

            return default;
        }
    }
}
