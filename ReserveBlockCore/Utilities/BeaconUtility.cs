using Newtonsoft.Json;
using ReserveBlockCore.Beacon;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;

namespace ReserveBlockCore.Utilities
{
    public class BeaconUtility
    {
        public static async Task<bool> SendAssets(string scUID, string assetName)
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
                        var beaconString = Globals.Locators.Values.FirstOrDefault().ToStringFromBase64();
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

                            await P2PClient.BeaconFileIsReady(scUID, assetName);

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

                            await P2PClient.BeaconFileIsReady(scUID, assetName);

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
