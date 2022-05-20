﻿using LiteDB;
using ReserveBlockCore.Data;
using ReserveBlockCore.Trillium;
using ReserveBlockCore.Utilities;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ReserveBlockCore.Models.SmartContracts
{
    public class SmartContractMain
    {
        public string Name { get; set; } //User Defined
        public string Description { get; set; } //User Defined
        public string MinterAddress { get; set; } //User Defined
        public string MinterName { get; set; }
        public string Address { get; set; }
        public SmartContractAsset SmartContractAsset { get; set; }
        public bool IsPublic { get; set; } //System Set
        public string SmartContractUID { get; set; }//System Set
        public string Signature { get; set; }//System Set
        public bool IsMinter { get; set; }
        public bool IsPublished { get; set; }
        public List<SmartContractFeatures>? Features { get; set; }

        public class SmartContractData
        {
            public static ILiteCollection<SmartContractMain> GetSCs()
            {
                var scs = DbContext.DB_Assets.GetCollection<SmartContractMain>(DbContext.RSRV_ASSETS);
                return scs;
            }

            public static SmartContractMain? GetSmartContract(string smartContractUID)
            {
                var scs = GetSCs();
                if(scs != null)
                {
                    var sc = scs.FindOne(x => x.SmartContractUID == smartContractUID);
                    if(sc != null)
                    {
                        return sc;
                    }
                }

                return null;
            }
            public static void SetSmartContractIsPublished(string scUID)
            {
                var scs = GetSCs();

                var scMain = GetSmartContract(scUID);

                if(scMain != null)
                {
                    scMain.IsPublished = true;

                    scs.Update(scMain);
                }              
            }

            public static void CreateSmartContract(string scText)
            {
                var byteArrayFromBase64 = scText.FromBase64ToByteArray();
                var decompressedByteArray = SmartContractUtility.Decompress(byteArrayFromBase64);
                var textFromByte = Encoding.Unicode.GetString(decompressedByteArray);

                var repl = new TrilliumRepl();
                repl.Run("#reset");
                repl.Run(textFromByte);

                var scUID = repl.Run(@"GetNFTId()").Value.ToString();
                var features = repl.Run(@"GetNFTFeatures()").Value.ToString();

                var minterName = repl.Run(@"MinterName").Value.ToString();
                var name = repl.Run(@"Name").Value.ToString();
                var description = repl.Run(@"Description").Value.ToString();
                var minterAddress = repl.Run(@"MinterAddress").Value.ToString();
                var address = repl.Run(@"Address").Value.ToString();
                var signature = repl.Run(@"Signature").Value.ToString();

                var extension = repl.Run(@"Extension").Value.ToString();
                var fileSize = Convert.ToInt32(repl.Run(@"FileSize").Value.ToString());
                var location = repl.Run(@"Location").Value.ToString();
                var fileName = repl.Run(@"FileName").Value.ToString();
                var assetAuthorName = repl.Run(@"AssetAuthorName").Value.ToString();

                var mainData = repl.Run(@"NftMain(""nftdata"")").Value.ToString();
                var mainDataArray = mainData.Split(new string[] { "|->" }, StringSplitOptions.None);

                var assetData = repl.Run(@"NftMain(""getnftassetdata"")").Value.ToString();
                var assetDataArray = assetData.Split(new string[] { "|->" }, StringSplitOptions.None);

                var smartContractMain = GetSmartContractMain(name, description, address, minterAddress, minterName, scUID, signature, features);
                var smartContractAssset = SmartContractAsset.GetSmartContractAsset(assetAuthorName, fileName, location, extension, fileSize);
                smartContractMain.SmartContractAsset = smartContractAssset;


                if ((string)features != "")
                {
                    List<SmartContractFeatures> featuresList = new List<SmartContractFeatures>();
                    var feats = (string)features;
                    if (feats.Contains(":"))
                    {
                        var featureList = feats.Split(':');
                        foreach (var feature in featureList)
                        {
                            SmartContractFeatures scFeature = new SmartContractFeatures();
                            var featureName = (FeatureName)Convert.ToInt32(feature);
                            switch (featureName)
                            {
                                case FeatureName.Royalty:
                                    var royaltyData = repl.Run(@"NftMain(""getroyaltydata"")").Value.ToString();
                                    var royaltyArray = royaltyData.Split(new string[] { "|->" }, StringSplitOptions.None);
                                    var royaltyFeature = RoyaltyFeature.GetRoyaltyFeature(royaltyArray);
                                    scFeature.FeatureName = FeatureName.Royalty;
                                    scFeature.FeatureFeatures = royaltyFeature;

                                    featuresList.Add(scFeature);
                                    break;
                                case FeatureName.MultiAsset:
                                    var multiAssetList = new List<string>();
                                    var multiAssetCount = Convert.ToInt32(repl.Run(@"MultiAssetCount").Value.ToString());
                                    for(int i = 1; i <= multiAssetCount; i++)
                                    {
                                        var funcLetter = FunctionNameUtility.GetFunctionLetter(i);
                                        var ma = repl.Run(@"MultiAsset" + funcLetter + "()").Value.ToString();
                                        multiAssetList.Add(ma);
                                    }

                                    var multiAssetFeatureList = MultiAssetFeature.GetMultiAssetFeature(multiAssetList);
                                    scFeature.FeatureName = FeatureName.MultiAsset;
                                    scFeature.FeatureFeatures = multiAssetFeatureList;
                                    featuresList.Add(scFeature);
                                    break;
                                case FeatureName.Evolving:
                                    var evolveList = new List<string>();
                                    var evolveCount = Convert.ToInt32(repl.Run(@"EvolveStates()").Value.ToString());
                                    for (int i = 1; i <= evolveCount; i++)
                                    {
                                        var funcLetter = FunctionNameUtility.GetFunctionLetter(i);
                                        var ma = repl.Run(@"EvolveState" + funcLetter + "()").Value.ToString();
                                        evolveList.Add(ma);
                                    }

                                    var evolveFeatureList = EvolvingFeature.GetEvolveFeature(evolveList);
                                    scFeature.FeatureName = FeatureName.Evolving;
                                    scFeature.FeatureFeatures = evolveFeatureList;
                                    featuresList.Add(scFeature);
                                    break;
                                default:
                                    break;
                            }

                        }
                    }
                    else
                    {
                        SmartContractFeatures scFeature = new SmartContractFeatures();
                        var featureName = (FeatureName)Convert.ToInt32(feats);
                        switch (featureName)
                        {
                            case FeatureName.Royalty:
                                var royaltyData = repl.Run(@"NftMain(""getroyaltydata"")").Value.ToString();
                                var royaltyArray = royaltyData.Split(new string[] { "|->" }, StringSplitOptions.None);
                                var royaltyFeature = RoyaltyFeature.GetRoyaltyFeature(royaltyArray);
                                scFeature.FeatureName = FeatureName.Royalty;
                                scFeature.FeatureFeatures = royaltyFeature;

                                featuresList.Add(scFeature);
                                break;
                            case FeatureName.MultiAsset:
                                var multiAssetList = new List<string>();
                                var multiAssetCount = Convert.ToInt32(repl.Run(@"MultiAssetCount").Value.ToString());
                                for (int i = 1; i <= multiAssetCount; i++)
                                {
                                    var funcLetter = FunctionNameUtility.GetFunctionLetter(i);
                                    var ma = repl.Run(@"MultiAsset" + funcLetter + "()").Value.ToString();
                                    multiAssetList.Add(ma);
                                }
                                var multiAssetFeatureList = MultiAssetFeature.GetMultiAssetFeature(multiAssetList);
                                scFeature.FeatureName = FeatureName.MultiAsset;
                                scFeature.FeatureFeatures = multiAssetFeatureList;
                                featuresList.Add(scFeature);
                                break;
                            case FeatureName.Evolving:
                                var evolveList = new List<string>();
                                var evolveCount = Convert.ToInt32(repl.Run(@"EvolveStates()").Value.ToString());
                                for (int i = 1; i <= evolveCount; i++)
                                {
                                    var funcLetter = FunctionNameUtility.GetFunctionLetter(i);
                                    var ma = repl.Run(@"EvolveState" + funcLetter + "()").Value.ToString();
                                    evolveList.Add(ma);
                                }

                                var evolveFeatureList = EvolvingFeature.GetEvolveFeature(evolveList);
                                scFeature.FeatureName = FeatureName.Evolving;
                                scFeature.FeatureFeatures = evolveFeatureList;
                                featuresList.Add(scFeature);
                                break;
                            default:
                                break;
                        }
                    }

                    smartContractMain.Features = featuresList;
                }

                

            }

            private static SmartContractMain GetSmartContractMain(string name, string desc, string address, string minterAddress, string minterName, string smartContractUID, string signature, string features)
            {
                SmartContractMain scMain = new SmartContractMain();

                scMain.SmartContractUID = smartContractUID;
                scMain.Name = name;
                scMain.Description = desc;
                scMain.Address = address;
                scMain.IsMinter = false;
                scMain.IsPublic = false;
                //scMain.Features = features;
                scMain.IsPublished = true;
                scMain.MinterName = minterName;
                scMain.MinterAddress = minterAddress;
                scMain.Signature = signature;

                return scMain;
            }

            public static void SaveSmartContract(SmartContractMain scMain, string scText)
            {
                var scs = GetSCs();

                scs.Insert(scMain);

                SaveSCLocally(scMain, scText);
            }

            public static void DeleteSmartContract(string scUID)
            {
                var scs = GetSCs();

                scs.DeleteMany(x => x.SmartContractUID == scUID);
            }
            public static async void SaveSCLocally(SmartContractMain scMain, string scText)
            {
                try
                {
                    var databaseLocation = Program.IsTestNet != true ? "SmartContracts" : "SmartContractsTestNet";
                    var text = scText;
                    string path = "";
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                        path = homeDirectory + Path.DirectorySeparatorChar + "rbx" + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                    }
                    else
                    {
                        if (Debugger.IsAttached)
                        {
                            path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                        }
                        else
                        {
                            path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + "RBX" + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                        }
                    }
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }

                    var scName = scMain.SmartContractUID.Split(':');
                    await File.AppendAllTextAsync(path + scName[0].ToString() + ".trlm", text);
                }
                catch (Exception ex)
                {

                }
            }
        }

    }
}
