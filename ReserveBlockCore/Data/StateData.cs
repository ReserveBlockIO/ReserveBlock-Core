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
                    Balance = 0, //subtract from the address
                    StateRoot = block.StateRoot
                };

                accStTrei.Add(acctStateTreiFrom);

                var acctStateTreiTo = new AccountStateTrei
                {
                    Key = x.ToAddress,
                    Nonce = 0, //increase nonce for next use
                    Balance = (x.Amount), //subtract from the address
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
        public static void UpdateTreis(Block block)
        {
            var txList = block.Transactions.ToList();
            var accStTrei = GetAccountStateTrei();

            

            txList.ForEach(x => {
                if (block.Height == 0)
                {
                    var acctStateTreiFrom = new AccountStateTrei
                    {
                        Key = x.FromAddress,
                        Nonce = x.Nonce + 1, //increase Nonce for next use
                        Balance = 0, //subtract from the address
                        StateRoot = block.StateRoot
                    };

                    accStTrei.Insert(acctStateTreiFrom);
                }
                else
                {
                    if (x.FromAddress != "Coinbase_TrxFees" && x.FromAddress != "Coinbase_BlkRwd")
                    {
                        var from = GetSpecificAccountStateTrei(x.FromAddress);

                        from.Nonce += 1;
                        from.StateRoot = block.StateRoot;
                        from.Balance -= (x.Amount + x.Fee);

                        accStTrei.Update(from);
                    }
                    else
                    {
                        //do nothing as its the coinbase fee
                    }
                    
                }

                var to = GetSpecificAccountStateTrei(x.ToAddress);

                if(to == null)
                {
                    var acctStateTreiTo = new AccountStateTrei
                    {
                        Key = x.ToAddress,
                        Nonce = 0, 
                        Balance = x.Amount, 
                        StateRoot = block.StateRoot
                    };

                    accStTrei.Insert(acctStateTreiTo);
                }
                else
                {
                    to.Balance = x.Amount;
                    to.StateRoot = block.StateRoot;

                    accStTrei.Update(to);
                }    

            });

            WorldTrei.UpdateWorldTrei(block);

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
