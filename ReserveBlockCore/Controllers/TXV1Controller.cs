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
        //Step 1.
        [HttpGet("GetTimestamp")]
        public async Task<string> GetTimestamp()
        {
            //use Id to get specific commands
            var output = "FAIL"; // this will only display if command not recognized.

            var timestamp = TimeUtil.GetTime();

            output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Timestamp Acquired.", Timestamp = timestamp });

            return output;
        }

        //Step 2.
        [HttpGet("GetAddressNonce/{address}")]
        public async Task<string> GetAddressNonce(string address)
        {
            //use Id to get specific commands
            var output = "FAIL"; // this will only display if command not recognized.

            var nextNonce = AccountStateTrei.GetNextNonce(address);

            output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Next Nonce Found.", Nonce = nextNonce });

            return output;
        }

        //step 2b if its NFT Minting
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
                output = JsonConvert.SerializeObject(new { Success = false, Message = ex.Message});
            }

            return output;
        }

        //2c - Skip this if you are needing to move files and use beacons.
        [HttpGet("CreateBeaconUploadRequest/{scUID}/{toAddress}/{**signature}")]
        public async Task<string> CreateBeaconUploadRequest(string scUID, string toAddress, string signature)
        {
            var output = "";

            var scStateTrei = SmartContractStateTrei.GetSmartContractState(scUID);
            if (scStateTrei != null)
            {
                var sc = SmartContractMain.GenerateSmartContractInMemory(scStateTrei.ContractData);
                if (sc != null)
                {
                    if (sc.IsPublished == true)
                    {
                        //Get beacons here!
                        var locators = await P2PClient.GetBeacons();
                        if (locators.Count() == 0)
                        {
                            output = "You are not connected to any beacons.";
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

        //2d if its NFT Transfers. This = Transaction.Data 
        [HttpGet("GetNFTTransferData/{scUID}/{toAddress}/{locators}")]
        public async Task<string> GetNFTTransferData(string scUID, string toAddress, string locators)
        {
            var output = "";
            var scStateTrei = SmartContractStateTrei.GetSmartContractState(scUID);

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
                output = JsonConvert.SerializeObject(new { Success = false, Message = ex.Message });
            }

            return output;
        }

        //2e if its NFT Burn. This = Transaction.Data 
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
                output = JsonConvert.SerializeObject(new { Success = false, Message = ex.Message });
            }

            return output;
        }

        //Step 3.
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
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Failed to calcuate Fee. Error: {ex.Message}" });
            }

            return output;
        }

        //Step 4.
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
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Failed to create Hash. Error: {ex.Message}" });
            }

            return output;
        }

        //Step 5.
        //You create the signature now in your application.

        //Step 6.
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
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Signature Not Verified. Unknown Error: {ex.Message}" });
            }
            
            return output;
        }

        //If validation was true
        //Step 7.
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
                    var result = await TransactionValidatorService.VerifyTXDetailed(transaction);
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
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Error - {ex.Message}. Please Try Again." });
            }

            return output;
        }

        //Step 8.
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
                    if (result == true)
                    {
                        TransactionData.AddToPool(transaction);
                        P2PClient.SendTXMempool(transaction);//send out to mempool

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
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Error - {ex.Message}. Please Try Again." });
            }

            return output;
        }

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
                output = $"Error - {ex.Message}. Please Try Again.";
            }

            return output;
        }

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
                            var nameCharCheck = Regex.IsMatch(name, @"^[a-zA-Z0-9]+$");
                            if (!nameCharCheck)
                            {
                                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = "A DNR may only contain letters and numbers." });
                                return output;
                            }
                            else
                            {
                                var nameCheck = adnr.FindOne(x => x.Name == name);
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
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Unknown Error: {ex.Message}" });
            }

            return output;
        }

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
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Unknown Error: {ex.Message}" });
            }

            return output;
        }

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
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Unknown Error: {ex.Message}" });
            }

            return output;
        }

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
