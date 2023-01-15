using ReserveBlockCore.Data;
using ReserveBlockCore.Utilities;
using System.Runtime.CompilerServices;

namespace ReserveBlockCore.Models
{
    public class Beacons
    {
        public int Id { get; set; }
        public string IPAddress { get; set; }
        public int Port { get; set; }
        public string Name { get; set; }
        public string BeaconUID { get; set; }
        public bool AutoDeleteAfterDownload { get; set; }
        public bool IsPrivateBeacon { get; set; }
        public int FileCachePeriodDays { get; set; }
        public string BeaconLocator { get; set; }
        public bool SelfBeacon { get; set; }
        public bool SelfBeaconActive { get; set; }

        public static LiteDB.ILiteCollection<Beacons>? GetBeacons()
        {
            try
            {
                var beacons = DbContext.DB_Beacon.GetCollection<Beacons>(DbContext.RSRV_BEACONS);
                return beacons;
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.ToString(), "Beacons.GetBeacons()");
                return null;
            }
        }

        public static bool SaveBeacon(Beacons beacon)
        {
            try
            {
                var beacons = GetBeacons();
                if (beacons == null)
                {
                    ErrorLogUtility.LogError("GetBeacon() returned a null value.", "BeaconInfo.SaveBeaconInfo()");
                }
                else
                {
                    var existingBeaconInfo = beacons.Query().Where(x => x.IPAddress == beacon.IPAddress).FirstOrDefault();
                    if (existingBeaconInfo == null)
                    {
                        beacons.InsertSafe(beacon); //inserts new record
                        return true;
                    }
                    else
                    {
                        existingBeaconInfo.Name = beacon.Name;
                        existingBeaconInfo.Port = beacon.Port;
                        existingBeaconInfo.SelfBeacon = beacon.SelfBeacon;
                        existingBeaconInfo.SelfBeaconActive = beacon.SelfBeaconActive;
                        existingBeaconInfo.BeaconLocator = beacon.BeaconLocator;
                        existingBeaconInfo.AutoDeleteAfterDownload = beacon.AutoDeleteAfterDownload;
                        existingBeaconInfo.FileCachePeriodDays = beacon.FileCachePeriodDays;
                        existingBeaconInfo.IsPrivateBeacon = beacon.IsPrivateBeacon;
                        existingBeaconInfo.BeaconUID = beacon.BeaconUID;

                        beacons.UpdateSafe(existingBeaconInfo); //update existing record
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public static bool DeleteBeacon(Beacons beacon)
        {
            var beacons = GetBeacons();
            if (beacons == null)
            {
                ErrorLogUtility.LogError("GetBeacons() returned a null value.", "BeaconInfo.SaveBeaconInfo()");
            }
            else
            {
                var existingBeaconInfo = beacons.Query().Where(x => x.IPAddress == beacon.IPAddress).FirstOrDefault();
                if (existingBeaconInfo != null)
                {
                    beacons.Delete(existingBeaconInfo.Id);
                    return true;
                }
            }

            return false;
        }

        public static bool? SetBeaconActiveState()
        {
            var beacons = GetBeacons();
            if (beacons == null)
            {
                ErrorLogUtility.LogError("GetBeacon() returned a null value.", "BeaconInfo.SaveBeaconInfo()");
            }
            else
            {
                var beaconInfo = beacons.Query().Where(x => x.SelfBeacon == true).FirstOrDefault();
                if (beaconInfo == null)
                {
                    return null;
                }
                else
                {
                    beaconInfo.SelfBeaconActive = !beaconInfo.SelfBeaconActive;
                    beacons.UpdateSafe(beaconInfo);
                    if(Globals.SelfBeacon != null)
                        Globals.SelfBeacon.SelfBeaconActive = beaconInfo.SelfBeaconActive;
                    return beaconInfo.SelfBeaconActive;
                }
            }

            return null;
        }
    }
}
