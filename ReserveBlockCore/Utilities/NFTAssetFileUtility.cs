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
                SCLogUtility.Log("Error Saving NFT File.", "NFTAssetFileUtility.MoveAsset(string fileLocation, string fileName)");
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
                SCLogUtility.Log($"Error Creating Thumbnail. Error: {ex.ToString()}", "NFTAssetFileUtility.CreateNFTAssetThumbnail()");
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
                SCLogUtility.Log("Error Saving NFT File.", "NFTAssetFileUtility.NFTAssetPath()");
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
