using ReserveBlockCore.Data;
using ReserveBlockCore.Utilities;
using System.Xml;

namespace ReserveBlockCore.Bitcoin.Models
{
    public class TokenizedWithdrawals
    {
        public string RequestorAddress { get; set; }
        public long OriginalRequestTime { get;set; }
        public string OriginalSignature { get; set; }
        public string OriginalUniqueId { get; set; }    
        public long Timestamp { get; set; }
        public string SmartContractUID { get; set; }
        public decimal Amount { get; set; }
        public WithdrawalRequestType WithdrawalRequestType { get; set; }
        public string TransactionHash { get; set; }
        public string ArbiterUniqueId { get; set; }
        public bool IsCompleted { get; set; }

        #region Get DB
        public static LiteDB.ILiteCollection<TokenizedWithdrawals>? GetTWDb()
        {
            try
            {
                var tw = DbContext.DB_TokenizedWithdrawals.GetCollection<TokenizedWithdrawals>(DbContext.RSRV_TOKENIZED_WITHDRAWALS);
                return tw;
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.ToString(), "TokenizedWithdrawals.GetTWDb()");
                return null;
            }

        }

        #endregion

        #region Get TokenizedWithdrawal Records
        public static TokenizedWithdrawals? GetTokenizedRecord(string address, string uniqueId, string scUID)
        {
            var twDb = GetTWDb();
            if (twDb == null)
            {
                ErrorLogUtility.LogError("GetTWDb() returned a null value.", "TokenizedWithdrawals.GetTokenizedRecord()");
            }
            else
            {
                var tw = twDb.Query().Where(x => x.RequestorAddress == address && x.OriginalUniqueId == uniqueId && x.SmartContractUID == scUID).FirstOrDefault();
                if (tw != null)
                {
                    return tw;
                }
                else
                {
                    return null;
                }
            }

            return null;

        }
        #endregion

        #region Get Incomplete Request Check
        public static bool? IncompleteRequestCheck(string address, string scUID)
        {
            var twDb = GetTWDb();
            if (twDb == null)
            {
                ErrorLogUtility.LogError("GetTWDb() returned a null value.", "TokenizedWithdrawals.GetTokenizedRecord()");
            }
            else
            {
                var tw = twDb.Query().Where(x => x.RequestorAddress == address && x.SmartContractUID == scUID && !x.IsCompleted).ToList();
                if (tw.Any())
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            return false;

        }
        #endregion

        #region Save Tokenized Withdrawals
        public static bool SaveTokenizedWithdrawals(TokenizedWithdrawals tw, bool update = false)
        {
            var twDb = GetTWDb();
            if (twDb == null)
            {
                ErrorLogUtility.LogError("GetTWDb() returned a null value.", "TokenizedWithdrawals.GetBitcoin()");
            }
            else
            {
                var twRec = twDb.FindOne(x => x.OriginalUniqueId == tw.OriginalUniqueId);
                if (twRec != null)
                {
                    if(!update)
                        return false;

                    twRec.WithdrawalRequestType = tw.WithdrawalRequestType;
                    twRec.TransactionHash = tw.TransactionHash;
                    twRec.IsCompleted = tw.IsCompleted;

                    twDb.UpdateSafe(twRec);
                    return true;
                }
                else
                {
                    twDb.InsertSafe(tw);
                    return true;
                }
            }

            return false;

        }
        #endregion

        #region Complete Tokenized Withdrawals
        public static bool CompleteTokenizedWithdrawals(string address, string uniqueId, string scUID, string txHash)
        {
            var tw = GetTokenizedRecord(address, uniqueId, scUID);

            if (tw == null)
                return false;

            tw.WithdrawalRequestType = WithdrawalRequestType.Owner;
            tw.TransactionHash = txHash;
            tw.IsCompleted = true;

            var result = SaveTokenizedWithdrawals(tw, true);
            if(result)
                return true;

            return false;
        }
        #endregion
    }

    public enum WithdrawalRequestType
    {
        Arbiter,
        Owner
    }
}
