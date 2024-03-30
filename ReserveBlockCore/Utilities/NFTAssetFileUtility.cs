using Docnet.Core;
using Docnet.Core.Models;
using ImageMagick;
using Newtonsoft.Json;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.P2P;
using System;
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
        public static string GetvBTCDefaultLogoLocation()
        {
            var assetLocation = Globals.IsTestNet != true ? "Logos" : "LogosTestNet";
            string path = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                path = homeDirectory + Path.DirectorySeparatorChar + MainFolder.ToLower() + Path.DirectorySeparatorChar + assetLocation + Path.DirectorySeparatorChar;
            }
            else
            {
                if (Debugger.IsAttached)
                {
                    path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "DBs" + Path.DirectorySeparatorChar + assetLocation + Path.DirectorySeparatorChar;
                }
                else
                {
                    path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + MainFolder + Path.DirectorySeparatorChar + assetLocation + Path.DirectorySeparatorChar;
                }
            }

            if (!string.IsNullOrEmpty(Globals.CustomPath))
            {
                path = Globals.CustomPath + MainFolder + Path.DirectorySeparatorChar + assetLocation + Path.DirectorySeparatorChar;
            }

            var newPath = path + "defaultvBTC.png";

            return newPath;

        }
        public static async Task GeneratevBTCDefaultLogo()
        {
            var assetLocation = Globals.IsTestNet != true ? "Logos" : "LogosTestNet";
            string path = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                path = homeDirectory + Path.DirectorySeparatorChar + MainFolder.ToLower() + Path.DirectorySeparatorChar + assetLocation + Path.DirectorySeparatorChar;
            }
            else
            {
                if (Debugger.IsAttached)
                {
                    path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "DBs" + Path.DirectorySeparatorChar + assetLocation + Path.DirectorySeparatorChar;
                }
                else
                {
                    path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + MainFolder + Path.DirectorySeparatorChar + assetLocation + Path.DirectorySeparatorChar;
                }
            }

            if (!string.IsNullOrEmpty(Globals.CustomPath))
            {
                path = Globals.CustomPath + MainFolder + Path.DirectorySeparatorChar + assetLocation + Path.DirectorySeparatorChar;
            }

            var logoPath = path;

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            var newPath = path + "defaultvBTC.png";

            if (!File.Exists(newPath))
            {
                byte[] rebuiltImageBytes = Convert.FromBase64String(DefaultVBTCLogo());
                await File.WriteAllBytesAsync(newPath, rebuiltImageBytes);
            }
        }

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
                                var newPathFileExt = newPath.ToFileExtension();
                                newPath = newPath.Replace(newPathFileExt, ".jpg");
                                ImageOptimizer optimizer = new ImageOptimizer();
                                image.Write(newPath, MagickFormat.Jpg);
                                FileInfo info = new FileInfo(newPath);
                                optimizer.Compress(info);
                                info.Refresh();
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
                        return "Error Generating Smart Contract in memory.";
                    }
                }

                var assetList = await MD5Utility.GetAssetList(md5List);             

                var locatorList = locators.Split(",").ToList();

                if (locatorList.Count > 0)
                {
                    var result = await P2PClient.BeaconDownloadRequest(locatorList, assetList, sc.SmartContractUID, preSigned);
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

        public static string DefaultVBTCLogo()
        {
            //default vBTC logo in base64
            string base64Logo = "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";
            return base64Logo;
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
