using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using Spectre.Console;
using System;
using System.Drawing;
using System.Security.Principal;

namespace ReserveBlockCore.Controllers
{
    [ActionFilterController]
    [Route("rsapi/[controller]")]
    [Route("rsapi/[controller]/{somePassword?}")]
    [ApiController]
    public class RSV1Controller : ControllerBase
    {
        /// <summary>
        /// Dumps out all reserve accounts locally stored.
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetAllReserveAccounts")]
        public async Task<string> GetAllReserveAccounts()
        {
            var output = "Command not recognized."; // this will only display if command not recognized.

            var reserveAccounts = ReserveAccount.GetReserveAccounts();
            if (reserveAccounts?.Count() > 0)
            {
                output = JsonConvert.SerializeObject(new { Success = true, Message = $"{reserveAccounts?.Count()} Found!", ReserveAccounts = reserveAccounts });
            }
            else
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = "No Accounts" });
            }

            return output;
        }

        /// <summary>
        /// Dumps out a specific reserve account locally stored.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        [HttpGet("GetReserveAccountInfo/{address}")]
        public async Task<string> GetReserveAccountInfo(string address)
        {
            var output = "Command not recognized."; // this will only display if command not recognized.

            var reserveAccount = ReserveAccount.GetReserveAccountSingle(address);
            if (reserveAccount != null)
            {
                output = JsonConvert.SerializeObject(new { Success = true, Message = $"Account Found!", ReserveAccount = reserveAccount });
            }
            else
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = "No Account Found." });
            }

            return output;
        }

        /// <summary>
        /// Dumps out a specific reserve account locally stored.
        /// </summary>
        /// <param name="scUID"></param>
        /// <param name="address"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        [HttpGet("GetReserveAccountNFTAssets/{scUID}/{address}/{**password}")]
        public async Task<string> GetReserveAccountNFTAssets(string scUID,string address, string password)
        {
            var output = "Command not recognized."; // this will only display if command not recognized.
            var aqDB = AssetQueue.GetAssetQueue();
            if(aqDB == null)
                return JsonConvert.SerializeObject(new { Success = false, Message = "Asset Queue DB Null." });

            var aq = aqDB.Query().Where(x => x.SmartContractUID == scUID).FirstOrDefault(); 

            if(aq == null)
                return JsonConvert.SerializeObject(new { Success = false, Message = "Did not find an active Asset Queue record. Please wait 30 seconds and try again." });

            var fromAddress = ReserveAccount.GetReserveAccountSingle(address);

            if(fromAddress == null)
                return JsonConvert.SerializeObject(new { Success = false, Message = "No ReserveAccount Found." });

            var privateKey = ReserveAccount.GetPrivateKey(fromAddress, password, true);

            if(privateKey == null)
                return JsonConvert.SerializeObject(new { Success = false, Message = "Password or Private Key did not match. Please try entering password again." });

            var preSig = SignatureService.CreateSignature(aq.SmartContractUID, privateKey, fromAddress.PublicKey);

            var result = await NFTAssetFileUtility.DownloadAssetFromBeacon(aq.SmartContractUID, aq.Locator, preSig, aq.MD5List);

            if(result == "Success")
            {
                aq.IsComplete = true;
                aq.Attempts = 0;
                aq.NextAttempt = DateTime.UtcNow;
                aqDB.UpdateSafe(aq);

                output = JsonConvert.SerializeObject(new { Success = true, Message = "Assets have been queued for download. Please check logs if you do not start to see them." });
            }
            else
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = "Assets failed to Queue for download. Please check logs for more details." });
            }
            
            

            return output;
        }

        /// <summary>
        /// Dumps out all reserve transactions locally stored.
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetReserveTransactions")]
        public async Task<string> GetReserveTransactions()
        {
            var output = "Command not recognized."; // this will only display if command not recognized.

            var rTXs = ReserveTransactions.GetReserveTransactionsDb();
            if (rTXs != null)
            {
                var rtxList = rTXs.Query().Where(x => true).ToEnumerable();
                if(rtxList.Any())
                {
                    output = JsonConvert.SerializeObject(new { Success = true, Message = $"{rtxList?.Count()} Found!", ReserveTransactions = rtxList }, Formatting.Indented);
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Success = false, Message = "No TXs" });
                }
            }
            else
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = "No TXs" });
            }

            return output;
        }

        /// <summary>
        /// Dumps out all reserve accounts locally stored.
        /// </summary>
        /// <param name="restoreCode"></param>
        /// <returns></returns>
        [HttpGet("DecodeRestoreCode/{restoreCodeBase}")]
        public async Task<string> DecodeRestoreCode(string restoreCodeBase)
        {
            var output = "Command not recognized."; // this will only display if command not recognized.

            if(restoreCodeBase != null)
            {
                var restoreCode = restoreCodeBase.ToStringFromBase64().Split("//");
                var rsrvKey = restoreCode[0];
                var recoveryKey = restoreCode[1];

                if (restoreCode.Length == 2)
                {
                    output = JsonConvert.SerializeObject(new { Success = true, Message = $"Restore Code Decoded", ReserveAccountPrivateKey = rsrvKey, RBXRecoveryAccountPrivateKey = recoveryKey }, Formatting.Indented);
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Success = false, Message = "Improper Restore Code" }, Formatting.Indented);
                }
            }
            

            return output;
        }

        /// <summary>
        /// Produces a new reserve address
        /// </summary>
        /// <returns></returns>
        [HttpPost("NewReserveAddress")]
        public async Task<string> NewReserveAddress([FromBody] object jsonData)
        {
            var output = "";
            try
            {
                if(jsonData != null)
                {
                    var rsrvAccountPayload = JsonConvert.DeserializeObject<ReserveAccount.ReserveAccountCreatePayload>(jsonData.ToString());
                    if(rsrvAccountPayload != null)
                    {
                        var result = ReserveAccount.CreateNewReserveAccount(rsrvAccountPayload.Password, rsrvAccountPayload.StoreRecoveryAccount);
                        if(result != null)
                        {
                            output = JsonConvert.SerializeObject(new { Success = true, Message = "Reserve Account Created", ReserveAccount = result }, Formatting.Indented);
                            return output;
                        }
                    }
                    else
                    {
                        output = JsonConvert.SerializeObject(new { Success = false, Message = "Failed to deserialize payload" }, Formatting.Indented);
                    }
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Success = false, Message = "Json Payload was empty." }, Formatting.Indented);
                }
                
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error. Error: {ex.ToString()}" }, Formatting.Indented);
            }

            return output;
        }

        /// <summary>
        /// Send a reserve transaction. Specify from, to, and amount
        /// </summary>
        /// <returns></returns>
        [HttpPost("SendReserveTransaction")]
        public async Task<string> SendReserveTransaction([FromBody] object jsonData)
        {
            var output = "";
            try
            {
                if (jsonData != null)
                {
                    var sendTxPayload = JsonConvert.DeserializeObject<ReserveAccount.SendTransactionPayload>(jsonData.ToString());
                    if(sendTxPayload != null)
                    {
                        var fromAddress = sendTxPayload.FromAddress;
                        var toAddress = sendTxPayload.ToAddress;
                        var amount = sendTxPayload.Amount * 1.0M; //ensure it is decimal formatted
                        var password = sendTxPayload.DecryptPassword;

                        if(fromAddress.StartsWith("xRBX") && toAddress.StartsWith("xRBX"))
                        {
                            output = JsonConvert.SerializeObject(new { Success = false, Message = "Reserve accounts cannot send to another Reserve Account." });
                            return output;
                        }

                        var addrCheck = AddressValidateUtility.ValidateAddress(toAddress);

                        if (addrCheck == false)
                        {
                            output = JsonConvert.SerializeObject(new { Success = false, Message = "This is not a valid RBX address to send to. Please verify again." });
                            return output;
                        }

                        var result = await ReserveAccount.CreateReserveTx(sendTxPayload);
                        output = JsonConvert.SerializeObject(new { Success = result.Item1 != null ? true : false, Message = result.Item1 != null ? $"Success! TX ID: {result.Item1.Hash}" : result.Item2 });
                        return output;

                    }
                    else
                    {
                        output = JsonConvert.SerializeObject(new { Success = false, Message = "Failed to deserialize payload" });
                    }
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Success = false, Message = "Json Payload was empty." });
                }
            }
            catch(Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error. Error: {ex.ToString()}" });
            }

            return output;
        }

        /// <summary>
        /// Send a reserve nft transfer transaction. Specify from, to, nft scuid, backupURL, and password
        /// </summary>
        /// <returns></returns>
        [HttpPost("ReserveTransferNFT")]
        public async Task<string> ReserveTransferNFT([FromBody] object jsonData)
        {
            var output = "";
            try
            {
                if(jsonData == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = "Json Payload was empty." });

                var sendNFTTransferPayload = JsonConvert.DeserializeObject<ReserveAccount.SendNFTTransferPayload>(jsonData.ToString());
                if (sendNFTTransferPayload == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = "Json Payload could not be deserialized." });

                var toAddress = sendNFTTransferPayload.ToAddress;
                var backupURL = sendNFTTransferPayload.BackupURL;

                var fromAddress = ReserveAccount.GetReserveAccountSingle(sendNFTTransferPayload.FromAddress);

                if (fromAddress == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = "From Address was not found in wallet. You may only send from addresses you own locally." });

                if (fromAddress.Address.StartsWith("xRBX") && toAddress.StartsWith("xRBX"))
                {
                    output = JsonConvert.SerializeObject(new { Success = false, Message = "Reserve accounts cannot send to another Reserve Account." });
                    return output;
                }

                var keyString = ReserveAccount.GetPrivateKey(sendNFTTransferPayload.FromAddress, sendNFTTransferPayload.DecryptPassword);

                if (keyString == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = "Unable to get private key. Please ensure account is in wallet, and password was correct." });

                var key = ReserveAccount.GetPrivateKey(keyString);

                if (key == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = "Unable to get private key. Please ensure account is in wallet, and password was correct." });

                var sc = SmartContractMain.SmartContractData.GetSmartContract(sendNFTTransferPayload.SmartContractUID);

                if (sc == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Could not find smart contract with UID of {sendNFTTransferPayload.SmartContractUID}" });

                if (!sc.IsPublished)
                    return JsonConvert.SerializeObject(new { Success = false, Message = "Smart contract found, but has not been minted." });

                var result = await ReserveAccount.CreateReserveNFTTransferTx(sendNFTTransferPayload);

                output = JsonConvert.SerializeObject(new { Success = result.Item1, Message = $"{result.Item2}" });
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Unknown Error Occurred. Error: {ex.ToString()}" });
                SCLogUtility.Log($"Unknown Error Transfering NFT. Error: {ex.ToString()}", "SCV1Controller.TransferNFT()");
            }

            return output;
        }

        /// <summary>
        /// Send a reserve transaction. Specify from, to, and amount
        /// </summary>
        /// <param name="address"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        [HttpGet("PublishReserveAccount/{address}/{**password}")]
        public async Task<string> PublishReserveAccount(string address, string password)
        {
            var output = "";
            try
            {
                var account = ReserveAccount.GetReserveAccountSingle(address);

                if(account == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Account cannot be null" });

                if(account.AvailableBalance < 5) 
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Account must have a balance of 5 RBX." });

                if(account.IsNetworkProtected)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Account has already been published to the network." });

                var result = await ReserveAccount.CreateReservePublishTx(account, password);

                if(result.Item1 == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"{result.Item2}" });

                output = JsonConvert.SerializeObject(new { Success = true, Message = $"Success! TX ID: {result.Item1.Hash}", result.Item1.Hash });
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error. Error: {ex.ToString()}" });
            }

            return output;
        }

        /// <summary>
        /// Call Back a Reserve Account Transaction
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        [HttpGet("CallBackReserveAccountTx/{hash}/{**password}")]
        public async Task<string> CallBackReserveAccountTx(string hash, string password)
        {
            var output = "";
            try
            {
                var tx = ReserveTransactions.GetTransactions(hash);

                if(tx == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Could not find a Reserve TX with the hash: {hash}" });

                var address = tx.FromAddress;

                var account = ReserveAccount.GetReserveAccountSingle(address);

                if (account == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Account cannot be null." });


                var result = await ReserveAccount.CreateReserveCallBackTx(account, password, tx.Hash);

                if (result.Item1 == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"{result.Item2}" });

                output = JsonConvert.SerializeObject(new { Success = true, Message = $"Success! TX ID: {result.Item1.Hash}", result.Item1.Hash });
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error. Error: {ex.ToString()}" });
            }

            return output;
        }

        /// <summary>
        /// Recover a Reserve Accounts entire chain worth (NFTs and RBX) to Recovery Account
        /// </summary>
        /// <param name="recoveryPhrase"></param>
        /// <param name="address"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        [HttpGet("RecoverReserveAccountTx/{recoveryPhrase}/{address}/{**password}")]
        public async Task<string> RecoverReserveAccountTx(string recoveryPhrase, string address, string password)
        {
            var output = "";
            try
            {
                var account = ReserveAccount.GetReserveAccountSingle(address);

                if (account == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Account cannot be null." });

                var result = await ReserveAccount.CreateReserveRecoverTx(account, password, recoveryPhrase);

                if (result.Item1 == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"{result.Item2}" });

                output = JsonConvert.SerializeObject(new { Success = true, Message = $"Success! TX ID: {result.Item1.Hash}", result.Item1.Hash });
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error. Error: {ex.ToString()}" });
            }

            return output;
        }

        /// <summary>
        /// Restores a reserve address to a wallet with recovery info
        /// </summary>
        /// <returns></returns>
        [HttpPost("RestoreReserveAddress")]
        public async Task<string> RestoreReserveAddress([FromBody] object jsonData)
        {
            var output = "[]";

            try
            {
                if (jsonData == null)
                    output = JsonConvert.SerializeObject(new { Success = false, Message = "Json Payload was empty." });
                
                var rsrvAccountPayload = JsonConvert.DeserializeObject<ReserveAccount.ReserveAccountRestorePayload>(jsonData.ToString());
                if (rsrvAccountPayload == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = "Failed to deserialize payload" });
                

                if(rsrvAccountPayload.OnlyRestoreRecovery)
                {
                    var restoreCode = rsrvAccountPayload.RestoreCode.ToStringFromBase64().Split("//");
                    var recoveryKey = restoreCode[1];

                    var account = await AccountData.RestoreAccount(recoveryKey, rsrvAccountPayload.RescanForTx);

                    if (account == null)
                    {
                        output = "NAC";
                    }
                    else if (account.Address == null || account.Address == "")
                    {
                        output = "NAC";
                    }
                    else
                    {
                        output = JsonConvert.SerializeObject(new { Success = true, Message = "Recovery Account Restored", Account = account });
                        return output;
                    }
                }
                else
                {
                    var result = ReserveAccount.RestoreReserveAccount(rsrvAccountPayload.RestoreCode, rsrvAccountPayload.Password, rsrvAccountPayload.StoreRecoveryAccount, rsrvAccountPayload.RescanForTx);
                    if (result != null)
                    {
                        output = JsonConvert.SerializeObject(new { Success = true, Message = "Reserve Account Restored", ReserveAccount = result });
                        return output;
                    }
                }
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error. Error: {ex.ToString()}" });
            }

            return output;
        }
    }
}
