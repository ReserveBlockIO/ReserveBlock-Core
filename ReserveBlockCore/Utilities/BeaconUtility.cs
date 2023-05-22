using Newtonsoft.Json;
using ReserveBlockCore.Beacon;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using System;
using System.Transactions;

namespace ReserveBlockCore.Utilities
{
    public class BeaconUtility
    {
        public static async Task<bool> EstablishBeaconConnection(bool upload = false, bool download = false)
        {
            var userInputedBeaconsUsed = false;
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
                    ErrorLogUtility.LogError("There was an issue with your self hosted beacon. Please ensure all ports are open and the beacon is properly configured.", "BeaconUtility.EstablishBeaconConnection()");
                    return false;
                }
            }
            else if(Globals.Beacons.Values.Where(x => x.DefaultBeacon == false && !x.SelfBeacon).Any())
            {
                var nonDefaultBeacon = Globals.Beacons.Values.Where(x => x.DefaultBeacon == false && !x.SelfBeacon).ToList();
                nonDefaultBeacon.Shuffle();
                var cResult = false;
                foreach(var beacon in nonDefaultBeacon)
                {
                    var pubBeacon = beacon;
                    var url = "http://" + pubBeacon.IPAddress + ":" + Globals.Port + "/beacon";
                    var result = await P2PClient.ConnectBeacon_New(url, pubBeacon, upload ? "y" : "n", download ? "y" : "n");
                    if (result)
                    {
                        cResult = true;
                        break;
                    }
                }
                if(cResult)
                {
                    userInputedBeaconsUsed = true;
                    return true;
                }
            }

            //User provided beacons failed. Attempting default ones.
            if(!userInputedBeaconsUsed)
            {
                var pubBeaconList = Globals.Beacons.Values.Where(x => !x.SelfBeacon && x.DefaultBeacon != false && x.Region != 0).OrderBy(x => x.Region).ToList();
                pubBeaconList.Shuffle();
                bool conResult = false;
                foreach(var beacon in pubBeaconList)
                {
                    var pubBeacon = beacon;
                    var url = "http://" + pubBeacon.IPAddress + ":" + Globals.Port + "/beacon";
                    var result = await P2PClient.ConnectBeacon_New(url, pubBeacon, upload ? "y" : "n", download ? "y" : "n");
                    if (result)
                    {
                        conResult = true;
                        break;
                    }
                }

                return conResult;
            }

            ErrorLogUtility.LogError("Failed to connect to every beacon stored in wallet. Please ensure you are not blocking outside connections to 3338, 13338, 23338, or 33338.", "BeaconUtility.EstablishBeaconConnection()");
            return false; //something failed if we reach here. This means ZERO connections were made to beacon
        }

        public static async Task SendAssets(string scUID, List<string> assets, BeaconNodeInfo connectedBeacon)
        {
            if (assets.Count() > 0)
            {
                NFTLogUtility.Log($"NFT Asset Transfer Beginning for: {scUID}. Assets: {assets}", "SCV1Controller.TransferNFT()");
                foreach (var asset in assets)
                {
                    await SendAssets(scUID, asset, connectedBeacon.Beacons.BeaconLocator);
                }

                connectedBeacon.Uploading = false;
                Globals.Beacon[connectedBeacon.IPAddress] = connectedBeacon;

                NFTLogUtility.Log($"NFT Asset Transfer Done for: {scUID}.", "SCV1Controller.TransferNFT()");
            }
        }

        public static async Task SendAssets_New(string scUID, List<string> assets, BeaconNodeInfo connectedBeacon)
        {
            if (assets.Count() > 0)
            {
                NFTLogUtility.Log($"NFT Asset Transfer Beginning for: {scUID}. Assets: {assets}", "SCV1Controller.TransferNFT()");
                foreach (var asset in assets)
                {
                    await SendAssets_New(scUID, asset, connectedBeacon.Beacons.BeaconLocator);
                }

                connectedBeacon.Uploading = false;
                Globals.Beacon[connectedBeacon.IPAddress] = connectedBeacon;

                NFTLogUtility.Log($"NFT Asset Transfer Done for: {scUID}.", "SCV1Controller.TransferNFT()");
            }
        }

        public static async Task<bool> SendAssets_New(string scUID, string assetName, string locator)
        {
            bool retry = true;
            int retryCount = 0;
            bool result = false;
            while(retry)
            {
                try
                {
                    if (retryCount < 4)
                    {
                        var filePath = NFTAssetFileUtility.NFTAssetPath(assetName, scUID);
                        var beaconString = locator.ToStringFromBase64();
                        var beacon = JsonConvert.DeserializeObject<BeaconInfo.BeaconInfoJson>(beaconString);

                        if(beacon != null)
                        {
                            NFTLogUtility.Log($"Beginning send on: {assetName}.", "BeaconProcessor.ProcessData() - send");
                            BeaconResponse rsp = await BeaconClient.Send_New(filePath, beacon.IPAddress, beacon.Port, scUID);
                            if (rsp.Status == 1)
                            {
                                //success
                                retry = false;
                                var aqDb = AssetQueue.GetAssetQueue();
                                if (aqDb != null)
                                {
                                    var aq = aqDb.FindOne(x => x.SmartContractUID == scUID && !x.IsComplete);
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
                            else
                            {
                                retryCount += 1;
                                NFTLogUtility.Log($"NFT Send for assets -> {assetName} <- failed. Status Code: {rsp.Status}. Status Message: {rsp.Description}", "BeaconProcessor.ProcessData() - send");
                            }
                        }
                        else
                        {
                            retryCount += 1;
                        }
                    }
                }
                catch(Exception ex)
                {
                    retryCount += 1;
                    NFTLogUtility.Log($"NFT Send for assets failed. Unknown Error {ex.ToString()}.", "BeaconProcessor.ProcessData() - send");
                }
            }

            return result;
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
                                var aq = aqDb.FindOne(x => x.SmartContractUID == scUID && !x.IsComplete);
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
                                var aq = aqDb.FindOne(x => x.SmartContractUID == scUID && !x.IsComplete);
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
