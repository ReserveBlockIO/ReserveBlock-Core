using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.Utilities;
using System;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Xml.Linq;

namespace ReserveBlockCore.Services
{
    public class TokenContractService
    {
        public static async Task<(bool, string)> TransferToken(SmartContractStateTrei sc, TokenAccount tokenAccount, string fromAddress, string toAddress, decimal amount, PrivateKey? reserveAccountKey = null, int unlockTime = 0)
        {
            var tokenTx = new Transaction();

            try
            {
                toAddress = toAddress.Replace(" ", "");

                bool isReserveAccount = fromAddress.StartsWith("xRBX") ? true : false;
                var account = AccountData.GetSingleAccount(fromAddress);
                var rAccount = isReserveAccount ? ReserveAccount.GetReserveAccountSingle(fromAddress) : null;

                if (account == null && rAccount == null)
                {
                    return (false, "Failed to located account");
                }

                var publicKey = !isReserveAccount ? account?.PublicKey : rAccount?.PublicKey;
                var privateKey = !isReserveAccount ? account?.GetPrivKey : reserveAccountKey;

                var txData = JsonConvert.SerializeObject(new { Function = "TokenTransfer()", ContractUID = sc.SmartContractUID, FromAddress = fromAddress, ToAddress = toAddress, Amount = amount, TokenTicker = tokenAccount.TokenTicker, TokenName = tokenAccount.TokenName });
                
                tokenTx = new Transaction
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

                tokenTx.Fee = FeeCalcService.CalculateTXFee(tokenTx);

                tokenTx.Build();

                var senderBalance = AccountStateTrei.GetAccountBalance(fromAddress);
                if ((tokenTx.Amount + tokenTx.Fee) > senderBalance)
                {

                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"Balance insufficient. SCUID: {sc.SmartContractUID}", "TokenContractService.TransferToken()");
                    return (false, $"Balance insufficient. SCUID: {sc.SmartContractUID}");
                }

                if (privateKey == null)
                {
                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"Private key was null for account {fromAddress}", "TokenContractService.TransferToken()");
                    return (false, $"Private key was null for account {fromAddress}");
                }

                var txHash = tokenTx.Hash;
                var signature = SignatureService.CreateSignature(txHash, privateKey, publicKey);
                if (signature == "ERROR")
                {
                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"TX Signature Failed. SCUID: {sc.SmartContractUID}", "TokenContractService.TransferToken()");
                    return (false, $"TX Signature Failed. SCUID: {sc.SmartContractUID}");
                }

                tokenTx.Signature = signature; //sigScript  = signature + '.' (this is a split char) + pubKey in Base58 format


                if (tokenTx.TransactionRating == null)
                {
                    var rating = await TransactionRatingService.GetTransactionRating(tokenTx);
                    tokenTx.TransactionRating = rating;
                }

