using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.SmartContractSourceGenerator;
using ReserveBlockCore.Trillium;
using ReserveBlockCore.Utilities;
using System.Text;

namespace ReserveBlockCore.Services
{
    public static class SmartContractWriterService
    {

        #region Write Smart Contract for new creation
        public static async Task<(string, SmartContractMain, bool)> WriteSmartContract(SmartContractMain scMain)
        {
            var scUID = Guid.NewGuid().ToString().Replace("-", "") + ":" + TimeUtil.GetTime().ToString();
            var features = "";
            var featuresList = scMain.Features;
            var hash = ""; //create hash
            string scText = "";
            var isToken = false;

            bool isDynamic = false;

            StringBuilder strRoyaltyBld = new StringBuilder();
            StringBuilder strEvolveBld = new StringBuilder();
            StringBuilder strMultiAssetBld = new StringBuilder();
            StringBuilder strTokenBld = new StringBuilder();
            StringBuilder strTokenizationBld = new StringBuilder();

            scMain.SmartContractUID = string.IsNullOrEmpty(scMain.SmartContractUID) ? scUID : scMain.SmartContractUID;
            scMain.IsMinter = true;
            scMain.MinterAddress = scMain.MinterAddress;
            scMain.IsPublished = false;

            var appendChar = "\"|->\"";
            var propertyAppendChat = "<|>";

            var scAsset = scMain.SmartContractAsset;

            //Save to local folder here for beacon transfer later
            if(!scAsset.Location.Contains("Asset Folder"))
            {
                var result = NFTAssetFileUtility.MoveAsset(scAsset.Location, scAsset.Name, scMain.SmartContractUID);
                if (result == false)
                {
                    return ("Failed to save smart contract asset. Please try again.", scMain, isToken);
                }
            }
            
            StringBuilder strBuild = new StringBuilder();

            try
            {
                if (featuresList != null)
                {
                    var Flist = new List<SmartContractFeatures>();
                    if (featuresList.Count == 1) // Just 1 feature //
                    {
                        var feature = featuresList.First();
                        features = ((int)feature.FeatureName).ToString();
                        Flist.Add(feature);
                        if (feature.FeatureName == FeatureName.Royalty)
                        {
                            var royalty = ((JObject)feature.FeatureFeatures).ToObject<RoyaltyFeature>();
                            feature.FeatureFeatures = royalty;

                            //create royalty code block
                            var royaltySource = await RoyaltySourceGenerator.Build(royalty);
                            strBuild = royaltySource.Item1;
                            strRoyaltyBld = royaltySource.Item2;
                        }
                        else if (feature.FeatureName == FeatureName.Evolving)
                        {
                            var evolve = JsonConvert.DeserializeObject<List<EvolvingFeature>>(feature.FeatureFeatures.ToString());
                            if (evolve != null)
                            {
                                feature.FeatureFeatures = evolve;
                                var maxEvoState = evolve.Count().ToString();
                                var evolutionaryState = "\"{*0}\"";

                                var evolveSource = await EvolveSourceGenerator.Build(evolve, strBuild, scUID);
                                strBuild = evolveSource.Item1;
                                strEvolveBld = evolveSource.Item2;
                                if (strBuild.ToString() == "Failed")
                                {
                                    return ("Failed to save smart contract asset for Evolving Asset. Please try again.", scMain, isToken);
                                }
                            }
                        }
                        else if (feature.FeatureName == FeatureName.MultiAsset)
                        {
                            var multiAsset = JsonConvert.DeserializeObject<List<MultiAssetFeature>>(feature.FeatureFeatures.ToString());
                            if (multiAsset != null)
                            {
                                int counter = 1;
                                feature.FeatureFeatures = multiAsset;

                                var multiAssetSource = await MultiAssetSourceGenerator.Build(multiAsset, strBuild, scUID);
                                strBuild = multiAssetSource.Item1;
                                strMultiAssetBld = multiAssetSource.Item2;

                                if (strBuild.ToString() == "Failed")
                                {
                                    return ("Failed to save smart contract asset for Multi-Asset. Please try again.", scMain, isToken);
                                }
                            }
                        }
                        else if (feature.FeatureName == FeatureName.Token)
                        {
                            var token = ((JObject)feature.FeatureFeatures).ToObject<TokenFeature>();
                            if (token != null)
                            {
                                feature.FeatureFeatures = token;

                                var imageBase = token.TokenImageURL == null && token.TokenImageBase == null ? TokenSourceGenerator.DefaultImageBase64 : token.TokenImageBase;
                                token.TokenImageBase = imageBase;

                                var tokenSource = await TokenSourceGenerator.Build(token, strBuild);
                                strBuild = tokenSource.Item1;
                                strTokenBld = tokenSource.Item2;
                                isToken = true;
                                scMain.IsToken = true;
                            }
                            if (strBuild.ToString() == "Failed")
                            {
                                return ("Failed to save smart contract asset for Token. Please try again.", scMain, isToken);
                            }
                        }
                        else if(feature.FeatureName == FeatureName.Tokenization)
                        {
                            var tokenization = ((JObject)feature.FeatureFeatures).ToObject<TokenizationFeature>();
                            if(tokenization != null)
                            {
                                feature.FeatureFeatures = tokenization;
                                var tokenizationSource = await TokenizationSourceGenerator.Build(tokenization, strBuild);
                                strBuild = tokenizationSource.Item1;
                                strTokenizationBld = tokenizationSource.Item2;
                            }
                            if (strBuild.ToString() == "Failed")
                            {
                                return ("Failed to save smart contract asset for Tokenization. Please try again.", scMain, isToken);
                            }
                        }
                        else
                        {
                            //do nothing
                        }

                    }
                    else // there is more than 1 feature //
                    {
                        int count = 1;
                        int featureCount = featuresList.Count();

                        foreach (var x in featuresList)
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
                                var royalty = ((JObject)x.FeatureFeatures).ToObject<RoyaltyFeature>();
                                x.FeatureFeatures = royalty;

                                Flist.Add(x);
                                var royaltySource = await RoyaltySourceGenerator.Build(royalty);
                                strBuild = royaltySource.Item1;
                                strRoyaltyBld = royaltySource.Item2;
                            }

                            if (x.FeatureName == FeatureName.Evolving)
                            {
                                var evolve = JsonConvert.DeserializeObject<List<EvolvingFeature>>(x.FeatureFeatures.ToString());
                                if (evolve != null)
                                {
                                    x.FeatureFeatures = evolve;
                                    Flist.Add(x);

                                    var maxEvoState = evolve.Count().ToString();
                                    var evolutionaryState = "\"{*0}\"";

                                    var evolveSource = await EvolveSourceGenerator.Build(evolve, strBuild, scUID);
                                    strBuild = evolveSource.Item1;
                                    strEvolveBld = evolveSource.Item2;

                                    if (strBuild.ToString() == "Failed")
                                    {
                                        return ("Failed to save smart contract asset for Evolving Asset. Please try again.", scMain, isToken);
                                    }
                                }

                            }

                            if (x.FeatureName == FeatureName.MultiAsset)
                            {
                                var multiAsset = JsonConvert.DeserializeObject<List<MultiAssetFeature>>(x.FeatureFeatures.ToString());
                                if (multiAsset != null)
                                {
                                    int counter = 1;
                                    x.FeatureFeatures = multiAsset;
                                    Flist.Add(x);

                                    var multiAssetSource = await MultiAssetSourceGenerator.Build(multiAsset, strBuild, scUID);
                                    strBuild = multiAssetSource.Item1;
                                    strMultiAssetBld = multiAssetSource.Item2;

                                    if (strBuild.ToString() == "Failed")
                                    {
                                        return ("Failed to save smart contract asset for Multi-Asset. Please try again.", scMain, isToken);
                                    }
                                }
                            }

                            if (x.FeatureName == FeatureName.Token)
                            {
                                var token = ((JObject)x.FeatureFeatures).ToObject<TokenFeature>();
                                if (token != null)
                                {
                                    x.FeatureFeatures = token;
                                    var tokenSource = await TokenSourceGenerator.Build(token, strBuild);
                                    strBuild = tokenSource.Item1;
                                    strTokenBld = tokenSource.Item2;
                                    isToken = true;
                                    scMain.IsToken = true;
                                }
                                if (strBuild.ToString() == "Failed")
                                {
                                    return ("Failed to save smart contract asset for Multi-Asset. Please try again.", scMain, isToken);
                                }
                            }

                            if(x.FeatureName == FeatureName.Tokenization)
                            {
                                var tokenization = ((JObject)x.FeatureFeatures).ToObject<TokenizationFeature>();
                                if(tokenization != null)
                                {
                                    x.FeatureFeatures = tokenization;
                                    var tokenizationSource = await TokenizationSourceGenerator.Build(tokenization, strBuild);
                                    strBuild = tokenizationSource.Item1;
                                    strTokenizationBld = tokenizationSource.Item2;
                                }
                                if (strBuild.ToString() == "Failed")
                                {
                                    return ("Failed to save smart contract asset for Multi-Asset. Please try again.", scMain, isToken);
                                }
                            }

                        }
                    }
                    scMain.Features = Flist;

                }


