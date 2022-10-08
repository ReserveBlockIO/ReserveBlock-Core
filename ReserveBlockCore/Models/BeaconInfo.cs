using ReserveBlockCore.Extensions;
using ReserveBlockCore.Data;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Models
{
    public class BeaconInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string BeaconLocator { get; set; }
        public bool IsBeaconActive { get; set; }
        public string BeaconUID { get; set; }

        public static LiteDB.ILiteCollection<BeaconInfo>? GetBeacon()
        {
            try
            {
                var beacon = DbContext.DB_Beacon.GetCollection<BeaconInfo>(DbContext.RSRV_BEACON_INFO);
                return beacon;
            }
            catch (Exception ex)
            {
                DbContext.Rollback();
                ErrorLogUtility.LogError(ex.Message, "BeaconInfo.GetBeacon()");
                return null;
            }
        }

        public static BeaconInfo? GetBeaconInfo()
        {
            try
            {
                var beacon = GetBeacon();

                var beaconInfo = beacon.FindOne(x => true);
                if (beaconInfo == null)
                {
                    return null;
                }
                return beaconInfo;
            }
            catch (Exception ex)
            {
                DbContext.Rollback();
                ErrorLogUtility.LogError(ex.Message, "BeaconInfo.GetBeaconInfo()");
                return null;
            }
        }

        public static string SaveBeaconInfo(BeaconInfo beaconInfo)
        {
            var beacon = GetBeacon();
            if (beacon == null)
            {
                ErrorLogUtility.LogError("GetBeacon() returned a null value.", "BeaconInfo.SaveBeaconInfo()");
            }
            else
            {
                var existingBeaconInfo = beacon.FindAll().FirstOrDefault();
                if (existingBeaconInfo == null)
                {
                    beacon.InsertSafe(beaconInfo); //inserts new record
                    return "Inserted";
                }
                else
                {
                    existingBeaconInfo.Name = beaconInfo.Name;
                    beacon.UpdateSafe(existingBeaconInfo); //update existing record
                    return "Updated";
                }
            }

            return "Error Saving Beacon Info";

        }

        public static bool? SetBeaconActiveState()
        {
            var beacon = GetBeacon();
            if (beacon == null)
            {
                ErrorLogUtility.LogError("GetBeacon() returned a null value.", "BeaconInfo.SaveBeaconInfo()");
            }
            else
            {
                var beaconInfo = beacon.FindAll().FirstOrDefault();
                if (beaconInfo == null)
                {
                    return null;
                }
                else
                {
                    beaconInfo.IsBeaconActive = !beaconInfo.IsBeaconActive;
                    beacon.UpdateSafe(beaconInfo);
                    return beaconInfo.IsBeaconActive;
                }

                
            }

            return null;
        }

        public class BeaconInfoJson
        {
            public string IPAddress { get; set; }
            public int Port { get; set; }
            public string Name { get; set; }
            public string BeaconUID { get; set; }
        }

    }
}
