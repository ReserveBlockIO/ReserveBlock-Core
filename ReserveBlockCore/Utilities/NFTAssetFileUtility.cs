using Docnet.Core;
using Docnet.Core.Models;
using ImageMagick;
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
        private static readonly HashSet<string> ValidExtensions = new HashSet<string>()
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".jp2",
            ".gif",
            ".tif",
            ".tiff",
            ".webp",
            ".bmp",
            ".pdf"
            // Other possible extensions
        };

        private const int ImageSize = 400;

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

            if (!string.IsNullOrEmpty(Globals.CustomPath))
            {
                path = Globals.CustomPath + MainFolder + Path.DirectorySeparatorChar + assetLocation + Path.DirectorySeparatorChar;
            }

            var thumbPath = path + Path.DirectorySeparatorChar + "thumbs" + Path.DirectorySeparatorChar;

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            if (!Directory.Exists(thumbPath))
            {
                Directory.CreateDirectory(thumbPath);
            }

            var newPath = path + fileName;
            var newThumbPath = thumbPath + fileName;

            var fileExt = fileLocation.ToFileExtension();

            try
            {
                var fileExist = File.Exists(newPath);
                if(!fileExist)
                {
                    File.Copy(fileLocation, newPath);
                }
                if(ValidExtensions.Contains(fileExt.ToLower()))
                    CreateNFTAssetThumbnail(newPath, newThumbPath);
                
                return true;
            }
            catch(Exception ex)
            {
                ErrorLogUtility.LogError(ex.ToString(), "NFTAssetFileUtility.MoveAsset(string fileLocation, string fileName)");
                NFTLogUtility.Log("Error Saving NFT File.", "NFTAssetFileUtility.MoveAsset(string fileLocation, string fileName)");
                return false;
            }
        }
        public static async Task GenerateThumbnails(string scUID)
        {
            var scMain = SmartContractMain.SmartContractData.GetSmartContract(scUID);
            if(scMain != null)
            {
                var assetList = await GetAssetListFromSmartContract(scMain);
                if(assetList?.Count > 0)
                {
                    foreach(var asset in assetList)
                    {
                        try
                        {
                            var originPath = CreateNFTAssetPath(asset, scUID);
                            var thumbsPath = CreateNFTAssetPath(asset, scUID, true);

                            if (File.Exists(originPath))
                            {
                                if (!File.Exists(thumbsPath))
                                {
                                    CreateNFTAssetThumbnail(originPath, thumbsPath);
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
        }
        public static void CreateNFTAssetThumbnail(string originPath, string newPath)
        {
            try
            {
                var fileExist = File.Exists(originPath);
                if (fileExist)
                {
                    var fileExt = originPath.ToFileExtension();
                    if (fileExt == ".pdf")
                    {
                        var pdfBytes = File.ReadAllBytes(originPath);
                        var pdfImage = PdfToImage(pdfBytes);

                        newPath = newPath.Replace(".pdf", ".jpg");
                        FileStream file = new FileStream(newPath, FileMode.Create, FileAccess.Write);
                        pdfImage.WriteTo(file);
                        file.Close();
                        pdfImage.Close();
                    }
                    else if(fileExt == ".mp4")
                    { 
                        //will need to integrate ffmpeg if we want this function.
                    }
                    else
                    {
                        using (var image = new MagickImage(originPath))
                        {
                            if (image.Height > ImageSize || image.Width > ImageSize)
                            {
                                var newPathFileExt = newPath.ToFileExtension();
                                var size = new MagickGeometry(ImageSize, ImageSize);
                                size.IgnoreAspectRatio = false;
                                image.Resize(size);
                                image.Quality = 45;
                                // Save the result
                                newPath = newPath.Replace(newPathFileExt, ".jpg");
                                ImageOptimizer optimizer = new ImageOptimizer();
                                image.Write(newPath, MagickFormat.Jpg);
                                FileInfo info = new FileInfo(newPath);
                                optimizer.Compress(info);
                                info.Refresh();
                            }
                            else
                            {
                                File.Copy(originPath, newPath);
                            }
                        }
                    }
                    
                }
            }
            catch (Exception ex) 
            {
                NFTLogUtility.Log($"Error Creating Thumbnail. Error: {ex.ToString()}", "NFTAssetFileUtility.CreateNFTAssetThumbnail()");
            }
            
        }
        public static string CreateNFTAssetPath(string fileName, string scUID, bool thumbs = false)
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
            if (thumbs)
            {
                if (!Directory.Exists(path + Path.DirectorySeparatorChar + "thumbs" + Path.DirectorySeparatorChar))
                {
                    Directory.CreateDirectory(path + Path.DirectorySeparatorChar + "thumbs" + Path.DirectorySeparatorChar);
                }
            }

            var newPath = thumbs ? path + Path.DirectorySeparatorChar + "thumbs" + Path.DirectorySeparatorChar + fileName : path + fileName;

            return newPath;
        }
        public static string NFTAssetPath(string fileName, string scUID, bool getThumbs = false)
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

            var newPath = getThumbs ? path + "thumbs" + Path.DirectorySeparatorChar + fileName : path + fileName;

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
                ErrorLogUtility.LogError(ex.ToString(), "NFTAssetFileUtility.NFTAssetPath()");
                NFTLogUtility.Log("Error Saving NFT File.", "NFTAssetFileUtility.NFTAssetPath()");
                return "NA";
            }

            return "NA";
        }

        public static byte[] GetNFTAssetByteArray(string path)
        {
            byte[] imageBytes = File.ReadAllBytes(path);

            return imageBytes;
        }

        public static async Task<List<string>> GetAssetListFromSmartContract(SmartContractMain sc)
        {
            List<string> assets = new List<string>();

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
                        List<object> myArrayObj = new List<object>();
                        List<EvolvingFeature> myArray = new List<EvolvingFeature>();
                        bool useObject = false;
                        var count = 0;
                        try
                        {
                            myArray = ((List<EvolvingFeature>)feature.FeatureFeatures);
                        }
                        catch
                        {
                            //need to explore why these are serializing weirdly. Might make more sense to json them in future.
                            myArrayObj = ((object[])feature.FeatureFeatures).ToList();
                            useObject = true;
                        }
                        if(useObject)
                        {
                            myArrayObj.ForEach(x => {
                                var evolveDict = (EvolvingFeature)myArrayObj[count];
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
                        else
                        {
                            myArray.ForEach(x => {
                                var evolveDict = myArray[count];
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
                        
                    }
                    if (feature.FeatureName == FeatureName.MultiAsset)
                    {
                        List<object> myArrayObj = new List<object>();
                        List<MultiAssetFeature> myArray = new List<MultiAssetFeature>();
                        bool useObject = false;
                        var count = 0;
                        try
                        {
                            myArray = ((List<MultiAssetFeature>)feature.FeatureFeatures);
                        }
                        catch
                        {
                            myArrayObj = ((object[])feature.FeatureFeatures).ToList();
                            useObject = true;
                        }

                        if(useObject)
                        {
                            myArrayObj.ForEach(x => {
                                var multiAssetDict = (MultiAssetFeature)myArrayObj[count];

                                if (multiAssetDict != null)
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
                        else
                        {
                            myArray.ForEach(x => {
                                var multiAssetDict = myArray[count];

                                if (multiAssetDict != null)
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
            }

            return assets;
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

                                    var beaconString = aq.Locator.ToStringFromBase64();
                                    var beacon = JsonConvert.DeserializeObject<BeaconInfo.BeaconInfoJson>(beaconString);

                                    Globals.Beacon.TryGetValue(beacon.IPAddress, out var globalBeacon);
                                    if(globalBeacon != null)
                                    {
                                        globalBeacon.Downloading = false;
                                        Globals.Beacon[globalBeacon.IPAddress] = globalBeacon;
                                    }

                                    foreach (string asset in assetList)
                                    {
                                        try
                                        {
                                            await P2PClient.BeaconFileIsDownloaded(aq.SmartContractUID, asset, aq.Locator);
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
                ErrorLogUtility.LogError($"Error downloading assets from beacon. Error Msg: {ex.ToString()}", "NFTAssetFileUtility.DownloadAssetFromBeacon()");
            }
            
            return output;
        }

        public static MemoryStream PdfToImage(byte[] pdfBytes /* the PDF file bytes */)
        {
            MemoryStream memoryStream = new MemoryStream();
            MagickImage imgBackdrop;
            MagickColor backdropColor = MagickColors.White; // replace transparent pixels with this color 
            int pdfPageNum = 0; // first page is 0

            using (IDocLib pdfLibrary = DocLib.Instance)
            {
                using (var docReader = pdfLibrary.GetDocReader(pdfBytes, new PageDimensions(1.0d)))
                {
                    using (var pageReader = docReader.GetPageReader(pdfPageNum))
                    {
                        var rawBytes = pageReader.GetImage(); // Returns image bytes as B-G-R-A ordered list.
                        rawBytes = RearrangeBytesToRGBA(rawBytes);
                        var width = pageReader.GetPageWidth();
                        var height = pageReader.GetPageHeight();

                        // specify that we are reading a byte array of colors in R-G-B-A order.
                        PixelReadSettings pixelReadSettings = new PixelReadSettings(width, height, StorageType.Char, PixelMapping.RGBA);
                        using (MagickImage imgPdfOverlay = new MagickImage(rawBytes, pixelReadSettings))
                        {
                            // turn transparent pixels into backdrop color using composite
                            imgBackdrop = new MagickImage(backdropColor, width, height);
                            imgBackdrop.Composite(imgPdfOverlay, CompositeOperator.Over);
                        }
                    }
                }
            }

            var size = new MagickGeometry(ImageSize, ImageSize);
            size.IgnoreAspectRatio = false;
            imgBackdrop.Resize(size);

            imgBackdrop.Write(memoryStream, MagickFormat.Jpg);
            imgBackdrop.Dispose();
            memoryStream.Position = 0;
            return memoryStream;
        }
        private static byte[] RearrangeBytesToRGBA(byte[] BGRABytes)
        {
            var max = BGRABytes.Length;
            var RGBABytes = new byte[max];
            var idx = 0;
            byte r;
            byte g;
            byte b;
            byte a;
            while (idx < max)
            {
                // get colors in original order: B G R A
                b = BGRABytes[idx];
                g = BGRABytes[idx + 1];
                r = BGRABytes[idx + 2];
                a = BGRABytes[idx + 3];

                // re-arrange to be in new order: R G B A
                RGBABytes[idx] = r;
                RGBABytes[idx + 1] = g;
                RGBABytes[idx + 2] = b;
                RGBABytes[idx + 3] = a;

                idx += 4;
            }
            return RGBABytes;
        }

        public static byte[][] SplitIntoPackets(byte[] data)
        {
            const int MaxPacketSize = 1024;

            var packets = new byte[(data.Length + MaxPacketSize - 1) / MaxPacketSize][];
            for (int i = 0; i < packets.Length; i++)
            {
                int offset = i * MaxPacketSize;
                int length = Math.Min(MaxPacketSize, data.Length - offset);
                packets[i] = new byte[length + sizeof(int)];
                BitConverter.GetBytes(i).CopyTo(packets[i], 0);
                Array.Copy(data, offset, packets[i], sizeof(int), length);
            }

            return packets;
        }

    }
}
