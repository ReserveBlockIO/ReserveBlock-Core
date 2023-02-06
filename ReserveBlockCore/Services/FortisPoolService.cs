using Newtonsoft.Json;

namespace ReserveBlockCore.Services
{
    public class FortisPoolService
    {
        static SemaphoreSlim FortisPoolServiceLock = new SemaphoreSlim(1, 1);

        public static async Task PopulateFortisPoolCache()
        {
            if (Globals.AdjudicateAccount == null)
                return;

            while(true)
            {
                var delay = Task.Delay(new TimeSpan(0, 2, 0));

                if (Globals.StopAllTimers && !Globals.IsChainSynced)
                {
                    await delay;
                    continue;
                }

                await FortisPoolServiceLock.WaitAsync();
                try
                {
                    var fortisPool = Globals.FortisPool.Values.ToList();
                    if(fortisPool.Count() > 0)
                    {
                        var peerIPs = fortisPool.Select(x => x.IpAddress).ToList();
                        Globals.FortisPoolCache = JsonConvert.SerializeObject(fortisPool);
                    }
                }
                finally
                {
                    FortisPoolServiceLock.Release();
                }

                await delay;
            }
            
        }
    }
}
