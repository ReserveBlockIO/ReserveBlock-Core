using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System.Text;
using System.Text.RegularExpressions;

namespace ReserveBlockCore.Controllers
{
    [ActionFilterController]
    [Route("txapi/[controller]")]
    [Route("txapi/[controller]/{somePassword?}")]
    [ApiController]
    public class TXV1Controller : ControllerBase
    {
        /// <summary>
        /// Returns the timestamp of the given wallet.
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetTimestamp")]
        public async Task<string> GetTimestamp()
        {
            //use Id to get specific commands
            var output = "FAIL"; // this will only display if command not recognized.

            var timestamp = TimeUtil.GetTime();

            output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Timestamp Acquired.", Timestamp = timestamp });

            return output;
        }

        /// <summary>
        /// Returns a list of successful transactions that are local to wallet
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetSuccessfulLocalTX")]
        public async Task<string> GetSuccessfulLocalTX()
        {
            //use Id to get specific commands
            var output = "[]"; // this will only display if command not recognized.

            var txList = TransactionData.GetSuccessfulLocalTransactions();

            if(txList.Count() > 0)
            {
                output = JsonConvert.SerializeObject(txList);
            }
            
            return output;
        }

        /// <summary>
        /// Returns a list of mined reward transactions that are local to wallet
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetMinedLocalTX")]
        public async Task<string> GetMinedLocalTX()
        {
            //use Id to get specific commands
            var output = "[]"; // this will only display if command not recognized.

            var txList = TransactionData.GetLocalMinedTransactions();

            if (txList.Count() > 0)
            {
                output = JsonConvert.SerializeObject(txList);
            }

            return output;
        }

        /// <summary>
        /// Returns a list of all transactions that are local to wallet
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetAllLocalTX")]
        public async Task<string> GetAllLocalTX()
        {
            //use Id to get specific commands
            var output = "[]"; // this will only display if command not recognized.

            var txList = TransactionData.GetAllLocalTransactions(true);

            if (txList.Count() > 0)
            {
                output = JsonConvert.SerializeObject(txList);
            }

            return output;
        }

        /// <summary>
        /// Returns a list of pending transactions that are local to wallet
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetPendingLocalTX")]
        public async Task<string> GetPendingLocalTX()
        {
            //use Id to get specific commands
            var output = "[]"; // this will only display if command not recognized.

            var txList = TransactionData.GetLocalPendingTransactions();

            if (txList.Count() > 0)
            {
                output = JsonConvert.SerializeObject(txList);
            }

            return output;
        }

        /// <summary>
        /// Returns a list of failed transactions that are local to wallet
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetFailedLocalTX")]
        public async Task<string> GetFailedLocalTX()
        {
            //use Id to get specific commands
            var output = "[]"; // this will only display if command not recognized.

            var txList = TransactionData.GetLocalFailedTransactions();

            if (txList.Count() > 0)
            {
                output = JsonConvert.SerializeObject(txList);
            }

            return output;
        }

        /// <summary>
        /// Returns a single transaction with hash as the search
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        [HttpGet("GetLocalTxByHash/{hash}")]
        public async Task<string> GetLocalTxByHash(string hash)
        {
            //use Id to get specific commands
            var output = "[]"; // this will only display if command not recognized.

            var txList = TransactionData.GetTxByHash(hash);

            if (txList != null)
            {
                output = JsonConvert.SerializeObject(txList);
            }

            return output;
        }

        /// <summary>
        /// Returns a single transaction with height as the search
        /// </summary>
        /// <param name="height"></param>
        /// <returns></returns>
        [HttpGet("GetLocalTxByBlock/{height}")]
        public async Task<string> GetLocalTxByBlock(long height)
        {
            //use Id to get specific commands
            var output = "[]"; // this will only display if command not recognized.

            var txList = TransactionData.GetTxByBlock(height);

            if (txList.Count() > 0)
            {
                output = JsonConvert.SerializeObject(txList);
            }

            return output;
        }

        /// <summary>
        /// Returns a all transactions before a block height as the search
        /// </summary>
        /// <param name="height"></param>
        /// <returns></returns>
        [HttpGet("GetLocalTxBeforeBlock/{height}")]
        public async Task<string> GetLocalTxBeforeBlock(long height)
        {
            //use Id to get specific commands
            var output = "[]"; // this will only display if command not recognized.

            var txList = TransactionData.GetLocalTransactionsBeforeBlock(height);

            if (txList.Count() > 0)
            {
                output = JsonConvert.SerializeObject(txList);
            }

            return output;
        }

