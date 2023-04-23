using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Services
{
    public class ReserveService
    {
        static SemaphoreSlim ReserveServiceLock = new SemaphoreSlim(1, 1);

        public static async Task Run()
        {
            while (true)
            {
                var delay = Task.Delay(new TimeSpan(0, 2, 0));
                await ReserveServiceLock.WaitAsync();
                try
                {
                    CheckReserveTransactions();
                }
                finally
                {
                    ReserveServiceLock.Release();
                }

                await delay;
            }
        }

        private static void CheckReserveTransactions()
        {
            var currentTime = TimeUtil.GetTime();
            var rTXDb = ReserveTransactions.GetReserveTransactionsDb();
            if(rTXDb != null)
            {
                var reserveTxList = rTXDb.Query().Where(x => x.ConfirmTimestamp < currentTime).ToList();
                if(reserveTxList.Count() > 0)
                {
                    StateData.UpdateTreiFromReserve(reserveTxList);
                }
            }
            
        }
    }
}
