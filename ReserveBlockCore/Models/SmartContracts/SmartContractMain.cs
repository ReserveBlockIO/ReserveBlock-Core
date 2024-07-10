using ReserveBlockCore.Extensions;
using ReserveBlockCore.Data;
using ReserveBlockCore.Services;
using ReserveBlockCore.Trillium;
using ReserveBlockCore.Utilities;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;

namespace ReserveBlockCore.Models.SmartContracts
{
    public class SmartContractMain
    {
        public long Id { get; set; }
        public string Name { get; set; } //User Defined
        public string Description { get; set; } //User Defined
        public string MinterAddress { get; set; } //User Defined
        public string MinterName { get; set; } //User Defined
        public SmartContractAsset SmartContractAsset { get; set; }
        public bool IsPublic { get; set; } //System Set
        public string SmartContractUID { get; set; }//System Set
        public bool IsMinter { get; set; }
        public bool IsPublished { get; set; }
        public bool IsToken { get; set; }
        public int? SCVersion { get; set; }
        public bool IsLocked { get { return SmartContractData.GetSmartContractLockState(SmartContractUID); } }
        public string? NextOwner { get { return SmartContractData.GetSmartContractNextOwner(SmartContractUID); } }
        public Dictionary<string, string>? Properties { get; set; }
        public List<SmartContractFeatures>? Features { get; set; }

        public class SmartContractData
        {
            public static LiteDB.ILiteCollection<SmartContractMain> GetSCs()
            {
                var scs = DbContext.DB_Assets.GetCollection<SmartContractMain>(DbContext.RSRV_ASSETS);
                return scs;
            }
            public static List<SmartContractMain>? GetSmartContractList()
            {
                var scs = GetSCs();
                if (scs != null)
                {
                    var sc = scs.FindAll().ToList();
                    if (sc != null)
                    {
                        return sc;
                    }
                }

                return null;
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
                    SCLogUtility.Log($"Smart Contract Has Been Minted to Network : {scMain.SmartContractUID}", "SmartContractMain.SetSmartContractIsPublished(string scUID)");
                    scs.UpdateSafe(scMain);
                }              
            }

            public static bool GetSmartContractLockState(string scUID)
            {
                try
                {
                    int count = 0;
                    bool exit = false;
                    while(Globals.TreisUpdating && !exit)
                    {
                        count += 1;
                        Thread.Sleep(500);

                        if (count >= 10)
                            exit = true;
                    }
                    var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
                    if (scStateTreiRec == null)
                        return false;

                    if (scStateTreiRec.IsLocked)
                        return true;
                }
                catch { }
                
                return false;
            }

            public static string? GetSmartContractNextOwner(string scUID)
            {
                try
                {
                    int count = 0;
                    bool exit = false;
                    while (Globals.TreisUpdating && !exit)
                    {
                        count += 1;
                        Thread.Sleep(500);

                        if (count >= 10)
                            exit = true;
                    }
                    var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
                    if (scStateTreiRec == null)
                        return null;

                    if (scStateTreiRec.NextOwner == null)
                        return null;

                    return scStateTreiRec.NextOwner;
                }
                catch { }

                return null;
            }
            public static void SaveSmartContract(SmartContractMain scMain, string? scText)
            {
                var scs = GetSCs();

                var exist = scs.FindOne(x => x.SmartContractUID == scMain.SmartContractUID);

                if (exist == null)
                {
                    scs.InsertSafe(scMain);
                }
                if(scText != null)
                {
                    SaveSCLocaly(scMain, scText);
                }
                
            }

            public static void UpdateSmartContract(SmartContractMain scMain)
            {
                var scs = GetSCs();

                scs.UpdateSafe(scMain);
            }

