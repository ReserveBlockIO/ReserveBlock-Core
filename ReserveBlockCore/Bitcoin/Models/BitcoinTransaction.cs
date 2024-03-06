using LiteDB;
using ReserveBlockCore.Data;
using ReserveBlockCore.Utilities;
using System.ComponentModel.DataAnnotations;

namespace ReserveBlockCore.Bitcoin.Models
{
    public class BitcoinTransaction
    {
        #region Variables
        public ObjectId Id { get; set; }
        public string Hash { get; set; }
        public string ToAddress { get; set; }
        public string FromAddress { get; set; }
        public decimal Amount { get; set; }
        public decimal Fee { get; set; }
        public long Timestamp { get; set; }
        public string Signature { get; set; }
        public BTCTransactionType TransactionType { get; set; }
        public long FeeRate { get; set; }

        #endregion

        #region BitcoinTransaction DB
        public static LiteDB.ILiteCollection<BitcoinTransaction>? GetBitcoinTX()
        {
            try
            {
                var bitcoin = DbContext.DB_Bitcoin.GetCollection<BitcoinTransaction>(DbContext.RSRV_BITCOIN_TXS);
                return bitcoin;
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.ToString(), "BitcoinTransaction.GetBitcoinTX()");
                return null;
            }

        }

        #endregion

        #region Save Bitcoin TX
        public static bool SaveBitcoinTX(BitcoinTransaction btcUTXO)
        {
            var bitcoin = GetBitcoinTX();
            if (bitcoin == null)
            {
                ErrorLogUtility.LogError("GetBitcoinTX() returned a null value.", "BitcoinTransaction.GetBitcoinTX()");
            }
            else
            {
                var utxo = bitcoin.FindOne(x => x.Hash == btcUTXO.Hash && x.FromAddress == btcUTXO.FromAddress);
                if (utxo != null)
                {
                    return false;
                }
                else
                {
                    bitcoin.InsertSafe(btcUTXO);
                    return true;
                }
            }

            return false;

        }
        #endregion

        #region Get Bitcoin Address TX List
        public static List<BitcoinTransaction> GetTXs(string address)
        {
            List<BitcoinTransaction> txList = new List<BitcoinTransaction>();
            var bitcoin = GetBitcoinTX();
            if (bitcoin == null)
            {
                ErrorLogUtility.LogError("GetTXs() returned a null value.", "BitcoinTransaction.GetTXs()");
            }
            else
            {
                var tx = bitcoin.Find(x => x.FromAddress == address);
                if (tx.Any())
                {
                    txList = tx.ToList();
                    return txList;
                }
                else
                {
                    return txList;
                }
            }

            return txList;

        }
        #endregion
    }

    #region Enum
    public enum BTCTransactionType
    {
        Send,
        Receive
    }
    #endregion
}
