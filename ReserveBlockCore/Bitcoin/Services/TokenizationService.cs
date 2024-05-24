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
using System.Runtime.ConstrainedExecution;
using System.Data;
using System.Net;
using System.Xml.Linq;
using NBitcoin;
using System.Security.Principal;

namespace ReserveBlockCore.Bitcoin.Services
{
    public class TokenizationService
    {
        public static async Task<SmartContractMain?> CreateTokenizationScMain(string address, string fileLocation, string depositAddress, string proofJson, string tokenName = "vBTC Token", string description = "vBTC Token")
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
                        FeatureFeatures = new TokenizationFeature { AssetName = "Bitcoin", AssetTicker = "BTC", DepositAddress = depositAddress, PublicKeyProofs = proofJson.ToBase64() }
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
                        AssetAuthorName = "Default",
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

        public static async Task<string> TransferCoin(object? jsonData)
        {
            try
            {
                if (jsonData == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Payload body was null" });

                var payload = JsonConvert.DeserializeObject<BTCTokenizeTransaction>(jsonData.ToString());

                if (payload == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Failed to deserialize payload" });

                if(payload.FromAddress == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"From address cannot be null." });

                var account = AccountData.GetSingleAccount(payload.FromAddress);

                if(account == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Could not find account." });

                var btcTkn = await TokenizedBitcoin.GetTokenizedBitcoin(payload.SCUID);

                if (btcTkn == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Failed to find BTC Token: {payload.SCUID}" });

                var sc = SmartContractMain.SmartContractData.GetSmartContract(btcTkn.SmartContractUID);

                if (sc == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Failed to find Smart Contract Data: {payload.SCUID}" });

                if (sc.Features == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Contract has no features: {payload.SCUID}" });

                var tknzFeature = sc.Features.Where(x => x.FeatureName == FeatureName.Tokenization).Select(x => x.FeatureFeatures).FirstOrDefault();

                if (tknzFeature == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Contract missing a tokenization feature: {payload.SCUID}" });

                var tknz = (TokenizationFeature)tknzFeature;

                if (tknz == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Token feature error: {payload.SCUID}" });

                var scState = SmartContractStateTrei.GetSmartContractState(sc.SmartContractUID);

                if (scState == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"SC State Missing: {payload.SCUID}" });

                bool isOwner = false;
                if (scState.OwnerAddress == account.Address)
                    isOwner = true;

                if(scState.SCStateTreiTokenizationTXes != null)
                {
                    var balances = scState.SCStateTreiTokenizationTXes.Where(x => x.FromAddress == account.Address || x.ToAddress == account.Address).ToList();
                    if(balances.Any() || isOwner)
                    {
                        var balance = balances.Sum(x => x.Amount);
                        bool good = false;
                        if(isOwner)
                        {
                            var finalBalance = btcTkn.Balance + balance;
                            if(finalBalance < payload.Amount)
                                return JsonConvert.SerializeObject(new { Success = false, Message = $"Insufficient Balance. Current Balance: {finalBalance}" });
                            good = true;
                        }
                        else
                        {
                            var finalBalance = balance;
                            if (finalBalance < payload.Amount)
                                return JsonConvert.SerializeObject(new { Success = false, Message = $"Insufficient Balance. Current Balance: {finalBalance}" });
                            good = true;
                        }

                        if(good)
                        {
                            var scTx = new ReserveBlockCore.Models.Transaction();
                            var newSCInfo = new[]
                            {
                                new { Function = "TransferCoin()" }
                            };

                            var txData = JsonConvert.SerializeObject(newSCInfo);

                            scTx = new ReserveBlockCore.Models.Transaction
                            {
                                Timestamp = TimeUtil.GetTime(),
                                FromAddress = payload.FromAddress,
                                ToAddress = payload.ToAddress,
                                Amount = 0.0M,
                                Fee = 0,
                                Nonce = AccountStateTrei.GetNextNonce(payload.FromAddress),
                                TransactionType = TransactionType.TKNZ_TX,
                                Data = txData,
                                UnlockTime = null //TODO: need to make compatible with reserve.
                            };

                            scTx.Fee = ReserveBlockCore.Services.FeeCalcService.CalculateTXFee(scTx);

                            scTx.Build();

                            var senderBalance = AccountStateTrei.GetAccountBalance(payload.FromAddress);
                            if ((scTx.Amount + scTx.Fee) > senderBalance)
                            {
                                scTx.TransactionStatus = TransactionStatus.Failed;
                                TransactionData.AddTxToWallet(scTx, true);
                                NFTLogUtility.Log($"Balance insufficient. SCUID: {payload.SCUID}", "TokenizationService.TransferCoin()");
                                return JsonConvert.SerializeObject(new { Success = false, Message = $"Balance insufficient. SCUID: {payload.SCUID}" });
                            }

                            if (account.GetPrivKey == null)
                            {
                                scTx.TransactionStatus = TransactionStatus.Failed;
                                TransactionData.AddTxToWallet(scTx, true);
                                NFTLogUtility.Log($"Private key was null for account {payload.FromAddress}", "TokenizationService.TransferCoin()");
                                return JsonConvert.SerializeObject(new { Success = false, Message = $"Private key was null for account {payload.FromAddress}" });
                            }
                            var txHash = scTx.Hash;
                            var signature = ReserveBlockCore.Services.SignatureService.CreateSignature(txHash, account.GetPrivKey, account.PublicKey);
                            if (signature == "ERROR")
                            {
                                scTx.TransactionStatus = TransactionStatus.Failed;
                                TransactionData.AddTxToWallet(scTx, true);
                                NFTLogUtility.Log($"TX Signature Failed. SCUID: {payload.SCUID}", "TokenizationService.TransferCoin()");
                                return JsonConvert.SerializeObject(new { Success = false, Message = $"TX Signature Failed. SCUID: {payload.SCUID}" });
                            }

                            scTx.Signature = signature; //sigScript  = signature + '.' (this is a split char) + pubKey in Base58 format


                            if (scTx.TransactionRating == null)
                            {
                                var rating = await TransactionRatingService.GetTransactionRating(scTx);
                                scTx.TransactionRating = rating;
                            }

                            var result = await TransactionValidatorService.VerifyTX(scTx);

                            if (result.Item1 == true)
                            {
                                scTx.TransactionStatus = TransactionStatus.Pending;

                                if (account != null)
                                {
                                    await WalletService.SendTransaction(scTx, account);
                                }
                                //if (rAccount != null)
                                //{
                                //    await WalletService.SendReserveTransaction(scTx, rAccount, true);
                                //}

                                NFTLogUtility.Log($"TX Success. SCUID: {payload.SCUID}", "TokenizationService.TransferCoin()");
                                return JsonConvert.SerializeObject(new { Success = true, Message = "Transaction Success!", Hash = txHash });
                            }
                            else
                            {
                                var output = "Fail! Transaction Verify has failed.";
                                scTx.TransactionStatus = TransactionStatus.Failed;
                                TransactionData.AddTxToWallet(scTx, true);
                                NFTLogUtility.Log($"Error Transfer Failed TX Verify: {payload.SCUID}. Result: {result.Item2}", "TokenizationService.TransferCoin()");
                                return JsonConvert.SerializeObject(new { Success = false, Message = $"Error Transfer Failed TX Verify: {payload.SCUID}. Result: {result.Item2}", Hash = txHash });
                            }
                        }
                    }
                }
                else
                {
                    //just send it
                    //check balance against amount and send.
                }

                //if(string.IsNullOrEmpty(tknz.PublicKeyProofs))
                //    return JsonConvert.SerializeObject(new { Success = false, Message = $"Missing Tokenization Proofs: {payload.SCUID}" });

                //var proofs = JsonConvert.DeserializeObject<List<ArbiterProof>?>(tknz.PublicKeyProofs.ToStringFromBase64());

                //if(proofs == null || !proofs.Any())
                //    return JsonConvert.SerializeObject(new { Success = false, Message = $"Failed to deserialize proofs: {payload.SCUID}" });

                //foreach (var proof in proofs)
                //{
                //    var arbiter = Globals.Arbiters.Where(x => x.SigningAddress == proof.SigningAddress).FirstOrDefault();
                //}


            }
            catch (Exception ex)
            {

            }

            return "ERROR!";
        }
    }
}
