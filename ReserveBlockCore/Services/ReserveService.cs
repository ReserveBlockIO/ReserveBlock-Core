using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Services
{
    public class ReserveService
    {
        static SemaphoreSlim RunLock = new SemaphoreSlim(1, 1);
        static SemaphoreSlim RunUnlockWipeLock = new SemaphoreSlim(1, 1);

        public static async Task Run()
        {
            try
            {
                await RunLock.WaitAsync();
                var latestBlockTime = Globals.LastBlock.Timestamp;
                var rTXDb = ReserveTransactions.GetReserveTransactionsDb();
                if (rTXDb != null)
                {
                    var reserveTxList = rTXDb.Query().Where(x => x.ConfirmTimestamp < latestBlockTime && x.ReserveTransactionStatus == ReserveTransactionStatus.Pending).ToList();
                    if (reserveTxList.Count() > 0)
                    {
                        StateData.UpdateTreiFromReserve(reserveTxList);
                    }
                }
            }
            finally
            {
                RunLock.Release();
            }
        }

        public static async Task RunUnlockWipe()
        {
            try
            {
                await RunUnlockWipeLock.WaitAsync();

                var delay = Task.Delay(new TimeSpan(0, 1, 0));

                if(Globals.ReserveAccountUnlockKeys.Any())
                {
                    foreach(var rAUK in  Globals.ReserveAccountUnlockKeys)
                    {
                        if(rAUK.Value.DeleteAfterTime < TimeUtil.GetTime())
                        {
                            Globals.ReserveAccountUnlockKeys.TryRemove(rAUK.Key, out _);
                        }
                    }
                }

                await delay;
            }
            catch { }
            finally { RunUnlockWipeLock.Release(); }
            
        }

    }
}
