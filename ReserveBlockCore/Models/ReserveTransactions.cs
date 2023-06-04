using LiteDB;
using ReserveBlockCore.Data;
using ReserveBlockCore.Utilities;
using System.ComponentModel.DataAnnotations;

namespace ReserveBlockCore.Models
{
    public class ReserveTransactions
    {
        [BsonId]
        public Guid Id { get; set; }
        public string Hash { get; set; }
        public long ConfirmTimestamp { get; set; } //will not be valid till after this.
        public string FromAddress { get; set; }
        public string ToAddress { get; set; }
        public decimal Amount { get; set; }
        public long Nonce { get; set; }
        public decimal Fee { get; set; }
        public long Timestamp { get; set; }
        public string? Data { get; set; } = null;
        public long? UnlockTime { get; set; } = null;

        [StringLength(512)]
        public string Signature { get; set; }
        public long Height { get; set; }
        public TransactionType TransactionType { get; set; }
        public ReserveTransactionStatus ReserveTransactionStatus { get; set; }


        #region Get ReserveTransactions DB
        public static LiteDB.ILiteCollection<ReserveTransactions>? GetReserveTransactionsDb()
        {
            try
            {
                var rTx = DbContext.DB_Reserve.GetCollection<ReserveTransactions>(DbContext.RSRV_RESERVE_TRANSACTIONS);
                return rTx;
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.ToString(), "ReserveTransactions.GetReserveTransactionsDb()");
                return null;
            }

        }

        #endregion

        #region Get ReserveTransactionsCalledBack DB
        public static LiteDB.ILiteCollection<string>? GetReserveTransactionsCalledBackDb()
        {
            try
            {
                var rTx = DbContext.DB_Reserve.GetCollection<string>(DbContext.RSRV_RESERVE_TRANSACTIONS_CALLED_BACK);
                return rTx;
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.ToString(), "ReserveTransactions.GetReserveTransactionsCalledBackDb()");
                return null;
            }

        }

        #endregion

        #region Get ReserveTransactions transaction
        public static ReserveTransactions? GetTransactions(string hash)
        {
            try
            {
                var db = GetReserveTransactionsDb();
                var rec = db.Query().Where(x => x.Hash == hash).FirstOrDefault();
                if (rec != null)
                {
                    return rec;
                }
                return null;
            }
            catch (Exception ex)
            {

            }
            return null;
        }

        #endregion

        #region Get ReserveTransactions transaction list
        public static IEnumerable<ReserveTransactions>? GetTransactionList(string fromAddress)
        {
            try
            {
                var db = GetReserveTransactionsDb();
                var rec = db.Query().Where(x => x.FromAddress == fromAddress).ToEnumerable();
                if (rec != null)
                {
                    return rec;
                }
                return null;
            }
            catch (Exception ex)
            {

            }
            return null;
        }

        #endregion

        #region Save Reserve Transactions
        public static void SaveReserveTx(ReserveTransactions rTx)
        {
            try
            {
                var db = GetReserveTransactionsDb();
                var rec = db.FindOne(x => x.Hash == rTx.Hash);
                if(rec == null)
                {
                    db.InsertSafe(rTx);
                }
            }
            catch (Exception ex)
            {

            }

        }

        #endregion

        #region Get ReserveTransactions transaction called back list
        public static bool GetTransactionsCalledBack(string hash)
        {
            try
            {
                var db = GetReserveTransactionsCalledBackDb();
                var rec = db.Query().Where(x => x == hash).FirstOrDefault();
                if (rec != null)
                {
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        #endregion

        #region Save Reserve Transactions
        public static void SaveReserveTxCallBack(string rTxHash)
        {
            try
            {
                var db = GetReserveTransactionsCalledBackDb();
                var rec = db.FindOne(x => x == rTxHash);
                if (rec == null)
                {
                    db.InsertSafe(rTxHash);
                }
            }
            catch (Exception ex)
            {

            }

        }

        #endregion

    }
    public enum ReserveTransactionStatus
    {
        Pending,
        Confirmed,
        CalledBack,
        Recovered
    }

}