        /// <summary>
        /// Returns a all transactions after a block height as the search
        /// </summary>
        /// <param name="height"></param>
        /// <returns></returns>
        [HttpGet("GetLocalTxAfterBlock/{height}")]
        public async Task<string> GetLocalTxAfterBlock(long height)
        {
            //use Id to get specific commands
            var output = "[]"; // this will only display if command not recognized.

            var txList = TransactionData.GetLocalTransactionsSinceBlock(height);

            if (txList.Count() > 0)
            {
                output = JsonConvert.SerializeObject(txList);
            }

            return output;
        }

        /// <summary>
        /// Returns a all transactions before a timestamp height as the search
        /// </summary>
        /// <param name="timestamp"></param>
        /// <returns></returns>
        [HttpGet("GetLocalTxBeforeTimestamp/{timestamp}")]
        public async Task<string> GetLocalTxBeforeTimestamp(long timestamp)
        {
            //use Id to get specific commands
            var output = "[]"; // this will only display if command not recognized.

            var txList = TransactionData.GetLocalTransactionsBeforeDate(timestamp);

            if (txList.Count() > 0)
            {
                output = JsonConvert.SerializeObject(txList);
            }

            return output;
        }

        /// <summary>
        /// Returns a all transactions after a timestamp height as the search
        /// </summary>
        /// <param name="timestamp"></param>
        /// <returns></returns>
        [HttpGet("GetLocalTxAfterTimestamp/{timestamp}")]
        public async Task<string> GetLocalTxAfterTimestamp(long timestamp)
        {
            //use Id to get specific commands
            var output = "[]"; // this will only display if command not recognized.

            var txList = TransactionData.GetLocalTransactionsSinceDate(timestamp);

            if (txList.Count() > 0)
            {
                output = JsonConvert.SerializeObject(txList);
            }

            return output;
        }

        /// <summary>
        /// Returns all transactions for ADNR
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetLocalADNRTX")]
        public async Task<string> GetLocalADNRTX()
        {
            //use Id to get specific commands
            var output = "[]"; // this will only display if command not recognized.

            var txList = TransactionData.GetLocalAdnrTransactions();

            if (txList.Count() > 0)
            {
                output = JsonConvert.SerializeObject(txList);
            }

            return output;
        }

        /// <summary>
        /// Returns and Address Nonce
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        [HttpGet("GetAddressNonce/{address}")]
        public async Task<string> GetAddressNonce(string address)
        {
            //use Id to get specific commands
            var output = "FAIL"; // this will only display if command not recognized.

            var nextNonce = AccountStateTrei.GetNextNonce(address);

            output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Next Nonce Found.", Nonce = nextNonce });

            return output;
        }

