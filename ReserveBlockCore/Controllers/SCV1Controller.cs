using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System.Diagnostics;
using System.Text;

namespace ReserveBlockCore.Controllers
{
    [ActionFilterController]
    [Route("scapi/[controller]")]
    [Route("scapi/[controller]/{somePassword?}")]
    [ApiController]
    public class SCV1Controller : ControllerBase
    {
        // GET: api/<V1>
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "Smart", "Contracts", "API" };
        }

        // GET api/<V1>/getgenesisblock
        [HttpGet("{id}")]
        public string Get(string id)
        {
            //use Id to get specific commands
            var output = "Command not recognized."; // this will only display if command not recognized.
            var command = id.ToLower();
            switch (command)
            {
                //This is initial example. Returns Genesis block in JSON format.
                case "getSCData":
                    //Do something later
                    break;
            }

            return output;
        }

        [HttpPost("SCPassTest")]
        public object SCPassTest([FromBody] object jsonData)
        {
            var output = jsonData;

            return output;
        }

        [HttpPost("SCPassDesTest")]
        public string SCPassDesTest([FromBody] object jsonData)
        {
            var output = jsonData.ToString();
            try
            {
                var scMain = JsonConvert.DeserializeObject<SmartContractMain>(jsonData.ToString());

                var json = JsonConvert.SerializeObject(scMain);

                output = json;
            }
            catch (Exception ex)
            {
                output = $"Error - {ex.Message}. Please Try Again.";
            }

            return output;
        }

        [HttpGet("GetCurrentSCOwner/{scUID}")]
        public async Task<string> GetCurrentSCOwner(string scUID)
        {
            var output = "";

            return output;
        }


        [HttpGet("GetAllSmartContracts/{pageNumber}")]
        public async Task<string> GetAllSmartContracts(int pageNumber)
        {
            var output = "";
            Stopwatch stopwatch3 = Stopwatch.StartNew();
            try
            {
                List<SmartContractMain> scMainList = new List<SmartContractMain>();
                List<SmartContractStateTrei> scStateMainList = new List<SmartContractStateTrei>();

                var maxIndex = pageNumber * 9;
                var startIndex = ((maxIndex - 9));
                var range = 9;

                var scs = SmartContractMain.SmartContractData.GetSCs()
                    .FindAll()
                    .ToList();

                var scStateTrei = SmartContractStateTrei.GetSCST();
                var accounts = AccountData.GetAccounts().FindAll().ToList();

                Parallel.ForEach(scs, sc => {
                    var scState = scStateTrei.FindOne(x => x.SmartContractUID == sc.SmartContractUID);
                    if(scState != null)
                    {
                        var exist = accounts.Exists(x => x.Address == scState.OwnerAddress);
                        if(exist)
                            scStateMainList.Add(scState);
                    }
                });

                var scStateCount = scStateMainList.Count();

                if (maxIndex > scStateCount)
                    range = (range - (maxIndex - scStateCount));

                scStateMainList = scStateMainList.GetRange(startIndex, range);

                if (scStateMainList.Count > 0)
                {
                    Parallel.ForEach(scStateMainList, scState =>
                    {
                        var scMain = SmartContractMain.GenerateSmartContractInMemory(scState.ContractData);
                        scMainList.Add(scMain);
                    });
                    if (scMainList.Count() > 0)
                    {
                        var orderedMainList = scMainList.OrderByDescending(x => x.Id).ToList();
                        var json = JsonConvert.SerializeObject(new { Count = scStateCount, Results = orderedMainList });
                        output = json;
                    }
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Count = 0, Results = scMainList}); ;
                }
            }
            catch(Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Count = 0, Results = "null" }); ;
            }

            return output;
        }

        [HttpGet("GetMintedSmartContracts/{pageNumber}")]
        public async Task<string> GetMintedSmartContracts(int pageNumber)
        {
            var output = "";
            try
            {
                List<SmartContractMain> scMainList = new List<SmartContractMain>();
                List<SmartContractMain> scEvoMainList = new List<SmartContractMain>();

                var scs = SmartContractMain.SmartContractData.GetSCs().Find(x => x.IsMinter == true)
                    .Where(x => x.Features != null && x.Features.Any(y => y.FeatureName == FeatureName.Evolving))
                    .ToList();

                var maxIndex = pageNumber * 9;
                var startIndex = ((maxIndex - 9));
                var range = 9;

                Parallel.ForEach(scs, sc => {
                    var scStateTrei = SmartContractStateTrei.GetSmartContractState(sc.SmartContractUID);
                    if (scStateTrei != null)
                    {
                        var scMain = SmartContractMain.GenerateSmartContractInMemory(scStateTrei.ContractData);
                        if (scMain.Features != null)
                        {
                            var evoFeatures = scMain.Features.Where(x => x.FeatureName == FeatureName.Evolving).Select(x => x.FeatureFeatures).FirstOrDefault();
                            var isDynamic = false;
                            if(evoFeatures != null)
                            {
                                var evoFeatureList = (List<EvolvingFeature>)evoFeatures;
                                foreach (var feature in evoFeatureList)
                                {
                                    var evoFeature = (EvolvingFeature)feature;
                                    if (evoFeature.IsDynamic == true)
                                        isDynamic = true;
                                }
                            }

                            if (!isDynamic)
                                scMainList.Add(scMain);
                        }
                    }
                });

                var scscMainListCount = scMainList.Count();

                if (maxIndex > scscMainListCount)
                    range = (range - (maxIndex - scscMainListCount));

                scMainList = scMainList.GetRange(startIndex, range);

                if (scMainList.Count() > 0)
                {
                    var orderedMainList = scMainList.OrderByDescending(x => x.Id).ToList();
                    var json = JsonConvert.SerializeObject(new { Count = scscMainListCount, Results = orderedMainList });
                    output = json;
                }
                else
                {
                    var json = JsonConvert.SerializeObject(new { Count = 0, Results = scMainList });
                    output = json;
                }
            }
            catch(Exception ex)
            {
                var json = JsonConvert.SerializeObject(new { Count = 0, Results = "null" });
                output = json;
            }
            

            return output;
        }

        [HttpGet("GetSingleSmartContract/{id}")]
        public async Task<string> GetSingleSmartContract(string id)
        {
            var output = "";
            try
            {
                var sc = SmartContractMain.SmartContractData.GetSmartContract(id);

                var result = await SmartContractReaderService.ReadSmartContract(sc);

                var scMain = result.Item2;
                var scCode = result.Item1;

                var bytes = Encoding.Unicode.GetBytes(scCode);
                var scBase64 = bytes.ToCompress().ToBase64();
                var scMainUpdated = SmartContractMain.GenerateSmartContractInMemory(scBase64);
                if(scMainUpdated.Features != null)
                {
                    var featuresList = scMainUpdated.Features.Where(x => x.FeatureName == FeatureName.Evolving).FirstOrDefault();
                    int currentState = 0;
                    if (featuresList != null)
                    {
                        var evoFeatureList = (List<EvolvingFeature>)featuresList.FeatureFeatures;
                        foreach (var evoFeature in evoFeatureList)
                        {
                            if (evoFeature.IsCurrentState == true)
                            {
                                currentState = evoFeature.EvolutionState;
                            }
                        }
                    }

                    var scMainFeatures = scMain.Features.Where(x => x.FeatureName == FeatureName.Evolving).FirstOrDefault();

                    if (scMainFeatures != null)
                    {
                        var scMainFeaturesList = (List<EvolvingFeature>)scMainFeatures.FeatureFeatures;
                        foreach (var evoFeature in scMainFeaturesList)
                        {
                            if (evoFeature.EvolutionState == currentState)
                            {
                                evoFeature.IsCurrentState = true;
                            }
                        }
                    }
                }
                
                scMainUpdated.Id = sc.Id;
                var currentOwner = "";
                var scState = SmartContractStateTrei.GetSmartContractState(scMain.SmartContractUID);
                if(scState != null)
                {
                    currentOwner = scState.OwnerAddress;
                }

                var scInfo = new[]
                {
                new { SmartContract = scMain, SmartContractCode = scCode, CurrentOwner = currentOwner}
            };

                if (sc != null)
                {
                    var json = JsonConvert.SerializeObject(scInfo);
                    output = json;
                }
                else
                {
                    output = "null";
                }
            }
            catch(Exception ex)
            {
                output = ex.Message;
            }
            
            return output;
        }

        [HttpGet("GetLastKnownLocators/{scUID}")]
        public async Task<string> GetLastKnownLocators(string scUID)
        {
            string output = "";

            var scState = SmartContractStateTrei.GetSmartContractState(scUID);

            if(scState != null)
            {
                output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Locators Found.", Locators = scState.Locators });
            }
            else
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Locators Not Found." });
            }

            return output;

        }

        [HttpGet("AssociateNFTAsset/{nftId}/{**assetPath}")]
        public async Task<string> AssociateNFTAsset(string nftId, string assetPath)
        {
            return $"NFT Id: {nftId} Asset Location: {assetPath}";
        }

        [HttpGet("DownloadNftAssets/{nftId}")]
        public async Task<string> DownloadNftAssets(string nftId)
        {
            return $"NFT Id: {nftId}";
        }

        [HttpPost("CreateSmartContract")]
        public async Task<string> CreateSmartContract([FromBody] object jsonData)
        {
            var output = "";

            try
            {
                SmartContractReturnData scReturnData = new SmartContractReturnData();
                var scMain = JsonConvert.DeserializeObject<SmartContractMain>(jsonData.ToString());
                if(scMain != null)
                {
                    NFTLogUtility.Log($"Creating Smart Contract: {scMain.SmartContractUID}", "SCV1Controller.CreateSmartContract([FromBody] object jsonData)");
                }
                else
                {
                    NFTLogUtility.Log($"scMain is null", "SCV1Controller.CreateSmartContract([FromBody] object jsonData) - Line 190");
                }
                try
                {
                    var result = await SmartContractWriterService.WriteSmartContract(scMain);
                    scReturnData.Success = true;
                    scReturnData.SmartContractCode = result.Item1;
                    scReturnData.SmartContractMain = result.Item2;
                    SmartContractMain.SmartContractData.SaveSmartContract(result.Item2, result.Item1);

                    var txData = "";

                    if (result.Item1 != null)
                    {
                        var bytes = Encoding.Unicode.GetBytes(result.Item1);
                        var scBase64 = bytes.ToCompress().ToBase64();
                        var newSCInfo = new[]
                        {
                            new { Function = "Mint()", ContractUID = scMain.SmartContractUID, Data = scBase64}
                        };

                        txData = JsonConvert.SerializeObject(newSCInfo);
                    }

                    var nTx = new Transaction
                    {
                        Timestamp = TimeUtil.GetTime(),
                        FromAddress = scReturnData.SmartContractMain.MinterAddress,
                        ToAddress = scReturnData.SmartContractMain.MinterAddress,
                        Amount = 0.0M,
                        Fee = 0,
                        Nonce = AccountStateTrei.GetNextNonce(scMain.MinterAddress),
                        TransactionType = TransactionType.NFT_MINT,
                        Data = txData
                    };

                    //Calculate fee for tx.
                    nTx.Fee = FeeCalcService.CalculateTXFee(nTx);

                    nTx.Build();

                    
                    var checkSize = await TransactionValidatorService.VerifyTXSize(nTx);

                    var scInfo = new[]
                    {
                    new {Success = true, SmartContract = result.Item2, SmartContractCode = result.Item1, Transaction = nTx}
                    };
                    var json = JsonConvert.SerializeObject(scInfo, Formatting.Indented);
                    output = json;
                    NFTLogUtility.Log($"Smart Contract Creation Success: {scMain.SmartContractUID}", "SCV1Controller.CreateSmartContract([FromBody] object jsonData)");
                }
                catch (Exception ex)
                {
                    NFTLogUtility.Log($"Failed to create TX for Smartcontract. Error: {ex.Message}", "SCV1Controller.CreateSmartContract([FromBody] object jsonData) - Line 231 catch");
                    scReturnData.Success = false;
                    scReturnData.SmartContractCode = "Failure";
                    scReturnData.SmartContractMain = scMain;

                    var scInfo = new[]
                    {
                    new {Success = false, SmartContract = scReturnData.SmartContractCode, SmartContractCode = scReturnData.SmartContractMain}
                    };
                    var json = JsonConvert.SerializeObject(scInfo, Formatting.Indented);
                    output = json;
                }

            }
            catch (Exception ex)
            {
                NFTLogUtility.Log($"Failed to create smart contract. Error Message: {ex.Message}", "SCV1Controller.CreateSmartContract([FromBody] object jsonData) - Line 247 catch");
                output = $"Error - {ex.Message}. Please Try Again...";
            }


            return output;
        }

        [HttpGet("MintSmartContract/{id}")]
        public async Task<string> MintSmartContract(string id)
        {
            var output = "";

            var scMain = SmartContractMain.SmartContractData.GetSmartContract(id);

            if(scMain.IsPublished == true)
            {
                output = "This NFT has already been published";
                NFTLogUtility.Log($"This NFT has already been published", "SCV1Controller.MintSmartContract(string id)");
            }
            else
            {
                var scTx = await SmartContractService.MintSmartContractTx(scMain);
                if(scTx == null)
                {
                    output = "Failed to publish smart contract: " + scMain.Name + ". Id: " + id;
                    NFTLogUtility.Log($"Failed to publish smart contract: {scMain.SmartContractUID}", "SCV1Controller.MintSmartContract(string id)");
                }
                else
                {
                    output = "Smart contract has been published to mempool";
                    NFTLogUtility.Log($"Smart contract has been published to mempool : {scMain.SmartContractUID}", "SCV1Controller.MintSmartContract(string id)");
                }
            }
            

            return output;
        }

        [HttpGet]
        [Route("TransferNFT/{id}/{toAddress}")]
        [Route("TransferNFT/{id}/{toAddress}/{**backupURL}")]
        public async Task<string> TransferNFT(string id, string toAddress, string? backupURL = "")
        {
            var output = "";
            try
            {
                var sc = SmartContractMain.SmartContractData.GetSmartContract(id);
                if (sc != null)
                {
                    if (sc.IsPublished == true)
                    {
                        //Get beacons here!
                        //This will eventually need to be a chosen parameter someone chooses. 
                        var locator = Globals.Locators.FirstOrDefault();
                        if (locator == null)
                        {
                            output = "You do not have any beacons stored.";
                            NFTLogUtility.Log("Error - You do not have any beacons stored.", "SCV1Controller.TransferNFT()");
                            return output;
                        }
                        else
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

                            var assetString = "";
                            assets.ForEach(x => { assetString = assetString + x + " "; });

                            toAddress = toAddress.Replace(" ", "");
                            var localAddress = AccountData.GetSingleAccount(toAddress);

                            NFTLogUtility.Log($"Sending the following assets for upload: {assetString}", "SCV1Controller.TransferNFT()");
                            var md5List = MD5Utility.MD5ListCreator(assets, sc.SmartContractUID);

                            bool result = false;
                            if (localAddress == null)
                            {
                                result = await P2PClient.BeaconUploadRequest(locator, assets, sc.SmartContractUID, toAddress, md5List);
                            }
                            else
                            {
                                result = true;
                            }

                            if (result == true)
                            {
                                var aqResult = AssetQueue.CreateAssetQueueItem(sc.SmartContractUID, toAddress, locator, md5List, assets,
                                    AssetQueue.TransferType.Upload);

                                if (aqResult)
                                {

                                    bool beaconSendFinalResult = true;
                                    if(assets.Count() > 0)
                                    {
                                        foreach(var asset in assets)
                                        {
                                            var sendResult = await BeaconUtility.SendAssets(sc.SmartContractUID, asset);
                                            if (!sendResult)
                                                beaconSendFinalResult = false;
                                        }
                                    }
                                    if(beaconSendFinalResult)
                                    {
                                        var tx = await SmartContractService.TransferSmartContract(sc, toAddress, locator, md5List, backupURL);
                                        NFTLogUtility.Log($"NFT Transfer TX response was : {tx.Hash}", "SCV1Controller.TransferNFT()");
                                        NFTLogUtility.Log($"NFT Transfer TX Data was : {tx.Data}", "SCV1Controller.TransferNFT()");

                                        var txJson = JsonConvert.SerializeObject(tx);
                                        output = txJson;
                                    }
                                    else
                                    {
                                        output = "Failed to upload to Beacon. Please check NFT logs for more details.";
                                        NFTLogUtility.Log($"Failed to upload to Beacon - TX terminated. Data: scUID: {sc.SmartContractUID} | toAddres: {toAddress} | Locator: {locator} | MD5List: {md5List} | backupURL: {backupURL}", "SCV1Controller.TransferNFT()");
                                    }
                                }
                                else
                                {
                                    output = "Failed to add upload to Asset Queue. Please check logs for more details.";
                                    NFTLogUtility.Log($"Failed to add upload to Asset Queue - TX terminated. Data: scUID: {sc.SmartContractUID} | toAddres: {toAddress} | Locator: {locator} | MD5List: {md5List} | backupURL: {backupURL}", "SCV1Controller.TransferNFT()");
                                     
                                }

                            }
                            else
                            {
                                NFTLogUtility.Log($"Beacon upload failed. Result was : {result}", "SCV1Controller.TransferNFT()");
                            }

                        }
                    }
                    else
                    {
                        output = "Smart Contract Found, but has not been minted.";
                    }
                }
                else
                {
                    output = "No Smart Contract Found Locally.";
                }
            }
            catch(Exception ex)
            {
                output = $"Unknown Error Occurred. Error: {ex.Message}";
                NFTLogUtility.Log($"Unknown Error Transfering NFT. Error: {ex.Message}", "SCV1Controller.TransferNFT()");
            }
            
            
            return output;
        }

        [HttpGet("Burn/{id}")]
        public async Task<string> Burn(string id)
        {
            var output = "";

            var sc = SmartContractMain.SmartContractData.GetSmartContract(id);
            if (sc != null)
            {
                if (sc.IsPublished == true)
                {
                    var tx = await SmartContractService.BurnSmartContract(sc);

                    var txJson = JsonConvert.SerializeObject(tx);
                    output = txJson;
                }
            }

            return output;
        }

        [HttpGet("Evolve/{id}/{toAddress}")]
        public async Task<string> Evolve(string id, string toAddress)
        {
            var output = "";

            var tx = await SmartContractService.EvolveSmartContract(id, toAddress);

            if (tx == null)
            {
                output = "Failed to Evolve - TX";
            }
            else
            {
                var txJson = JsonConvert.SerializeObject(tx);
                output = txJson;
            }

            return output;
        }

        [HttpGet("Devolve/{id}/{toAddress}")]
        public async Task<string> Devolve(string id, string toAddress)
        {
            string output;

            var tx = await SmartContractService.DevolveSmartContract(id, toAddress);

            if (tx == null)
            {
                output = "Failed to Devolve - TX";
            }
            else
            {
                var txJson = JsonConvert.SerializeObject(tx);
                output = txJson;
            }

            return output;
        }

        [HttpGet("EvolveSpecific/{id}/{toAddress}/{evolveState}")]
        public async Task<string> EvolveSpecific(string id, string toAddress, int evolveState)
        {
            string output;

            var tx = await SmartContractService.ChangeEvolveStateSpecific(id, toAddress, evolveState);

            if (tx == null)
            {
                output = "Failed to Change State - TX";
            }
            else
            {
                var txJson = JsonConvert.SerializeObject(tx);
                output = txJson;
            }

            return output;
        }

        [HttpGet("ChangeNFTPublicState/{id}")]
        public async Task<string> ChangeNFTPublicState(string id)
        {
            var output = "";

            //Get SmartContractMain.IsPublic and set to True.
            var scs = SmartContractMain.SmartContractData.GetSCs();
            var sc = SmartContractMain.SmartContractData.GetSmartContract(id);
            sc.IsPublic ^= true;

            scs.UpdateSafe(sc);

            return output;

        }

        [HttpGet("GetNFTAssetLocation/{scUID}/{**fileName}")]
        public async Task<string> GetNFTAssetLocation(string scUID, string fileName)
        {
            var output = "";

            try
            {
                output = NFTAssetFileUtility.NFTAssetPath(fileName, scUID);
            }
            catch { output = "Error"; }

            return output;

        }

        [HttpGet("GetSmartContractData/{id}")]
        public async Task<string> GetSmartContractData(string id)
        {
            var output = "";

            var scStateTrei = SmartContractStateTrei.GetSmartContractState(id);
            if (scStateTrei != null)
            {
                var scMain = SmartContractMain.GenerateSmartContractInMemory(scStateTrei.ContractData);
                output = JsonConvert.SerializeObject(scMain);
            }

            return output;
        }

        [HttpGet("TestDynamicNFT/{id}")]
        public async Task<string> TestDynamicNFT(string id)
        {
            var output = "";

            var sc = SmartContractMain.SmartContractData.GetSmartContract(id);

            var result = await SmartContractReaderService.ReadSmartContract(sc);

            var scMain = result.Item2;
            var scCode = result.Item1;

            var bytes = Encoding.Unicode.GetBytes(scCode);
            var scBase64 = bytes.ToCompress().ToBase64();

            SmartContractMain.SmartContractData.CreateSmartContract(scBase64);

            return output;
        }

        [HttpGet("TestRemove/{id}/{toAddress}")]
        public async Task<string> TestRemove(string id, string toAddress)
        {
            var output = "";

            var sc = SmartContractMain.SmartContractData.GetSmartContract(id);
            if (sc != null)
            {
                if (sc.IsPublished == true)
                {
                    var result = await SmartContractReaderService.ReadSmartContract(sc);

                    var scText = result.Item1;
                    var bytes = Encoding.Unicode.GetBytes(scText);
                    var compressBase64 = SmartContractUtility.Compress(bytes).ToBase64();

                    SmartContractMain.SmartContractData.CreateSmartContract(compressBase64);

                }
                else
                {
                    output = "Smart Contract Found, but has not been minted.";
                }
            }
            else
            {
                output = "No Smart Contract Found Locally.";
            }

            return output;
        }
    }
}
