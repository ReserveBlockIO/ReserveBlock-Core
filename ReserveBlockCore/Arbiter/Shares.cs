using ReserveBlockCore.Bitcoin.Models;
using ReserveBlockCore.Data;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Arbiter
{
    public class Shares
    {
        #region Variables

        public string SCUID { get; set; }
        public string Share {  get; set; }
        public bool IsEncrypted { get; set; }

        #endregion

        #region Get Shares Db
        public static LiteDB.ILiteCollection<Shares>? GetSharesDb()
        {
            try
            {
                var shares = DbContext.DB_Shares.GetCollection<Shares>(DbContext.RSRV_SHARES);
                return shares;
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.ToString(), "Shares.GetSharesDb()");
                return null;
            }

        }

        #endregion

        #region GetShare(string scUID)
        public static string? GetShare(string scUID)
        {
            bool result = false;
            string strResult = "";

            var shares = GetSharesDb();
            var sharesExist = shares.FindOne(x => x.SCUID == scUID);
            if (sharesExist != null)
            {
                return sharesExist.Share.ToString();
            }

            return null;
        }
        #endregion

        #region SaveShare(Shares share)
        public static bool SaveShare(Shares share)
        {
            var shares = GetSharesDb();
            if (shares == null)
            {
                ErrorLogUtility.LogError("GetSharesDb() returned a null value.", "Shares.GetSharesDb() -CALLFROM: SaveShare()");
            }
            else
            {
                var shareData = shares.FindOne(x => x.SCUID == share.SCUID);
                if (shareData != null)
                {
                    return false;
                }
                else
                {
                    shares.InsertSafe(share);
                    return true;
                }
            }

            return false;
        }
        #endregion

        #region DeleteShare(string scUID)
        public static void DeleteShare(string scUID)
        {
            var shares = GetSharesDb();
            if (shares == null)
            {
                ErrorLogUtility.LogError("GetSharesDb() returned a null value.", "Shares.GetSharesDb() -CALLFROM: DeleteShare()");
            }
            else
            {
                shares.DeleteManySafe(x => x.SCUID == scUID);
            }
        }
        #endregion
    }
}
