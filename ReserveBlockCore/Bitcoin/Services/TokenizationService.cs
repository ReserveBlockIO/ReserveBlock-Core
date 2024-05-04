using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System.Text;
using ReserveBlockCore.Bitcoin.Models;
using System.IO;
using System;
using ReserveBlockCore.Data;
using ReserveBlockCore.Arbiter;

namespace ReserveBlockCore.Bitcoin.Services
{
    public class TokenizationService
    {
        public static async Task<SmartContractMain?> CreateTokenizationScMain(string address, string fileLocation, string tokenName = "vBTC Token", string description = "vBTC Token")
        {
            try
            {
                string fileName = "";
                string fileExtension = "";
                long fileSizeInBytes = 0;
                if (fileLocation != "default")
                {
                    if (!File.Exists(fileLocation))
                    {
                        return null;
                    }
                    FileInfo fileInfo = new FileInfo(fileLocation);
                    fileName = fileInfo.Name;
                    fileExtension = fileInfo.Extension;
                    fileSizeInBytes = fileInfo.Length;
                }
                else
                {
                    fileName = "defaultvBTC.png";
                    fileExtension = ".png";
                    fileSizeInBytes = 46056;
                    fileLocation = NFTAssetFileUtility.GetvBTCDefaultLogoLocation();
                }
                
                var scMain = new SmartContractMain
                {
                    MinterAddress = address,
                    MinterName = address,
                    Name = tokenName,
                    Description = description,
                    IsPublic = true,
                    SCVersion = Globals.SCVersion,
                    Features = new List<SmartContractFeatures> {
                    new SmartContractFeatures {
                        FeatureName = FeatureName.Tokenization,
                        FeatureFeatures = new TokenizationFeature { AssetName = "Bitcoin", AssetTicker = "BTC" }
                    }
                },
                    IsMinter = true,
                    IsPublished = false,
                    IsToken = false,
                    Id = 0,
                    SmartContractAsset = new SmartContractAsset
                    {
                        AssetId = new Guid(),
                        Name = fileName,
                        Location = fileLocation,
                        AssetAuthorName = "Author Man",
                        Extension = fileExtension,
                        FileSize = fileSizeInBytes
                    },
                };

                return scMain;
            }
            catch (Exception ex)
            {
                NFTLogUtility.Log($"Error creating Tokenization Main. Error: {ex}", "TokenizationService.CreateTokenizationScMain()");
            }

            return null;
        }
        public static async Task<(bool, string)> CreateTokenizationSmartContract(SmartContractMain smartContractMain)
        {
            try
            {
                SmartContractReturnData scReturnData = new SmartContractReturnData();
                var scMain = smartContractMain;

                if(scMain == null)
                {
                    NFTLogUtility.Log($"scMain is null", "TokenizationService.CreateTokenizationSmartContract()");
                    return (false, "");
                }
                
                NFTLogUtility.Log($"Creating Smart Contract: {scMain.SmartContractUID}", "TokenizationService.CreateTokenizationSmartContract()");
                
                try
                {
                    var featureList = scMain.Features;

                    if (featureList?.Count() > 0)
                    {
                        var royalty = featureList.Where(x => x.FeatureName == FeatureName.Royalty).FirstOrDefault();
                        if (royalty != null)
                        {
                            var royaltyFeatures = ((JObject)royalty.FeatureFeatures).ToObject<RoyaltyFeature>();
                            if (royaltyFeatures != null)
                            {
                                if (royaltyFeatures.RoyaltyType == RoyaltyType.Flat)
                                {
                                    throw new Exception("Flat rates may no longer be used.");
                                }
                                if (royaltyFeatures.RoyaltyAmount >= 1.0M)
                                {
                                    throw new Exception("Royalty cannot be over 1. Must be .99 or less.");
                                }
                            }
                        }
                    }

                    scMain.SCVersion = Globals.SCVersion;

                    var result = await SmartContractWriterService.WriteSmartContract(scMain);
                    scReturnData.Success = true;
                    scReturnData.SmartContractCode = result.Item1;
                    scReturnData.SmartContractMain = result.Item2;
                    SmartContractMain.SmartContractData.SaveSmartContract(result.Item2, result.Item1);
                    await TokenizedBitcoin.SaveSmartContract(result.Item2, result.Item1);

                    NFTLogUtility.Log($"Smart Contract Creation Success: {scMain.SmartContractUID}", "TokenizationService.CreateTokenizationSmartContract()");
                    return (true, result.Item2.SmartContractUID);
                }
                catch (Exception ex)
                {
                    NFTLogUtility.Log($"Failed to create TX for Smartcontract. Error: {ex.ToString()}", "TokenizationService.CreateTokenizationSmartContract()");
                    return (false, "");
                }

            }
            catch (Exception ex)
            {
                NFTLogUtility.Log($"Failed to create smart contract. Error Message: {ex.ToString()}", "TokenizationService.CreateTokenizationSmartContract()");
                return (false, "");
            }
        }

