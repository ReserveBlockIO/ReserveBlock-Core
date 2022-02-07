using LiteDB;
using Newtonsoft.Json;
using ReserveBlockCore.Models;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Data
{
    public class StateData
    {
        public static void CreateGenesisWorldTrei(Block block)
        {
            var trxList = block.Transactions.ToList();
            var accStTrei = new List<AccountStateTrei>();

            trxList.ForEach(x => {
                var acctStateTreiFrom = new AccountStateTrei {
                    Key = x.FromAddress,
                    Nonce = x.Nonce + 1, //increase Nonce for next use
                    Balance = (x.Amount + x.Fee) * -1, //subtract from the address
                    StateRoot = block.StateRoot
                };

                accStTrei.Add(acctStateTreiFrom);

                var acctStateTreiTo = new AccountStateTrei
                {
                    Key = x.ToAddress,
                    Nonce = x.Nonce + 1, //increase nonce for next use
                    Balance = (x.Amount + x.Fee), //subtract from the address
                    StateRoot = block.StateRoot
                };

                accStTrei.Add(acctStateTreiTo);

            });

            var worldTrei = new WorldTrei {
                StateRoot = block.StateRoot,
            };

            var wTrei = DbContext.DB.GetCollection<WorldTrei>(DbContext.RSRV_WSTATE_TREI);
            wTrei.Insert(worldTrei);
            var aTrei = DbContext.DB.GetCollection<AccountStateTrei>(DbContext.RSRV_ASTATE_TREI);
            aTrei.InsertBulk(accStTrei);
        }
        //public static void UpdateWorldStateTrei(List<Transaction> prcTranList)
        //{
        //    if (prcTranList == null)
        //        return;
        //    var state = DbContext.DB.GetCollection<AccountStateTrei>(DbContext.RSRV_STATE_TREI);
        //    var stateList = state.FindAll().ToString();
        //    //var transactions = DbContext.DB.GetCollection<Transaction>(DbContext.RSRV_TRANSACTIONS);
        //    prcTranList.ForEach(x => {
        //        var toAddr = state.Query().Where(y => y.Key == x.ToAddress).FirstOrDefault();
        //        var fromAddr = state.Query().Where(y => y.Key == x.FromAddress).FirstOrDefault();
        //        if (toAddr != null)
        //        {
        //            toAddr.Balance += x.Amount;

        //            state.Update(toAddr);
        //        }
        //        else
        //        {
        //            AccountStateTrei nStateTreiRec = new AccountStateTrei {
        //                Balance = x.Amount,
        //                Key = x.ToAddress,
        //                Nonce = 1,
        //                StateRoot = "",//Update
        //                CodeHash = ""//Update for NFT SC
        //            };

        //            state.Insert(nStateTreiRec);
        //        }

        //        if (fromAddr != null)
        //        {
        //            fromAddr.Balance -= x.Amount;
        //            fromAddr.Nonce += 1;
        //            state.Update(toAddr);
        //        }
        //        else
        //        {
        //            //a From should never be null. 
        //            //report an error if null.
        //        }

        //    });
        //}
    }
}
