using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Utilities;
using System;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Security.Principal;
using System.Text;
using System.Xml.Linq;

namespace ReserveBlockCore.Services
{
    public static class SmartContractService
    {
        #region MintSmartContractTx
        public static async Task<Transaction?> MintSmartContractTx(SmartContractMain scMain)
        {
            Transaction? scTx = null;

            var account = AccountData.GetSingleAccount(scMain.MinterAddress);
            if (account == null)
            {
                NFTLogUtility.Log($"Minter address is not found for : {scMain.SmartContractUID}", "SmartContractService.MintSmartContractTx(SmartContractMain scMain)");
                return null;//Minter address is not found
            }

            var scStateTrei = SmartContractStateTrei.GetSmartContractState(scMain.SmartContractUID);

            if(scStateTrei != null)
            {
                NFTLogUtility.Log($"This NFT has already be minted : {scMain.SmartContractUID}", "SmartContractService.MintSmartContractTx(SmartContractMain scMain)");
                return null;// record already exist
            }

            var scData = await SmartContractReaderService.ReadSmartContract(scMain);

            var txData = "";

            var md5List = await MD5Utility.GetMD5FromSmartContract(scMain);

            if (!string.IsNullOrWhiteSpace(scData.Item1))
            {
                var bytes = Encoding.Unicode.GetBytes(scData.Item1);
                var scBase64 = bytes.ToCompress().ToBase64();
                var newSCInfo = new[]
                {
                    new { Function = "Mint()", ContractUID = scMain.SmartContractUID, Data = scBase64, MD5List = md5List}
                };

                txData = JsonConvert.SerializeObject(newSCInfo);
            }

            scTx = new Transaction
            {
                Timestamp = TimeUtil.GetTime(),
                FromAddress = scMain.MinterAddress,
                ToAddress = scMain.MinterAddress,
                Amount = 0.0M,
                Fee = 0,
                Nonce = AccountStateTrei.GetNextNonce(scMain.MinterAddress),
                TransactionType = TransactionType.NFT_MINT,
                Data = txData
            };

            scTx.Fee = FeeCalcService.CalculateTXFee(scTx);

            scTx.Build();

            var senderBalance = AccountStateTrei.GetAccountBalance(account.Address);
            if ((scTx.Amount + scTx.Fee) > senderBalance)
            {
                NFTLogUtility.Log($"Balance insufficient to send NFT : {scMain.SmartContractUID}", "SmartContractService.MintSmartContractTx(SmartContractMain scMain)");
                return null;//balance insufficient
            }

            BigInteger b1 = BigInteger.Parse(account.GetKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

            var txHash = scTx.Hash;
            var signature = SignatureService.CreateSignature(txHash, privateKey, account.PublicKey);
            if (signature == "ERROR")
            {
                NFTLogUtility.Log($"Signing NFT TX failed : {scMain.SmartContractUID}", "SmartContractService.MintSmartContractTx(SmartContractMain scMain)");
                return null; //TX sig failed
            }

            scTx.Signature = signature; //sigScript  = signature + '.' (this is a split char) + pubKey in Base58 format

            try
            {
                if (scTx.TransactionRating == null)
                {
                    var rating = await TransactionRatingService.GetTransactionRating(scTx);
                    scTx.TransactionRating = rating;
                }

                var result = await TransactionValidatorService.VerifyTX(scTx);
                if (result.Item1 == true)
                {
                    scTx.TransactionStatus = TransactionStatus.Pending;

                    if (account.IsValidating == true && (account.Balance - (scTx.Fee + scTx.Amount) < ValidatorService.ValidatorRequiredAmount()))
                    {
                        var validator = Validators.Validator.GetAll().FindOne(x => x.Address.ToLower() == scTx.FromAddress.ToLower());
                        ValidatorService.StopValidating(validator);
                        TransactionData.AddToPool(scTx);
                        TransactionData.AddTxToWallet(scTx, true);
                        AccountData.UpdateLocalBalance(scTx.FromAddress, (scTx.Fee + scTx.Amount));
                        await P2PClient.SendTXMempool(scTx);//send out to mempool
                    }
                    else if (account.IsValidating)
                    {
                        TransactionData.AddToPool(scTx);
                        TransactionData.AddTxToWallet(scTx, true);
                        AccountData.UpdateLocalBalance(scTx.FromAddress, (scTx.Fee + scTx.Amount));
                        await P2PClient.SendTXToAdjudicator(scTx);//send directly to adjs
                    }
                    else
                    {
                        TransactionData.AddToPool(scTx);
                        TransactionData.AddTxToWallet(scTx, true);
                        AccountData.UpdateLocalBalance(scTx.FromAddress, (scTx.Fee + scTx.Amount));
                        await P2PClient.SendTXMempool(scTx);//send out to mempool
                    }
                    return scTx;
                }
                else
                {
                    NFTLogUtility.Log($"Transaction Failed Verify and was not Sent to Mempool : {scMain.SmartContractUID}. Result: {result.Item2}", "SmartContractService.MintSmartContractTx(SmartContractMain scMain)");
                    var output = "Fail! Transaction Verify has failed.";
                    return null;
                }
            }
            catch (Exception ex)
            {                
                Console.WriteLine("Error: {0}", ex.ToString());
                NFTLogUtility.Log($"Error Minting Smart Contract: {ex.ToString()}", "SmartContractService.MintSmartContractTx(SmartContractMain scMain)");
            }

            return null;
        }

        #endregion

        #region TransferSmartContract
        public static async Task TransferSmartContract(SmartContractMain scMain, string toAddress, BeaconNodeInfo beaconNodeInfo, string md5List = "NA", string backupURL = "", bool isReserveAccount = false, PrivateKey? reserveAccountKey = null, int unlockTime = 0)
        {
            var scTx = new Transaction();
            try
            {
                var assets = await NFTAssetFileUtility.GetAssetListFromSmartContract(scMain);

                bool beaconSendFinalResult = true;
                if (assets.Count() > 0)
                {
                    NFTLogUtility.Log($"NFT Asset Transfer Beginning for: {scMain.SmartContractUID}. Assets: {assets}", "SCV1Controller.TransferNFT()");
                    foreach (var asset in assets)
                    {
                        var sendResult = await BeaconUtility.SendAssets(scMain.SmartContractUID, asset, beaconNodeInfo.Beacons.BeaconLocator);
                        if (!sendResult)
                            beaconSendFinalResult = false;
                    }

                    beaconNodeInfo.Uploading = false;
                    Globals.Beacon[beaconNodeInfo.IPAddress] = beaconNodeInfo;

                    NFTLogUtility.Log($"NFT Asset Transfer Done for: {scMain.SmartContractUID}.", "SCV1Controller.TransferNFT()");
                }
                if (beaconSendFinalResult)
                {
                    var scst = SmartContractStateTrei.GetSmartContractState(scMain.SmartContractUID);

                    if (scst == null)
                    {
                        NFTLogUtility.Log($"Failed to find SC Locally. SCUID: {scMain.SmartContractUID}", "SmartContractService.TransferSmartContract()");
                        return;
                    }

                    toAddress = toAddress.Replace(" ", "");

                    var account = AccountData.GetSingleAccount(scst.OwnerAddress);
                    var rAccount = isReserveAccount ? ReserveAccount.GetReserveAccountSingle(scst.OwnerAddress) : null;
                    if (account == null && rAccount == null)
                    {
                        NFTLogUtility.Log($"Minter address not found. SCUID: {scMain.SmartContractUID}", "SmartContractService.TransferSmartContract()");
                        return;
                    }
                    var fromAddress = !isReserveAccount ? account?.Address : rAccount?.Address;
                    var publicKey = !isReserveAccount ? account?.PublicKey : rAccount?.PublicKey;
                    var privateKey = !isReserveAccount ? account?.GetPrivKey : reserveAccountKey;

                    var scData = SmartContractReaderService.ReadSmartContract(scMain);

                    var txData = "";


                    if (!string.IsNullOrWhiteSpace(scData.Result.Item1))
                    {
                        var bytes = Encoding.Unicode.GetBytes(scData.Result.Item1);
                        var scBase64 = SmartContractUtility.Compress(bytes).ToBase64();
                        var newSCInfo = new[]
                        {
                        new { Function = "Transfer()", ContractUID = scMain.SmartContractUID, ToAddress = toAddress, Data = scBase64,
                            Locators = beaconNodeInfo.Beacons.BeaconLocator, MD5List = md5List, BackupURL = backupURL != "" ? backupURL : "NA"}
                    };

                        txData = JsonConvert.SerializeObject(newSCInfo);
                    }

                    scTx = new Transaction
                    {
                        Timestamp = TimeUtil.GetTime(),
                        FromAddress = fromAddress,
                        ToAddress = toAddress,
                        Amount = 0.0M,
                        Fee = 0,
                        Nonce = AccountStateTrei.GetNextNonce(fromAddress),
                        TransactionType = TransactionType.NFT_TX,
                        Data = txData,
                        UnlockTime = rAccount != null ? TimeUtil.GetReserveTime(unlockTime) : null
                    };

                    scTx.Fee = FeeCalcService.CalculateTXFee(scTx);

                    scTx.Build();

                    var senderBalance = AccountStateTrei.GetAccountBalance(fromAddress);
                    if ((scTx.Amount + scTx.Fee) > senderBalance)
                    {
                        scTx.TransactionStatus = TransactionStatus.Failed;
                        TransactionData.AddTxToWallet(scTx, true);
                        NFTLogUtility.Log($"Balance insufficient. SCUID: {scMain.SmartContractUID}", "SmartContractService.TransferSmartContract()");
                        return;
                    }

                    //BigInteger b1 = BigInteger.Parse(account.GetKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
                    //PrivateKey privateKey = isReserveAccount && reserveAccountKey != null ? reserveAccountKey : new PrivateKey("secp256k1", b1);

                    if (privateKey == null)
                    {
                        scTx.TransactionStatus = TransactionStatus.Failed;
                        TransactionData.AddTxToWallet(scTx, true);
                        NFTLogUtility.Log($"Private key was null for account {fromAddress}", "SmartContractService.TransferSmartContract()");
                        return;
                    }

                    var txHash = scTx.Hash;
                    var signature = SignatureService.CreateSignature(txHash, privateKey, publicKey);
                    if (signature == "ERROR")
                    {
                        scTx.TransactionStatus = TransactionStatus.Failed;
                        TransactionData.AddTxToWallet(scTx, true);
                        NFTLogUtility.Log($"TX Signature Failed. SCUID: {scMain.SmartContractUID}", "SmartContractService.TransferSmartContract()");
                        return;
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
                        if(rAccount != null)
                        {
                            await WalletService.SendReserveTransaction(scTx, rAccount, true);
                        }
                        
                        NFTLogUtility.Log($"TX Success. SCUID: {scMain.SmartContractUID}", "SmartContractService.TransferSmartContract()");
                        return;
                    }
                    else
                    {
                        var output = "Fail! Transaction Verify has failed.";
                        scTx.TransactionStatus = TransactionStatus.Failed;
                        TransactionData.AddTxToWallet(scTx, true);
                        NFTLogUtility.Log($"Error Transfer Failed TX Verify: {scMain.SmartContractUID}. Result: {result.Item2}", "SmartContractService.TransferSmartContract()");
                        return;
                    }
                
                }
                else
                {
                    NFTLogUtility.Log($"Failed to upload to Beacon - TX terminated. Data: scUID: {scMain.SmartContractUID} | toAddres: {toAddress} | Locator: {beaconNodeInfo.Beacons.BeaconLocator} | MD5List: {md5List} | backupURL: {backupURL}", "SCV1Controller.TransferNFT()");
                }
            }
            catch (Exception ex)
            {
                scTx.Timestamp = TimeUtil.GetTime();
                scTx.TransactionStatus = TransactionStatus.Failed;
                scTx.TransactionType = TransactionType.NFT_TX;
                scTx.ToAddress = toAddress;
                scTx.FromAddress = !string.IsNullOrEmpty(scTx.FromAddress) ? scTx.FromAddress : "FAIL";
                scTx.Amount = 0.0M;
                scTx.Fee = 0;
                scTx.Nonce = scTx.Nonce != 0 ? scTx.Nonce : 0;

                scTx.Fee = FeeCalcService.CalculateTXFee(scTx);
                scTx.Signature = "FAIL";

                scTx.Build();

                scTx.TransactionRating = TransactionRating.F;

                TransactionData.AddTxToWallet(scTx, true);
                NFTLogUtility.Log($"Error Transferring Smart Contract: {ex.ToString()}", "SmartContractService.TransferSmartContract()");
            }
        }

        #endregion

        #region BurnSmartContract
        public static async Task<Transaction?> BurnSmartContract(SmartContractMain scMain)
        {
            Transaction? scTx = null;

            var scst = SmartContractStateTrei.GetSmartContractState(scMain.SmartContractUID);

            if (scst == null)
            {
                return null;
            }

            var account = AccountData.GetSingleAccount(scst.OwnerAddress);
            if (account == null)
            {
                return null;//Minter address is not found
            }

            var scData = SmartContractReaderService.ReadSmartContract(scMain);

            var txData = "";

            if (!string.IsNullOrWhiteSpace(scData.Result.Item1))
            {
                var bytes = Encoding.Unicode.GetBytes(scData.Result.Item1);
                var scBase64 = SmartContractUtility.Compress(bytes).ToBase64();
                var newSCInfo = new[]
                {
                    new { Function = "Burn()", ContractUID = scMain.SmartContractUID, FromAddress = account.Address}
                };

                txData = JsonConvert.SerializeObject(newSCInfo);
            }

            scTx = new Transaction
            {
                Timestamp = TimeUtil.GetTime(),
                FromAddress = account.Address,
                ToAddress = account.Address,
                Amount = 0.0M,
                Fee = 0,
                Nonce = AccountStateTrei.GetNextNonce(account.Address),
                TransactionType = TransactionType.NFT_BURN,
                Data = txData
            };

            scTx.Fee = FeeCalcService.CalculateTXFee(scTx);

            scTx.Build();

            var senderBalance = AccountStateTrei.GetAccountBalance(account.Address);
            if ((scTx.Amount + scTx.Fee) > senderBalance)
            {
                return null;//balance insufficient
            }

            BigInteger b1 = BigInteger.Parse(account.GetKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

            var txHash = scTx.Hash;
            var signature = SignatureService.CreateSignature(txHash, privateKey, account.PublicKey);
            if (signature == "ERROR")
            {
                return null; //TX sig failed
            }

            scTx.Signature = signature; //sigScript  = signature + '.' (this is a split char) + pubKey in Base58 format

            try
            {
                if (scTx.TransactionRating == null)
                {
                    var rating = await TransactionRatingService.GetTransactionRating(scTx);
                    scTx.TransactionRating = rating;
                }

                var result = await TransactionValidatorService.VerifyTX(scTx);
                if (result.Item1 == true)
                {
                    scTx.TransactionStatus = TransactionStatus.Pending;

                    if (account.IsValidating == true && (account.Balance - (scTx.Fee + scTx.Amount) < ValidatorService.ValidatorRequiredAmount()))
                    {
                        var validator = Validators.Validator.GetAll().FindOne(x => x.Address.ToLower() == scTx.FromAddress.ToLower());
                        ValidatorService.StopValidating(validator);
                        TransactionData.AddToPool(scTx);
                        TransactionData.AddTxToWallet(scTx, true);
                        AccountData.UpdateLocalBalance(scTx.FromAddress, (scTx.Fee + scTx.Amount));
                        await P2PClient.SendTXMempool(scTx);//send out to mempool
                    }
                    else if (account.IsValidating)
                    {
                        TransactionData.AddToPool(scTx);
                        TransactionData.AddTxToWallet(scTx, true);
                        AccountData.UpdateLocalBalance(scTx.FromAddress, (scTx.Fee + scTx.Amount));
                        await P2PClient.SendTXToAdjudicator(scTx);//send directly to adjs
                    }
                    else
                    {
                        TransactionData.AddToPool(scTx);
                        TransactionData.AddTxToWallet(scTx, true);
                        AccountData.UpdateLocalBalance(scTx.FromAddress, (scTx.Fee + scTx.Amount));
                        await P2PClient.SendTXMempool(scTx);//send out to mempool
                    }
                    return scTx;
                }
                else
                {
                    var output = "Fail! Transaction Verify has failed.";
                    NFTLogUtility.Log($"Error Evolve Failed TX Verify: {scMain.SmartContractUID}. Result: {result.Item2}", "SmartContractService.BurnSmartContract()");
                    return null;
                }
            }
            catch (Exception ex)
            {                
                Console.WriteLine("Error: {0}", ex.ToString());
                NFTLogUtility.Log($"Error Burning Smart Contract: {ex.ToString()}", "SmartContractService.BurnSmartContract()");
            }

            return null;
        }
        #endregion

        #region EvolveSmartContract
        public static async Task<Transaction?> EvolveSmartContract(string scUID, string toAddress)
        {
            Transaction? scTx = null;

            var smartContractStateTrei = SmartContractStateTrei.GetSmartContractState(scUID);
            if(smartContractStateTrei == null)
            {
                return null;
            }

            //Can't evolve if you are not the minter
            var account = AccountData.GetSingleAccount(smartContractStateTrei.MinterAddress);
            if (account == null)
            {
                return null;//Minter address is not found
            }

            //Can't evolve if the ToAddress does not own contract
            if(toAddress != smartContractStateTrei.OwnerAddress)
            {
                return null;
            }

            var minterAddress = smartContractStateTrei.MinterAddress;
            var evolve = await EvolvingFeature.GetNewEvolveState(smartContractStateTrei.ContractData);
            
            var evolveResult = evolve.Item1;
            if(evolveResult != true)
            {
                return null;
            }

            var evolveData = evolve.Item2;

            var bytes = Encoding.Unicode.GetBytes(evolveData);
            var scBase64 = SmartContractUtility.Compress(bytes).ToBase64();

            var newSCInfo = new[]
            {
                new { Function = "Evolve()", ContractUID = scUID, FromAddress = minterAddress, ToAddress = toAddress, Data = scBase64}
            };

            var txData = JsonConvert.SerializeObject(newSCInfo);
            

            scTx = new Transaction
            {
                Timestamp = TimeUtil.GetTime(),
                FromAddress = minterAddress,
                ToAddress = toAddress,
                Amount = 0.0M,
                Fee = 0,
                Nonce = AccountStateTrei.GetNextNonce(minterAddress),
                TransactionType = TransactionType.NFT_TX,
                Data = txData
            };

            scTx.Fee = FeeCalcService.CalculateTXFee(scTx);

            scTx.Build();

            var senderBalance = AccountStateTrei.GetAccountBalance(account.Address);
            if ((scTx.Amount + scTx.Fee) > senderBalance)
            {
                return null;//balance insufficient
            }

            BigInteger b1 = BigInteger.Parse(account.GetKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

            var txHash = scTx.Hash;
            var signature = SignatureService.CreateSignature(txHash, privateKey, account.PublicKey);
            if (signature == "ERROR")
            {
                return null; //TX sig failed
            }

            scTx.Signature = signature; //sigScript  = signature + '.' (this is a split char) + pubKey in Base58 format

            try
            {
                if (scTx.TransactionRating == null)
                {
                    var rating = await TransactionRatingService.GetTransactionRating(scTx);
                    scTx.TransactionRating = rating;
                }

                var result = await TransactionValidatorService.VerifyTX(scTx);
                if (result.Item1 == true)
                {
                    scTx.TransactionStatus = TransactionStatus.Pending;

                    if (account.IsValidating == true && (account.Balance - (scTx.Fee + scTx.Amount) < ValidatorService.ValidatorRequiredAmount()))
                    {
                        var validator = Validators.Validator.GetAll().FindOne(x => x.Address.ToLower() == scTx.FromAddress.ToLower());
                        ValidatorService.StopValidating(validator);
                        TransactionData.AddToPool(scTx);
                        TransactionData.AddTxToWallet(scTx, true);
                        AccountData.UpdateLocalBalance(scTx.FromAddress, (scTx.Fee + scTx.Amount));
                        await P2PClient.SendTXMempool(scTx);//send out to mempool
                    }
                    else if (account.IsValidating)
                    {
                        TransactionData.AddToPool(scTx);
                        TransactionData.AddTxToWallet(scTx, true);
                        AccountData.UpdateLocalBalance(scTx.FromAddress, (scTx.Fee + scTx.Amount));
                        await P2PClient.SendTXToAdjudicator(scTx);//send directly to adjs
                    }
                    else
                    {
                        TransactionData.AddToPool(scTx);
                        TransactionData.AddTxToWallet(scTx, true);
                        AccountData.UpdateLocalBalance(scTx.FromAddress, (scTx.Fee + scTx.Amount));
                        await P2PClient.SendTXMempool(scTx);//send out to mempool
                    }
                    return scTx;
                }
                else
                {
                    var output = "Fail! Transaction Verify has failed.";
                    NFTLogUtility.Log($"Error Evolve Failed TX Verify: {scUID}. Result {result.Item2}", "SmartContractService.EvolveSmartContract()");
                    return null;
                }
            }
            catch (Exception ex)
            {                
                Console.WriteLine("Error: {0}", ex.ToString());
                NFTLogUtility.Log($"Error Evolving Smart Contract: {ex.ToString()}", "SmartContractService.EvolveSmartContract()");
            }

            return null;
        }

        #endregion

        #region DevolveSmartContract
        public static async Task<Transaction?> DevolveSmartContract(string scUID, string toAddress)
        {
            Transaction? scTx = null;

            var smartContractStateTrei = SmartContractStateTrei.GetSmartContractState(scUID);
            if (smartContractStateTrei == null)
            {
                return null;
            }

            //Can't evolve if you are not the minter
            var account = AccountData.GetSingleAccount(smartContractStateTrei.MinterAddress);
            if (account == null)
            {
                return null;//Minter address is not found
            }

            //Can't evolve if the ToAddress does not own contract
            if (toAddress != smartContractStateTrei.OwnerAddress)
            {
                return null;
            }

            var minterAddress = smartContractStateTrei.MinterAddress;
            var evolve = await EvolvingFeature.GetNewDevolveState(smartContractStateTrei.ContractData);

            var evolveResult = evolve.Item1;
            if (evolveResult != true)
            {
                return null;
            }

            var evolveData = evolve.Item2;

            var bytes = Encoding.Unicode.GetBytes(evolveData);
            var scBase64 = SmartContractUtility.Compress(bytes).ToBase64();

            var newSCInfo = new[]
            {
                new { Function = "Devolve()", ContractUID = scUID, FromAddress = minterAddress, ToAddress = toAddress, Data = scBase64}
            };

            var txData = JsonConvert.SerializeObject(newSCInfo);


            scTx = new Transaction
            {
                Timestamp = TimeUtil.GetTime(),
                FromAddress = minterAddress,
                ToAddress = toAddress,
                Amount = 0.0M,
                Fee = 0,
                Nonce = AccountStateTrei.GetNextNonce(minterAddress),
                TransactionType = TransactionType.NFT_TX,
                Data = txData
            };

            scTx.Fee = FeeCalcService.CalculateTXFee(scTx);

            scTx.Build();

            var senderBalance = AccountStateTrei.GetAccountBalance(account.Address);
            if ((scTx.Amount + scTx.Fee) > senderBalance)
            {
                return null;//balance insufficient
            }

            BigInteger b1 = BigInteger.Parse(account.GetKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

            var txHash = scTx.Hash;
            var signature = SignatureService.CreateSignature(txHash, privateKey, account.PublicKey);
            if (signature == "ERROR")
            {
                return null; //TX sig failed
            }

            scTx.Signature = signature; //sigScript  = signature + '.' (this is a split char) + pubKey in Base58 format

            try
            {
                if (scTx.TransactionRating == null)
                {
                    var rating = await TransactionRatingService.GetTransactionRating(scTx);
                    scTx.TransactionRating = rating;
                }

                var result = await TransactionValidatorService.VerifyTX(scTx);
                if (result.Item1 == true)
                {
                    scTx.TransactionStatus = TransactionStatus.Pending;

                    if (account.IsValidating == true && (account.Balance - (scTx.Fee + scTx.Amount) < ValidatorService.ValidatorRequiredAmount()))
                    {
                        var validator = Validators.Validator.GetAll().FindOne(x => x.Address.ToLower() == scTx.FromAddress.ToLower());
                        ValidatorService.StopValidating(validator);
                        TransactionData.AddToPool(scTx);
                        TransactionData.AddTxToWallet(scTx, true);
                        AccountData.UpdateLocalBalance(scTx.FromAddress, (scTx.Fee + scTx.Amount));
                        await P2PClient.SendTXMempool(scTx);//send out to mempool
                    }
                    else if (account.IsValidating)
                    {
                        TransactionData.AddToPool(scTx);
                        TransactionData.AddTxToWallet(scTx, true);
                        AccountData.UpdateLocalBalance(scTx.FromAddress, (scTx.Fee + scTx.Amount));
                        await P2PClient.SendTXToAdjudicator(scTx);//send directly to adjs
                    }
                    else
                    {
                        TransactionData.AddToPool(scTx);
                        TransactionData.AddTxToWallet(scTx, true);
                        AccountData.UpdateLocalBalance(scTx.FromAddress, (scTx.Fee + scTx.Amount));
                        await P2PClient.SendTXMempool(scTx);//send out to mempool
                    }
                    return scTx;
                }
                else
                {
                    var output = "Fail! Transaction Verify has failed.";
                    NFTLogUtility.Log($"Error Devolve Failed TX Verify: {scUID}. Result: {result.Item2}", "SmartContractService.DevolveSmartContract()");
                    return null;
                }
            }
            catch (Exception ex)
            {                
                Console.WriteLine("Error: {0}", ex.ToString());
                NFTLogUtility.Log($"Error Burning Smart Contract: {ex.ToString()}", "SmartContractService.DevolveSmartContract()");
            }

            return null;
        }

        #endregion

        #region ChangeEvolveStateSpecific
        public static async Task<Transaction?> ChangeEvolveStateSpecific(string scUID, string toAddress, int evoState)
        {
            Transaction? scTx = null;

            var smartContractStateTrei = SmartContractStateTrei.GetSmartContractState(scUID);
            if (smartContractStateTrei == null)
            {
                return null;
            }

            //Can't evolve if you are not the minter
            var account = AccountData.GetSingleAccount(smartContractStateTrei.MinterAddress);
            if (account == null)
            {
                return null;//Minter address is not found
            }

            //Can't evolve if the ToAddress does not own contract
            if (toAddress != smartContractStateTrei.OwnerAddress)
            {
                return null;
            }

            var minterAddress = smartContractStateTrei.MinterAddress;
            var evolve = await EvolvingFeature.GetNewSpecificState(smartContractStateTrei.ContractData, evoState);

            var evolveResult = evolve.Item1;
            if (evolveResult != true)
            {
                return null;
            }

            var evolveData = evolve.Item2;

            var bytes = Encoding.Unicode.GetBytes(evolveData);
            var scBase64 = SmartContractUtility.Compress(bytes).ToBase64();

            var newSCInfo = new[]
            {
                new { Function = "ChangeEvolveStateSpecific()", ContractUID = scUID, FromAddress = minterAddress, ToAddress = toAddress, NewEvoState = evoState, Data = scBase64}
            };

            var txData = JsonConvert.SerializeObject(newSCInfo);


            scTx = new Transaction
            {
                Timestamp = TimeUtil.GetTime(),
                FromAddress = minterAddress,
                ToAddress = toAddress,
                Amount = 0.0M,
                Fee = 0,
                Nonce = AccountStateTrei.GetNextNonce(minterAddress),
                TransactionType = TransactionType.NFT_TX,
                Data = txData
            };

            scTx.Fee = FeeCalcService.CalculateTXFee(scTx);

            scTx.Build();

            var senderBalance = AccountStateTrei.GetAccountBalance(account.Address);
            if ((scTx.Amount + scTx.Fee) > senderBalance)
            {
                return null;//balance insufficient
            }

            BigInteger b1 = BigInteger.Parse(account.GetKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

            var txHash = scTx.Hash;
            var signature = SignatureService.CreateSignature(txHash, privateKey, account.PublicKey);
            if (signature == "ERROR")
            {
                return null; //TX sig failed
            }

            scTx.Signature = signature; //sigScript  = signature + '.' (this is a split char) + pubKey in Base58 format

            try
            {
                if (scTx.TransactionRating == null)
                {
                    var rating = await TransactionRatingService.GetTransactionRating(scTx);
                    scTx.TransactionRating = rating;
                }

                var result = await TransactionValidatorService.VerifyTX(scTx);
                if (result.Item1 == true)
                {
                    scTx.TransactionStatus = TransactionStatus.Pending;

                    if (account.IsValidating == true && (account.Balance - (scTx.Fee + scTx.Amount) < ValidatorService.ValidatorRequiredAmount()))
                    {
                        var validator = Validators.Validator.GetAll().FindOne(x => x.Address.ToLower() == scTx.FromAddress.ToLower());
                        ValidatorService.StopValidating(validator);
                        TransactionData.AddToPool(scTx);
                        TransactionData.AddTxToWallet(scTx, true);
                        AccountData.UpdateLocalBalance(scTx.FromAddress, (scTx.Fee + scTx.Amount));
                        await P2PClient.SendTXMempool(scTx);//send out to mempool
                    }
                    else if (account.IsValidating)
                    {
                        TransactionData.AddToPool(scTx);
                        TransactionData.AddTxToWallet(scTx, true);
                        AccountData.UpdateLocalBalance(scTx.FromAddress, (scTx.Fee + scTx.Amount));
                        await P2PClient.SendTXToAdjudicator(scTx);//send directly to adjs
                    }
                    else
                    {
                        TransactionData.AddToPool(scTx);
                        TransactionData.AddTxToWallet(scTx, true);
                        AccountData.UpdateLocalBalance(scTx.FromAddress, (scTx.Fee + scTx.Amount));
                        await P2PClient.SendTXMempool(scTx);//send out to mempool
                    }
                    return scTx;
                }
                else
                {
                    var output = "Fail! Transaction Verify has failed.";
                    NFTLogUtility.Log($"Error Evo Specific Failed TX Verify: {scUID}. Result: {result.Item2}", "SmartContractService.ChangeEvolveStateSpecific()");
                    return null;
                }
            }
            catch (Exception ex)
            {                
                Console.WriteLine("Error: {0}", ex.ToString());
                NFTLogUtility.Log($"Error Evo Specific Smart Contract: {ex.ToString()}", "SmartContractService.ChangeEvolveStateSpecific()");
            }

            return null;
        }

        #endregion

        #region Start Sale Smart Contract
        public static async Task<Transaction?> StartSaleSmartContractTX(string scUID, string toAddress, decimal amountSoldFor)
        {
            Transaction? scTx = null;

            var smartContractStateTrei = SmartContractStateTrei.GetSmartContractState(scUID);
            if (smartContractStateTrei == null) return null;
            
            var account = AccountData.GetSingleAccount(smartContractStateTrei.OwnerAddress);
            if (account == null) return null;//Owner address not found.

            var keyToSign = RandomStringUtility.GetRandomStringOnlyLetters(10, true);

            var txData = JsonConvert.SerializeObject(new { Function = "Sale_Start()", ContractUID = scUID, NextOwner = toAddress, SoldFor = amountSoldFor, KeySign = keyToSign });

            scTx = new Transaction
            {
                Timestamp = TimeUtil.GetTime(),
                FromAddress = account.Address,
                ToAddress = toAddress,
                Amount = 0.0M,
                Fee = 0,
                Nonce = AccountStateTrei.GetNextNonce(account.Address),
                TransactionType = TransactionType.NFT_SALE,
                Data = txData
            };

            scTx.Fee = FeeCalcService.CalculateTXFee(scTx);

            scTx.Build();

            var senderBalance = AccountStateTrei.GetAccountBalance(account.Address);
            if ((scTx.Amount + scTx.Fee) > senderBalance) return null;//balance insufficient

            var privateKey = account.GetPrivKey;

            if(privateKey == null) return null;

            var txHash = scTx.Hash;
            var signature = SignatureService.CreateSignature(txHash, privateKey, account.PublicKey);
            if (signature == "ERROR") return null; //TX sig failed

            scTx.Signature = signature;

            try
            {
                if (scTx.TransactionRating == null)
                {
                    var rating = await TransactionRatingService.GetTransactionRating(scTx);
                    scTx.TransactionRating = rating;
                }

                var result = await TransactionValidatorService.VerifyTX(scTx);

                if (!result.Item1) return null;

                scTx.TransactionStatus = TransactionStatus.Pending;

                await WalletService.SendTransaction(scTx, account);

                return scTx;
            }
            catch { }

            return null;
        }
        #endregion

        #region Complete Sale Smart Contract

        public static async Task<(Transaction?, string)> CompleteSaleSmartContractTX(string scUID, string toAddress, decimal amountSoldFor)
        {
            Transaction? scTx = null;
            bool isRoyalty = false;
            decimal royaltyAmount = 0.0M;
            string royaltyPayTo = "";
            List<Transaction> scTxList = new List<Transaction>(); 

            var smartContractStateTrei = SmartContractStateTrei.GetSmartContractState(scUID);
            if (smartContractStateTrei == null) 
                return (null, $"Could not find State record for smart contract: {scUID}");

            if(smartContractStateTrei.NextOwner == null) 
                return (null, "There was no next owner identified on the state trei level.");

            var account = AccountData.GetSingleAccount(smartContractStateTrei.NextOwner);
            if (account == null) 
                return (null, $"Next owner is {smartContractStateTrei.NextOwner}, but this account was not found within your wallet.");//Owner address not found.

            var senderBalance = AccountStateTrei.GetAccountBalance(account.Address);

            var privateKey = account.GetPrivKey;

            var keySign = smartContractStateTrei.PurchaseKey;

            try
            {
                var scMain = SmartContractMain.GenerateSmartContractInMemory(smartContractStateTrei.ContractData);
                if (scMain.Features != null)
                {
                    var royalty = scMain.Features?.Where(x => x.FeatureName == FeatureName.Royalty).FirstOrDefault();
                    if (royalty != null)
                    {
                        //calc royalty
                        var royaltyDetails = (RoyaltyFeature)royalty.FeatureFeatures;
                        isRoyalty = true;
                        royaltyAmount = royaltyDetails.RoyaltyAmount;
                        royaltyPayTo = royaltyDetails.RoyaltyPayToAddress;

                        var royaltyRBX = amountSoldFor * royaltyAmount;
                        var saleRBXLessRoyalty = amountSoldFor - royaltyRBX;

                        var payRoyaltyOff = amountSoldFor - (saleRBXLessRoyalty + royaltyRBX) > 1.0M ? false : true;
                        if(payRoyaltyOff)
                            return (null, $"The Royalty VS amount being paid is not settling. Please verify all things are correct. Royalty % {royaltyAmount}.  Amount Sold For: {amountSoldFor}. Royalty Due: {royaltyRBX}. Sale less the royalty {saleRBXLessRoyalty}");

                        Transaction scSaleTx = new Transaction
                        {
                            Timestamp = TimeUtil.GetTime(),
                            FromAddress = account.Address,
                            ToAddress = toAddress,
                            Amount = saleRBXLessRoyalty,
                            Fee = 0,
                            Nonce = AccountStateTrei.GetNextNonce(account.Address),
                            TransactionType = TransactionType.NFT_SALE,
                            Data = JsonConvert.SerializeObject(new { Function = "Sale_Complete()", ContractUID = scUID, Royalty = true, RoyaltyAmount = royaltyRBX, RoyaltyPaidTo = royaltyPayTo, TXNum = "1/2" })
                        };

                        scSaleTx.Fee = FeeCalcService.CalculateTXFee(scSaleTx);

                        scSaleTx.Build();

                        if ((scSaleTx.Amount + scSaleTx.Fee) > senderBalance)
                            return (null, "Amount exceeds balance.");//balance insufficient

                        if (privateKey == null)
                            return (null, "Private key was null.");

                        var txHashRoyaltyPay = scSaleTx.Hash;
                        var signatureRoyaltyPay = SignatureService.CreateSignature(txHashRoyaltyPay, privateKey, account.PublicKey);
                        if (signatureRoyaltyPay == "ERROR")
                            return (null, "Failed to create signature."); //TX sig failed

                        scSaleTx.Signature = signatureRoyaltyPay;

                        if (scSaleTx.TransactionRating == null)
                        {
                            var rating = await TransactionRatingService.GetTransactionRating(scSaleTx);
                            scSaleTx.TransactionRating = rating;
                        }

                        scTxList.Add(scSaleTx);

                        Transaction scRoyaltyTx = new Transaction
                        {
                            Timestamp = TimeUtil.GetTime(),
                            FromAddress = account.Address,
                            ToAddress = royaltyPayTo,
                            Amount = royaltyRBX,
                            Fee = 0,
                            Nonce = AccountStateTrei.GetNextNonce(account.Address),
                            TransactionType = TransactionType.NFT_SALE,
                            Data = JsonConvert.SerializeObject(new { Function = "Sale_Complete()", ContractUID = scUID, Royalty = true, TXNum = "2/2" })
                        };

                        scRoyaltyTx.Fee = FeeCalcService.CalculateTXFee(scRoyaltyTx);

                        scRoyaltyTx.Build();

                        if ((scRoyaltyTx.Amount + scRoyaltyTx.Fee) > senderBalance)
                            return (null, "Amount exceeds balance.");//balance insufficient

                        if (privateKey == null)
                            return (null, "Private key was null.");

                        var txHashRoyalty = scRoyaltyTx.Hash;
                        var signatureRoyalty = SignatureService.CreateSignature(txHashRoyalty, privateKey, account.PublicKey);
                        if (signatureRoyalty == "ERROR")
                            return (null, "Failed to create signature."); //TX sig failed

                        scRoyaltyTx.Signature = signatureRoyalty;

                        if (scRoyaltyTx.TransactionRating == null)
                        {
                            var rating = await TransactionRatingService.GetTransactionRating(scRoyaltyTx);
                            scRoyaltyTx.TransactionRating = rating;
                        }

                        scTxList.Add(scRoyaltyTx);
                    }
                    else
                    {
                        Transaction scSaleTx = new Transaction
                        {
                            Timestamp = TimeUtil.GetTime(),
                            FromAddress = account.Address,
                            ToAddress = toAddress,
                            Amount = amountSoldFor,
                            Fee = 0,
                            Nonce = AccountStateTrei.GetNextNonce(account.Address),
                            TransactionType = TransactionType.NFT_SALE,
                            Data = JsonConvert.SerializeObject(new { Function = "Sale_Complete()", ContractUID = scUID })
                        };

                        scSaleTx.Fee = FeeCalcService.CalculateTXFee(scSaleTx);

                        scSaleTx.Build();

                        if ((scSaleTx.Amount + scSaleTx.Fee) > senderBalance)
                            return (null, "Amount exceeds balance.");//balance insufficient

                        if (privateKey == null)
                            return (null, "Private key was null.");

                        var txHashNoRoyalty = scSaleTx.Hash;
                        var signatureNoRoyalty = SignatureService.CreateSignature(txHashNoRoyalty, privateKey, account.PublicKey);
                        if (signatureNoRoyalty == "ERROR")
                            return (null, "Failed to create signature."); //TX sig failed

                        scSaleTx.Signature = signatureNoRoyalty;

                        if (scSaleTx.TransactionRating == null)
                        {
                            var rating = await TransactionRatingService.GetTransactionRating(scSaleTx);
                            scSaleTx.TransactionRating = rating;
                        }

                        scTxList.Add(scSaleTx);
                    }
                }
            }
            catch(Exception ex)
            { 
                return (null, $"Unknown error decompiling SC. Error: {ex.ToString()}");
            }
            
            var txData = JsonConvert.SerializeObject(new { Function = "Sale_Complete()", ContractUID = scUID, Royalty = isRoyalty, RoyaltyAmount = royaltyAmount, RoyaltyPayTo = royaltyPayTo, Transactions = scTxList, KeySign = keySign });

            scTx = new Transaction
            {
                Timestamp = TimeUtil.GetTime(),
                FromAddress = account.Address,
                ToAddress = toAddress,
                Amount = 0.0M,
                Fee = 0,
                Nonce = AccountStateTrei.GetNextNonce(account.Address),
                TransactionType = TransactionType.NFT_SALE,
                Data = txData
            };

            scTx.Fee = FeeCalcService.CalculateTXFee(scTx);

            scTx.Build();

            if ((scTx.Amount + scTx.Fee + scTxList.Select(x => x.Amount + x.Fee).Sum()) > senderBalance)  // not sure about this... double check.
                return (null, "Amount exceeds balance.");//balance insufficient

            if (privateKey == null) 
                return (null, "Private key was null.");

            var txHash = scTx.Hash;
            var signature = SignatureService.CreateSignature(txHash, privateKey, account.PublicKey);
            if (signature == "ERROR") 
                return (null, "Failed to create signature."); //TX sig failed

            scTx.Signature = signature;

            try
            {
                if (scTx.TransactionRating == null)
                {
                    var rating = await TransactionRatingService.GetTransactionRating(scTx);
                    scTx.TransactionRating = rating;
                }

                var result = await TransactionValidatorService.VerifyTX(scTx);

                if (!result.Item1) 
                    return (null, $"Transaction failed to verify. Reason: {result.Item2}");

                scTx.TransactionStatus = TransactionStatus.Pending;

                //await WalletService.SendTransaction(scTx, account);

                return (scTx, "TX Sent to Mempool");
            }
            catch { }

            return (null, "Fail");
        }

        #endregion
    }
}
