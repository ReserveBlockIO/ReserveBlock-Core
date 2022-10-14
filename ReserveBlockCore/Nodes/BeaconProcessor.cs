using Newtonsoft.Json;
using ReserveBlockCore.Beacon;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Nodes
{
    public class BeaconProcessor
    {
        public static async Task ProcessData(string message, string data)
        {
            if (message == null || message == "")
            {
                return;
            }
            if (Globals.StopAllTimers == false && Globals.BlocksDownloading != 1) //this will prevent new blocks from coming in if flag. Normally only flagged when syncing chain.
            {
                if (message == "send")
                {
                    try
                    {
                        var payload = JsonConvert.DeserializeObject<string[]>(data);
                        if (payload != null)
                        {
                            var scUID = payload[0];
                            var assetName = payload[1];

                            var filePath = NFTAssetFileUtility.NFTAssetPath(assetName, scUID);
                            var beaconString = Globals.Locators.FirstOrDefault().ToStringFromBase64();
                            var beacon = JsonConvert.DeserializeObject<BeaconInfo.BeaconInfoJson>(beaconString);

                            BeaconResponse rsp = BeaconClient.Send(filePath, beacon.IPAddress, beacon.Port);
                            if (rsp.Status == 1)
                            {
                                //success
                                NFTLogUtility.Log($"Success sending asset: {assetName}. Description: {rsp.Description}", "BeaconProcessor.ProcessData() - send");
                                await P2PClient.BeaconFileIsReady(scUID, assetName);
                            }
                            else
                            {
                                NFTLogUtility.Log($"NFT Send for assets -> {assetName} <- failed.", "BeaconProcessor.ProcessData() - send");
                            }
                        }
                    }
                    catch(Exception ex)
                    {
                        NFTLogUtility.Log($"NFT Send for assets failed. Unknown Error {ex.Message}. Data: {data}", "BeaconProcessor.ProcessData() - send");
                    }

                }

                if (message == "receive")
                {
                    try
                    {
                        var payload = JsonConvert.DeserializeObject<string[]>(data);
                        if (payload != null)
                        {
                            var scUID = payload[0];
                            var assetName = payload[1];

                            var beaconString = Globals.Locators.FirstOrDefault().ToStringFromBase64();
                            var beacon = JsonConvert.DeserializeObject<BeaconInfo.BeaconInfoJson>(beaconString);

                            if(beacon != null)
                            {
                                BeaconResponse rsp = BeaconClient.Receive(assetName, beacon.IPAddress, beacon.Port, scUID);
                                if (rsp.Status == 1)
                                {
                                    //success
                                    //report to beacon file is downloaded
                                    //beacon will close connection and terminate relationship between you and sender
                                    NFTLogUtility.Log($"File was received - {assetName}", "BeaconProcessor.ProcessData() - receive");
                                }
                                else
                                {
                                    //failed
                                    NFTLogUtility.Log($"Failed to get rsp.Status = 1. Status was {rsp.Status}", "BeaconProcessor.ProcessData() - receive");
                                }
                            }
                            else
                            {
                                NFTLogUtility.Log($"Beacon was null.", "BeaconProcessor.ProcessData() - receive");
                            }
                            
                        }
                        else
                        {
                            NFTLogUtility.Log($"Payload was null.", "BeaconProcessor.ProcessData() - receive");
                        }
                    }
                    catch(Exception ex)
                    {
                        NFTLogUtility.Log($"Error Receiving File. Error: {ex.Message}", "BeaconProcessor.ProcessData() - receive");
                    }
                }

            }
        }
    }
}
