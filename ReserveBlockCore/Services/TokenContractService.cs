using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.Utilities;
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

                var scMain = SmartContractMain.GenerateSmartContractInMemory(sc.ContractData);
                var scData = SmartContractReaderService.ReadSmartContract(scMain);

                var txData = "";

                if (!string.IsNullOrWhiteSpace(scData.Result.Item1))
                {
                    var bytes = Encoding.Unicode.GetBytes(scData.Result.Item1);
                    var scBase64 = SmartContractUtility.Compress(bytes).ToBase64();
                    var newSCInfo = new[]
                    {
                        new { Function = "TokenTransfer()", ContractUID = sc.SmartContractUID, FromAddress = fromAddress, ToAddress = toAddress, Amount = amount}
                };

                    txData = JsonConvert.SerializeObject(newSCInfo);
                }

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
                    NFTLogUtility.Log($"Balance insufficient. SCUID: {scMain.SmartContractUID}", "TokenContractService.TransferToken()");
                    return (false, $"Balance insufficient. SCUID: {scMain.SmartContractUID}");
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
                    NFTLogUtility.Log($"TX Signature Failed. SCUID: {scMain.SmartContractUID}", "TokenContractService.TransferToken()");
                    return (false, $"TX Signature Failed. SCUID: {scMain.SmartContractUID}");
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

                    NFTLogUtility.Log($"TX Success. SCUID: {scMain.SmartContractUID}", "TokenContractService.TransferToken()");
                    return (true, $"TX Success. SCUID: {scMain.SmartContractUID}");
                }
                else
                {
                    var output = "Fail! Transaction Verify has failed.";
                    tokenTx.TransactionStatus = TransactionStatus.Failed;
                    TransactionData.AddTxToWallet(tokenTx, true);
                    NFTLogUtility.Log($"Error Transfer Failed TX Verify: {scMain.SmartContractUID}. Result: {result.Item2}", "TokenContractService.TransferToken()");
                    return (false, $"Error Transfer Failed TX Verify: {scMain.SmartContractUID}. Reason: {result.Item2}");
                }
            }
            catch { }

            return (false, "Reached end of method.");
            
        }
    }
}
