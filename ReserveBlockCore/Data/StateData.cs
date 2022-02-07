using LiteDB;
using Newtonsoft.Json;
using ReserveBlockCore.Models;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Data
{
    public class StateData
    {
        public static void UpdateStateTrei(List<Transaction> prcTranList)
        {
            if (prcTranList == null)
                return;
            var state = DbContext.DB.GetCollection<StateTrei>(DbContext.RSRV_STATE_TREI);
            var stateList = state.FindAll().ToString();
            //var transactions = DbContext.DB.GetCollection<Transaction>(DbContext.RSRV_TRANSACTIONS);
            prcTranList.ForEach(x => {
                var toAddr = state.Query().Where(y => y.Key == x.ToAddress).FirstOrDefault();
                var fromAddr = state.Query().Where(y => y.Key == x.FromAddress).FirstOrDefault();
                if (toAddr != null)
                {
                    toAddr.Balance += x.Amount;

                    state.Update(toAddr);
                }
                else
                {
                    StateTrei nStateTreiRec = new StateTrei {
                        Balance = x.Amount,
                        Key = x.ToAddress,
                        Nonce = 1,
                        StateRoot = "",//Update
                        CodeHash = ""//Update for NFT SC
                    };

                    state.Insert(nStateTreiRec);
                }

                if (fromAddr != null)
                {
                    fromAddr.Balance -= x.Amount;
                    fromAddr.Nonce += 1;
                    state.Update(toAddr);
                }
                else
                {
                    //a From should never be null. 
                    //report an error if null.
                }

            });
        }
    }
}
