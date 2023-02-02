using ReserveBlockCore.Data;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Models
{
    public class BadTransaction
    {
        #region Variables

        public int Id { get; set; }
        public string Hash { get; set; }
        public string FromAddress { get; set; } 
        public TransactionType TransactionType { get; set; }

        #endregion

        #region Get Bad Transactions DB
        public static LiteDB.ILiteCollection<BadTransaction>? GetBadTransactions()
        {
            try
            {
                var badTXDB = DbContext.DB_WorldStateTrei.GetCollection<BadTransaction>(DbContext.RSRV_BAD_TX);
                return badTXDB;
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.ToString(), "BadTransaction.GetBadTransactions()");
                return null;
            }

        }

        #endregion

        #region Save Bad Transaction 
        public static bool SaveBadTransaction(BadTransaction badTX)
        {
            var badTxDb = GetBadTransactions();
            if (badTxDb == null)
            {
                ErrorLogUtility.LogError("GetBadTransactions() returned a null value.", "BadTransaction.SaveBadTransaction()");
            }
            else
            {
                var badTxRecData = badTxDb.FindOne(x => x.Hash == badTX.Hash );
                if (badTxRecData != null)
                {
                    return false;
                }
                else
                {
                    badTxDb.InsertSafe(badTX);
                    UpdateTXList(badTX);
                    return true;
                }
            }

            return false;

        }
        #endregion

        #region Delete Bad TX
        public static bool DeleteBadTransaction(string hash)
        {
            var badTxDb = GetBadTransactions();
            if (badTxDb == null)
            {
                ErrorLogUtility.LogError("GetBadTransactions() returned a null value.", "BadTransaction.DeleteAdnr()");
                return false;
            }
            else
            {
                badTxDb.DeleteManySafe(x => x.Hash == hash);
                return true;
            }
        }
        #endregion

        #region Update in memory bad TX list
        public static void UpdateTXList(BadTransaction badTX, bool remove = false)
        {
            switch(badTX.TransactionType)
            {
                case TransactionType.ADNR:
                    if(!remove)
                        Globals.BadADNRTxList.Add(badTX.Hash);
                    else
                        Globals.BadADNRTxList.Remove(badTX.Hash);
                    break;
                case TransactionType.DSTR:
                    if (!remove)
                        Globals.BadDSTList.Add(badTX.Hash);
                    else
                        Globals.BadDSTList.Remove(badTX.Hash);
                    break;
                case TransactionType.NFT_BURN:
                    if (!remove)
                        Globals.BadNFTTxList.Add(badTX.Hash);
                    else
                        Globals.BadNFTTxList.Remove(badTX.Hash);
                    break;
                case TransactionType.NFT_MINT:
                    if (!remove)
                        Globals.BadNFTTxList.Add(badTX.Hash);
                    else
                        Globals.BadNFTTxList.Remove(badTX.Hash);
                    break;
                case TransactionType.NFT_SALE:
                    if (!remove)
                        Globals.BadNFTTxList.Add(badTX.Hash);
                    else
                        Globals.BadNFTTxList.Remove(badTX.Hash);
                    break;
                case TransactionType.NFT_TX:
                    if (!remove)
                        Globals.BadNFTTxList.Add(badTX.Hash);
                    else
                        Globals.BadNFTTxList.Remove(badTX.Hash);
                    break;
                case TransactionType.NODE:
                    if (!remove)
                        Globals.BadNodeList.Add(badTX.Hash);
                    else
                        Globals.BadNodeList.Remove(badTX.Hash);
                    break;
                case TransactionType.TX:
                    if (!remove)
                        Globals.BadTxList.Add(badTX.Hash);
                    else
                        Globals.BadTxList.Remove(badTX.Hash);
                    break;
                case TransactionType.VOTE:
                    if (!remove)
                        Globals.BadVoteTxList.Add(badTX.Hash);
                    else
                        Globals.BadVoteTxList.Remove(badTX.Hash);
                    break;
                case TransactionType.VOTE_TOPIC:
                    if (!remove)
                        Globals.BadTopicTxList.Add(badTX.Hash);
                    else
                        Globals.BadTopicTxList.Remove(badTX.Hash);
                    break;
                default:
                    break;
            }
        }

        #endregion

        public static async Task PopulateBadTXList()
        {
            var badTxList = GetBadTransactions().Query().Where(x => true).ToList();
            if(badTxList.Count > 0)
            {
                foreach (var badTX in badTxList)
                {
                    switch (badTX.TransactionType)
                    {
                        case TransactionType.ADNR:
                            Globals.BadADNRTxList.Add(badTX.Hash);
                            break;
                        case TransactionType.DSTR:
                            Globals.BadDSTList.Add(badTX.Hash);
                            break;
                        case TransactionType.NFT_BURN:
                            Globals.BadNFTTxList.Add(badTX.Hash);
                            break;
                        case TransactionType.NFT_MINT:
                            Globals.BadNFTTxList.Add(badTX.Hash);
                            break;
                        case TransactionType.NFT_SALE:
                            Globals.BadNFTTxList.Add(badTX.Hash);
                            break;
                        case TransactionType.NFT_TX:
                            Globals.BadNFTTxList.Add(badTX.Hash);
                            break;
                        case TransactionType.NODE:
                            Globals.BadNodeList.Add(badTX.Hash);
                            break;
                        case TransactionType.TX:
                            Globals.BadTxList.Add(badTX.Hash);
                            break;
                        case TransactionType.VOTE:
                            Globals.BadVoteTxList.Add(badTX.Hash);
                            break;
                        case TransactionType.VOTE_TOPIC:
                            Globals.BadTopicTxList.Add(badTX.Hash);
                            break;
                        default:
                            break;
                    }
                }
            }
            
        }
    }
}
