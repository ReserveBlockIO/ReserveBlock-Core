using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReserveBlockCore.Services;
using ReserveBlockCore.Trillium;
using ReserveBlockCore.Utilities;
using System.Text;

namespace ReserveBlockCore.Models.SmartContracts
{
    public class EvolvingFeature
    {
        public int EvolutionState { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsDynamic { get; set; } 
        public bool IsCurrentState { get; set; }
        public DateTime? EvolveDate { get; set; }
        public long? EvolveBlockHeight { get; set; }
        public SmartContractAsset? SmartContractAsset { get; set; }

        #region GetEvolveFeature

        public static List<EvolvingFeature> GetEvolveFeature(List<string> eList, int? currentEvoState = null)
        {
            List<EvolvingFeature> evolveFeatures = new List<EvolvingFeature>();

            eList.ForEach(x => {
                EvolvingFeature evolveFeature = new EvolvingFeature();

                var evArray = x.Split(new string[] { "|->" }, StringSplitOptions.None);
                
                evolveFeature.EvolutionState = Convert.ToInt32(evArray[0].ToString());
                evolveFeature.Name = evArray[1].ToString();
                evolveFeature.Description = evArray[2].ToString();
                if (currentEvoState != null)
                {
                    if(evolveFeature.EvolutionState == currentEvoState)
                    {
                        evolveFeature.IsCurrentState = true;
                    }
                }
                //evolveFeature.EvolveDate = 
                if (evArray.Count() == 6)
                {
                    var blockHeight = evArray[5].ToString();
                    if (!string.IsNullOrWhiteSpace(blockHeight))
                    {
                        evolveFeature.EvolveBlockHeight = Convert.ToInt64(evArray[5].ToString());
                    }
                    else
                    {
                        evolveFeature.EvolveBlockHeight = null;
                    }
                }
                else
                {
                    evolveFeature.EvolveBlockHeight = null;
                }    
                

                //Custom vars
                var assetName = evArray[3].ToString();
                if(!string.IsNullOrWhiteSpace(assetName))
                {
                    SmartContractAsset scAsset = new SmartContractAsset { 
                        AssetAuthorName = "",
                        AssetId = Guid.NewGuid(),
                        Extension = "",
                        FileSize = 0,
                        Location = "",
                        Name = assetName,
                    };

                    evolveFeature.SmartContractAsset = scAsset;
                }
                else
                {
                    evolveFeature.SmartContractAsset = null;
                }    
                var evolveDateTicks = evArray[4].ToString();
                if(!string.IsNullOrWhiteSpace(evolveDateTicks))
                {
                    DateTime myDate = new DateTime(Convert.ToInt64(evolveDateTicks));
                    evolveFeature.EvolveDate = myDate;
                }
                else
                {
                    evolveFeature.EvolveDate = null;
                }

                evolveFeatures.Add(evolveFeature);

            });

            return evolveFeatures;
        }

        #endregion

        #region GetNewEvolveState

        public static async Task<(bool, string)> GetNewEvolveState(string scText)
        {
            var byteArrayFromBase64 = scText.FromBase64ToByteArray();
            var decompressedByteArray = SmartContractUtility.Decompress(byteArrayFromBase64);
            var textFromByte = Encoding.Unicode.GetString(decompressedByteArray);

            var repl = new TrilliumRepl();
            repl.Run("#reset");
            repl.Run(textFromByte);

            var features = repl.Run(@"GetNFTFeatures()");
            if(features != null)
            {
                var featureList = features.Value.ToString();
                if(!string.IsNullOrWhiteSpace(featureList))
                {
                    if(featureList.Contains("0"))
                    {
                        var evolveState = repl.Run(@"GetCurrentEvolveState()").Value.ToString();
                        var evolveMaxState = repl.Run(@"EvolveStates()");

                        try
                        {
                            var currentEvoState = evolveState.Replace("{*", "").Replace("}", "");
                            var evolve = repl.Run(@"Evolve(" + currentEvoState + ")").Value.ToString();
                            if(evolve == "Failed to Evolve.")
                            {
                                return (false, "Failed to Evolve NFT");
                            }
                            var newEvolveState = repl.Run(@"GetCurrentEvolveState()").Value.ToString();

                            var newSCData = textFromByte.Replace(evolveState, newEvolveState);

                            return (true, newSCData);
                        }
                        catch(Exception ex)
                        {                            
                            return (false, "Failed to Evolve NFT");
                        }
                    }
                }
            }
            return (false, "Failed to Evolve NFT");
        }

        #endregion

        #region GetNewDevolveState
        public static async Task<(bool, string)> GetNewDevolveState(string scText)
        {
            var byteArrayFromBase64 = scText.FromBase64ToByteArray();
            var decompressedByteArray = SmartContractUtility.Decompress(byteArrayFromBase64);
            var textFromByte = Encoding.Unicode.GetString(decompressedByteArray);

            var repl = new TrilliumRepl();
            repl.Run("#reset");
            repl.Run(textFromByte);

            var features = repl.Run(@"GetNFTFeatures()");
            if (features != null)
            {
                var featureList = features.Value.ToString();
                if (!string.IsNullOrWhiteSpace(featureList))
                {
                    if (featureList.Contains("0"))
                    {
                        var evolveState = repl.Run(@"GetCurrentEvolveState()").Value.ToString();

                        try
                        {
                            var currentEvoState = evolveState.Replace("{*", "").Replace("}", "");
                            var evolve = repl.Run(@"Devolve(" + currentEvoState + ")").Value.ToString();
                            if (evolve == "Failed to Devolve.")
                            {
                                return (false, "Failed to Devolve NFT");
                            }
                            var newEvolveState = repl.Run(@"GetCurrentEvolveState()").Value.ToString();

                            var newSCData = textFromByte.Replace(evolveState, newEvolveState);

                            return (true, newSCData);
                        }
                        catch (Exception ex)
                        {
                            return (false, "Failed to Devolve NFT");
                        }
                    }
                }
            }
            return (false, "Failed to Devolve NFT");
        }

        #endregion

        #region GetNewSpecificState

        public static async Task<(bool, string)> GetNewSpecificState(string scText, int evoState)
        {
            var byteArrayFromBase64 = scText.FromBase64ToByteArray();
            var decompressedByteArray = SmartContractUtility.Decompress(byteArrayFromBase64);
            var textFromByte = Encoding.Unicode.GetString(decompressedByteArray);

            var repl = new TrilliumRepl();
            repl.Run("#reset");
            repl.Run(textFromByte);

            var features = repl.Run(@"GetNFTFeatures()");
            if (features != null)
            {
                var featureList = features.Value.ToString();
                if (!string.IsNullOrWhiteSpace(featureList))
                {
                    if (featureList.Contains("0"))
                    {
                        var evolveState = repl.Run(@"GetCurrentEvolveState()").Value.ToString();

                        try
                        {
                            var specEvoState = evoState.ToString();
                            var evolve = repl.Run(@"ChangeEvolveStateSpecific(" + specEvoState + ")").Value.ToString();
                            if (evolve == "Failed to Evolve.")
                            {
                                return (false, "Failed to Change State for NFT");
                            }
                            var newEvolveState = repl.Run(@"GetCurrentEvolveState()").Value.ToString();

                            var newSCData = textFromByte.Replace(evolveState, newEvolveState);

                            return (true, newSCData);
                        }
                        catch (Exception ex)
                        {
                            return (false, "Failed to Change State for NFT");
                        }
                    }
                }
            }
            return (false, "Failed to Change State for NFT");
        }

        #endregion

        #region EvolveNFT

        public static async void EvolveNFT(Transaction tx)
        {
            if (!string.IsNullOrWhiteSpace(tx.Data))
            {
                var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
                if (scDataArray != null)
                {
                    var scData = scDataArray[0];
                    var data = (string?)scData["Data"];
                    var scUID = (string?)scData["ContractUID"];

                    var byteArrayFromBase64 = data.FromBase64ToByteArray();
                    var decompressedByteArray = SmartContractUtility.Decompress(byteArrayFromBase64);
                    var textFromByte = Encoding.Unicode.GetString(decompressedByteArray);

                    var repl = new TrilliumRepl();
                    repl.Run("#reset");
                    repl.Run(textFromByte);

                    var scMain = SmartContractMain.SmartContractData.GetSmartContract(scUID);
                    if(scMain != null)
                    {
                        var evolveFeatures = scMain.Features.Where(x => x.FeatureName == FeatureName.Evolving).FirstOrDefault();
                        if (evolveFeatures != null)
                        {
                            try
                            {
                                var evolveState = repl.Run(@"GetCurrentEvolveState()");
                                var evoStateString = evolveState.Value.ToString().Replace("{*", "").Replace("}", "");
                                var evoStateNum = Convert.ToInt32(evoStateString);

                                if(evoStateNum > 0)
                                {
                                    SmartContractAsset evoAsset = new SmartContractAsset();
                                    List<EvolvingFeature> evolve = new List<EvolvingFeature>();
                                    var myArray = ((object[])evolveFeatures.FeatureFeatures).ToList();

                                    var newSCMain = await SmartContractReaderService.ReadSmartContract(scMain, evoStateNum);
                                    if(newSCMain.Item2 != null)
                                    {
                                        scMain.Features = newSCMain.Item2.Features;
                                        SmartContractMain.SmartContractData.UpdateSmartContract(scMain);
                                    }
                                    
                                }
                                else
                                {
                                    var newSCMain = await SmartContractReaderService.ReadSmartContract(scMain, null);
                                    if (newSCMain.Item2 != null)
                                    {
                                        scMain.Features = newSCMain.Item2.Features;
                                        SmartContractMain.SmartContractData.UpdateSmartContract(scMain);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {

                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region DevolveNFT
        public static async void DevolveNFT(Transaction tx)
        {
            if (!string.IsNullOrWhiteSpace(tx.Data))
            {
                var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
                if (scDataArray != null)
                {
                    var scData = scDataArray[0];
                    var data = (string?)scData["Data"];
                    var scUID = (string?)scData["ContractUID"];

                    var byteArrayFromBase64 = data.FromBase64ToByteArray();
                    var decompressedByteArray = SmartContractUtility.Decompress(byteArrayFromBase64);
                    var textFromByte = Encoding.Unicode.GetString(decompressedByteArray);

                    var repl = new TrilliumRepl();
                    repl.Run("#reset");
                    repl.Run(textFromByte);

                    var scMain = SmartContractMain.SmartContractData.GetSmartContract(scUID);
                    if (scMain != null)
                    {
                        var evolveFeatures = scMain.Features.Where(x => x.FeatureName == FeatureName.Evolving).FirstOrDefault();
                        if (evolveFeatures != null)
                        {
                            try
                            {
                                var evolveState = repl.Run(@"GetCurrentEvolveState()");
                                var evoStateString = evolveState.Value.ToString().Replace("{*", "").Replace("}", "");
                                var evoStateNum = Convert.ToInt32(evoStateString);

                                if (evoStateNum > 0)
                                {
                                    SmartContractAsset evoAsset = new SmartContractAsset();
                                    List<EvolvingFeature> evolve = new List<EvolvingFeature>();
                                    var myArray = ((object[])evolveFeatures.FeatureFeatures).ToList();

                                    var newSCMain = await SmartContractReaderService.ReadSmartContract(scMain, evoStateNum);
                                    if (newSCMain.Item2 != null)
                                    {
                                        scMain.Features = newSCMain.Item2.Features;
                                        SmartContractMain.SmartContractData.UpdateSmartContract(scMain);
                                    }

                                }
                                else
                                {
                                    var newSCMain = await SmartContractReaderService.ReadSmartContract(scMain, null);
                                    if (newSCMain.Item2 != null)
                                    {
                                        scMain.Features = newSCMain.Item2.Features;
                                        SmartContractMain.SmartContractData.UpdateSmartContract(scMain);
                                    }
                                }


                            }
                            catch (Exception ex)
                            {

                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region EvolveToSpecificStateNFT
        public static async void EvolveToSpecificStateNFT(Transaction tx)
        {
            if (!string.IsNullOrWhiteSpace(tx.Data))
            {
                var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
                if (scDataArray != null)
                {
                    var scData = scDataArray[0];
                    var data = (string?)scData["Data"];
                    var scUID = (string?)scData["ContractUID"];

                    var byteArrayFromBase64 = data.FromBase64ToByteArray();
                    var decompressedByteArray = SmartContractUtility.Decompress(byteArrayFromBase64);
                    var textFromByte = Encoding.Unicode.GetString(decompressedByteArray);

                    var repl = new TrilliumRepl();
                    repl.Run("#reset");
                    repl.Run(textFromByte);

                    var scMain = SmartContractMain.SmartContractData.GetSmartContract(scUID);
                    if (scMain != null)
                    {
                        var evolveFeatures = scMain.Features.Where(x => x.FeatureName == FeatureName.Evolving).FirstOrDefault();
                        if (evolveFeatures != null)
                        {
                            try
                            {
                                var evolveState = repl.Run(@"GetCurrentEvolveState()");
                                var evoStateString = evolveState.Value.ToString().Replace("{*", "").Replace("}", "");
                                var evoStateNum = Convert.ToInt32(evoStateString);

                                if (evoStateNum > 0)
                                {
                                    SmartContractAsset evoAsset = new SmartContractAsset();
                                    List<EvolvingFeature> evolve = new List<EvolvingFeature>();
                                    var myArray = ((object[])evolveFeatures.FeatureFeatures).ToList();

                                    var newSCMain = await SmartContractReaderService.ReadSmartContract(scMain, evoStateNum);
                                    if (newSCMain.Item2 != null)
                                    {
                                        scMain.Features = newSCMain.Item2.Features;
                                        SmartContractMain.SmartContractData.UpdateSmartContract(scMain);
                                    }

                                }
                                else
                                {
                                    var newSCMain = await SmartContractReaderService.ReadSmartContract(scMain, 0);
                                    if (newSCMain.Item2 != null)
                                    {
                                        scMain.Features = newSCMain.Item2.Features;
                                        SmartContractMain.SmartContractData.UpdateSmartContract(scMain);
                                    }
                                }


                            }
                            catch (Exception ex)
                            {

                            }
                        }
                    }
                }
            }
        }

        #endregion
    }
}