                var result = await TransactionValidatorService.VerifyTX(tokenTx);
                if (result.Item1 == true)
                {
                    tokenTx.TransactionStatus = TransactionStatus.Pending;

                    if (account != null)
                    {
                        await WalletService.SendTransaction(tokenTx, account);
                    }
                    if (rAccount != null)
                    {
                        await WalletService.SendReserveTransaction(tokenTx, rAccount, true);
                    }

                    NFTLogUtility.Log($"TX Success. SCUID: {sc.SmartContractUID}", "TokenContractService.TransferToken()");
                    return (true, $"TX Success. SCUID: {sc.SmartContractUID}");
                }
                else
                {
                    var output = "Fail! Transaction Verify has failed.";
                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"Error Transfer Failed TX Verify: {sc.SmartContractUID}. Result: {result.Item2}", "TokenContractService.TransferToken()");
                    return (false, $"Error Transfer Failed TX Verify: {sc.SmartContractUID}. Reason: {result.Item2}");
                }
            }
            catch { }

            return (false, "Reached end of method.");
        }

        public static async Task<(bool, string)> TokenMint(SmartContractStateTrei sc, string fromAddress, decimal amount)
        {
            var tokenTx = new Transaction();

            try
            {
                var account = AccountData.GetSingleAccount(fromAddress);
                if (account == null)
                {
                    return (false, "Failed to located account");
                }
                var publicKey = account?.PublicKey;
                var privateKey = account?.GetPrivKey;

                var txData = JsonConvert.SerializeObject(new { Function = "TokenMint()", ContractUID = sc.SmartContractUID, FromAddress = fromAddress, Amount = amount });

                tokenTx = new Transaction
                {
                    Timestamp = TimeUtil.GetTime(),
                    FromAddress = fromAddress,
                    ToAddress = "Token_Base",
                    Amount = 0.0M,
                    Fee = 0,
                    Nonce = AccountStateTrei.GetNextNonce(fromAddress),
                    TransactionType = TransactionType.NFT_TX,
                    Data = txData,
                    UnlockTime = null
                };

                tokenTx.Fee = FeeCalcService.CalculateTXFee(tokenTx);

                tokenTx.Build();

                var senderBalance = AccountStateTrei.GetAccountBalance(fromAddress);
                if ((tokenTx.Amount + tokenTx.Fee) > senderBalance)
                {

                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"Balance insufficient. SCUID: {sc.SmartContractUID}", "TokenContractService.TransferToken()");
                    return (false, $"Balance insufficient. SCUID: {sc.SmartContractUID}");
                }

                if (privateKey == null)
                {
                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"Private key was null for account {fromAddress}", "TokenContractService.TransferToken()");
                    return (false, $"Private key was null for account {fromAddress}");
                }

                var txHash = tokenTx.Hash;
                var signature = SignatureService.CreateSignature(txHash, privateKey, publicKey);
                if (signature == "ERROR")
                {
                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"TX Signature Failed. SCUID: {sc.SmartContractUID}", "TokenContractService.TransferToken()");
                    return (false, $"TX Signature Failed. SCUID: {sc.SmartContractUID}");
                }

                tokenTx.Signature = signature; //sigScript  = signature + '.' (this is a split char) + pubKey in Base58 format


                if (tokenTx.TransactionRating == null)
                {
                    var rating = await TransactionRatingService.GetTransactionRating(tokenTx);
                    tokenTx.TransactionRating = rating;
                }

                var result = await TransactionValidatorService.VerifyTX(tokenTx);
                if (result.Item1 == true)
                {
                    tokenTx.TransactionStatus = TransactionStatus.Pending;

                    if (account != null)
                    {
                        await WalletService.SendTransaction(tokenTx, account);
                    }

                    NFTLogUtility.Log($"TX Success. SCUID: {sc.SmartContractUID}", "TokenContractService.TransferToken()");
                    return (true, $"TX Success. SCUID: {sc.SmartContractUID}");
                }
                else
                {
                    var output = "Fail! Transaction Verify has failed.";
                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"Error Transfer Failed TX Verify: {sc.SmartContractUID}. Result: {result.Item2}", "TokenContractService.TransferToken()");
                    return (false, $"Error Transfer Failed TX Verify: {sc.SmartContractUID}. Reason: {result.Item2}");
                }

            }
            catch (Exception ex)
            {
                return (false, $"Unknown Error. Error: {ex.ToString()}");
            }
        }

        public static async Task<(bool, string)> BurnToken(SmartContractStateTrei sc, TokenAccount tokenAccount, string fromAddress, decimal amount, PrivateKey? reserveAccountKey = null, int unlockTime = 0)
        {
            var tokenTx = new Transaction();

            try
            {
                bool isReserveAccount = fromAddress.StartsWith("xRBX") ? true : false;
                var account = AccountData.GetSingleAccount(fromAddress);
                var rAccount = isReserveAccount ? ReserveAccount.GetReserveAccountSingle(fromAddress) : null;

                if (account == null && rAccount == null)
                {
                    return (false, "Failed to located account");
                }

                var publicKey = !isReserveAccount ? account?.PublicKey : rAccount?.PublicKey;
                var privateKey = !isReserveAccount ? account?.GetPrivKey : reserveAccountKey;

                var txData = JsonConvert.SerializeObject(new { Function = "TokenBurn()", ContractUID = sc.SmartContractUID, FromAddress = fromAddress, Amount = amount, TokenTicker = tokenAccount.TokenTicker, TokenName = tokenAccount.TokenName });

                tokenTx = new Transaction
                {
                    Timestamp = TimeUtil.GetTime(),
                    FromAddress = fromAddress,
                    ToAddress = "Token_Base",
                    Amount = 0.0M,
                    Fee = 0,
                    Nonce = AccountStateTrei.GetNextNonce(fromAddress),
                    TransactionType = TransactionType.NFT_TX,
                    Data = txData,
                    UnlockTime = rAccount != null ? TimeUtil.GetReserveTime(unlockTime) : null
                };

                tokenTx.Fee = FeeCalcService.CalculateTXFee(tokenTx);

                tokenTx.Build();

                var senderBalance = AccountStateTrei.GetAccountBalance(fromAddress);
                if ((tokenTx.Amount + tokenTx.Fee) > senderBalance)
                {

                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"Balance insufficient. SCUID: {sc.SmartContractUID}", "TokenContractService.TransferToken()");
                    return (false, $"Balance insufficient. SCUID: {sc.SmartContractUID}");
                }

                if (privateKey == null)
                {
                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"Private key was null for account {fromAddress}", "TokenContractService.TransferToken()");
                    return (false, $"Private key was null for account {fromAddress}");
                }

                var txHash = tokenTx.Hash;
                var signature = SignatureService.CreateSignature(txHash, privateKey, publicKey);
                if (signature == "ERROR")
                {
                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"TX Signature Failed. SCUID: {sc.SmartContractUID}", "TokenContractService.TransferToken()");
                    return (false, $"TX Signature Failed. SCUID: {sc.SmartContractUID}");
                }

                tokenTx.Signature = signature; //sigScript  = signature + '.' (this is a split char) + pubKey in Base58 format


                if (tokenTx.TransactionRating == null)
                {
                    var rating = await TransactionRatingService.GetTransactionRating(tokenTx);
                    tokenTx.TransactionRating = rating;
                }

                var result = await TransactionValidatorService.VerifyTX(tokenTx);
                if (result.Item1 == true)
                {
                    tokenTx.TransactionStatus = TransactionStatus.Pending;

                    if (account != null)
                    {
                        await WalletService.SendTransaction(tokenTx, account);
                    }
                    if (rAccount != null)
                    {
                        await WalletService.SendReserveTransaction(tokenTx, rAccount, true);
                    }

                    NFTLogUtility.Log($"TX Success. SCUID: {sc.SmartContractUID}", "TokenContractService.TransferToken()");
                    return (true, $"TX Success. SCUID: {sc.SmartContractUID}");
                }
                else
                {
                    var output = "Fail! Transaction Verify has failed.";
                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"Error Transfer Failed TX Verify: {sc.SmartContractUID}. Result: {result.Item2}", "TokenContractService.TransferToken()");
                    return (false, $"Error Transfer Failed TX Verify: {sc.SmartContractUID}. Reason: {result.Item2}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Unknown Error. Error: {ex.ToString()}");
            }
        }

        public static async Task<(bool, string)> BanAddress(SmartContractStateTrei sc, string fromAddress, string banAddress)
        {
            var tokenTx = new Transaction();

            try
            {
                var account = AccountData.GetSingleAccount(fromAddress);
                if (account == null)
                {
                    return (false, "Failed to located account");
                }
                var publicKey = account?.PublicKey;
                var privateKey = account?.GetPrivKey;

                var txData = JsonConvert.SerializeObject(new { Function = "TokenBanAddress()", ContractUID = sc.SmartContractUID, FromAddress = fromAddress, BanAddress = banAddress });

                tokenTx = new Transaction
                {
                    Timestamp = TimeUtil.GetTime(),
                    FromAddress = fromAddress,
                    ToAddress = "Token_Base",
                    Amount = 0.0M,
                    Fee = 0,
                    Nonce = AccountStateTrei.GetNextNonce(fromAddress),
                    TransactionType = TransactionType.NFT_TX,
                    Data = txData,
                    UnlockTime = null
                };

                tokenTx.Fee = FeeCalcService.CalculateTXFee(tokenTx);

                tokenTx.Build();

                var senderBalance = AccountStateTrei.GetAccountBalance(fromAddress);
                if ((tokenTx.Amount + tokenTx.Fee) > senderBalance)
                {

                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"Balance insufficient. SCUID: {sc.SmartContractUID}", "TokenContractService.TransferToken()");
                    return (false, $"Balance insufficient. SCUID: {sc.SmartContractUID}");
                }

                if (privateKey == null)
                {
                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"Private key was null for account {fromAddress}", "TokenContractService.TransferToken()");
                    return (false, $"Private key was null for account {fromAddress}");
                }

                var txHash = tokenTx.Hash;
                var signature = SignatureService.CreateSignature(txHash, privateKey, publicKey);
                if (signature == "ERROR")
                {
                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"TX Signature Failed. SCUID: {sc.SmartContractUID}", "TokenContractService.TransferToken()");
                    return (false, $"TX Signature Failed. SCUID: {sc.SmartContractUID}");
                }

                tokenTx.Signature = signature; //sigScript  = signature + '.' (this is a split char) + pubKey in Base58 format


                if (tokenTx.TransactionRating == null)
                {
                    var rating = await TransactionRatingService.GetTransactionRating(tokenTx);
                    tokenTx.TransactionRating = rating;
                }

                var result = await TransactionValidatorService.VerifyTX(tokenTx);
                if (result.Item1 == true)
                {
                    tokenTx.TransactionStatus = TransactionStatus.Pending;

                    if (account != null)
                    {
                        await WalletService.SendTransaction(tokenTx, account);
                    }

                    NFTLogUtility.Log($"TX Success. SCUID: {sc.SmartContractUID}", "TokenContractService.TransferToken()");
                    return (true, $"TX Success. SCUID: {sc.SmartContractUID}");
                }
                else
                {
                    var output = "Fail! Transaction Verify has failed.";
                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"Error Transfer Failed TX Verify: {sc.SmartContractUID}. Result: {result.Item2}", "TokenContractService.TransferToken()");
                    return (false, $"Error Transfer Failed TX Verify: {sc.SmartContractUID}. Reason: {result.Item2}");
                }

            }
            catch (Exception ex)
            {
                return (false, $"Unknown Error. Error: {ex.ToString()}");
            }
        }

        public static async Task<(bool, string)> ChangeTokenContractOwnership(SmartContractStateTrei sc, string fromAddress, string toAddress)
        {
            var tokenTx = new Transaction();

            try
            {
                var account = AccountData.GetSingleAccount(fromAddress);
                if (account == null)
                {
                    return (false, "Failed to located account");
                }
                var publicKey = account?.PublicKey;
                var privateKey = account?.GetPrivKey;

                var txData = JsonConvert.SerializeObject(new { Function = "TokenContractOwnerChange()", ContractUID = sc.SmartContractUID, FromAddress = fromAddress, ToAddress = toAddress });

                tokenTx = new Transaction
                {
                    Timestamp = TimeUtil.GetTime(),
                    FromAddress = fromAddress,
                    ToAddress = toAddress,
                    Amount = 0.0M,
                    Fee = 0,
                    Nonce = AccountStateTrei.GetNextNonce(fromAddress),
                    TransactionType = TransactionType.NFT_TX,
                    Data = txData,
                    UnlockTime = null
                };

                tokenTx.Fee = FeeCalcService.CalculateTXFee(tokenTx);

                tokenTx.Build();

                var senderBalance = AccountStateTrei.GetAccountBalance(fromAddress);
                if ((tokenTx.Amount + tokenTx.Fee) > senderBalance)
                {

                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"Balance insufficient. SCUID: {sc.SmartContractUID}", "TokenContractService.TransferToken()");
                    return (false, $"Balance insufficient. SCUID: {sc.SmartContractUID}");
                }

                if (privateKey == null)
                {
                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"Private key was null for account {fromAddress}", "TokenContractService.TransferToken()");
                    return (false, $"Private key was null for account {fromAddress}");
                }

                var txHash = tokenTx.Hash;
                var signature = SignatureService.CreateSignature(txHash, privateKey, publicKey);
                if (signature == "ERROR")
                {
                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"TX Signature Failed. SCUID: {sc.SmartContractUID}", "TokenContractService.TransferToken()");
                    return (false, $"TX Signature Failed. SCUID: {sc.SmartContractUID}");
                }

                tokenTx.Signature = signature; //sigScript  = signature + '.' (this is a split char) + pubKey in Base58 format


                if (tokenTx.TransactionRating == null)
                {
                    var rating = await TransactionRatingService.GetTransactionRating(tokenTx);
                    tokenTx.TransactionRating = rating;
                }

                var result = await TransactionValidatorService.VerifyTX(tokenTx);
                if (result.Item1 == true)
                {
                    tokenTx.TransactionStatus = TransactionStatus.Pending;

                    if (account != null)
                    {
                        await WalletService.SendTransaction(tokenTx, account);
                    }

                    NFTLogUtility.Log($"TX Success. SCUID: {sc.SmartContractUID}", "TokenContractService.TransferToken()");
                    return (true, $"TX Success. SCUID: {sc.SmartContractUID}");
                }
                else
                {
                    var output = "Fail! Transaction Verify has failed.";
                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"Error Transfer Failed TX Verify: {sc.SmartContractUID}. Result: {result.Item2}", "TokenContractService.TransferToken()");
                    return (false, $"Error Transfer Failed TX Verify: {sc.SmartContractUID}. Reason: {result.Item2}");
                }

            }
            catch (Exception ex)
            {
                return (false, $"Unknown Error. Error: {ex.ToString()}");
            }
        }

        public static async Task<(bool, string)> PauseTokenContract(SmartContractStateTrei sc, string fromAddress, bool pause)
        {
            var tokenTx = new Transaction();

            try
            {
                var account = AccountData.GetSingleAccount(fromAddress);
                if (account == null)
                {
                    return (false, "Failed to located account");
                }
                var publicKey = account?.PublicKey;
                var privateKey = account?.GetPrivKey;

                var txData = JsonConvert.SerializeObject(new { Function = "TokenPause()", ContractUID = sc.SmartContractUID, FromAddress = fromAddress, Pause = pause });
                
                tokenTx = new Transaction
                {
                    Timestamp = TimeUtil.GetTime(),
                    FromAddress = fromAddress,
                    ToAddress = "Token_Base",
                    Amount = 0.0M,
                    Fee = 0,
                    Nonce = AccountStateTrei.GetNextNonce(fromAddress),
                    TransactionType = TransactionType.NFT_TX,
                    Data = txData,
                    UnlockTime = null
                };

                tokenTx.Fee = FeeCalcService.CalculateTXFee(tokenTx);

                tokenTx.Build();

                var senderBalance = AccountStateTrei.GetAccountBalance(fromAddress);
                if ((tokenTx.Amount + tokenTx.Fee) > senderBalance)
                {

                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"Balance insufficient. SCUID: {sc.SmartContractUID}", "TokenContractService.TransferToken()");
                    return (false, $"Balance insufficient. SCUID: {sc.SmartContractUID}");
                }

                if (privateKey == null)
                {
                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"Private key was null for account {fromAddress}", "TokenContractService.TransferToken()");
                    return (false, $"Private key was null for account {fromAddress}");
                }

                var txHash = tokenTx.Hash;
                var signature = SignatureService.CreateSignature(txHash, privateKey, publicKey);
                if (signature == "ERROR")
                {
                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"TX Signature Failed. SCUID: {sc.SmartContractUID}", "TokenContractService.TransferToken()");
                    return (false, $"TX Signature Failed. SCUID: {sc.SmartContractUID}");
                }

                tokenTx.Signature = signature; //sigScript  = signature + '.' (this is a split char) + pubKey in Base58 format


                if (tokenTx.TransactionRating == null)
                {
                    var rating = await TransactionRatingService.GetTransactionRating(tokenTx);
                    tokenTx.TransactionRating = rating;
                }

                var result = await TransactionValidatorService.VerifyTX(tokenTx);
                if (result.Item1 == true)
                {
                    tokenTx.TransactionStatus = TransactionStatus.Pending;

                    if (account != null)
                    {
                        await WalletService.SendTransaction(tokenTx, account);
                    }

                    NFTLogUtility.Log($"TX Success. SCUID: {sc.SmartContractUID}", "TokenContractService.TransferToken()");
                    return (true, $"TX Success. SCUID: {sc.SmartContractUID}");
                }
                else
                {
                    var output = "Fail! Transaction Verify has failed.";
                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"Error Transfer Failed TX Verify: {sc.SmartContractUID}. Result: {result.Item2}", "TokenContractService.TransferToken()");
                    return (false, $"Error Transfer Failed TX Verify: {sc.SmartContractUID}. Reason: {result.Item2}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Unknown Error. Error: {ex.ToString()}");
            }
        }

        public static async Task<(bool, string)> CreateTokenVoteTopic(SmartContractStateTrei sc, string fromAddress, TokenVoteTopic tokenTopic)
        {
            var tokenTx = new Transaction();

            try
            {
                var account = AccountData.GetSingleAccount(fromAddress);
                if (account == null)
                {
                    return (false, "Failed to located account");
                }
                var publicKey = account?.PublicKey;
                var privateKey = account?.GetPrivKey;

                var txData = JsonConvert.SerializeObject(new { Function = "TokenVoteTopicCreate()", ContractUID = sc.SmartContractUID, FromAddress = fromAddress, TokenVoteTopic = tokenTopic });

                tokenTx = new Transaction
                {
                    Timestamp = TimeUtil.GetTime(),
                    FromAddress = fromAddress,
                    ToAddress = "Token_Base",
                    Amount = 0.0M,
                    Fee = 0,
                    Nonce = AccountStateTrei.GetNextNonce(fromAddress),
                    TransactionType = TransactionType.NFT_TX,
                    Data = txData,
                    UnlockTime = null
                };

                tokenTx.Fee = FeeCalcService.CalculateTXFee(tokenTx);

                tokenTx.Build();

                var senderBalance = AccountStateTrei.GetAccountBalance(fromAddress);
                if ((tokenTx.Amount + tokenTx.Fee) > senderBalance)
                {

                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"Balance insufficient. SCUID: {sc.SmartContractUID}", "TokenContractService.TransferToken()");
                    return (false, $"Balance insufficient. SCUID: {sc.SmartContractUID}");
                }

                if (privateKey == null)
                {
                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"Private key was null for account {fromAddress}", "TokenContractService.TransferToken()");
                    return (false, $"Private key was null for account {fromAddress}");
                }

                var txHash = tokenTx.Hash;
                var signature = SignatureService.CreateSignature(txHash, privateKey, publicKey);
                if (signature == "ERROR")
                {
                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"TX Signature Failed. SCUID: {sc.SmartContractUID}", "TokenContractService.TransferToken()");
                    return (false, $"TX Signature Failed. SCUID: {sc.SmartContractUID}");
                }

                tokenTx.Signature = signature; //sigScript  = signature + '.' (this is a split char) + pubKey in Base58 format


                if (tokenTx.TransactionRating == null)
                {
                    var rating = await TransactionRatingService.GetTransactionRating(tokenTx);
                    tokenTx.TransactionRating = rating;
                }

                var result = await TransactionValidatorService.VerifyTX(tokenTx);
                if (result.Item1 == true)
                {
                    tokenTx.TransactionStatus = TransactionStatus.Pending;

                    if (account != null)
                    {
                        await WalletService.SendTransaction(tokenTx, account);
                    }

                    NFTLogUtility.Log($"TX Success. SCUID: {sc.SmartContractUID}", "TokenContractService.TransferToken()");
                    return (true, $"TX Success. SCUID: {sc.SmartContractUID}");
                }
                else
                {
                    var output = "Fail! Transaction Verify has failed.";
                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"Error Transfer Failed TX Verify: {sc.SmartContractUID}. Result: {result.Item2}", "TokenContractService.TransferToken()");
                    return (false, $"Error Transfer Failed TX Verify: {sc.SmartContractUID}. Reason: {result.Item2}");
                }

            }
            catch (Exception ex)
            {
                return (false, $"Unknown Error. Error: {ex.ToString()}");
            }
        }

        public static async Task<(bool, string)> CastTokenVoteTopic(SmartContractStateTrei sc, TokenAccount tokenAccount, string fromAddress, string topicUID, VoteType voteType, PrivateKey? reserveAccountKey = null, int unlockTime = 0)
        {
            var tokenTx = new Transaction();

            try
            {
                bool isReserveAccount = fromAddress.StartsWith("xRBX") ? true : false;
                var account = AccountData.GetSingleAccount(fromAddress);
                var rAccount = isReserveAccount ? ReserveAccount.GetReserveAccountSingle(fromAddress) : null;

                if (account == null && rAccount == null)
                {
                    return (false, "Failed to located account");
                }

                var publicKey = !isReserveAccount ? account?.PublicKey : rAccount?.PublicKey;
                var privateKey = !isReserveAccount ? account?.GetPrivKey : reserveAccountKey;

                var txData = JsonConvert.SerializeObject(new { Function = "TokenVoteTopicCast()", ContractUID = sc.SmartContractUID, FromAddress = fromAddress, TopicUID = topicUID , VoteType = voteType});

                tokenTx = new Transaction
                {
                    Timestamp = TimeUtil.GetTime(),
                    FromAddress = fromAddress,
                    ToAddress = "Token_Base",
                    Amount = 0.0M,
                    Fee = 0,
                    Nonce = AccountStateTrei.GetNextNonce(fromAddress),
                    TransactionType = TransactionType.NFT_TX,
                    Data = txData,
                    UnlockTime = rAccount != null ? TimeUtil.GetReserveTime(unlockTime) : null
                };

                tokenTx.Fee = FeeCalcService.CalculateTXFee(tokenTx);

                tokenTx.Build();

                var senderBalance = AccountStateTrei.GetAccountBalance(fromAddress);
                if ((tokenTx.Amount + tokenTx.Fee) > senderBalance)
                {

                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"Balance insufficient. SCUID: {sc.SmartContractUID}", "TokenContractService.TransferToken()");
                    return (false, $"Balance insufficient. SCUID: {sc.SmartContractUID}");
                }

                if (privateKey == null)
                {
                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"Private key was null for account {fromAddress}", "TokenContractService.TransferToken()");
                    return (false, $"Private key was null for account {fromAddress}");
                }

                var txHash = tokenTx.Hash;
                var signature = SignatureService.CreateSignature(txHash, privateKey, publicKey);
                if (signature == "ERROR")
                {
                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"TX Signature Failed. SCUID: {sc.SmartContractUID}", "TokenContractService.TransferToken()");
                    return (false, $"TX Signature Failed. SCUID: {sc.SmartContractUID}");
                }

                tokenTx.Signature = signature; //sigScript  = signature + '.' (this is a split char) + pubKey in Base58 format


                if (tokenTx.TransactionRating == null)
                {
                    var rating = await TransactionRatingService.GetTransactionRating(tokenTx);
                    tokenTx.TransactionRating = rating;
                }

                var result = await TransactionValidatorService.VerifyTX(tokenTx);
                if (result.Item1 == true)
                {
                    tokenTx.TransactionStatus = TransactionStatus.Pending;

                    if (account != null)
                    {
                        await WalletService.SendTransaction(tokenTx, account);
                    }
                    if (rAccount != null)
                    {
                        await WalletService.SendReserveTransaction(tokenTx, rAccount, true);
                    }

                    NFTLogUtility.Log($"TX Success. SCUID: {sc.SmartContractUID}", "TokenContractService.TransferToken()");
                    return (true, $"TX Success. SCUID: {sc.SmartContractUID}");
                }
                else
                {
                    var output = "Fail! Transaction Verify has failed.";
                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"Error Transfer Failed TX Verify: {sc.SmartContractUID}. Result: {result.Item2}", "TokenContractService.TransferToken()");
                    return (false, $"Error Transfer Failed TX Verify: {sc.SmartContractUID}. Reason: {result.Item2}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Unknown Error. Error: {ex.ToString()}");
            }
        }
    }
}
