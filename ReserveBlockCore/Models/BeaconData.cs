using ReserveBlockCore.Extensions;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Models
{
    public class BeaconData
    {
        public int Id { get; set; }
        public string SmartContractUID { get; set; }
        public string AssetName { get; set; }
        public string IPAdress { get; set; }
        public string DownloadIPAddress { get; set; }
        public long AssetReceiveDate { get; set; }
        public long AssetExpireDate { get; set; }
        public string NextAssetOwnerAddress { get; set; }

        public class BeaconSendData
        {
            public string SmartContractUID { get; set; }
            public List<string> Assets { get; set; }
            public string Signature { get; set; }
            public string NextAssetOwnerAddress { get; set; }
        }

        public class BeaconDownloadData
        {
            public string SmartContractUID { get; set; }
            [JsonIgnore]
            public List<string> Assets { get; set; }
            public string Signature { get; set; }
        }

        public static LiteDB.ILiteCollection<BeaconData>? GetBeacon()
        {
            try
            {
                var beacon = DbContext.DB_Beacon.GetCollection<BeaconData>(DbContext.RSRV_BEACON_DATA);
                return beacon;
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.Message, "BeaconData.GetBeacon()");
                return null;
            }

        }

        public static List<BeaconData>? GetBeaconData()
        {
            try
            {
                var beacon = GetBeacon();

                var beaconInfo = beacon.FindAll().ToList();
                if (beaconInfo.Count() == 0)
                {
                    return null;
                }
                return beaconInfo;
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.Message, "BeaconData.GetBeaconData()");
                return null;
            }

        }

        public static string SaveBeaconData(BeaconData beaconData)
        {
            var beacon = GetBeacon();
            if (beacon == null)
            {
                ErrorLogUtility.LogError("GetBeacon() returned a null value.", "BeaconData.SaveBeaconInfo()");
            }
            else
            {
                var beaconDataRec = beacon.FindOne(x => x.AssetName == beaconData.AssetName);
                if(beaconDataRec != null)
                {
                    return "Record Already Exist";
                }
                else
                {
                    beacon.InsertSafe(beaconData);
                }
            }

            return "Error Saving Beacon Data";

        }

        public static void DeleteAssets(string txHash)
        {
            var beacon = GetBeacon();
            if (beacon == null)
            {
                ErrorLogUtility.LogError("GetBeacon() returned a null value.", "BeaconData.SaveBeaconInfo()");
            }
            else
            {
                //beacon.DeleteManySafe(x => x.TxHash == txHash);
            }
        }


    }
}
