using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.Utilities;
using System.Text;

namespace ReserveBlockCore.Services
{
    public static class SmartContractReaderService
    {
        public static async Task<(string, SmartContractMain)> ReadSmartContract(SmartContractMain scMain)
        {
            var scUID = scMain.SmartContractUID;
            var features = "";
            var featuresList = scMain.Features;
            var signature = scMain.Signature;
            StringBuilder strRoyaltyBld = new StringBuilder();
            StringBuilder strEvolveBld = new StringBuilder();

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
                        strBuild.AppendLine("let RoyaltyType = \"" + ((int)royalty.RoyaltyType).ToString() + "\"");
                        strBuild.AppendLine("let RoyaltyAmount = \"" + royalty.RoyaltyAmount.ToString() + "\"");
                        strBuild.AppendLine("let RoyaltyPayToAddress = \"" + royalty.RoyaltyPayToAddress + "\"");

                        strRoyaltyBld.AppendLine("function GetRoyaltyData(royaltyType  : string, royaltyAmount : string, royaltyPayToAddress : string) : string");
                        strRoyaltyBld.AppendLine("{");
                        strRoyaltyBld.AppendLine("return (royaltyType + " + appendChar + " + royaltyAmount + " + appendChar + " + royaltyPayToAddress)");
                        strRoyaltyBld.AppendLine("}");
                    }
                    else if (feature.FeatureName == FeatureName.Evolving)
                    {
                        List<EvolvingFeature> evolve = new List<EvolvingFeature>();
                        var myArray = ((object[])feature.FeatureFeatures).ToList();

                        var count = 0;
                        myArray.ForEach(x => {
                            var evolveDict = (Dictionary<string, object>)myArray[count];
                            EvolvingFeature evoFeature = new EvolvingFeature
                            {
                                Name = evolveDict["Name"].ToString(),
                                Description = evolveDict["Description"].ToString(),
                                EvolutionState = (int)evolveDict["EvolutionState"],
                                IsDynamic = (bool)evolveDict["IsDynamic"],
                                IsCurrentState = (bool)evolveDict["IsCurrentState"],
                                EvolveDate = evolveDict.ContainsKey("EvolveDate") == true ? (DateTime)evolveDict["EvolveDate"] : null,
                                SmartContractAsset = evolveDict.ContainsKey("SmartContractAsset") == true ? (SmartContractAsset)evolveDict["SmartContractAsset"] : null

                            };
                            count += 1;
                            evolve.Add(evoFeature);
                        });

                        if (evolve.Count() > 0)
                        {
                            feature.FeatureFeatures = evolve;

                            Flist.Add(feature);
                            var maxEvoState = evolve.Count().ToString();
                            var evolutionaryState = "\"{*0}\"";

                            //Evolve Constants
                            strBuild.AppendLine("var EvolutionaryState = " + evolutionaryState);
                            strBuild.AppendLine("let EvolutionaryMaxState = \"" + maxEvoState + "\"");

                            //Methods
                            //Get Current Evolve State Method
                            strEvolveBld.AppendLine("function GetCurrentEvolveState() : string");
                            strEvolveBld.AppendLine("{");
                            strEvolveBld.AppendLine("var evoState = EvolutionaryState");
                            strEvolveBld.AppendLine("return evoState");
                            strEvolveBld.AppendLine("}");

                            //Get Evolve States
                            strEvolveBld.AppendLine("function EvolveStates() : string");
                            strEvolveBld.AppendLine("{");
                            strEvolveBld.AppendLine(@"return ""Coming Soon""");
                            strEvolveBld.AppendLine("}");

                            //Evolve
                            strEvolveBld.AppendLine("function Evolve(evoState : int) : string");
                            strEvolveBld.AppendLine("{");
                            strEvolveBld.AppendLine("if evoState < int(EvolutionaryMaxState)");
                            strEvolveBld.AppendLine("{");
                            strEvolveBld.AppendLine("var newEvolveState = evoState + 1");
                            strEvolveBld.AppendLine("if(newEvolveState > int(EvolutionaryMaxState))");
                            strEvolveBld.AppendLine("{");
                            strEvolveBld.AppendLine(@"return ""Failed to Evolve.""");
                            strEvolveBld.AppendLine("}");
                            strEvolveBld.AppendLine(@"EvolutionaryState = ""{*"" + string(newEvolveState) + ""}""");
                            strEvolveBld.AppendLine("return string(newEvolveState)");
                            strEvolveBld.AppendLine("}");
                            strEvolveBld.AppendLine(@"return ""Failed to Evolve.""");
                            strEvolveBld.AppendLine("}");

                            //Devolve
                            strEvolveBld.AppendLine("function Devolve(evoState : int) : string");
                            strEvolveBld.AppendLine("{");
                            strEvolveBld.AppendLine("if evoState > 0");
                            strEvolveBld.AppendLine("{");
                            strEvolveBld.AppendLine("var newEvolveState = evoState - 1");
                            strEvolveBld.AppendLine("if(newEvolveState < 0)");
                            strEvolveBld.AppendLine("{");
                            strEvolveBld.AppendLine(@"return ""Failed to Devolve.""");
                            strEvolveBld.AppendLine("}");
                            strEvolveBld.AppendLine(@"EvolutionaryState = ""{*"" + string(newEvolveState) + ""}""");
                            strEvolveBld.AppendLine("return string(newEvolveState)");
                            strEvolveBld.AppendLine("}");
                            strEvolveBld.AppendLine(@"return ""Failed to Devolve.""");
                            strEvolveBld.AppendLine("}");

                            //Evolve Specific
                            strEvolveBld.AppendLine("function ChangeEvolveStateSpecific(evoState : int) : string");
                            strEvolveBld.AppendLine("{");
                            strEvolveBld.AppendLine("if evoState <= int(EvolutionaryMaxState) && evoState >= 0");
                            strEvolveBld.AppendLine("{");
                            strEvolveBld.AppendLine(@"EvolutionaryState = ""{*"" + string(evoState) + ""}""");
                            strEvolveBld.AppendLine("return string(evoState)");
                            strEvolveBld.AppendLine("}");
                            strEvolveBld.AppendLine(@"return ""Failed to Evolve.""");
                            strEvolveBld.AppendLine("}");

                            int counter = 1;
                            evolve.ForEach(x =>
                            {
                                var evoLetter = EvolveStateUtility.GetEvolveStateLetter(x.EvolutionState);
                                strEvolveBld.AppendLine("function EvolveState" + evoLetter + "() : string");
                                strEvolveBld.AppendLine("{");
                                strEvolveBld.AppendLine(@"var evoState = " + "\"" + x.EvolutionState.ToString() + "\"");
                                strEvolveBld.AppendLine(@"var name = " + "\"" + x.Name + "\"");
                                strEvolveBld.AppendLine(@"var description = " + "\"" + x.Description + "\"");
                                strEvolveBld.AppendLine(@"var assetName = " + "\"" + (x.SmartContractAsset == null ? "" : x.SmartContractAsset.Name) + "\"");
                                strEvolveBld.AppendLine(@"var evolveDate = " + "\"" + (x.EvolveDate == null ? "" : x.EvolveDate.Value.Ticks.ToString()) + "\"");
                                strEvolveBld.AppendLine("return (evoState + " + appendChar + " + name + " + appendChar + " + description + " + appendChar + " + assetName + " + appendChar + " + evolveDate)");
                                strEvolveBld.AppendLine("}");

                                counter += 1;
                            });
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
                    featuresList.ForEach(x =>
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
                            strBuild.AppendLine("let RoyaltyType = \"" + ((int)royalty.RoyaltyType).ToString() + "\"");
                            strBuild.AppendLine("let RoyaltyAmount = \"" + royalty.RoyaltyAmount.ToString() + "\"");
                            strBuild.AppendLine("let RoyaltyPayToAddress = \"" + royalty.RoyaltyPayToAddress + "\"");

                            strRoyaltyBld.AppendLine("function GetRoyaltyData(royaltyType  : string, royaltyAmount : string, royaltyPayToAddress : string) : string");
                            strRoyaltyBld.AppendLine("{");
                            strRoyaltyBld.AppendLine("return (royaltyType + " + appendChar + " + royaltyAmount + " + appendChar + " + royaltyPayToAddress)");
                            strRoyaltyBld.AppendLine("}");
                        }

                        if (x.FeatureName == FeatureName.Evolving)
                        {

                            List<EvolvingFeature> evolve = new List<EvolvingFeature>();
                            var myArray = ((object[])x.FeatureFeatures).ToList();

                            var count = 0;
                            myArray.ForEach(x => {
                                var evolveDict = (Dictionary<string, object>)myArray[count];
                                EvolvingFeature evoFeature = new EvolvingFeature {
                                    Name = evolveDict["Name"].ToString(),
                                    Description = evolveDict["Description"].ToString(),
                                    EvolutionState = (int)evolveDict["EvolutionState"],
                                    IsDynamic = (bool)evolveDict["IsDynamic"],
                                    IsCurrentState = (bool)evolveDict["IsCurrentState"],
                                    EvolveDate = evolveDict.ContainsKey("EvolveDate") == true ? (DateTime)evolveDict["EvolveDate"] : null,
                                    SmartContractAsset = evolveDict.ContainsKey("SmartContractAsset") == true ? (SmartContractAsset)evolveDict["SmartContractAsset"] : null
                                    
                                };
                                count += 1;
                                evolve.Add(evoFeature);
                            });

                            if (evolve.Count() > 0)
                            {
                                x.FeatureFeatures = evolve;
                                Flist.Add(x);
                                var maxEvoState = evolve.Count().ToString();
                                var evolutionaryState = "\"{*0}\"";

                                //Evolve Constants
                                strBuild.AppendLine("var EvolutionaryState = " + evolutionaryState);
                                strBuild.AppendLine("let EvolutionaryMaxState = \"" + maxEvoState + "\"");

                                //Methods
                                //Get Current Evolve State Method
                                strEvolveBld.AppendLine("function GetCurrentEvolveState() : string");
                                strEvolveBld.AppendLine("{");
                                strEvolveBld.AppendLine("var evoState = EvolutionaryState");
                                strEvolveBld.AppendLine("return evoState");
                                strEvolveBld.AppendLine("}");

                                //Get Evolve States
                                strEvolveBld.AppendLine("function EvolveStates() : string");
                                strEvolveBld.AppendLine("{");
                                strEvolveBld.AppendLine(@"return ""Coming Soon""");
                                strEvolveBld.AppendLine("}");

                                //Evolve
                                strEvolveBld.AppendLine("function Evolve(evoState : int) : string");
                                strEvolveBld.AppendLine("{");
                                strEvolveBld.AppendLine("if evoState < int(EvolutionaryMaxState)");
                                strEvolveBld.AppendLine("{");
                                strEvolveBld.AppendLine("var newEvolveState = evoState + 1");
                                strEvolveBld.AppendLine("if(newEvolveState > int(EvolutionaryMaxState))");
                                strEvolveBld.AppendLine("{");
                                strEvolveBld.AppendLine(@"return ""Failed to Evolve.""");
                                strEvolveBld.AppendLine("}");
                                strEvolveBld.AppendLine(@"EvolutionaryState = ""{*"" + string(newEvolveState) + ""}""");
                                strEvolveBld.AppendLine("return string(newEvolveState)");
                                strEvolveBld.AppendLine("}");
                                strEvolveBld.AppendLine(@"return ""Failed to Evolve.""");
                                strEvolveBld.AppendLine("}");

                                //Devolve
                                strEvolveBld.AppendLine("function Devolve(evoState : int) : string");
                                strEvolveBld.AppendLine("{");
                                strEvolveBld.AppendLine("if evoState > 0");
                                strEvolveBld.AppendLine("{");
                                strEvolveBld.AppendLine("var newEvolveState = evoState - 1");
                                strEvolveBld.AppendLine("if(newEvolveState < 0)");
                                strEvolveBld.AppendLine("{");
                                strEvolveBld.AppendLine(@"return ""Failed to Devolve.""");
                                strEvolveBld.AppendLine("}");
                                strEvolveBld.AppendLine(@"EvolutionaryState = ""{*"" + string(newEvolveState) + ""}""");
                                strEvolveBld.AppendLine("return string(newEvolveState)");
                                strEvolveBld.AppendLine("}");
                                strEvolveBld.AppendLine(@"return ""Failed to Devolve.""");
                                strEvolveBld.AppendLine("}");

                                //Evolve Specific
                                strEvolveBld.AppendLine("function ChangeEvolveStateSpecific(evoState : int) : string");
                                strEvolveBld.AppendLine("{");
                                strEvolveBld.AppendLine("if evoState <= int(EvolutionaryMaxState) && evoState >= 0");
                                strEvolveBld.AppendLine("{");
                                strEvolveBld.AppendLine(@"EvolutionaryState = ""{*"" + string(evoState) + ""}""");
                                strEvolveBld.AppendLine("return string(evoState)");
                                strEvolveBld.AppendLine("}");
                                strEvolveBld.AppendLine(@"return ""Failed to Evolve.""");
                                strEvolveBld.AppendLine("}");

                                int counter = 1;
                                evolve.ForEach(x =>
                                {
                                    var evoLetter = EvolveStateUtility.GetEvolveStateLetter(x.EvolutionState);
                                    strEvolveBld.AppendLine("function EvolveState" + evoLetter + "() : string");
                                    strEvolveBld.AppendLine("{");
                                    strEvolveBld.AppendLine(@"var evoState = " + "\"" + x.EvolutionState.ToString() + "\"");
                                    strEvolveBld.AppendLine(@"var name = " + "\"" + x.Name + "\"");
                                    strEvolveBld.AppendLine(@"var description = " + "\"" + x.Description + "\"");
                                    strEvolveBld.AppendLine(@"var assetName = " + "\"" + (x.SmartContractAsset == null ? "" : x.SmartContractAsset.Name) + "\"");
                                    strEvolveBld.AppendLine(@"var evolveDate = " + "\"" + (x.EvolveDate == null ? "" : x.EvolveDate.Value.Ticks.ToString()) + "\"");
                                    strEvolveBld.AppendLine("return (evoState + " + appendChar + " + name + " + appendChar + " + description + " + appendChar + " + assetName + " + appendChar + " + evolveDate)");
                                    strEvolveBld.AppendLine("}");

                                    counter += 1;
                                });
                            }

                        }

                    });
                }
                scMain.Features = Flist;

            }


            //NFT Main Data
            strBuild.AppendLine(("let Name = \"{#NFTName}\"").Replace("{#NFTName}", scMain.Name));
            strBuild.AppendLine(("let Description = \"{#Description}\"").Replace("{#Description}", scMain.Description));
            strBuild.AppendLine(("let Address = \"{#Address}\"").Replace("{#Address}", scMain.Address));
            strBuild.AppendLine(("let MinterAddress = \"{#MinterAddress}\"").Replace("{#MinterAddress}", scMain.MinterAddress));
            strBuild.AppendLine(("let SmartContractUID = \"" + scUID + "\""));
            strBuild.AppendLine(("let Signature = \"" + signature + "\""));
            strBuild.AppendLine(("let Features = \"" + features + "\""));

            //NFT asset Data
            strBuild.AppendLine(("let Extension = \"" + scAsset.Extension + "\""));
            strBuild.AppendLine(("let FileSize = \"" + scAsset.FileSize.ToString() + "\""));
            strBuild.AppendLine(("let Location = \"" + scAsset.Location + "\""));
            strBuild.AppendLine(("let FileName = \"" + scAsset.Name + "\""));
            strBuild.AppendLine("function NftMain(data : string) : string");
            strBuild.AppendLine("{");
            strBuild.AppendLine(@"if data == ""nftdata""");
            strBuild.AppendLine("{");
            strBuild.AppendLine("return GetNFTData(Name, Description, Address)");
            strBuild.AppendLine("}");
            strBuild.AppendLine(@"else if data == ""getnftassetdata""");
            strBuild.AppendLine("{");
            strBuild.AppendLine("return GetNFTAssetData(FileName, Location, FileSize, Extension)");
            strBuild.AppendLine("}");
            if (featuresList.Exists(x => x.FeatureName == FeatureName.Royalty))
            {
                strBuild.AppendLine(@"else if data == ""getroyaltydata""");
                strBuild.AppendLine("{");
                strBuild.AppendLine("return GetRoyaltyData(RoyaltyType, RoyaltyAmount, RoyaltyPayToAddress)");
                strBuild.AppendLine("}");
            }
            strBuild.AppendLine(@"return ""No Method Named "" + data + "" was found.""");
            strBuild.AppendLine("}");
            strBuild.AppendLine("function GetNFTData(name : string, desc : string, addr : string) : string");
            strBuild.AppendLine("{");
            strBuild.AppendLine("return name + " + appendChar + " + desc + " + appendChar + " + addr");
            strBuild.AppendLine("}");
            strBuild.AppendLine("function GetNFTAssetData(fileName : string, loc : string, fileSize : string, ext : string) : string");
            strBuild.AppendLine("{");
            strBuild.AppendLine("return (fileName + " + appendChar + " + loc + " + appendChar + " + fileSize + " + appendChar + " + ext)");
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
            }

            var scText = strBuild.ToString();

            return (scText, scMain);

        }
    }
}
