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
using NBitcoin.Protocol;
using ReserveBlockCore.P2P;
using static ReserveBlockCore.Services.ArbiterService;
using System.Security.Cryptography.X509Certificates;

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
                string imageBase64 = "default";
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

                    byte[] imageBytes = File.ReadAllBytes(fileLocation);
                    var imageCompressBase64 = imageBytes.ToCompress().ToBase64();

                    imageBase64 = imageCompressBase64;
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
                        FeatureFeatures = new TokenizationFeature { 
                            AssetName = "Bitcoin", AssetTicker = "BTC", DepositAddress = depositAddress, PublicKeyProofs = proofJson.ToBase64(), ImageBase = imageBase64
                        }
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

                SmartContractReturnData scReturnData = new SmartContractReturnData();
                var result = await SmartContractReaderService.ReadSmartContract(scMain);
                scReturnData.Success = true;
                scReturnData.SmartContractCode = result.Item1;
                scReturnData.SmartContractMain = result.Item2;
                var txData = "";

                //var md5List = await MD5Utility.GetMD5FromSmartContract(scMain);

                if (!string.IsNullOrWhiteSpace(result.Item1))
                {
                    var bytes = Encoding.Unicode.GetBytes(result.Item1);
                    var scBase64 = bytes.ToCompress().ToBase64();
                    string function = result.Item3 ? "TokenDeploy()" : "Mint()";
                    var newSCInfo = new[]
                    {
                        new { Function = function, ContractUID = scMain.SmartContractUID, Data = scBase64, MD5List = "ASSUME SOMETHING HERE"}
                    };

                    txData = JsonConvert.SerializeObject(newSCInfo);
                }
                else
                {
                    return null;
                }

                var nTx = new ReserveBlockCore.Models.Transaction
                {
                    Timestamp = TimeUtil.GetTime(),
                    FromAddress = scReturnData.SmartContractMain.MinterAddress,
                    ToAddress = scReturnData.SmartContractMain.MinterAddress,
                    Amount = 0.0M,
                    Fee = 0,
                    Nonce = AccountStateTrei.GetNextNonce(scMain.MinterAddress),
                    TransactionType = !result.Item3 ? TransactionType.NFT_MINT : TransactionType.FTKN_MINT,
                    Data = txData
                };

                //Calculate fee for tx.
                nTx.Fee = ReserveBlockCore.Services.FeeCalcService.CalculateTXFee(nTx);

                nTx.Build();

                var checkSize = await TransactionValidatorService.VerifyTXSize(nTx);

                if(!checkSize)
                {
                    SCLogUtility.Log($"Transaction was too large. Most likely due to image being too large. TX size must be below 30kb.", "TokenizationService.CreateTokenizationScMain()");
                    return null;
                }

                return scMain;
            }
            catch (Exception ex)
            {
                SCLogUtility.Log($"Error creating Tokenization Main. Error: {ex}", "TokenizationService.CreateTokenizationScMain()");
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
                    SCLogUtility.Log($"scMain is null", "TokenizationService.CreateTokenizationSmartContract()");
                    return (false, "");
                }
                
                SCLogUtility.Log($"Creating Smart Contract: {scMain.SmartContractUID}", "TokenizationService.CreateTokenizationSmartContract()");
                
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

                    SCLogUtility.Log($"Smart Contract Creation Success: {scMain.SmartContractUID}", "TokenizationService.CreateTokenizationSmartContract()");
                    return (true, result.Item2.SmartContractUID);
                }
                catch (Exception ex)
                {
                    SCLogUtility.Log($"Failed to create TX for Smartcontract. Error: {ex.ToString()}", "TokenizationService.CreateTokenizationSmartContract()");
                    return (false, "");
                }

            }
            catch (Exception ex)
            {
                SCLogUtility.Log($"Failed to create smart contract. Error Message: {ex.ToString()}", "TokenizationService.CreateTokenizationSmartContract()");
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
                    SCLogUtility.Log($"This vBTC Token does not exist.", "TokenizationService.MintSmartContract(string id)");
                    return (false, "This vBTC Token does not exist.");
                }

                if (scMain.IsPublished == true)
                {
                    SCLogUtility.Log($"This vBTC Token has already been published", "TokenizationService.MintSmartContract(string id)");
                    return (false, "This vBTC Token has already been published");
                }
                else
                {
                    var scTx = await SmartContractService.MintSmartContractTx(scMain, txType);
                    if (scTx == null)
                    {
                        SCLogUtility.Log($"Failed to publish smart contract: {scMain.SmartContractUID}", "TokenizationService.MintSmartContract(string id)");
                        return (false, "Failed to publish smart contract: " + scMain.Name + ". Id: " + id);
                    }
                    else
                    {
                        await TokenizedBitcoin.SetTokenContractIsPublished(scMain.SmartContractUID);
                        SCLogUtility.Log($"Smart contract has been published to mempool : {scMain.SmartContractUID}", "TokenizationService.MintSmartContract(string id)");

                        if (returnTx)
                            return (true, scTx.Hash);

                        return (true, "Smart contract has been published to mempool");
                    }
                }
            }
            catch (Exception ex)
            {
                SCLogUtility.Log($"Fatal Error: {ex}", "TokenizationService.MintSmartContract(string id)");
                return (false, $"Fatal Error: {ex}");
            }
        }

        public static async Task<string> TransferOwnership(string scUID, string toAddress, string? backupURL = "")
        {
            var btcTkn = await TokenizedBitcoin.GetTokenizedBitcoin(scUID);

            if (btcTkn == null)
                return await SCLogUtility.LogAndReturn($"Failed to find BTC Token: {scUID}", "TokenizationService.TransferOwnership()", false);

            var sc = SmartContractMain.SmartContractData.GetSmartContract(scUID);

            if (sc == null)
                return await SCLogUtility.LogAndReturn($"Failed to find Smart Contract Data: {scUID}", "TokenizationService.TransferOwnership()", false);

            if (sc.Features == null)
                return await SCLogUtility.LogAndReturn($"Contract has no features: {scUID}", "TokenizationService.TransferOwnership()", false);

            var tknzFeature = sc.Features.Where(x => x.FeatureName == FeatureName.Tokenization).Select(x => x.FeatureFeatures).FirstOrDefault();

            if (tknzFeature == null)
                return await SCLogUtility.LogAndReturn($"Contract missing a tokenization feature: {scUID}", "TokenizationService.TransferOwnership()", false);

            var tknz = (TokenizationFeature)tknzFeature;

            if (tknz == null)
                return await SCLogUtility.LogAndReturn($"Token feature error: {scUID}", "TokenizationService.TransferOwnership()", false);

            var scState = SmartContractStateTrei.GetSmartContractState(sc.SmartContractUID);

            if (scState == null)
                return await SCLogUtility.LogAndReturn($"SC State Missing: {scUID}", "TokenizationService.TransferOwnership()", false);

            //Checking to see if owner account is present.
            var account = AccountData.GetSingleAccount(scState.OwnerAddress);

            if (account == null)
                return await SCLogUtility.LogAndReturn($"Owner address account not found.", "TokenizationService.TransferOwnership()", false);

            if (!Globals.Beacons.Any())
                return await SCLogUtility.LogAndReturn("Error - You do not have any beacons stored.", "TokenizationService.TransferOwnership()", false);

            if (!Globals.Beacon.Values.Where(x => x.IsConnected).Any())
            {
                var beaconConnectionResult = await BeaconUtility.EstablishBeaconConnection(true, false);
                if (!beaconConnectionResult)
                {
                    return await SCLogUtility.LogAndReturn("Error - You failed to connect to any beacons.", "TokenizationService.TransferOwnership()", false);
                }
            }

            var connectedBeacon = Globals.Beacon.Values.Where(x => x.IsConnected).FirstOrDefault();
            if (connectedBeacon == null)
                return await SCLogUtility.LogAndReturn("Error - You have lost connection to beacons. Please attempt to resend.", "TokenizationService.TransferOwnership()", false);


            toAddress = toAddress.Replace(" ", "").ToAddressNormalize();
            var localAddress = AccountData.GetSingleAccount(toAddress);

            var assets = await NFTAssetFileUtility.GetAssetListFromSmartContract(sc);
            var md5List = await MD5Utility.GetMD5FromSmartContract(sc);

            SCLogUtility.Log($"Sending the following assets for upload: {md5List}", "TokenizationService.TransferOwnership()");

            bool result = false;
            if (localAddress == null)
            {
                result = await P2PClient.BeaconUploadRequest(connectedBeacon, assets, sc.SmartContractUID, toAddress, md5List).WaitAsync(new TimeSpan(0, 0, 10));
                SCLogUtility.Log($"SC Beacon Upload Request Completed. SCUID: {sc.SmartContractUID}", "TokenizationService.TransferOwnership()");
            }
            else
            {
                result = true;
            }

            if (result == true)
            {
                var aqResult = AssetQueue.CreateAssetQueueItem(sc.SmartContractUID, toAddress, connectedBeacon.Beacons.BeaconLocator, md5List, assets,
                    AssetQueue.TransferType.Upload);
                SCLogUtility.Log($"SC Asset Queue Items Completed. SCUID: {sc.SmartContractUID}", "TokenizationService.TransferOwnership()");

                if (aqResult)
                {
                    _ = Task.Run(() => SmartContractService.TransferSmartContract(sc, toAddress, connectedBeacon, md5List, backupURL, false, null, 0, TransactionType.TKNZ_TX));
                    var success = JsonConvert.SerializeObject(new { Success = true, Message = "vBTC Token Transfer has been started." });
                    SCLogUtility.Log($"SC Process Completed in CLI. SCUID: {sc.SmartContractUID}. Response: {success}", "TokenizationService.TransferOwnership()");
                    return success;
                }
                else
                {
                    return await SCLogUtility.LogAndReturn($"Failed to add upload to Asset Queue - TX terminated. Data: scUID: {sc.SmartContractUID} | toAddres: {toAddress} | Locator: {connectedBeacon.Beacons.BeaconLocator} | MD5List: {md5List} | backupURL: {backupURL}", "TokenizationService.TransferOwnership()", false);
                }

            }
            else
            {
                return await SCLogUtility.LogAndReturn($"Beacon upload failed. Result was : {result}", "TokenizationService.TransferOwnership()", false);
            }
        }

        public static async Task<string> TransferCoin(BTCTokenizeTransaction? jsonData)
        {
            try
            {
                if (jsonData == null)
                    return await SCLogUtility.LogAndReturn($"Payload body was null", "TokenizationService.TransferCoin()", false);

                var payload = jsonData;

                if (payload == null)
                    return await SCLogUtility.LogAndReturn($"Failed to deserialize payload", "TokenizationService.TransferCoin()", false);

                if(payload.FromAddress == null)
                    return await SCLogUtility.LogAndReturn($"From address cannot be null.", "TokenizationService.TransferCoin()", false);

                var account = AccountData.GetSingleAccount(payload.FromAddress);

                if(account == null)
                    return await SCLogUtility.LogAndReturn($"Could not find account.", "TokenizationService.TransferCoin()", false);

                var btcTkn = await TokenizedBitcoin.GetTokenizedBitcoin(payload.SCUID);

                if (btcTkn == null)
                    return await SCLogUtility.LogAndReturn($"Failed to find BTC Token: {payload.SCUID}", "TokenizationService.TransferCoin()", false);

                var sc = SmartContractMain.SmartContractData.GetSmartContract(btcTkn.SmartContractUID);

                if (sc == null)
                    return await SCLogUtility.LogAndReturn($"Failed to find Smart Contract Data: {payload.SCUID}", "TokenizationService.TransferCoin()", false);

                if (sc.Features == null)
                    return await SCLogUtility.LogAndReturn($"Contract has no features: {payload.SCUID}", "TokenizationService.TransferCoin()", false);

                var tknzFeature = sc.Features.Where(x => x.FeatureName == FeatureName.Tokenization).Select(x => x.FeatureFeatures).FirstOrDefault();

                if (tknzFeature == null)
                    return await SCLogUtility.LogAndReturn($"Contract missing a tokenization feature: {payload.SCUID}", "TokenizationService.TransferCoin()", false);

                var tknz = (TokenizationFeature)tknzFeature;

                if (tknz == null)
                    return await SCLogUtility.LogAndReturn($"Token feature error: {payload.SCUID}", "TokenizationService.TransferCoin()", false);

                var scState = SmartContractStateTrei.GetSmartContractState(sc.SmartContractUID);

                if (scState == null)
                    return await SCLogUtility.LogAndReturn($"SC State Missing: {payload.SCUID}", "TokenizationService.TransferCoin()", false);

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
                                return await SCLogUtility.LogAndReturn($"Insufficient Balance. Current Balance: {finalBalance}", "TokenizationService.TransferCoin()", false);
                            good = true;
                        }
                        else
                        {
                            var finalBalance = balance;
                            if (finalBalance < payload.Amount)
                                return await SCLogUtility.LogAndReturn($"Insufficient Balance. Current Balance: {finalBalance}", "TokenizationService.TransferCoin()", false);
                            good = true;
                        }

                        if(good)
                        {
                            var scTxResult = await CreateVFXTokenizedTransaction(payload.FromAddress, payload.ToAddress, account, payload.Amount, payload.SCUID);

                            if(scTxResult.Item1 == null)
                                return await SCLogUtility.LogAndReturn(scTxResult.Item2, "TokenizationService.TransferCoin()", false);

                            var scTx = scTxResult.Item1;

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

                                SCLogUtility.Log($"TX Success. SCUID: {payload.SCUID}", "TokenizationService.TransferCoin()");
                                VFXLogging.LogInfo($"TX Success. SCUID: {payload.SCUID}", "TokenizationService.TransferCoin()", true);
                                return JsonConvert.SerializeObject(new { Success = true, Message = "Transaction Success!", Hash = scTx.Hash });
                            }
                            else
                            {
                                var output = "Fail! Transaction Verify has failed.";
                                scTx.TransactionStatus = TransactionStatus.Failed;
                                TransactionData.AddTxToWallet(scTx, true);
                                return await SCLogUtility.LogAndReturn($"Error Transfer Failed TX Verify: {payload.SCUID}. Result: {result.Item2}", "TokenizationService.TransferCoin()", false);
                            }
                        }
                    }
                }
                else
                {
                    if (btcTkn.MyBalance < payload.Amount)
                        return await SCLogUtility.LogAndReturn($"Insufficient Balance. Current Balance: {btcTkn.MyBalance}", "TokenizationService.TransferCoin()", false);

                    var scTxResult = await CreateVFXTokenizedTransaction(payload.FromAddress, payload.ToAddress, account, payload.Amount, payload.SCUID);

                    if (scTxResult.Item1 == null)
                        return await SCLogUtility.LogAndReturn(scTxResult.Item2, "TokenizationService.TransferCoin()", false);

                    var scTx = scTxResult.Item1;

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

                        SCLogUtility.Log($"TX Success. SCUID: {payload.SCUID}", "TokenizationService.TransferCoin()");
                        return JsonConvert.SerializeObject(new { Success = true, Message = "Transaction Success!", Hash = scTx.Hash });
                    }
                    else
                    {
                        var output = "Fail! Transaction Verify has failed.";
                        scTx.TransactionStatus = TransactionStatus.Failed;
                        TransactionData.AddTxToWallet(scTx, true);
                        return await SCLogUtility.LogAndReturn($"Error Transfer Failed TX Verify: {payload.SCUID}. Result: {result.Item2}", "TokenizationService.TransferCoin()", false);
                    }
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
                return await SCLogUtility.LogAndReturn($"Unknown Error: {ex}", "TokenizationService.TransferCoin()", false);
            }

            return await SCLogUtility.LogAndReturn($"EOM ERROR", "TokenizationService.TransferCoin()", false);
        }

        public static async Task<string> WithdrawalCoin(string address, string toAddress, string scUID, decimal amount, long chosenFeeRate = 10)
        {
            try
            {
                var account = AccountData.GetSingleAccount(address);

                if(account == null)
                    return await SCLogUtility.LogAndReturn($"Account was either not found, or you are attempting to withdrawal from a Reserve Account.", "TokenizationService.WithdrawalCoin()", false);

                var scMain = SmartContractMain.SmartContractData.GetSmartContract(scUID);

                if (scMain == null)
                    return await SCLogUtility.LogAndReturn($"Could not find smart contract.", "TokenizationService.WithdrawalCoin()", false);

                var scState = SmartContractStateTrei.GetSmartContractState(scUID);

                if (scState == null)
                    return await SCLogUtility.LogAndReturn($"Could not find smart contract state.", "TokenizationService.WithdrawalCoin()", false);

                var btcTkn = await TokenizedBitcoin.GetTokenizedBitcoin(scUID);

                if (btcTkn == null)
                    return await SCLogUtility.LogAndReturn($"Could not find vBTC Token.", "TokenizationService.WithdrawalCoin()", false);

                if(btcTkn.DepositAddress == null)
                    return await SCLogUtility.LogAndReturn($"Deposit Address cannot be null!", "TokenizationService.WithdrawalCoin()", false);

                if (scMain.Features == null)
                    return await SCLogUtility.LogAndReturn($"Could not find smart contract features", "TokenizationService.WithdrawalCoin()", false);

                var tknzFeature = scMain.Features.Where(x => x.FeatureName == FeatureName.Tokenization).Select(x => x.FeatureFeatures).FirstOrDefault();

                if (tknzFeature == null)
                    return await SCLogUtility.LogAndReturn($"Found smart contract features, but not a vBTC token feature.", "TokenizationService.WithdrawalCoin()", false);

                var tknz = (TokenizationFeature)tknzFeature;

                if (tknz == null)
                    return await SCLogUtility.LogAndReturn($"Token feature was null.", "TokenizationService.WithdrawalCoin()", false);

                var isOwner = scState.OwnerAddress == address ? true : false;

                var vBTCBalances = scState.SCStateTreiTokenizationTXes?.Where(x => x.ToAddress == address || x.FromAddress == address).ToList();

                if (vBTCBalances == null && !isOwner)
                    return await SCLogUtility.LogAndReturn($"Balances were null.", "TokenizationService.WithdrawalCoin()", false);

                if(amount >= btcTkn.Balance)
                    return await SCLogUtility.LogAndReturn($"Withdrawal amount cannot exceed the total balance of the vBTC token.", "TokenizationService.WithdrawalCoin()", false);

                if(vBTCBalances != null)
                {
                    var balance = vBTCBalances.Sum(x => x.Amount);
                    bool good = false;
                    if (isOwner)
                    {
                        var finalBalance = btcTkn.Balance + balance;
                        if (finalBalance < amount)
                            return await SCLogUtility.LogAndReturn($"Insufficient Balances for Owner", "TokenizationService.WithdrawalCoin()", false); ;

                        good = true;
                    }
                    else
                    {
                        var finalBalance = balance;
                        if (finalBalance < amount)
                            return await SCLogUtility.LogAndReturn($"Insufficient Balances for sub Owner", "TokenizationService.WithdrawalCoin()", false);
                        good = true;
                    }

                    if (good)
                    {
                        //pass to transaction now.
                        var arbProofs = JsonConvert.DeserializeObject<List<ArbiterProof>>(tknz.PublicKeyProofs.ToStringFromBase64());
                        List<PubKey> pubKeys = new List<PubKey>();

                        foreach (var proof in arbProofs)
                        {
                            PubKey pubKey = new PubKey(proof.PublicKey);
                            pubKeys.Add(pubKey);
                        }
                        return await TransactionService.SendMultiSigTransactions(pubKeys, amount, toAddress, btcTkn.DepositAddress, chosenFeeRate, scUID);
                    }
                }
                else if(isOwner)
                {
                    //Do this is you are owner and there are no state level balances yet.
                    //pass to transaction now.
                    var arbProofs = JsonConvert.DeserializeObject<List<ArbiterProof>>(tknz.PublicKeyProofs.ToStringFromBase64());
                    List<PubKey> pubKeys = new List<PubKey>();

                    foreach (var proof in arbProofs)
                    {
                        PubKey pubKey = new PubKey(proof.PublicKey);
                        pubKeys.Add(pubKey);
                    }
                    return await TransactionService.SendMultiSigTransactions(pubKeys, amount, toAddress, btcTkn.DepositAddress, chosenFeeRate, scUID);
                }
                else
                {
                    return await SCLogUtility.LogAndReturn($"No balances and you are not the owner.", "TokenizationService.WithdrawalCoin()", false);
                }


            }
            catch (Exception ex)
            {
                return await SCLogUtility.LogAndReturn($"Unknown Error: {ex}", "TokenizationService.WithdrawalCoin()", false);
            }

            return await SCLogUtility.LogAndReturn($"EOM ERROR", "TokenizationService.WithdrawalCoin()", false);
        }

        private static async Task<(ReserveBlockCore.Models.Transaction?, string)> CreateVFXTokenizedTransaction(string fromAddress, string toAddress, Account account, decimal amount, string scUID)
        {
            var scTx = new ReserveBlockCore.Models.Transaction();
            var newSCInfo = new[]
            {
                                new { Function = "TransferCoin()", ContractUID = scUID, Amount = amount,  }
                            };

            var txData = JsonConvert.SerializeObject(newSCInfo);

            scTx = new ReserveBlockCore.Models.Transaction
            {
                Timestamp = TimeUtil.GetTime(),
                FromAddress = fromAddress,
                ToAddress = toAddress,
                Amount = 0.0M,
                Fee = 0,
                Nonce = AccountStateTrei.GetNextNonce(fromAddress),
                TransactionType = TransactionType.TKNZ_TX,
                Data = txData,
                UnlockTime = null //TODO: need to make compatible with reserve.
            };

            scTx.Fee = ReserveBlockCore.Services.FeeCalcService.CalculateTXFee(scTx);

            scTx.Build();

            var senderBalance = AccountStateTrei.GetAccountBalance(fromAddress);
            if ((scTx.Amount + scTx.Fee) > senderBalance)
            {
                scTx.TransactionStatus = TransactionStatus.Failed;
                TransactionData.AddTxToWallet(scTx, true);
                return (null, $"Balance insufficient. SCUID: {scUID}");
            }

            if (account.GetPrivKey == null)
            {
                scTx.TransactionStatus = TransactionStatus.Failed;
                TransactionData.AddTxToWallet(scTx, true);
                return (null, $"Private key was null for account {fromAddress}");
            }
            var txHash = scTx.Hash;
            var signature = ReserveBlockCore.Services.SignatureService.CreateSignature(txHash, account.GetPrivKey, account.PublicKey);
            if (signature == "ERROR")
            {
                scTx.TransactionStatus = TransactionStatus.Failed;
                TransactionData.AddTxToWallet(scTx, true);
                return (null, $"TX Signature Failed. SCUID: {scUID}");
            }

            scTx.Signature = signature; //sigScript  = signature + '.' (this is a split char) + pubKey in Base58 format


            if (scTx.TransactionRating == null)
            {
                var rating = await TransactionRatingService.GetTransactionRating(scTx);
                scTx.TransactionRating = rating;
            }

            return (scTx, "");
        }
    }
}
