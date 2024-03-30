using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System.Text;
using ReserveBlockCore.Bitcoin.Models;

namespace ReserveBlockCore.Bitcoin.Services
{
    public class TokenizationService
    {
        public static async Task<SmartContractMain?> CreateTokenizationScMain(string address, string fileLocation, string tokenName = "vBTC Token", string description = "vBTC Token")
        {
            try
            {
                if (!File.Exists(fileLocation))
                {
                    return null;
                }
                FileInfo fileInfo = new FileInfo(fileLocation);
                var fileName = fileInfo.Name;
                var fileExtension = fileInfo.Extension;
                var fileSizeInBytes = fileInfo.Length;

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

        public static async Task<(bool, string)> MintSmartContract(string id, bool returnTx = false)
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
                    var scTx = await SmartContractService.MintSmartContractTx(scMain);
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

        public static async Task<string> GenerateAddressFromMPC()
        {
            try
            {
                Random random = new Random();
                string address = "btc"; // btc-like address format
                for (int i = 0; i < 40; i++)
                {
                    address += random.Next(16).ToString("X"); // Random hexadecimal digit
                }

                if (address.Length > 100)
                    throw new Exception("Error: Unable to connect to MPC to generate address.");

                return address;
            }
            catch(Exception ex) { return "FAIL"; }
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
