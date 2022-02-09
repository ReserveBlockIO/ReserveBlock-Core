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

            var wTrei = DbContext.DB_WorldStateTrei.GetCollection<WorldTrei>(DbContext.RSRV_WSTATE_TREI);
            wTrei.Insert(worldTrei);
            var aTrei = DbContext.DB_AccountStateTrei.GetCollection<AccountStateTrei>(DbContext.RSRV_ASTATE_TREI);
            aTrei.InsertBulk(accStTrei);
        }

        public static void UpdateAccountNonce(string address, long ?nonce = null)
        {
            var account = GetSpecificAccountStateTrei(address);
            if(nonce == null)
            {
                account.Nonce += 1;
            }    
            else
            {
                account.Nonce = nonce.Value;
            }
            var accountTrei = GetAccountStateTrei();
            accountTrei.Update(account);
        }

        public static ILiteCollection<AccountStateTrei> GetAccountStateTrei()
        {
            var aTrei = DbContext.DB_AccountStateTrei.GetCollection<AccountStateTrei>(DbContext.RSRV_ASTATE_TREI);
            return aTrei;
            
        }

        public static AccountStateTrei GetSpecificAccountStateTrei(string address)
        {
            var aTrei = DbContext.DB_AccountStateTrei.GetCollection<AccountStateTrei>(DbContext.RSRV_ASTATE_TREI);
            var account = aTrei.FindOne(x => x.Key == address);
            if (account == null)
            {
                return null;
            }
            else
            {
                return account;
            }
        }
        
    }
}