                //NFT Main Data
                strBuild.AppendLine(("let Name = \"{#NFTName}\"").Replace("{#NFTName}", scMain.Name));
                strBuild.AppendLine(("let Description = \"{#Description}\"").Replace("{#Description}", scMain.Description));
                strBuild.AppendLine(("let MinterAddress = \"{#MinterAddress}\"").Replace("{#MinterAddress}", scMain.MinterAddress));
                strBuild.AppendLine(("let MinterName = \"{#MinterName}\"").Replace("{#MinterName}", scMain.MinterName));
                strBuild.AppendLine(("let SmartContractUID = \"" + scUID + "\""));
                strBuild.AppendLine(("let Features = \"" + features + "\""));
                strBuild.AppendLine(("let SCVersion = " + scMain.SCVersion.ToString()));

                //NFT asset Data
                strBuild.AppendLine(("let FileSize = \"" + scAsset.FileSize.ToString() + "\""));
                strBuild.AppendLine(("let FileName = \"" + scAsset.Name + "\""));
                strBuild.AppendLine(("let AssetAuthorName = \"" + scAsset.AssetAuthorName + "\""));

                if (scMain.Properties != null)
                {
                    strBuild.AppendLine("let Properties = \"" + scMain.Properties.ToTrilliumStringFromDict() + "\"");
                }

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
                if (featuresList != null)
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
                    if (featuresList.Exists(x => x.FeatureName == FeatureName.Token))
                    {
                        strBuild.Append(strTokenBld);
                    }
                    if (featuresList.Exists(x => x.FeatureName == FeatureName.Tokenization))
                    {
                        strBuild.Append(strTokenizationBld);
                    }
                }

