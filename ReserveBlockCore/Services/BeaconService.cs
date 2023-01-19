using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Services
{
    public class BeaconService
    {
        static SemaphoreSlim BeaconServiceLock = new SemaphoreSlim(1, 1);

        public static async Task BeaconRunService()
        {
            var selfBeacon = Globals.SelfBeacon;
            if(selfBeacon?.SelfBeaconActive == true)
            {
                while (true)
                {
                    var delay = Task.Delay(60000 * 30); // runs every 30 mins
                    if (Globals.StopAllTimers && !Globals.IsChainSynced)
                    {
                        await delay;
                        continue;
                    }

                    await BeaconServiceLock.WaitAsync();
                    try
                    {
                        DeleteCacheFileRun();
                        DeleteStaleBeaconDataRun();
                        DeleteCompletedBeaconDataRun();
                    }
                    finally
                    {
                        BeaconServiceLock.Release();
                    }

                    await delay;
                }
            }
        }

        public static void DeleteCacheFileRun()
        {
            var currentTime = TimeUtil.GetTime();
            var beaconDataDb = BeaconData.GetBeacon();
            var beaconDataList = BeaconData.GetBeaconData();
            if(beaconDataList?.Count() > 0 )
            {
                var beaconDataListFiltered = beaconDataList.Where(x => x.IsReady && !x.IsDownloaded && x.AssetExpireDate <= currentTime).ToList();
                if(beaconDataListFiltered?.Count() > 0 )
                {
                    foreach (var beaconData in beaconDataListFiltered)
                    {
                        //delete file
                        DeleteFile(beaconData.AssetName);
                        if(beaconDataDb != null )
                        {
                            //delete beacon reference
                            beaconDataDb.DeleteSafe(beaconData.Id);
                        }
                    }
                }
            }
        }

        public static void DeleteStaleBeaconDataRun()
        {
            var currentTime = TimeUtil.GetTime();
            var beaconDataDb = BeaconData.GetBeacon();
            var beaconDataList = BeaconData.GetBeaconData();
            if (beaconDataList?.Count() > 0)
            {
                var beaconDataListFiltered = beaconDataList.Where(x => !x.IsReady && !x.IsDownloaded && x.AssetExpireDate <= currentTime).ToList();
                if (beaconDataListFiltered?.Count() > 0)
                {
                    foreach (var beaconData in beaconDataListFiltered)
                    {
                        //delete file
                        DeleteFile(beaconData.AssetName);
                        if (beaconDataDb != null)
                        {
                            //delete beacon reference
                            beaconDataDb.DeleteSafe(beaconData.Id);
                        }
                    }
                }
            }
        }

        public static void DeleteCompletedBeaconDataRun()
        {
            var currentTime = TimeUtil.GetTime();
            var beaconDataDb = BeaconData.GetBeacon();
            var beaconDataList = BeaconData.GetBeaconData();
            if (beaconDataList?.Count() > 0)
            {
                var beaconDataListFiltered = beaconDataList.Where(x => x.IsReady && x.IsDownloaded && x.AssetExpireDate <= currentTime).ToList();
                if (beaconDataListFiltered?.Count() > 0)
                {
                    foreach (var beaconData in beaconDataListFiltered)
                    {
                        //delete file
                        DeleteFile(beaconData.AssetName);
                        if (beaconDataDb != null)
                        {
                            //delete beacon reference
                            beaconDataDb.DeleteSafe(beaconData.Id);
                        }
                    }
                }
            }

        }

        //(is authorizard to use beacon, auto delete files after download)
        public static async Task<(bool, bool)> BeaconAuthorization(string address)
        {
            var selfBeacon = Globals.SelfBeacon;
            if(selfBeacon?.SelfBeaconActive == true)
            {
                if(selfBeacon.IsPrivateBeacon)
                {
                    var localAddress = AccountData.GetSingleAccount(address);
                    if (localAddress != null)
                        return selfBeacon.AutoDeleteAfterDownload ? (true, true) : (true,false);
                }
                else
                {
                    return selfBeacon.AutoDeleteAfterDownload ? (true, true) : (true, false); ;
                }
            }
            return (false, false);
        }

        public static void DeleteFile(string assetName)
        {
            var deleteFrom = GetPathUtility.GetBeaconPath();

            try
            {
                var fileExist = File.Exists(deleteFrom + assetName);
                if(fileExist)
                    File.Delete(deleteFrom + assetName);
            }
            catch { }
        }
    }
}