            public static void DeleteSmartContract(string scUID)
            {
                try
                {
                    var scs = GetSCs();

                    scs.DeleteManySafe(x => x.SmartContractUID == scUID);
                }
                catch(Exception ex)
                {                    
                    ErrorLogUtility.LogError(ex.ToString(), "SmartContractMain.DeleteSmartContract()");
                }
            }
            public static async void SaveSCLocaly(SmartContractMain scMain, string scText)
            {
                try
                {
                    string MainFolder = Globals.IsTestNet != true ? "RBX" : "RBXTest";
                    var databaseLocation = Globals.IsTestNet != true ? "SmartContracts" : "SmartContractsTestNet";
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
                            path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "DBs" + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                        }
                        else
                        {
                            path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + MainFolder + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
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
                    SCLogUtility.Log($"Failed to save smart contract locally: {scMain.SmartContractUID}. Error Message: {ex.ToString()}",
                    "SmartContractMain.SaveSCLocally(SmartContractMain scMain, string scText)");
                }
            }

            public static async void CreateSmartContract(string scText, bool alreadyDecoded = false)
            {
                try
                {
                    string textFromByte = "";
                    if (alreadyDecoded == false)
                    {
                        var byteArrayFromBase64 = scText.FromBase64ToByteArray();
                        var decompressedByteArray = SmartContractUtility.Decompress(byteArrayFromBase64);
                        textFromByte = Encoding.Unicode.GetString(decompressedByteArray);
                    }

                    if (alreadyDecoded)
                    {
                        textFromByte = scText;
                    }

                    var repl = new TrilliumRepl();
                    repl.Run("#reset");
                    repl.Run(textFromByte);

                    var scUID = repl.Run(@"GetNFTId()").Value.ToString();
                    var features = repl.Run(@"GetNFTFeatures()").Value.ToString();

                    var minterName = repl.Run(@"MinterName").Value.ToString();
                    var name = repl.Run(@"Name").Value.ToString();
                    var description = repl.Run(@"Description").Value.ToString();
                    var minterAddress = repl.Run(@"MinterAddress").Value.ToString();
                    var fileSize = Convert.ToInt32(repl.Run(@"FileSize").Value.ToString());
                    var fileName = repl.Run(@"FileName").Value.ToString();
                    var assetAuthorName = repl.Run(@"AssetAuthorName").Value.ToString();

                    var scVersion = repl.Run(@"SCVersion").Value != null ? (int)repl.Run(@"SCVersion").Value : 0;
                    var properties = repl.Run(@"getProperties(Properties)").Value;

                    var mainData = repl.Run(@"NftMain(""nftdata"")").Value.ToString();
                    var mainDataArray = mainData.Split(new string[] { "|->" }, StringSplitOptions.None);

                    var assetData = repl.Run(@"NftMain(""getnftassetdata"")").Value.ToString();
                    var assetDataArray = assetData.Split(new string[] { "|->" }, StringSplitOptions.None);

                    var extension = !string.IsNullOrWhiteSpace(fileName) ? Path.GetExtension(fileName) : "";
                    var smartContractMain = GetSmartContractMain(name, description, minterAddress, minterName, scUID, features, scVersion);
                    if (properties != null)
                    {
                        smartContractMain.Properties = (Dictionary<string, string>)properties;
                    }
                    var smartContractAssset = SmartContractAsset.GetSmartContractAsset(assetAuthorName, fileName, "Asset Folder", extension, fileSize);
                    smartContractMain.SmartContractAsset = smartContractAssset;


                    if (!string.IsNullOrWhiteSpace((string)features))
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
                                    case FeatureName.Token:
                                        {
                                            var tokenObject = repl.Run(@"GetTokenDetails()").Value;
                                            if (tokenObject != null)
                                            {
                                                var tokenObjectSerialized = JsonConvert.SerializeObject(tokenObject);
                                                var tokenData = JsonConvert.DeserializeObject<Token>(tokenObjectSerialized);
                                                if (tokenData != null)
                                                {
                                                    var tokenFeature = TokenFeature.CreateTokenFeature(tokenData);
                                                    scFeature.FeatureName = FeatureName.Token;
                                                    scFeature.FeatureFeatures = tokenFeature;
                                                    featuresList.Add(scFeature);
                                                    smartContractMain.IsToken = true;
                                                }
                                            }
                                            break;
                                        }
                                    case FeatureName.Tokenization:
                                        {
                                            var assetName = repl.Run(@"AssetName").Value;
                                            var assetTicker = repl.Run(@"AssetTicker").Value;
                                            var depositAddress = repl.Run(@"DepositAddress").Value;
                                            var pubKeyProofs = repl.Run(@"GetPublicKeyProofs()").Value;

                                            if(assetName != null && assetTicker != null && depositAddress != null && pubKeyProofs != null)
                                            {
                                                TokenizationFeature tokenizationFeature = new TokenizationFeature
                                                {
                                                    AssetName = assetName.ToString(),
                                                    AssetTicker = assetTicker.ToString(),
                                                    PublicKeyProofs = pubKeyProofs.ToString(),
                                                    DepositAddress = depositAddress.ToString(),
                                                    
                                                };

                                                scFeature.FeatureName = FeatureName.Tokenization;
                                                scFeature.FeatureFeatures = tokenizationFeature;

                                                featuresList.Add(scFeature);
                                            }

                                            break;
                                        }
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
                                        Dictionary<int, object?> evoPropertiesDict = new Dictionary<int, object?>();
                                        for (int i = 1; i <= evolveCount; i++)
                                        {
                                            var funcLetter = FunctionNameUtility.GetFunctionLetter(i);
                                            var ev = repl.Run(@"EvolveState" + funcLetter + "()").Value.ToString();
                                            evolveList.Add(ev);

                                            var evP = repl.Run($"getProperties(EvolveState{funcLetter}Properties)").Value;
                                            if (evP != null)
                                            {
                                                evoPropertiesDict.Add(i, evP);
                                            }
                                        }

                                        var evolveFeatureList = EvolvingFeature.GetEvolveFeature(evolveList, evoPropertiesDict);
                                        var isDynamic = (bool)repl.Run(@"EvolveDynamic").Value;

                                        if (isDynamic == true)
                                        {
                                            var blockHeight = Globals.LastBlock.Height.ToString();
                                            var evolveState = (int)repl.Run(@"DynamicEvolve(1, " + blockHeight + ")").Value;

                                            var evolveFeature = evolveFeatureList.Where(x => x.EvolutionState == evolveState).FirstOrDefault();
                                            if (evolveFeature != null)
                                            {
                                                evolveFeature.IsCurrentState = true;
                                            }
                                        }

                                        scFeature.FeatureName = FeatureName.Evolving;
                                        scFeature.FeatureFeatures = evolveFeatureList;
                                        featuresList.Add(scFeature);
                                        break;
                                    default:
                                        break;
                                }

                            }
                        }
                        else /////////////////////////////////////////////////////////////////////////////////////////////////////
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
                                case FeatureName.Token:
                                    {
                                        var tokenObject = repl.Run(@"GetTokenDetails()").Value;
                                        if (tokenObject != null)
                                        {
                                            var tokenObjectSerialized = JsonConvert.SerializeObject(tokenObject);
                                            var tokenData = JsonConvert.DeserializeObject<Token>(tokenObjectSerialized);
                                            if (tokenData != null)
                                            {
                                                var tokenFeature = TokenFeature.CreateTokenFeature(tokenData);
                                                scFeature.FeatureName = FeatureName.Token;
                                                scFeature.FeatureFeatures = tokenFeature;
                                                featuresList.Add(scFeature);
                                                smartContractMain.IsToken = true;
                                            }
                                        }
                                        break;
                                    }

