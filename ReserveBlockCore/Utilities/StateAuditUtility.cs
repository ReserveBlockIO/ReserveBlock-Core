using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using System.Collections.Concurrent;

namespace ReserveBlockCore.Utilities
{
    public class StateAuditUtility
    {
        public static async Task AuditAccountStateTrei(ConcurrentDictionary<string, StateTreiAuditData> accountStateTreiDict)
        {
            var accountStateTrei = StateData.GetAccountStateTrei();
            foreach(var acctStateTreiAudit in accountStateTreiDict.Values)
            {
                var acctSTRec = StateData.GetSpecificAccountStateTrei(acctStateTreiAudit.Address);

                if(acctSTRec == null)
                {
                    var acctStateTreiTo = new AccountStateTrei
                    {
                        Key = acctStateTreiAudit.Address,
                        Nonce = 0,
                        Balance = acctStateTreiAudit.NewValue,
                        StateRoot = acctStateTreiAudit.StateRoot
                    };
                    LogUtility.Log($"Balance Audit Record Corrected for: {acctStateTreiAudit.Address} - Record was inserted", "StateAuditUtility.AuditAccountStateTrei()");
                    accountStateTrei.InsertSafe(acctStateTreiTo);
                }
                else
                {
                    //audit it
                    if(acctSTRec.Balance != acctStateTreiAudit.NewValue)
                    {
                        //fix it.
                        var oldBalance = acctSTRec.Balance;
                        acctSTRec.Balance = acctStateTreiAudit.NewValue;
                        acctSTRec.Nonce = acctStateTreiAudit.NextNonce;
                        acctSTRec.StateRoot = acctStateTreiAudit.StateRoot;

                        LogUtility.Log($"Balance Audit Record Corrected for: {acctStateTreiAudit.Address} - Old Balance: {oldBalance}, New Balance {acctStateTreiAudit.NewValue}", "StateAuditUtility.AuditAccountStateTrei()");
                        accountStateTrei.UpdateSafe(acctSTRec);
                    }
                }
            }
        }
            
    }
}
