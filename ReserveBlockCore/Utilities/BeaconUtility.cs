using Newtonsoft.Json;
using ReserveBlockCore.Beacon;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;

namespace ReserveBlockCore.Utilities
{
    public class BeaconUtility
    {
        public static async Task<bool> EstablishBeaconConnection(bool upload = false, bool download = false)
        {
            var selfBeacon = Globals.SelfBeacon;
            if (selfBeacon?.SelfBeaconActive == true)
            {
                var url = "http://" + selfBeacon.IPAddress + ":" + Globals.Port + "/beacon";
                var result = await P2PClient.ConnectBeacon_New(url, selfBeacon, upload ? "y" : "n", download ? "y" : "n");
                if(result)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                var pubBeaconList = Globals.Beacons.Values.Where(x => !x.SelfBeacon).ToList();
                bool conResult = false;
                while(true)
                {
                    if (pubBeaconList.Count() == 0)
                        break;

                    var random = new Random().Next(pubBeaconList.Count);
                    var pubBeacon = pubBeaconList[random];
                    var url = "http://" + pubBeacon.IPAddress + ":" + Globals.Port + "/beacon";
                    var result = await P2PClient.ConnectBeacon_New(url, pubBeacon, upload ? "y" : "n", download ? "y" : "n");
                    if(!result)
                    {
                        pubBeaconList.Remove(pubBeacon);
                    }
                    else
                    {
                        conResult = true;
                        break;
                    }
                }

                return conResult;
            }
        }
        public static async Task<bool> SendAssets(string scUID, string assetName, string locator)
        {
            bool retry = true;
            int retryCount = 0;
            bool result = false;
            while(retry)
            {
                try
                {
                    if(retryCount < 4)
                    {
                        var filePath = NFTAssetFileUtility.NFTAssetPath(assetName, scUID);
                        var beaconString = locator.ToStringFromBase64();
                        var beacon = JsonConvert.DeserializeObject<BeaconInfo.BeaconInfoJson>(beaconString);

                        NFTLogUtility.Log($"Beginning send on: {assetName}.", "BeaconProcessor.ProcessData() - send");
                        BeaconResponse rsp = BeaconClient.Send(filePath, beacon.IPAddress, beacon.Port);
                        if (rsp.Status == 1)
                        {
                            //success
                            retry = false;
                            var aqDb = AssetQueue.GetAssetQueue();
                            if (aqDb != null)
                            {
                                var aq = aqDb.FindOne(x => x.SmartContractUID == scUID);
                                if (aq != null)
                                {
                                    aq.IsComplete = true;
                                    aq.IsDownloaded = true;

                                    aqDb.UpdateSafe(aq);
                                }
                            }
                            NFTLogUtility.Log($"Success sending asset: {assetName}. Description: {rsp.Description}", "BeaconProcessor.ProcessData() - send");

                            await P2PClient.BeaconFileIsReady(scUID, assetName, locator);

                            result = true;
                        }
                        else if(rsp.Status == 777)
                        {
                            retry = false;
                            var aqDb = AssetQueue.GetAssetQueue();
                            if (aqDb != null)
                            {
                                var aq = aqDb.FindOne(x => x.SmartContractUID == scUID);
                                if (aq != null)
                                {
                                    aq.IsComplete = true;
                                    aq.IsDownloaded = true;

                                    aqDb.UpdateSafe(aq);
                                }
                            }
                            NFTLogUtility.Log($"Asset already existed: {assetName}. Description: {rsp.Description}", "BeaconProcessor.ProcessData() - send");

                            await P2PClient.BeaconFileIsReady(scUID, assetName, locator);

                            result = true;
                        }
                        else
                        {
                            retryCount += 1;
                            NFTLogUtility.Log($"NFT Send for assets -> {assetName} <- failed. Status Code: {rsp.Status}. Status Message: {rsp.Description}", "BeaconProcessor.ProcessData() - send");
                        }
                    }
                    else
                    {
                        retry = false;
                    }
                }
                catch (Exception ex)
                {
                    retryCount += 1;
                    NFTLogUtility.Log($"NFT Send for assets failed. Unknown Error {ex.ToString()}.", "BeaconProcessor.ProcessData() - send");
                }
            }

            return result;            
        }
    }
}
