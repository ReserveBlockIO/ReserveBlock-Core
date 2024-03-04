using ReserveBlockCore.Data;
using ReserveBlockCore.Utilities;
using System.Net;

namespace ReserveBlockCore.Bitcoin.Models
{
    public class BitcoinUTXO
    {
        #region Variables
        public long Id { get; set; }
        public string Address { get; set; }
        public string TxId { get; set; }
        public long Value { get; set; }
        public int Vout { get; set; }
        public bool IsUsed { get; set; }

        #endregion

        #region GetBitcoinUTXO DB
        public static LiteDB.ILiteCollection<BitcoinUTXO>? GetBitcoinUTXO()
        {
            try
            {
                var bitcoin = DbContext.DB_Bitcoin.GetCollection<BitcoinUTXO>(DbContext.RSRV_BITCOIN_UTXO);
                return bitcoin;
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.ToString(), "BitcoinUTXO.GetBitcoin()");
                return null;
            }

        }

        #endregion

        #region Save Bitcoin Address UTXO
        public static bool SaveBitcoinUTXO(BitcoinUTXO btcUTXO, bool unspend = false)
        {
            var bitcoin = GetBitcoinUTXO();
            if (bitcoin == null)
            {
                ErrorLogUtility.LogError("GetBitcoinUTXO() returned a null value.", "BitcoinUTXO.SaveBitcoinUTXO()");
            }
            else
            {
                var utxo = bitcoin.FindOne(x => x.TxId == btcUTXO.TxId && x.Address == btcUTXO.Address);
                if (utxo != null)
                {
                    utxo.Address = btcUTXO.Address;
                    utxo.Value = btcUTXO.Value;
                    utxo.Vout = btcUTXO.Vout;
                    utxo.IsUsed = unspend ? false : true;
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

        #region Get Bitcoin Address Unspent UTXO List
        public static List<BitcoinUTXO> GetUnspetUTXOs(string address)
        {
            List<BitcoinUTXO> utxoList = new List<BitcoinUTXO>();
            var bitcoin = GetBitcoinUTXO();
            if (bitcoin == null)
            {
                ErrorLogUtility.LogError("GetBitcoinUTXO() returned a null value.", "BitcoinUTXO.GetUTXOs()");
            }
            else
            {
                var utxo = bitcoin.Find(x => x.Address == address && x.IsUsed == false);
                if (utxo.Any())
                {
                    utxoList = utxo.ToList();
                    return utxoList;
                }
                else
                {
                    return utxoList;
                }
            }

            return utxoList;

        }
        #endregion

        #region Get Bitcoin Address UTXO List
        public static List<BitcoinUTXO> GetUTXOs(string address)
        {
            List<BitcoinUTXO> utxoList = new List<BitcoinUTXO>();
            var bitcoin = GetBitcoinUTXO();
            if (bitcoin == null)
            {
                ErrorLogUtility.LogError("GetBitcoinUTXO() returned a null value.", "BitcoinUTXO.GetUTXOs()");
            }
            else
            {
                var utxo = bitcoin.Find(x => x.Address == address);
                if (utxo.Any())
                {
                    utxoList = utxo.ToList();
                    return utxoList;
                }
                else
                {
                    return utxoList;
                }
            }

            return utxoList;

        }
        #endregion

        #region Spend Bitcoin UTXO
        public static bool SpendUTXO(string txId, string address)
        {
            var bitcoin = GetBitcoinUTXO();

            if (bitcoin == null)
                return false;

            var utxo = bitcoin.FindOne(x => x.TxId == txId && x.Address == address);

            if (utxo == null)
                return false;

            utxo.IsUsed = true;

            bitcoin.Update(utxo);

            return true;
        }

        #endregion
    }
}
