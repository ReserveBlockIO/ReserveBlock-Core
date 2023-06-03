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
            await ReserveServiceLock.WaitAsync();
            try
            {
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
                ReserveServiceLock.Release();
            }
        }

    }
}