                                case FeatureName.Tokenization:
                                    {
                                        var assetName = repl.Run(@"AssetName").Value;
                                        var assetTicker = repl.Run(@"AssetTicker").Value;
                                        var depositAddress = repl.Run(@"DepositAddress").Value;
                                        var pubKeyProofs = repl.Run(@"GetPublicKeyProofs()").Value;

                                        if (assetName != null && assetTicker != null && depositAddress != null && pubKeyProofs != null)
                                        {
                                            TokenizationFeature tokenizationFeature = new TokenizationFeature
                                            {
                                                AssetName = assetName.ToString(),
                                                AssetTicker = assetTicker.ToString(),
                                                PublicKeyProofs = pubKeyProofs.ToString(),
                                                DepositAddress = depositAddress.ToString(),
                                            };

                                            scFeature.FeatureName = FeatureName.Tokenization;
                                            scFeature.FeatureFeatures = tokenizationFeature;

                                            featuresList.Add(scFeature);
                                        }

                                        break;
                                    }
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
                                    Dictionary<int, object?> evoPropertiesDict = new Dictionary<int, object?>();
                                    var evolveCount = Convert.ToInt32(repl.Run(@"EvolveStates()").Value.ToString());
                                    for (int i = 1; i <= evolveCount; i++)
                                    {
                                        var funcLetter = FunctionNameUtility.GetFunctionLetter(i);
                                        var ev = repl.Run(@"EvolveState" + funcLetter + "()").Value.ToString();
                                        evolveList.Add(ev);

                                        var evP = repl.Run($"getProperties(EvolveState{funcLetter}Properties)").Value;
                                        if (evP != null)
                                        {
                                            evoPropertiesDict.Add(i, evP);
                                        }
                                    }

