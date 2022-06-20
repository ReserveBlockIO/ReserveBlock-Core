using LiteDB;
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

        public static ILiteCollection<BeaconInfo>? GetBeacon()
        {
            try
            {
                var beacon = DbContext.DB_Beacon.GetCollection<BeaconInfo>(DbContext.RSRV_BEACON_INFO);
                return beacon;
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.Message, "BeaconInfo.GetBeacon()");
                return null;
            }

        }

        public static BeaconInfo? GetBeaconInfo()
        {
            try
            {
                var beacon = GetBeacon();

                var beaconInfo = beacon.FindAll().FirstOrDefault();
                if (beaconInfo == null)
                {
                    return null;
                }
                return beaconInfo;
            }
            catch (Exception ex)
            {
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
                    beacon.Insert(beaconInfo); //inserts new record
                    return "Inserted";
                }
                else
                {
                    existingBeaconInfo.BeaconLocator = beaconInfo.BeaconLocator;
                    existingBeaconInfo.IsBeaconActive = beaconInfo.IsBeaconActive;
                    existingBeaconInfo.Name = beaconInfo.Name;
                    beacon.Update(existingBeaconInfo); //update existing record
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
                    //no record
                }
                else
                {
                    beaconInfo.IsBeaconActive = !beaconInfo.IsBeaconActive;
                    beacon.Update(beaconInfo);
                }

                return beaconInfo.IsBeaconActive;
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