                scText = strBuild.ToString();

            }
            catch(Exception ex)
            {
                SCLogUtility.Log($"Error Writing Smart Contract: {scMain.SmartContractUID}. Error Message: {ex.ToString()}", 
                    "SmartContractWriterService.WriteSmartContract(SmartContractMain scMain)");
            }


            return (scText, scMain, isToken);

        }

        #endregion

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        #region WriteSmartContractFromTX
        public static async Task<(string, SmartContractMain)> WriteSmartContractFromTX(SmartContractMain scMain)
        {
            //var scUID = Guid.NewGuid().ToString().Replace("-", "") + ":" + TimeUtil.GetTime().ToString();
            var features = "";
            var featuresList = scMain.Features;
            var signature = "Insert Signature";
            StringBuilder strRoyaltyBld = new StringBuilder();
            StringBuilder strEvolveBld = new StringBuilder();
            StringBuilder strMultiAssetBld = new StringBuilder();

            scMain.IsMinter = false;
            scMain.IsPublished = true;

            var appendChar = "\"|->\"";

            //var featureFeaturesList = List<SmartContract>

            var scAsset = scMain.SmartContractAsset;
            StringBuilder strBuild = new StringBuilder();

            if (featuresList != null)
            {
                var Flist = new List<SmartContractFeatures>();
                if (featuresList.Count == 1)
                {
                    var feature = featuresList.First();
                    features = ((int)feature.FeatureName).ToString();
                    Flist.Add(feature);
                    if (feature.FeatureName == FeatureName.Royalty)
                    {
                        var royalty = (RoyaltyFeature)feature.FeatureFeatures;
                        feature.FeatureFeatures = royalty;

                        //create royalty code block
                        var royaltySource = await RoyaltySourceGenerator.Build(royalty);
                        strBuild = royaltySource.Item1;
                        strRoyaltyBld = royaltySource.Item2;
                    }
                    else if (feature.FeatureName == FeatureName.Evolving)
                    {
                        var evolve = (List<EvolvingFeature>)feature.FeatureFeatures;
                        if (evolve != null)
                        {
                            feature.FeatureFeatures = evolve;
                            var maxEvoState = evolve.Count().ToString();
                            var evolutionaryState = "\"{*0}\"";

                            var evolveSource = await EvolveSourceGenerator.Build(evolve, strBuild, scMain.SmartContractUID);
                            strBuild = evolveSource.Item1;
                            strEvolveBld = evolveSource.Item2;
                        }
                    }
                    else if (feature.FeatureName == FeatureName.MultiAsset)
                    {
                        var multiAsset = (List<MultiAssetFeature>)feature.FeatureFeatures;
                        if (multiAsset != null)
                        {
                            int counter = 1;
                            feature.FeatureFeatures = multiAsset;

                            var multiAssetSource = await MultiAssetSourceGenerator.Build(multiAsset, strBuild, scMain.SmartContractUID);
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
                            var royalty = (RoyaltyFeature)x.FeatureFeatures;
                            x.FeatureFeatures = royalty;

                            Flist.Add(x);
                            //create royalty code block
                            var royaltySource = await RoyaltySourceGenerator.Build(royalty);
                            strBuild = royaltySource.Item1;
                            strRoyaltyBld = royaltySource.Item2;
                        }

                        if (x.FeatureName == FeatureName.Evolving)
                        {
                            var evolve = (List<EvolvingFeature>)x.FeatureFeatures;
                            if (evolve != null)
                            {
                                x.FeatureFeatures = evolve;
                                Flist.Add(x);

                                var maxEvoState = evolve.Count().ToString();
                                var evolutionaryState = "\"{*0}\"";

                                var evolveSource = await EvolveSourceGenerator.Build(evolve, strBuild, scMain.SmartContractUID);
                                strBuild = evolveSource.Item1;
                                strEvolveBld = evolveSource.Item2;
                            }

                        }

                        if (x.FeatureName == FeatureName.MultiAsset)
                        {
                            var multiAsset = (List<MultiAssetFeature>)x.FeatureFeatures;
                            if (multiAsset != null)
                            {
                                int counter = 1;
                                x.FeatureFeatures = multiAsset;
                                Flist.Add(x);

                                var multiAssetSource = await MultiAssetSourceGenerator.Build(multiAsset, strBuild, scMain.SmartContractUID);
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
            strBuild.AppendLine(("let SmartContractUID = \"" + scMain.SmartContractUID + "\""));
            //strBuild.AppendLine(("let Signature = \"" + signature + "\""));
            strBuild.AppendLine(("let Features = \"" + features + "\""));

            //NFT asset Data
            //strBuild.AppendLine(("let Extension = \"" + scAsset.Extension + "\""));
            strBuild.AppendLine(("let FileSize = \"" + scAsset.FileSize.ToString() + "\""));
            //strBuild.AppendLine(("let Location = \"" + scAsset.Location + "\""));
            strBuild.AppendLine(("let FileName = \"" + scAsset.Name + "\""));
            strBuild.AppendLine(("let AssetAuthorName = \"" + scAsset.AssetAuthorName + "\""));

            if (scMain.Properties != null)
            {
                strBuild.AppendLine("let Properties = \"" + scMain.Properties.ToTrilliumStringFromDict() + "\"");
            }

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
            if (featuresList != null)
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

        #endregion
    }
}