                                    var evolveFeatureList = EvolvingFeature.GetEvolveFeature(evolveList, evoPropertiesDict);

                                    var isDynamic = (bool)repl.Run(@"EvolveDynamic").Value;

                                    if (isDynamic == true)
                                    {
                                        var blockHeight = Globals.LastBlock.Height.ToString();
                                        var evolveState = (int)repl.Run(@"DynamicEvolve(1, " + blockHeight + ")").Value;

                                        var evolveFeature = evolveFeatureList.Where(x => x.EvolutionState == evolveState).FirstOrDefault();
                                        if (evolveFeature != null)
                                        {
                                            evolveFeature.IsCurrentState = true;

                                        }

                                        var evoDynamicList = evolveFeatureList.Where(x => x.EvolveBlockHeight != null || x.EvolveDate != null).ToList();
                                        if (evoDynamicList.Count() > 0)
                                        {
                                            foreach (var evo in evoDynamicList)
                                            {
                                                var update = evolveFeatureList.Where(x => x.EvolutionState == evo.EvolutionState).FirstOrDefault();
                                                if (update != null)
                                                    update.IsDynamic = true;
                                            }
                                        }

                                    }

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

                    var result = await SmartContractWriterService.WriteSmartContractFromTX(smartContractMain);
                    SaveSmartContract(result.Item2, result.Item1);
                }
                catch (Exception ex)
                {

                }
            }
        }

        public static SmartContractMain GenerateSmartContractInMemory(string scText)
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
            var fileSize = Convert.ToInt32(repl.Run(@"FileSize").Value.ToString());
            var fileName = repl.Run(@"FileName").Value.ToString();
            var assetAuthorName = repl.Run(@"AssetAuthorName").Value.ToString();
            var scVersion = repl.Run(@"SCVersion").Value != null ? (int)repl.Run(@"SCVersion").Value : 0;
            var properties = repl.Run(@"getProperties(Properties)").Value;

            var mainData = repl.Run(@"NftMain(""nftdata"")").Value.ToString();
            var mainDataArray = mainData.Split(new string[] { "|->" }, StringSplitOptions.None);

            var assetData = repl.Run(@"NftMain(""getnftassetdata"")").Value.ToString();
            var assetDataArray = assetData.Split(new string[] { "|->" }, StringSplitOptions.None);

            var smartContractMain = GetSmartContractMain(name, description, minterAddress, minterName, scUID, features, scVersion);
            
            var extension = !string.IsNullOrWhiteSpace(fileName) ? Path.GetExtension(fileName) : "";
            var smartContractAssset = SmartContractAsset.GetSmartContractAsset(assetAuthorName, fileName, "Asset Folder", extension, fileSize);
            smartContractMain.SmartContractAsset = smartContractAssset;

            if (properties != null)
            {
                smartContractMain.Properties = (Dictionary<string, string>)properties;
            }

            if (!string.IsNullOrWhiteSpace((string)features))
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

                            case FeatureName.Token:
                                {
                                    var tokenObject = repl.Run(@"GetTokenDetails()").Value;
                                    if (tokenObject != null)
                                    {
                                        var tokenObjectSerialized = JsonConvert.SerializeObject(tokenObject);
                                        var tokenData = JsonConvert.DeserializeObject<Token>(tokenObjectSerialized);
                                        if (tokenData != null)
                                        {
                                            var tokenFeature = TokenFeature.CreateTokenFeature(tokenData);
                                            scFeature.FeatureName = FeatureName.Token;
                                            scFeature.FeatureFeatures = tokenFeature;
                                            featuresList.Add(scFeature);
                                            smartContractMain.IsToken = true;
                                        }
                                    }
                                    break;
                                }
                            case FeatureName.Tokenization:
                                {
                                    var assetName = repl.Run(@"AssetName").Value != null ? (string)repl.Run(@"AssetName").Value : "vBTC Token";
                                    var assetTicket = repl.Run(@"AssetTicker").Value != null ? (string)repl.Run(@"AssetTicker").Value : "BTC";
                                    var depositAddress = repl.Run(@"DepositAddress").Value != null ? (string)repl.Run(@"DepositAddress").Value : "ERROR";
                                    var proofs =  repl.Run(@"GetPublicKeyProofs()").Value.ToString();

                                    var tokenizationFeature = new TokenizationFeature { 
                                        AssetName = assetName,
                                        AssetTicker = assetTicket,
                                        DepositAddress = depositAddress,
                                        PublicKeyProofs = proofs
                                    };

                                    scFeature.FeatureName = FeatureName.Tokenization;
                                    scFeature.FeatureFeatures = tokenizationFeature;
                                    featuresList.Add(scFeature);
                                    break;
                                }
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
                                Dictionary<int, object?> evoPropertiesDict = new Dictionary<int, object?>();
                                var evolveCount = Convert.ToInt32(repl.Run(@"EvolveStates()").Value.ToString());

                                var evolveState = repl.Run(@"GetCurrentEvolveState()");
                                var evoStateString = evolveState.Value.ToString().Replace("{*", "").Replace("}", "");
                                var evoStateNum = Convert.ToInt32(evoStateString);

                                for (int i = 1; i <= evolveCount; i++)
                                {
                                    var funcLetter = FunctionNameUtility.GetFunctionLetter(i);
                                    var ev = repl.Run(@"EvolveState" + funcLetter + "()").Value.ToString();
                                    evolveList.Add(ev);

                                    var evP = repl.Run($"getProperties(EvolveState{funcLetter}Properties)").Value;
                                    if (evP != null)
                                    {
                                        evoPropertiesDict.Add(i, evP);
                                    }
                                }

                                var evolveFeatureList = EvolvingFeature.GetEvolveFeature(evolveList, evoPropertiesDict, evoStateNum > 0 ? evoStateNum : null);

                                var isDynamic = (bool)repl.Run(@"EvolveDynamic").Value;

                                if (isDynamic == true)
                                {
                                    var blockHeight = Globals.LastBlock.Height.ToString();
                                    var evolveStateDynamic = (int)repl.Run(@"DynamicEvolve(1, " + blockHeight + ")").Value;

                                    var evolveFeature = evolveFeatureList.Where(x => x.EvolutionState == evolveStateDynamic).FirstOrDefault();
                                    if (evolveFeature != null)
                                    {
                                        var evoFeaturesList = evolveFeatureList.Where(x => x.IsCurrentState == true).ToList();
                                        foreach(var evoFeature in evoFeaturesList)
                                        {
                                            evoFeature.IsCurrentState = false;
                                        }
                                        evolveFeature.IsCurrentState = true;
                                        
                                    }


                                }

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

                        case FeatureName.Token:
                            {
                                var tokenObject = repl.Run(@"GetTokenDetails()").Value;
                                if (tokenObject != null)
                                {
                                    var tokenObjectSerialized = JsonConvert.SerializeObject(tokenObject);
                                    var tokenData = JsonConvert.DeserializeObject<Token>(tokenObjectSerialized);
                                    if(tokenData != null)
                                    {
                                        var tokenFeature = TokenFeature.CreateTokenFeature(tokenData);
                                        scFeature.FeatureName = FeatureName.Token;
                                        scFeature.FeatureFeatures = tokenFeature;
                                        featuresList.Add(scFeature);
                                        smartContractMain.IsToken = true;
                                    }
                                }
                                break;
                            }
                        case FeatureName.Tokenization:
                            {
                                var assetName = repl.Run(@"AssetName").Value != null ? (string)repl.Run(@"AssetName").Value : "vBTC Token";
                                var assetTicket = repl.Run(@"AssetTicker").Value != null ? (string)repl.Run(@"AssetTicker").Value : "BTC";
                                var depositAddress = repl.Run(@"DepositAddress").Value != null ? (string)repl.Run(@"DepositAddress").Value : "ERROR";
                                var proofs = repl.Run(@"GetPublicKeyProofs()").Value.ToString();

                                var tokenizationFeature = new TokenizationFeature
                                {
                                    AssetName = assetName,
                                    AssetTicker = assetTicket,
                                    DepositAddress = depositAddress,
                                    PublicKeyProofs = proofs
                                };

                                scFeature.FeatureName = FeatureName.Tokenization;
                                scFeature.FeatureFeatures = tokenizationFeature;
                                featuresList.Add(scFeature);
                                break;
                            }
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
                            Dictionary<int, object?> evoPropertiesDict = new Dictionary<int, object?>();
                            var evolveCount = Convert.ToInt32(repl.Run(@"EvolveStates()").Value.ToString());
                            for (int i = 1; i <= evolveCount; i++)
                            {
                                var funcLetter = FunctionNameUtility.GetFunctionLetter(i);
                                var ev = repl.Run(@"EvolveState" + funcLetter + "()").Value.ToString();
                                evolveList.Add(ev);

                                var evP = repl.Run($"getProperties(EvolveState{funcLetter}Properties)").Value;
                                if (evP != null)
                                {
                                    evoPropertiesDict.Add(i, evP);
                                }
                            }
                            var evolveState = repl.Run(@"GetCurrentEvolveState()");
                            var evoStateString = evolveState.Value.ToString().Replace("{*", "").Replace("}", "");
                            var evoStateNum = Convert.ToInt32(evoStateString);

                            var evolveFeatureList = EvolvingFeature.GetEvolveFeature(evolveList, evoPropertiesDict, evoStateNum > 0 ? evoStateNum : null);

                            var isDynamic = (bool)repl.Run(@"EvolveDynamic").Value;

                            if (isDynamic == true)
                            {
                                var blockHeight = Globals.LastBlock.Height.ToString();
                                var evolveStateDynamic = (int)repl.Run(@"DynamicEvolve(1, " + blockHeight + ")").Value;

                                var evolveFeature = evolveFeatureList.Where(x => x.EvolutionState == evolveStateDynamic).FirstOrDefault();
                                if (evolveFeature != null)
                                {
                                    evolveFeature.IsCurrentState = true;
                                }
                            }

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
            return smartContractMain;
        }

        private static SmartContractMain GetSmartContractMain(string name, string desc, string minterAddress, string minterName, string smartContractUID, string features, int scVersion)
        {
            SmartContractMain scMain = new SmartContractMain();

            scMain.SmartContractUID = smartContractUID;
            scMain.Name = name;
            scMain.Description = desc;
            scMain.IsMinter = false;
            scMain.IsPublic = false;
            //scMain.Features = features;
            scMain.IsPublished = true;
            scMain.MinterName = minterName;
            scMain.MinterAddress = minterAddress;
            scMain.SCVersion = scVersion;

            return scMain;
        }


    }
}