        /// <summary>
        /// Creates a minting tranasctions
        /// </summary>
        /// <param name="jsonData"></param>
        /// <returns></returns>
        [HttpPost("GetNFTMintData")]
        public async Task<string> GetNFTMintData([FromBody] object jsonData)
        {
            var output = "";

            var scMain = JsonConvert.DeserializeObject<SmartContractMain>(jsonData.ToString());

            try
            {
                var result = await SmartContractWriterService.WriteSmartContract(scMain);

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
                    var txJToken = JToken.Parse(txData.ToString());
                    //Type type = typeof(string);
                    //var dataTest = txJToken["Data"] != null ? txJToken["Data"].ToString(Formatting.None) : null;//sometest["Data"].ToObject<string>();
                    //txJToken["Data"] = dataTest;
                    output = txData;
                }
            }
            catch(Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = ex.ToString()});
            }

            return output;
        }

        /// <summary>
        /// Creates a beacon upload request transactions
        /// </summary>
        /// <param name="scUID"></param>
        /// <param name="toAddress"></param>
        /// <param name="signature"></param>
        /// <returns></returns>
        [HttpGet("CreateBeaconUploadRequest/{scUID}/{toAddress}/{**signature}")]
        public async Task<string> CreateBeaconUploadRequest(string scUID, string toAddress, string signature)
        {
            var output = "";
            toAddress = toAddress.ToAddressNormalize();
            var scStateTrei = SmartContractStateTrei.GetSmartContractState(scUID);
            if (scStateTrei != null)
            {
                var sc = SmartContractMain.GenerateSmartContractInMemory(scStateTrei.ContractData);
                if (sc != null)
                {
                    if (sc.IsPublished == true)
                    {
                        //Get beacons here!                        
                        if (!Globals.Locators.Any())
                        {
                            output = "You are not connected to any beacons.";
                        }
                        else
                        {
                            var locators = Globals.Locators.Values.FirstOrDefault();
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
                                            var evolveDict = (Dictionary<string, object>)myArray[count];
                                            SmartContractAsset evoAsset = new SmartContractAsset();
                                            if (evolveDict.ContainsKey("SmartContractAsset"))
                                            {

                                                var assetEvo = (Dictionary<string, object>)evolveDict["SmartContractAsset"];
                                                evoAsset.Name = (string)assetEvo["Name"];
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
                                            var multiAssetDict = (Dictionary<string, object>)myArray[count];

                                            var fileName = multiAssetDict["FileName"].ToString();
                                            if (!assets.Contains(fileName))
                                            {
                                                assets.Add(fileName);
                                            }

                                            count += 1;

                                        });

                                    }
                                }
                            }

                            var result = await P2PClient.BeaconUploadRequest(locators, assets, sc.SmartContractUID, toAddress, signature);
                            if (result == true)
                            {
                                var md5List = MD5Utility.MD5ListCreator(assets, sc.SmartContractUID);

                                var finalOutput = JsonConvert.SerializeObject(new { Locators = result, MD5List = md5List });
                                output = finalOutput;

                            }
                        }
                    }
                }
            }

            return output;
        }

        /// <summary>
        /// Creates a NFT transfer transaction
        /// </summary>
        /// <param name="scUID"></param>
        /// <param name="toAddress"></param>
        /// <param name="locators"></param>
        /// <returns></returns>
        [HttpGet("GetNFTTransferData/{scUID}/{toAddress}/{locators}")]
        public async Task<string> GetNFTTransferData(string scUID, string toAddress, string locators)
        {
            var output = "";
            var scStateTrei = SmartContractStateTrei.GetSmartContractState(scUID);
            toAddress = toAddress.ToAddressNormalize();

            var sc = SmartContractMain.GenerateSmartContractInMemory(scStateTrei.ContractData);
            try
            {
                //var result = await SmartContractWriterService.WriteSmartContract(sc);
                var txData = "";

                if (scStateTrei != null)
                {
                    var newSCInfo = new[]
                    {
                            new { Function = "Transfer()", 
                                ContractUID = sc.SmartContractUID, 
                                ToAddress = toAddress, 
                                Data = scStateTrei.ContractData, 
                                Locators = locators, //either beacons, or self kept (NA).
                                MD5List = "NA"}
                    };

                    txData = JsonConvert.SerializeObject(newSCInfo);
                    var txJToken = JToken.Parse(txData.ToString());
                    output = txData;
                }
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = ex.ToString() });
            }

            return output;
        }

        /// <summary>
        /// Creates a NFT Burn data transactions
        /// </summary>
        /// <param name="scUID"></param>
        /// <param name="fromAddress"></param>
        /// <returns></returns>
        [HttpGet("GetNFTBurnData/{scUID}/{fromAddress}/")]
        public async Task<string> GetNFTBurnData(string scUID, string fromAddress)
        {
            var output = "";
            var scStateTrei = SmartContractStateTrei.GetSmartContractState(scUID);

            if(scStateTrei == null)
            {
                output = JsonConvert.SerializeObject(new {Success = false, Message = "Smart contract does not exist." });
                return output;
            }
            
            try
            {
                var txData = "";
                var newSCInfo = new[]
                {
                        new { Function = "Burn()", 
                            ContractUID = scUID, 
                            FromAddress = fromAddress}
                };

                txData = JsonConvert.SerializeObject(newSCInfo);
                var txJToken = JToken.Parse(txData.ToString());
                output = JsonConvert.SerializeObject(new {Success = true, Message = txData });
                
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = ex.ToString() });
            }

            return output;
        }

        /// <summary>
        /// Produces the estimated fee for a transaction
        /// </summary>
        /// <param name="jsonData"></param>
        /// <returns></returns>
        [HttpPost("GetRawTxFee")]
        public async Task<string> GetRawTxFee([FromBody] object jsonData)
        {
            var output = "";
            try
            {
                var txJToken = JToken.Parse(jsonData.ToString());
                Type type = typeof(string);
                var dataTest = txJToken["Data"] != null ? txJToken["Data"].ToString(Formatting.None) : null;//sometest["Data"].ToObject<string>();
                txJToken["Data"] = dataTest;
                var tx = JsonConvert.DeserializeObject<Transaction>(txJToken.ToString());

                var nTx = new Transaction
                {
                    Timestamp = tx.Timestamp,
                    FromAddress = tx.FromAddress,
                    ToAddress = tx.ToAddress,
                    Amount = tx.Amount + 0.0M,
                    Fee = 0,
                    Nonce = AccountStateTrei.GetNextNonce(tx.FromAddress),
                    TransactionType = tx.TransactionType,
                    Data = tx.Data
                };

                //Calculate fee for tx.
                nTx.Fee = FeeCalcService.CalculateTXFee(nTx);

                output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"TX Fee Calculated", Fee = nTx.Fee });
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Failed to calcuate Fee. Error: {ex.ToString()}" });
            }

            return output;
        }

        /// <summary>
        /// Produces the TX Hash
        /// </summary>
        /// <param name="jsonData"></param>
        /// <returns></returns>
        [HttpPost("GetTxHash")]
        public async Task<string> GetTxHash([FromBody] object jsonData)
        {
            var output = "";
            try
            {
                var txJToken = JToken.Parse(jsonData.ToString());
                Type type = typeof(string);
                var dataTest = txJToken["Data"] != null ? txJToken["Data"].ToString(Formatting.None) : null;//sometest["Data"].ToObject<string>();
                txJToken["Data"] = dataTest;
                var tx = JsonConvert.DeserializeObject<Transaction>(txJToken.ToString());

                tx.Build();

                output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"TX Fee Calculated", Hash = tx.Hash });
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Failed to create Hash. Error: {ex.ToString()}" });
            }

            return output;
        }

        //Step 5.
        //You create the signature now in your application.

        /// <summary>
        /// Validate a signature
        /// </summary>
        /// <param name="message"></param>
        /// <param name="address"></param>
        /// <param name="sigScript"></param>
        /// <returns></returns>
        [HttpGet("ValidateSignature/{message}/{address}/{**sigScript}")]
        public async Task<string> ValidateSignature(string message, string address, string sigScript)
        {
            string output;

            try
            {
                var result = SignatureService.VerifySignature(address, message, sigScript);

                if (result == true)
                {
                    output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Signature Verified." });
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Signature Not Verified." });
                }
            }
            catch(Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Signature Not Verified. Unknown Error: {ex.ToString()}" });
            }
            
            return output;
        }

        //If validation was true
        //Step 7.
        /// <summary>
        /// Verify a raw transaction
        /// </summary>
        /// <returns></returns>
        [HttpPost("VerifyRawTransaction")]
        public async Task<string> VerifyRawTransaction([FromBody] object jsonData)
        {
            var output = "";
            try
            {
                var txJToken = JToken.Parse(jsonData.ToString());
                Type type = typeof(string);
                var dataTest = txJToken["Data"] != null ? txJToken["Data"].ToString(Formatting.None) : null;//sometest["Data"].ToObject<string>();
                txJToken["Data"] = dataTest;
                var transaction = JsonConvert.DeserializeObject<Transaction>(txJToken.ToString());

                if (transaction != null)
                {
                    var result = await TransactionValidatorService.VerifyTX(transaction);
                    if (result.Item1 == true)
                    {

                        output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Transaction has been verified.", Hash = transaction.Hash });
                    }
                    else
                    {
                        output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Transaction was not verified. Error: {result.Item2}" });
                    }
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Failed to deserialize transaction. Please try again." });
                }

            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Error - {ex.ToString()}. Please Try Again." });
            }

            return output;
        }

        //Step 8.
        /// <summary>
        /// Sends a raw TX over the network
        /// </summary>
        /// <returns></returns>
        [HttpPost("SendRawTransaction")]
        public async Task<string> SendRawTransaction([FromBody] object jsonData)
        {
            var output = "";
            try
            {
                var txJToken = JToken.Parse(jsonData.ToString());
                Type type = typeof(string);
                var dataTest = txJToken["Data"] != null ? txJToken["Data"].ToString(Formatting.None) : null;//sometest["Data"].ToObject<string>();
                txJToken["Data"] = dataTest;
                var transaction = JsonConvert.DeserializeObject<Transaction>(txJToken.ToString());

                if (transaction != null)
                {
                    var result = await TransactionValidatorService.VerifyTX(transaction);
                    if (result.Item1 == true)
                    {
                        TransactionData.AddToPool(transaction);
                        await P2PClient.SendTXMempool(transaction);//send out to mempool

                        output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Transaction has been broadcasted.", Hash = transaction.Hash });
                    }
                    else
                    {
                        output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Transaction was not verified." });
                    }
                }
                else
                {

                    output = JsonConvert.SerializeObject(new { Result = "Fail", Message = "Failed to deserialize transaction. Please try again." });
                }

            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Error - {ex.ToString()}. Please Try Again." });
            }

            return output;
        }

        /// <summary>
        /// Test raw transaction is received and return the input
        /// </summary>
        /// <returns></returns>
        [HttpPost("TestRawTransaction")]
        public string TestRawTransaction([FromBody] object jsonData)
        {
            var output = jsonData.ToString();
            try
            {
                var tx = JsonConvert.DeserializeObject<Transaction>(jsonData.ToString());

                var json = JsonConvert.SerializeObject(tx);

                output = json;
            }
            catch (Exception ex)
            {
                output = $"Error - {ex.ToString()}. Please Try Again.";
            }

            return output;
        }

        /// <summary>
        /// Create an ADNR and associate it to address
        /// </summary>
        /// <param name="address"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        [HttpGet("CreateAdnr/{address}/{name}")]
        public async Task<string> CreateAdnr(string address, string name)
        {
            string output = "";

            try
            {
                var wallet = AccountData.GetSingleAccount(address);
                if(wallet != null)
                {
                    var addressFrom = wallet.Address;
                    var adnr = Adnr.GetAdnr();
                    if(adnr != null)
                    {
                        var adnrAddressCheck = adnr.FindOne(x => x.Address == addressFrom);
                        if (adnrAddressCheck != null)
                        {
                            output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"This address already has a DNR associated with it: {adnrAddressCheck.Name}" });
                            return output;
                        }

                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            name = name.ToLower();

                            var limit = Globals.ADNRLimit;

                            if(name.Length > limit)
                            {
                                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = "A DNR may only be a max of 65 characters" });
                                return output;
                            }

                            var nameCharCheck = Regex.IsMatch(name, @"^[a-zA-Z0-9]+$");
                            if (!nameCharCheck)
                            {
                                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = "A DNR may only contain letters and numbers." });
                                return output;
                            }
                            else
                            {
                                var nameRBX = name.ToLower() + ".rbx";
                                var nameCheck = adnr.FindOne(x => x.Name == nameRBX);
                                if (nameCheck == null)
                                {
                                    var result = await Adnr.CreateAdnrTx(address, name);
                                    if (result.Item1 != null)
                                    {
                                        output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Transaction has been broadcasted.", Hash = result.Item1.Hash });
                                    }
                                    else
                                    {
                                        output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Transaction failed to broadcast. Error: {result.Item2}" });
                                    }
                                }
                                else
                                {
                                    output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"This name already has a DNR associated with it: {nameCheck.Name}" });
                                    return output;
                                }
                            }

                        }
                        else
                        {
                            output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Name was empty." });
                        }
                    }
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Account with address: {address} was not found." });
                    return output;
                }
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Unknown Error: {ex.ToString()}" });
            }

            return output;
        }

        /// <summary>
        /// Transfer ADNR from one address to another
        /// </summary>
        /// <param name="fromAddress"></param>
        /// <param name="toAddress"></param>
        /// <returns></returns>
        [HttpGet("TransferAdnr/{fromAddress}/{toAddress}")]
        public async Task<string> TransferAdnr(string fromAddress, string toAddress)
        {
            string output = "";

            try
            {
                var wallet = AccountData.GetSingleAccount(fromAddress);
                if (wallet != null)
                {
                    var addressFrom = wallet.Address;
                    var adnr = Adnr.GetAdnr();
                    if (adnr != null)
                    {
                        var adnrCheck = adnr.FindOne(x => x.Address == addressFrom);
                        if (adnrCheck == null)
                        {
                            output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"This address does not have a DNR associated with it." });
                            return output;
                        }
                        if (!string.IsNullOrWhiteSpace(toAddress))
                        {
                            var addrVerify = AddressValidateUtility.ValidateAddress(toAddress);
                            if (addrVerify == true)
                            {
                                var toAddrAdnr = adnr.FindOne(x => x.Address == toAddress);
                                if(toAddrAdnr == null)
                                {
                                    var result = await Adnr.TransferAdnrTx(fromAddress, toAddress);
                                    if (result.Item1 != null)
                                    {
                                        output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Transaction has been broadcasted.", Hash = result.Item1.Hash });
                                    }
                                    else
                                    {
                                        output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Transaction failed to broadcast. Error: {result.Item2}" });
                                    }
                                }
                                else
                                {
                                    output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"To Address already has adnr associated to it." });
                                }
                            }
                            else
                            {
                                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"To Address is not a valid RBX address." });
                            }

                        }
                        else
                        {
                            output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Name was empty." });
                        }
                    }
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Account with address: {fromAddress} was not found." });
                    return output;
                }
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Unknown Error: {ex.ToString()}" });
            }

            return output;
        }

        /// <summary>
        /// Permanently remove ADNR from address.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        [HttpGet("DeleteAdnr/{address}")]
        public async Task<string> DeleteAdnr(string address)
        {
            string output = "";

            try
            {
                var wallet = AccountData.GetSingleAccount(address);
                if (wallet != null)
                {
                    var addressFrom = wallet.Address;
                    var adnr = Adnr.GetAdnr();
                    if (adnr != null)
                    {
                        var adnrCheck = adnr.FindOne(x => x.Address == addressFrom);
                        if (adnrCheck == null)
                        {
                            output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"This address already has a DNR associated with it: {adnrCheck.Name}" });
                            return output;
                        }

                        var result = await Adnr.DeleteAdnrTx(address);
                        if (result.Item1 != null)
                        {
                            output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Transaction has been broadcasted.", Hash = result.Item1.Hash });
                        }
                        else
                        {
                            output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Transaction failed to broadcast. Error: {result.Item2}" });
                        }
                    }
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Account with address: {address} was not found." });
                    return output;
                }
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Unknown Error: {ex.ToString()}" });
            }

            return output;
        }

        /// <summary>
        /// Shows the consensus broadcast list of txs
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetConsensusBroadcastTx")]
        public async Task<string> GetConsensusBroadcastTx()
        {
            var output = "";

            var txlist = Globals.ConsensusBroadcastedTrxDict.Values.ToList();

            if(txlist.Count > 0)
            {
                output = JsonConvert.SerializeObject(txlist);
            }

            return output;
        }

        /// <summary>
        /// Shows the validator broadcast list of txs
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetValBroadcastTx")]
        public async Task<string> GetValBroadcastTx()
        {
            var output = "";

            var txlist = Globals.BroadcastedTrxDict.Values.ToList();

            if (txlist.Count > 0)
            {
                output = JsonConvert.SerializeObject(txlist);
            }

            return output;
        }

        /// <summary>
        /// Send a transaction. Specify from, to, and amount
        /// </summary>
        /// <param name="faddr"></param>
        /// <param name="taddr"></param>
        /// <param name="amt"></param>
        /// <returns></returns>
        [HttpGet("SendTransaction/{faddr}/{taddr}/{amt}")]
        public async Task<string> SendTransaction(string faddr, string taddr, string amt)
        {
            var output = "FAIL";
            var fromAddress = faddr;
            var toAddress = taddr;
            var strAmount = amt;

            var addrCheck = AddressValidateUtility.ValidateAddress(toAddress);

            if (addrCheck == false)
            {
                output = "This is not a valid RBX address to send to. Please verify again.";
                return output;
            }

            decimal amount = new decimal();

            try
            {
                amount = decimal.Parse(strAmount);
            }
            catch
            {
                return output;
            }

            if (Globals.IsWalletEncrypted == true)
            {
                if (Globals.EncryptPassword.Length > 0)
                {
                    var result = await WalletService.SendTXOut(fromAddress, toAddress, amount);

                    output = result;
                }
                else
                {
                    output = "FAIL. Please type in wallet encryption password first.";
                }
            }
            else
            {
                var result = await WalletService.SendTXOut(fromAddress, toAddress, amount);
                output = result;
            }

            return output;
        }
    }
}
