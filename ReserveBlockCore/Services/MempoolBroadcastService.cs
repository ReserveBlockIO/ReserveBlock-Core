using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Utilities;
using System.Collections.Concurrent;

namespace ReserveBlockCore.Services
{
    public class MempoolBroadcastService
    {
        static SemaphoreSlim MempoolBroadcastServiceLock = new SemaphoreSlim(1, 1);
        private static ConcurrentDictionary<string, int> RebroadcastDict = new ConcurrentDictionary<string, int>();
        public static async Task RunBroadcastService()
        {
            while(true)
            {
                var delay = Task.Delay(90000);
                if (Globals.StopAllTimers && !Globals.IsChainSynced)
                {
                    await delay;
                    continue;
                }

                await MempoolBroadcastServiceLock.WaitAsync();

                try
                {
                    await StartupService.ClearStaleMempool();

                    var currentTimeMinusFiveMins = TimeUtil.GetTime(-300);

                    var mempoolList = TransactionData.GetMempool();
                    if (mempoolList != null)
                    {
                        var mempool = mempoolList.Where(x => x.Timestamp <= currentTimeMinusFiveMins).ToList();
                        if (mempool.Count() > 0)
                        {
                            foreach (var mempoolEntry in mempool)
                            {
                                if (RebroadcastDict.TryGetValue(mempoolEntry.Hash, out var rebr))
                                {
                                    if (rebr < 3)
                                    {
                                        RebroadcastDict[mempoolEntry.Hash] += 1;
                                        var account = AccountData.GetSingleAccount(mempoolEntry.FromAddress);
                                        if (account != null)
                                        {
                                            if (account.IsValidating || !string.IsNullOrEmpty(Globals.ValidatorAddress))
                                            {
                                                await P2PValidatorClient.SendTXMempool(mempoolEntry);//send directly to adjs
                                            }
                                            else
                                            {
                                                await P2PClient.SendTXMempool(mempoolEntry);//send out to mempool
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    RebroadcastDict.TryAdd(mempoolEntry.Hash, 1);
                                    var account = AccountData.GetSingleAccount(mempoolEntry.FromAddress);
                                    if (account != null)
                                    {
                                        if (account.IsValidating || !string.IsNullOrEmpty(Globals.ValidatorAddress))
                                        {
                                            await P2PValidatorClient.SendTXMempool(mempoolEntry);//send directly to adjs
                                        }
                                        else
                                        {
                                            await P2PClient.SendTXMempool(mempoolEntry);//send out to mempool
                                        }
                                    }

                                }
                            }
                        }
                    }
                }
                finally
                {
                    MempoolBroadcastServiceLock.Release();
                }

                await delay;
            }   
        }
    }
}