        public static async Task<(bool, string)> MintSmartContract(string id, bool returnTx = false, TransactionType txType = TransactionType.NFT_MINT)
        {
            try
            {
                var scMain = SmartContractMain.SmartContractData.GetSmartContract(id);

                if (scMain == null)
                {
                    NFTLogUtility.Log($"This vBTC Token does not exist.", "TokenizationService.MintSmartContract(string id)");
                    return (false, "This vBTC Token does not exist.");
                }

                if (scMain.IsPublished == true)
                {
                    NFTLogUtility.Log($"This vBTC Token has already been published", "TokenizationService.MintSmartContract(string id)");
                    return (false, "This vBTC Token has already been published");
                }
                else
                {
                    var scTx = await SmartContractService.MintSmartContractTx(scMain, txType);
                    if (scTx == null)
                    {
                        NFTLogUtility.Log($"Failed to publish smart contract: {scMain.SmartContractUID}", "TokenizationService.MintSmartContract(string id)");
                        return (false, "Failed to publish smart contract: " + scMain.Name + ". Id: " + id);
                    }
                    else
                    {
                        await TokenizedBitcoin.SetTokenContractIsPublished(scMain.SmartContractUID);
                        NFTLogUtility.Log($"Smart contract has been published to mempool : {scMain.SmartContractUID}", "TokenizationService.MintSmartContract(string id)");

                        if (returnTx)
                            return (true, scTx.Hash);

                        return (true, "Smart contract has been published to mempool");
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, $"Fatal Error: {ex}");
            }
        }

        public static async Task<string> GenerateAddress(string scUID)
        {
            try
            {
                using (var client = Globals.HttpClientFactory.CreateClient())
                {
                    var sc = SmartContractStateTrei.GetSmartContractState(scUID);
                    if (sc == null)
                        return "FAIL";

                    var account = AccountData.GetSingleAccount(sc.OwnerAddress);

                    if (account == null)
                        return "FAIL";

                    var message = TimeUtil.GetTime(1);
                    var signature = SignatureService.CreateSignature(account.PrivateKey, message.ToString());
                    string url = $"{Globals.ArbiterURI}/depositaddress/{account.Address}/{scUID}/{message}/{signature}";
                    var response = await client.GetAsync(url);

                    if(response != null)
                    {
                        if(response.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            var responseContent = await response.Content.ReadAsStringAsync();
                            if (responseContent != null)
                            {
                                ArbiterResponse.ArbiterAddressRequest? arbiterResponse = JsonConvert.DeserializeObject<ArbiterResponse.ArbiterAddressRequest>(responseContent);
                                if (arbiterResponse == null)
                                    return "FAIL";

                                //Add Deposit address to token tool
                                await TokenizedBitcoin.AddDepositAddress(scUID, arbiterResponse.Address);

                                //Update SCMain
                                var scMain = SmartContractMain.SmartContractData.GetSmartContract(scUID);
                                if(scMain != null)
                                {
                                    var nonTokenizedFeatures = scMain.Features?.Where(x => x.FeatureName != FeatureName.Tokenization).ToList();
                                    var tokenFeature = scMain.Features?.Where(x => x.FeatureName == FeatureName.Tokenization).FirstOrDefault();
                                    if(tokenFeature != null)
                                    {
                                        var tokenization = (TokenizationFeature)tokenFeature.FeatureFeatures;
                                        tokenization.DepositAddress = arbiterResponse.Address;
                                        tokenization.Share = arbiterResponse.Share;
                                        tokenization.BackupShare = arbiterResponse.EncryptedShare;

                                        //This may not be necessary will need to step through code.
                                        if(nonTokenizedFeatures?.Count > 0)
                                        {
                                            nonTokenizedFeatures.Add(tokenFeature);
                                            scMain.Features = nonTokenizedFeatures;
                                        }
                                        else
                                        {
                                            scMain.Features = new List<SmartContractFeatures> { tokenFeature };
                                        }

                                        SmartContractMain.SmartContractData.UpdateSmartContract(scMain);
                                    }
                                    //TODO Create TX:
                                    //_ = SmartContractService.UpdateSmartContractTX(scMain);

                                    return arbiterResponse.Address;
                                }
                            }
                        }
                        else
                        {
                            return "FAIL";
                        }
                    }
                }
            }
            catch(Exception ex) { return "FAIL"; }

            return "FAIL";
        }

        public static async Task<List<string>> AddressGenerationMutation(string scUID)
        {
            List<string> shares = new List<string>();

            try
            {
                char[] charArray = scUID.ToCharArray();

                // Mutate the characters in the array
                for (int i = 0; i < charArray.Length; i++)
                {
                    if (char.IsLetter(charArray[i]))
                    {
                        // Increment the ASCII value of letters
                        charArray[i] = (char)(charArray[i] + 1);
                    }
                }

                // Convert the character array back to a string
                string mutatedString = new string(charArray);

                charArray = mutatedString.ToCharArray();
                Array.Reverse(charArray);
                mutatedString = new string(charArray);

                // Convert lowercase letters to uppercase
                mutatedString = mutatedString.ToUpper();

                // Split the input string into equal parts
                int shareSize = mutatedString.Length / 3; // Divide the string into 3 equal parts
                for (int i = 0; i < mutatedString.Length; i += shareSize)
                {
                    // Extract the current share
                    string share = mutatedString.Substring(i, Math.Min(shareSize, mutatedString.Length - i));
                    shares.Add(share);
                }
            }
            catch (Exception ex) 
            { 
            }

            return shares;
        }
    }
}
