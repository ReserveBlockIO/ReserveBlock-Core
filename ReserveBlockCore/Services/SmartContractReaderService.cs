using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.SmartContractSourceGenerator;
using ReserveBlockCore.Utilities;
using System.Diagnostics.Metrics;
using System.Text;

namespace ReserveBlockCore.Services
{
    public static class SmartContractReaderService
    {
        public static async Task<(string?, SmartContractMain)> ReadSmartContract(SmartContractMain scMain, int? activeEvoState = null)
        {
            try
            {
                var scUID = scMain.SmartContractUID;
                var features = "";
                var featuresList = scMain.Features;
                StringBuilder strRoyaltyBld = new StringBuilder();
                StringBuilder strEvolveBld = new StringBuilder();
                StringBuilder strMultiAssetBld = new StringBuilder();

                var appendChar = "\"|->\"";

                var scAsset = scMain.SmartContractAsset;
                StringBuilder strBuild = new StringBuilder();

                if (featuresList != null)
                {
                    var Flist = new List<SmartContractFeatures>();
                    if (featuresList.Count == 1)
                    {
                        var feature = featuresList.First();
                        features = ((int)feature.FeatureName).ToString();
                        if (feature.FeatureName == FeatureName.Royalty)
                        {
                            var royalty = ((RoyaltyFeature)feature.FeatureFeatures);
                            feature.FeatureFeatures = royalty;

                            Flist.Add(feature);

                            //create royalty code block
                            var royaltySource = await RoyaltySourceGenerator.Build(royalty);
                            strBuild = royaltySource.Item1;
                            strRoyaltyBld = royaltySource.Item2;

                        }
                        else if (feature.FeatureName == FeatureName.Evolving)
                        {
                            List<EvolvingFeature> evolve = new List<EvolvingFeature>();
                            var myArray = ((object[])feature.FeatureFeatures).ToList();
                            
                            var count = 0;
                            myArray.ForEach(x => {
                                //var evolveDict = (Dictionary<string, object>)myArray[count];
                                //SmartContractAsset evoAsset = new SmartContractAsset();
                                //if (evolveDict.ContainsKey("SmartContractAsset"))
                                //{

                                //    var assetEvo = (Dictionary<string, object>)evolveDict["SmartContractAsset"];
                                //    evoAsset.Name = (string)assetEvo["Name"];
                                //    evoAsset.FileSize = (long)assetEvo["FileSize"];
                                //    evoAsset.AssetId = (Guid)assetEvo["AssetId"];
                                //    evoAsset.Location = (string)assetEvo["Location"];
                                //    evoAsset.Extension = (string)assetEvo["Extension"];
                                //}

                                //EvolvingFeature evoFeature = new EvolvingFeature
                                //{
                                //    Name = evolveDict["Name"].ToString(),
                                //    Description = evolveDict["Description"].ToString(),
                                //    EvolutionState = (int)evolveDict["EvolutionState"],
                                //    IsDynamic = (bool)evolveDict["IsDynamic"],
                                //    IsCurrentState = (bool)evolveDict["IsCurrentState"],
                                //    EvolveDate = evolveDict.ContainsKey("EvolveDate") == true ? (DateTime)evolveDict["EvolveDate"] : null,
                                //    EvolveBlockHeight = evolveDict.ContainsKey("EvolveBlockHeight") == true ? (long)evolveDict["EvolveBlockHeight"] : null,
                                //    SmartContractAsset = evolveDict.ContainsKey("SmartContractAsset") == true ? evoAsset : null
                                //};

                                var evolveDict = myArray[count] as EvolvingFeature;
                                
                                SmartContractAsset evoAsset = new SmartContractAsset();
                                if (evolveDict.SmartContractAsset != null)
                                {

                                    var assetEvo = evolveDict.SmartContractAsset;
                                    evoAsset.Name = assetEvo.Name;
                                    evoAsset.FileSize = assetEvo.FileSize;
                                    evoAsset.AssetId = assetEvo.AssetId;
                                    evoAsset.Location = assetEvo.Location;
                                    evoAsset.Extension = assetEvo.Extension;
                                }

                                EvolvingFeature evoFeature = new EvolvingFeature
                                {
                                    Name = evolveDict.Name.ToString(),
                                    Description = evolveDict.Description.ToString(),
                                    EvolutionState = evolveDict.EvolutionState,
                                    IsDynamic = evolveDict.IsDynamic,
                                    IsCurrentState = evolveDict.IsCurrentState,
                                    EvolveDate = evolveDict.EvolveDate != null ? evolveDict.EvolveDate : null,
                                    EvolveBlockHeight = evolveDict.EvolveBlockHeight != null ? evolveDict.EvolveBlockHeight : null,
                                    SmartContractAsset = evolveDict.SmartContractAsset != null ? evoAsset : null
                                };


                                if (activeEvoState != null)
                                {
                                    if(evoFeature.EvolutionState == activeEvoState.Value)
                                    {
                                        evoFeature.IsCurrentState = true;
                                    }
                                    else
                                    {
                                        evoFeature.IsCurrentState = false;
                                    }
                                }

                                count += 1;
                                evolve.Add(evoFeature);
                            });

                            if (evolve.Count() > 0)
                            {
                                feature.FeatureFeatures = evolve;

                                Flist.Add(feature);
                                var maxEvoState = evolve.Count().ToString();
                                var evolutionaryState = "\"{*0}\"";

                                var evolveSource = await EvolveSourceGenerator.Build(evolve, strBuild, scUID, activeEvoState, true);
                                strBuild = evolveSource.Item1;
                                strEvolveBld = evolveSource.Item2;
                            }
                        }
                        else if (feature.FeatureName == FeatureName.MultiAsset)
                        {
                            List<MultiAssetFeature> multiAsset = new List<MultiAssetFeature>();
                            var myArray = ((object[])feature.FeatureFeatures).ToList();

                            var count = 0;
                            myArray.ForEach(x => {
                                //changed on 9.28.2022
                                //var multiAssetDict = (Dictionary<string, object>)myArray[count];
                                //MultiAssetFeature maFeature = new MultiAssetFeature
                                //{
                                //    FileName = multiAssetDict["FileName"].ToString(),
                                //    Extension = multiAssetDict["Extension"].ToString(),
                                //    Location = multiAssetDict["Location"].ToString(),
                                //    FileSize = (long)multiAssetDict["FileSize"],
                                //    AssetAuthorName = multiAssetDict["AssetAuthorName"].ToString(),
                                //};
                                var multiAssetDict = (MultiAssetFeature)myArray[count];
                                MultiAssetFeature maFeature = new MultiAssetFeature
                                {
                                    FileName = multiAssetDict.FileName.ToString(),
                                    Extension = multiAssetDict.Extension.ToString(),
                                    Location = multiAssetDict.Location.ToString(),
                                    FileSize = multiAssetDict.FileSize,
                                    AssetAuthorName = multiAssetDict.AssetAuthorName.ToString(),
                                };
                                count += 1;
                                multiAsset.Add(maFeature);
                            });

                            if (multiAsset.Count() > 0)
                            {
                                int counter = 1;
                                feature.FeatureFeatures = multiAsset;
                                Flist.Add(feature);

                                var multiAssetSource = await MultiAssetSourceGenerator.Build(multiAsset, strBuild, scUID, true);
                                strBuild = multiAssetSource.Item1;
                                strMultiAssetBld = multiAssetSource.Item2;
                            }
                        }
                        else if (feature.FeatureName == FeatureName.Ticket)
                        {

                        }
                    }
                    else
                    {
                        int count = 1;
                        int featureCount = featuresList.Count();
                        foreach(var x in featuresList)
                        {
                            if (featureCount == 1)
                            {
                                features = ((int)x.FeatureName).ToString();
                            }
                            else
                            {
                                if (features == "")
                                {
                                    features = ((int)x.FeatureName).ToString();
                                }
                                else
                                {
                                    features = features + ":" + ((int)x.FeatureName).ToString();
                                }

                            }

                            if (x.FeatureName == FeatureName.Royalty)
                            {
                                var royalty = ((RoyaltyFeature)x.FeatureFeatures);
                                x.FeatureFeatures = royalty;

                                Flist.Add(x);
                                //create royalty code block
                                var royaltySource = await RoyaltySourceGenerator.Build(royalty);
                                strBuild = royaltySource.Item1;
                                strRoyaltyBld = royaltySource.Item2;
                            }

                            if (x.FeatureName == FeatureName.Evolving)
                            {
                                
                                List<EvolvingFeature> evolve = new List<EvolvingFeature>();
                                var myArray = ((object[])x.FeatureFeatures).ToList();

                                var counter = 0;
                                myArray.ForEach(x => {
                                    var evolveDict = myArray[counter] as EvolvingFeature;
                                    if(evolveDict != null)
                                    {

                                    }
                                    SmartContractAsset evoAsset = new SmartContractAsset();
                                    if (evolveDict.SmartContractAsset != null)
                                    {

                                        var assetEvo = evolveDict.SmartContractAsset;
                                        evoAsset.Name = assetEvo.Name;
                                        evoAsset.FileSize = assetEvo.FileSize;
                                        evoAsset.AssetId = assetEvo.AssetId;
                                        evoAsset.Location = assetEvo.Location;
                                        evoAsset.Extension = assetEvo.Extension;
                                    }

                                    EvolvingFeature evoFeature = new EvolvingFeature
                                    {
                                        Name = evolveDict.Name.ToString(),
                                        Description = evolveDict.Description.ToString(),
                                        EvolutionState = evolveDict.EvolutionState,
                                        IsDynamic = evolveDict.IsDynamic,
                                        IsCurrentState = evolveDict.IsCurrentState,
                                        EvolveDate = evolveDict.EvolveDate != null ? evolveDict.EvolveDate : null,
                                        EvolveBlockHeight = evolveDict.EvolveBlockHeight != null ? evolveDict.EvolveBlockHeight : null,
                                        SmartContractAsset = evolveDict.SmartContractAsset != null ? evoAsset : null
                                    };

                                    if (activeEvoState != null)
                                    {
                                        if (evoFeature.EvolutionState == activeEvoState.Value)
                                        {
                                            evoFeature.IsCurrentState = true;
                                        }
                                        else
                                        {
                                            evoFeature.IsCurrentState = false;
                                        }
                                    }
                                    counter += 1;
                                    evolve.Add(evoFeature);
                                });

                                if (evolve.Count() > 0)
                                {
                                    x.FeatureFeatures = evolve;
                                    Flist.Add(x);
                                    var maxEvoState = evolve.Count().ToString();
                                    var evolutionaryState = "\"{*0}\"";
                                    var evolveSource = await EvolveSourceGenerator.Build(evolve, strBuild, scUID, activeEvoState, true);
                                    strBuild = evolveSource.Item1;
                                    strEvolveBld = evolveSource.Item2;
                                }

                            }

                            if (x.FeatureName == FeatureName.MultiAsset)
                            {
                                List<MultiAssetFeature> multiAsset = new List<MultiAssetFeature>();
                                var myArray = ((object[])x.FeatureFeatures).ToList();

                                var counter = 0;
                                myArray.ForEach(x => {
                                    var multiAssetDict = myArray[counter] as MultiAssetFeature;
                                    MultiAssetFeature maFeature = new MultiAssetFeature
                                    {
                                        FileName = multiAssetDict.FileName.ToString(),
                                        Extension = multiAssetDict.Extension.ToString(),
                                        Location = multiAssetDict.Location.ToString(),
                                        FileSize = multiAssetDict.FileSize,
                                        AssetAuthorName = multiAssetDict.AssetAuthorName.ToString(),
                                    };
                                    counter += 1;
                                    multiAsset.Add(maFeature);
                                });

                                if (multiAsset.Count() > 0)
                                {
                                    x.FeatureFeatures = multiAsset;
                                    Flist.Add(x);

                                    var multiAssetSource = await MultiAssetSourceGenerator.Build(multiAsset, strBuild, scUID, true);
                                    strBuild = multiAssetSource.Item1;
                                    strMultiAssetBld = multiAssetSource.Item2;
                                }
                            }

                        }
                    }
                    scMain.Features = Flist;

                }

                //NFT Main Data
                strBuild.AppendLine(("let Name = \"{#NFTName}\"").Replace("{#NFTName}", scMain.Name));
                strBuild.AppendLine(("let Description = \"{#Description}\"").Replace("{#Description}", scMain.Description));
                //strBuild.AppendLine(("let Address = \"{#Address}\"").Replace("{#Address}", scMain.Address));
                strBuild.AppendLine(("let MinterAddress = \"{#MinterAddress}\"").Replace("{#MinterAddress}", scMain.MinterAddress));
                strBuild.AppendLine(("let MinterName = \"{#MinterName}\"").Replace("{#MinterName}", scMain.MinterName));
                strBuild.AppendLine(("let SmartContractUID = \"" + scUID + "\""));
                //strBuild.AppendLine(("let Signature = \"" + signature + "\""));
                strBuild.AppendLine(("let Features = \"" + features + "\""));

                //NFT asset Data
                //strBuild.AppendLine(("let Extension = \"" + scAsset.Extension + "\""));
                strBuild.AppendLine(("let FileSize = \"" + scAsset.FileSize.ToString() + "\""));
                //strBuild.AppendLine(("let Location = \"" + scAsset.Location + "\""));
                strBuild.AppendLine(("let FileName = \"" + scAsset.Name + "\""));
                strBuild.AppendLine(("let AssetAuthorName = \"" + scAsset.AssetAuthorName + "\""));

                strBuild.AppendLine("function NftMain(data : string) : string");
                strBuild.AppendLine("{");
                strBuild.AppendLine(@"  if data == ""nftdata""");
                strBuild.AppendLine("   {");
                strBuild.AppendLine("       return GetNFTData(Name, Description, MinterAddress)");
                strBuild.AppendLine("   }");
                strBuild.AppendLine(@"  else if data == ""getnftassetdata""");
                strBuild.AppendLine("   {");
                strBuild.AppendLine("       return GetNFTAssetData(FileName, FileSize, AssetAuthorName)");
                strBuild.AppendLine("   }");
                if(featuresList != null)
                {
                    if (featuresList.Exists(x => x.FeatureName == FeatureName.Royalty))
                    {
                        strBuild.AppendLine(@"  else if data == ""getroyaltydata""");
                        strBuild.AppendLine("   {");
                        strBuild.AppendLine("       return GetRoyaltyData(RoyaltyType, RoyaltyAmount, RoyaltyPayToAddress)");
                        strBuild.AppendLine("   }");
                    }
                }
                strBuild.AppendLine(@"  return ""No Method Named "" + data + "" was found.""");
                strBuild.AppendLine("}");

                //Returns NFT Main Data
                strBuild.AppendLine("function GetNFTData(name : string, desc : string, mintAddr: string) : string");
                strBuild.AppendLine("{");
                strBuild.AppendLine("   return name + " + appendChar + " + desc + " + appendChar + " + mintAddr");
                strBuild.AppendLine("}");

                //Returns NFT Asset Data
                strBuild.AppendLine("function GetNFTAssetData(fileName : string, fileSize : string, assetAuthor : string) : string");
                strBuild.AppendLine("{");
                strBuild.AppendLine("   return (fileName + " + appendChar + " + fileSize + " + appendChar + " + assetAuthor)");
                strBuild.AppendLine("}");

                //Returns NFT SmartContractUID
                strBuild.AppendLine("function GetNFTId() : string");
                strBuild.AppendLine("{");
                strBuild.AppendLine("   return SmartContractUID");
                strBuild.AppendLine("}");

                //Return NFT Features
                strBuild.AppendLine("function GetNFTFeatures() : string");
                strBuild.AppendLine("{");
                strBuild.AppendLine("   return Features");
                strBuild.AppendLine("}");

                if (featuresList != null)
                {
                    if (featuresList.Exists(x => x.FeatureName == FeatureName.Royalty))
                    {
                        strBuild.Append(strRoyaltyBld);
                    }
                    if (featuresList.Exists(x => x.FeatureName == FeatureName.Evolving))
                    {
                        strBuild.Append(strEvolveBld);
                    }
                    if (featuresList.Exists(x => x.FeatureName == FeatureName.MultiAsset))
                    {
                        strBuild.Append(strMultiAssetBld);
                    }
                }

                var scText = strBuild.ToString();

                return (scText, scMain);
            }
            catch(Exception ex)
            {
                return (null, scMain);
            }
        }
    }
}
