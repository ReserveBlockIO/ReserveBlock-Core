using Newtonsoft.Json;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.P2P;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ReserveBlockCore.Utilities
{
    public class NFTAssetFileUtility 
    {
        private static string MainFolder = Globals.IsTestNet != true ? "RBX" : "RBXTest";

        public static bool MoveAsset(string fileLocation, string fileName, string scUID)
        {
            var assetLocation = Globals.IsTestNet != true ? "Assets" : "AssetsTestNet";

            scUID = scUID.Replace(":", ""); //remove the ':' because some folder structures won't allow it.

            string path = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                path = homeDirectory + Path.DirectorySeparatorChar + MainFolder.ToLower() + Path.DirectorySeparatorChar + assetLocation + Path.DirectorySeparatorChar + scUID + Path.DirectorySeparatorChar;
            }
            else
            {
                if (Debugger.IsAttached)
                {
                    path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "DBs" + Path.DirectorySeparatorChar + assetLocation + Path.DirectorySeparatorChar + scUID + Path.DirectorySeparatorChar;
                }
                else
                {
                    path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + MainFolder + Path.DirectorySeparatorChar + assetLocation + Path.DirectorySeparatorChar + scUID + Path.DirectorySeparatorChar;
                }
            }
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var newPath = path + fileName;

            try
            {
                var fileExist = File.Exists(newPath);
                if(!fileExist)
                {
                    File.Copy(fileLocation, newPath);
                }
                return true;
            }
            catch(Exception ex)
            {
                ErrorLogUtility.LogError(ex.Message, "NFTAssetFileUtility.MoveAsset(string fileLocation, string fileName)");
                NFTLogUtility.Log("Error Saving NFT File.", "NFTAssetFileUtility.MoveAsset(string fileLocation, string fileName)");
                return false;
            }
        }
        public static string CreateNFTAssetPath(string fileName, string scUID)
        {
            var assetLocation = Globals.IsTestNet != true ? "Assets" : "AssetsTestNet";

            scUID = scUID.Replace(":", ""); //remove the ':' because some folder structures won't allow it.

            string path = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                path = homeDirectory + Path.DirectorySeparatorChar + MainFolder.ToLower() + Path.DirectorySeparatorChar + assetLocation + Path.DirectorySeparatorChar + scUID + Path.DirectorySeparatorChar;
            }
            else
            {
                if (Debugger.IsAttached)
                {
                    path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "DBs" + Path.DirectorySeparatorChar + assetLocation + Path.DirectorySeparatorChar + scUID + Path.DirectorySeparatorChar;
                }
                else
                {
                    path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + MainFolder + Path.DirectorySeparatorChar + assetLocation + Path.DirectorySeparatorChar + scUID + Path.DirectorySeparatorChar;
                }
            }
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var newPath = path + fileName;

            return newPath;
        }
        public static string NFTAssetPath(string fileName, string scUID)
        {
            var assetLocation = Globals.IsTestNet != true ? "Assets" : "AssetsTestNet";

            scUID = scUID.Replace(":", ""); //remove the ':' because some folder structures won't allow it.

            string path = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                path = homeDirectory + Path.DirectorySeparatorChar + MainFolder.ToLower() + Path.DirectorySeparatorChar + assetLocation + Path.DirectorySeparatorChar + scUID + Path.DirectorySeparatorChar;
            }
            else
            {
                if (Debugger.IsAttached)
                {
                    path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "DBs" + Path.DirectorySeparatorChar + assetLocation + Path.DirectorySeparatorChar + scUID + Path.DirectorySeparatorChar;
                }
                else
                {
                    path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + MainFolder + Path.DirectorySeparatorChar + assetLocation + Path.DirectorySeparatorChar + scUID + Path.DirectorySeparatorChar;
                }
            }

            var newPath = path + fileName;

            try
            {
                var fileExist = File.Exists(newPath);
                if (fileExist)
                {
                    return newPath;
                }
                
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.Message, "NFTAssetFileUtility.NFTAssetPath()");
                NFTLogUtility.Log("Error Saving NFT File.", "NFTAssetFileUtility.NFTAssetPath()");
                return "NA";
            }

            return "NA";
        }

        public static async Task CheckForAssets(AssetQueue aq)
        {
            try
            {
                var aqDB = AssetQueue.GetAssetQueue();
                //Look to see if media exist
                if(aqDB != null)
                {
                    if (aq.MediaListJson != null)
                    {
                        var assetList = JsonConvert.DeserializeObject<List<string>>(aq.MediaListJson);

                        if (assetList != null)
                        {
                            if (assetList.Count() > 0)
                            {
                                var assetCount = assetList.Count();
                                var assestExistCount = 0;
                                foreach (string asset in assetList)
                                {
                                    var path = NFTAssetPath(asset, aq.SmartContractUID);
                                    var fileExist = File.Exists(path);
                                    if (fileExist)
                                        assestExistCount += 1;
                                }

                                if (assetCount == assestExistCount)
                                {
                                    aq.IsDownloaded = true;
                                    aq.IsComplete = true;
                                    aqDB.UpdateSafe(aq);

                                    foreach (string asset in assetList)
                                    {
                                        try
                                        {
                                            await P2PClient.BeaconFileIsDownloaded(aq.SmartContractUID, asset);
                                        }
                                        catch { }
                                    }

                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        public static async Task<string> DownloadAssetFromBeacon(string scUID, string locators, string preSigned = "NA", string md5List = "NA")
        {
            var output = "Fail";
            try
            {
                List<string> assets = new List<string>();
                var sc = SmartContractMain.SmartContractData.GetSmartContract(scUID);
                if (sc == null)
                {
                    var scStateTrei = SmartContractStateTrei.GetSmartContractState(scUID);
                    if (scStateTrei == null)
                    {
                        return "SC Does not exist.";
                    }
                    sc = SmartContractMain.GenerateSmartContractInMemory(scStateTrei.ContractData);
                    if (sc == null)
                    {
                        return "Not locally owned.";
                    }
                    return "Not locally owned.";
                }

                if (sc.SmartContractAsset != null)
                {
                    assets.Add(sc.SmartContractAsset.Name);
                }

                if (sc.Features != null)
                {
                    foreach (var feature in sc.Features)
                    {
                        if (feature.FeatureName == FeatureName.Evolving)
                        {
                            var count = 0;
                            var myArray = ((object[])feature.FeatureFeatures).ToList();
                            myArray.ForEach(x => {
                                var evolveDict = (EvolvingFeature)myArray[count];
                                SmartContractAsset evoAsset = new SmartContractAsset();
                                if (evolveDict.SmartContractAsset != null)
                                {

                                    var assetEvo = evolveDict.SmartContractAsset;
                                    evoAsset.Name = assetEvo.Name;
                                    if (!assets.Contains(evoAsset.Name))
                                    {
                                        assets.Add(evoAsset.Name);
                                    }
                                    count += 1;
                                }

                            });
                        }
                        if (feature.FeatureName == FeatureName.MultiAsset)
                        {
                            var count = 0;
                            var myArray = ((object[])feature.FeatureFeatures).ToList();

                            myArray.ForEach(x => {
                                var multiAssetDict = (MultiAssetFeature)myArray[count];

                                if(multiAssetDict != null)
                                {
                                    var fileName = multiAssetDict.FileName;
                                    if (!assets.Contains(fileName))
                                    {
                                        assets.Add(fileName);
                                    }
                                }
                                
                                count += 1;

                            });

                        }
                    }
                }

                var locatorList = locators.Split(",").ToList();

                if (locatorList.Count > 0)
                {
                    var result = await P2PClient.BeaconDownloadRequest(locatorList, assets, sc.SmartContractUID, preSigned);
                    if (result != false)
                    {
                        output = "Success";
                    }
                }
            }
            catch(Exception ex)
            {
                ErrorLogUtility.LogError($"Error downloading assets from beacon. Error Msg: {ex.Message}", "NFTAssetFileUtility.DownloadAssetFromBeacon()");
            }
            
            return output;
        }


    }
}
