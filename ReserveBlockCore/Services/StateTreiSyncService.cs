using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Services
{
    public class StateTreiSyncService
    {
        private static bool IsRunning = false;
        public static async Task SyncAccountStateTrei()
        {
            try
            {
                DbContext.BeginTrans();
                if (IsRunning == false)
                {
                    IsRunning = true;

                    List<AccountStateTrei> blockBalances = new List<AccountStateTrei>();
                    List<AccountStateTrei> stateBalances = new List<AccountStateTrei>();

                    var blocks = BlockchainData.GetBlocks().FindAll().ToList();

                    foreach (Block block in blocks)
                    {
                        var txList = block.Transactions.ToList();
                        txList.ForEach(x =>
                        {
                            if (block.Height == 0)
                            {
                                var acctStateTreiFrom = new AccountStateTrei
                                {
                                    Key = x.FromAddress,
                                    Nonce = x.Nonce + 1, //increase Nonce for next use
                                    Balance = 0, //subtract from the address
                                    StateRoot = block.StateRoot
                                };

                                blockBalances.Add(acctStateTreiFrom);
                            }
                            else
                            {
                                if (x.FromAddress != "Coinbase_TrxFees" && x.FromAddress != "Coinbase_BlkRwd")
                                {
                                    var from = blockBalances.Where(a => a.Key == x.FromAddress).FirstOrDefault();

                                    from.Nonce += 1;
                                    from.StateRoot = block.StateRoot;
                                    from.Balance -= (x.Amount + x.Fee);

                                }
                                else
                                {
                                    //do nothing as its the coinbase fee
                                }

                            }
                            if (x.ToAddress != "Adnr_Base" && x.ToAddress != "DecShop_Base")
                            {
                                if (x.TransactionType == TransactionType.TX)
                                {
                                    var to = blockBalances.Where(a => a.Key == x.ToAddress).FirstOrDefault();

                                    if (to == null)
                                    {
                                        var acctStateTreiTo = new AccountStateTrei
                                        {
                                            Key = x.ToAddress,
                                            Nonce = 0,
                                            Balance = x.Amount,
                                            StateRoot = block.StateRoot
                                        };

                                        blockBalances.Add(acctStateTreiTo);
                                    }
                                    else
                                    {
                                        to.Balance += x.Amount;
                                        to.StateRoot = block.StateRoot;
                                    }
                                }
                            }
                        });
                    }

                    var stateTrei = StateData.GetAccountStateTrei();
                    stateBalances = StateData.GetAccountStateTrei().Find(x => x.Key != "rbx_genesis_transaction").ToList();

                    foreach (var bb in blockBalances)
                    {
                        var stateTreiRec = stateBalances.Where(x => x.Key == bb.Key).FirstOrDefault();
                        if (stateTreiRec != null)
                        {
                            if (stateTreiRec.Balance != bb.Balance)
                            {
                                ErrorLogUtility.LogError(
                                    $"Balance Off: {stateTreiRec.Key} | Reported: {stateTreiRec.Balance} - Actual: {bb.Balance}",
                                    "StateTreiSyncService()");
                                stateTreiRec.Balance = bb.Balance;
                                stateTrei.UpdateSafe(stateTreiRec);
                                
                            }

                        }
                        else
                        {
                            if (bb.Key != "rbx_genesis_transaction")
                            {
                                AccountStateTrei nAcctST = new AccountStateTrei
                                {
                                    Key = bb.Key,
                                    Nonce = 0,
                                    Balance = bb.Balance,
                                    StateRoot = bb.StateRoot

                                };

                                stateTrei.InsertSafe(nAcctST);
                            }

                        }
                    }
                }

                IsRunning = false;
                DbContext.Commit();
            }
            catch(Exception ex)
            {
                DbContext.Rollback();
                ErrorLogUtility.LogError($"Erroring Running SyncAccountStateTrei. Error : {ex.Message}", "StateTreiSyncService.SyncAccountStateTrei()");
            }
        }
    }
}
