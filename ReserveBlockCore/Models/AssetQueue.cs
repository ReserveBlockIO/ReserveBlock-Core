using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Extensions;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Models
{
    public class AssetQueue
    {
        public int Id { get; set; }
        public string SmartContractUID { get; set; }
        public string FromAddress { get; set; }
        public string ToAddress { get; set; }
        public string Locator { get; set; }
        public string MD5List { get; set; }
        public string MediaListJson { get; set; }
        public int ManualCheckCount { get; set; }
        public DateTime SubmitDate { get; set; }
        public DateTime LastAttempt { get; set; }
        public DateTime? NextAttempt { get; set; }
        public TransferType AssetTransferType { get; set; }
        public bool IsComplete { get; set; }
        
        public enum TransferType
        {
            Download,
            Upload
        }

        #region Get Asset Queue
        public static LiteDB.ILiteCollection<AssetQueue>? GetAssetQueue()
        {
            try
            {
                var aq = DbContext.DB_AssetQueue.GetCollection<AssetQueue>(DbContext.RSRV_ASSET_QUEUE);
                return aq;
            }
            catch (Exception ex)
            {
                DbContext.Rollback();
                ErrorLogUtility.LogError(ex.Message, "AssetQueue.GetAssetQueue()");
                return null;
            }

        }
        #endregion

        #region Save Asset Queue Item
        public static bool CreateAssetQueueItem(string scUID, string toAddress, string locator, string md5List, List<string> mediaList, TransferType assetTransferType)
        {
            var aqDB = GetAssetQueue();
            if (aqDB == null)
            {
                ErrorLogUtility.LogError("GetAssetQueue() returned a null value.", "AssetQueue.SaveAssetQueueItem()");
            }
            else
            {
                try
                {
                    AssetQueue aqItem = new AssetQueue
                    {
                        SmartContractUID = scUID,
                        AssetTransferType = assetTransferType,
                        FromAddress = "",
                        ToAddress = toAddress,
                        Locator = locator,
                        MD5List = md5List,
                        MediaListJson = JsonConvert.SerializeObject(mediaList),
                        IsComplete = false,
                        SubmitDate = DateTime.UtcNow,
                        ManualCheckCount = 0,
                        LastAttempt = DateTime.UtcNow,
                        NextAttempt = null
                    };

                    var result = SaveAssetQueueItem(aqItem);

                    return result;
                    
                }
                catch(Exception ex)
                {
                    DbContext.Rollback();
                    ErrorLogUtility.LogError($"Failed to create asset queue item. Error:  {ex.Message}", "AssetQueue.CreateAssetQueueItem()");
                }
                
            }

            return false;
        }
        #endregion

        #region Save Asset Queue Item
        public static bool SaveAssetQueueItem(AssetQueue aq)
        {
            var aqDB = GetAssetQueue();
            if (aqDB == null)
            {
                ErrorLogUtility.LogError("GetAssetQueue() returned a null value.", "AssetQueue.SaveAssetQueueItem()");
            }
            else
            {
                var acRec = aqDB.FindOne(x => x.SmartContractUID == aq.SmartContractUID);
                if (acRec != null)
                {
                    return false;
                }
                else
                {
                    try
                    {
                        aqDB.InsertSafe(aq);
                        return true;
                    }
                    catch(Exception ex)
                    {
                        ErrorLogUtility.LogError("Failed to saved AQ Item.", "AssetQueue.SaveAssetQueueItem()");
                    }
                    
                }
            }

            return false;
        }
        #endregion

        #region Delete Asset Queue
        public static void DeleteAssetQueueItem(AssetQueue aqa)
        {
            var aq = GetAssetQueue();
            if (aq == null)
            {
                ErrorLogUtility.LogError("GetAdnr() returned a null value.", "Adnr.GetAdnr()");
            }
            else
            {
                try
                {
                    aq.DeleteSafe(aqa.Id);
                }
                catch (Exception ex)
                {
                    DbContext.Rollback();
                    ErrorLogUtility.LogError(ex.Message, "AssetQueue.DeleteAssetQueue()");
                }
            }
        }
        #endregion
    }
}
