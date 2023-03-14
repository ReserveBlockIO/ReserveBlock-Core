using LiteDB;
using ReserveBlockCore.Data;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Models
{
    public class ReserveTransactions
    {
        [BsonId]
        public Guid Id { get; set; }
        public Transaction Transaction { get; set; }
        public string Hash { get; set; }
        public long ConfirmTimestamp { get; set; } //will not be valid till after this.
        public string FromAddress { get; set; }
        public string ToAddress { get; set; }

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

        #region Get ReserveTransactions DB
        public static void SaveReserveTx(ReserveTransactions rTx)
        {
            try
            {
                var db = GetReserveTransactionsDb();
                var rec = db.Query().Where(x => x.Hash == rTx.Hash).FirstOrDefault();
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

    }
}
