using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Newtonsoft.Json;
using ReserveBlockCore.Bitcoin.Models;
using ReserveBlockCore.Bitcoin.Services;
using ReserveBlockCore.Controllers;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Principal;
using System.Text.RegularExpressions;
using static ReserveBlockCore.Globals;
using static ReserveBlockCore.Services.ArbiterService;

namespace ReserveBlockCore.Bitcoin.Controllers
{
    [ActionFilterController]
    [Route("btcapi/[controller]")]
    [Route("btcapi/[controller]/{somePassword?}")]
    [ApiController]
    public class BTCV2Controller : ControllerBase
    {
        /// <summary>
        /// Check Status of API
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "RBX/BTC-Wallet", "BTC API Standard V2" };
        }

        /// <summary>
        /// Get Default Address Type
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetDefaultAddressType")]
        public async Task<string> GetDefaultAddressType()
        {
            return JsonConvert.SerializeObject(new { Success = true, Message = $"", AddressType = Globals.ScriptPubKeyType.ToString() });
        }

        /// <summary>
        /// Produces a new address
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetNewAddress")]
        public async Task<string> GetNewAddress()
        {           
            var account = BitcoinAccount.CreateAddress();
            
            LogUtility.Log("New Address Created: " + account.Address, "BTCV2Controller.GetNewAddress()");

            return JsonConvert.SerializeObject(new { Success = true, Message = $"New Address Added", account.Address, account.PrivateKey, account.WifKey });
        }

        /// <summary>
        /// Imports Address from Key
        /// </summary>
        /// <returns></returns>
        [HttpGet("ImportPrivateKey/{privateKey}/{addressFormat?}")]
        public async Task<string> ImportPrivateKey(string privateKey, Bitcoin.BitcoinAddressFormat? addressFormat = Bitcoin.BitcoinAddressFormat.Segwit)
        {
            if (privateKey == null)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Key cannot be null." }); ;
            }
            if (privateKey?.Length < 50)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Incorrect key format. Please try again." }); ;
            }

            ScriptPubKeyType scriptPubKeyType = addressFormat == Bitcoin.BitcoinAddressFormat.SegwitP2SH ? NBitcoin.ScriptPubKeyType.SegwitP2SH :
                addressFormat == Bitcoin.BitcoinAddressFormat.Segwit ? NBitcoin.ScriptPubKeyType.Segwit : NBitcoin.ScriptPubKeyType.TaprootBIP86;

            //hex key
            if (privateKey?.Length > 58)
            {
                BitcoinAccount.ImportPrivateKey(privateKey, scriptPubKeyType);
            }
            else
            {
                BitcoinAccount.ImportPrivateKeyWIF(privateKey, scriptPubKeyType);
            }

            LogUtility.Log("Key Import Successful.", "BTCV2Controller.GetNewAddress()");

            return JsonConvert.SerializeObject(new { Success = true, Message = $"New address has been imported." });
        }
        /// <summary>
        /// Get all addresses
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetBitcoinAccountList/{omitKeys?}")]
        public async Task<string> GetBitcoinAccountList(bool? omitKeys = false)
        {
            var btcAccounts = BitcoinAccount.GetBitcoinAccounts();

            if (btcAccounts == null)
                return JsonConvert.SerializeObject(new { Success = true, Message = $"No Account Found For this Address." });

            if (omitKeys.Value)
            {
                btcAccounts.ForEach(x => {
                    x.PrivateKey = "REMOVED";
                    x.WifKey = "REMOVED";
                });
            }

            return JsonConvert.SerializeObject(new { Success = true, Message = $"Accounts Founds", BitcoinAccounts = btcAccounts });
        }

        /// <summary>
        /// Get address 
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetBitcoinAccount/{address}/{omitKeys?}")]
        public async Task<string> GetBitcoinAccount(string address, bool? omitKeys = false)
        {
            if (address == null)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"address cannot be null." }); ;
            }

            var btcAccount = BitcoinAccount.GetBitcoinAccount(address);

            if (btcAccount == null)
                return JsonConvert.SerializeObject(new { Success = true, Message = $"No Account Found For this Address." });

            if(omitKeys.Value)
            {
                btcAccount.PrivateKey = "REMOVED";
                btcAccount.WifKey = "REMOVED";
            }

            return JsonConvert.SerializeObject(new { Success = true, Message = $"Account Found", BitcoinAccount = btcAccount });
        }

        /// <summary>
        /// Resets bitcoin accounts and their UTXOs
        /// </summary>
        /// <returns></returns>
        [HttpGet("ResetAccount")]
        public async Task<string> ResetAccount()
        {
            //Only can do once every 5 mins
            var now = DateTime.Now;
            var nextRun = Globals.LastRanBTCReset.AddMinutes(5);
            if(now < nextRun)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Last ran at: {Globals.LastRanBTCReset}. Cannot reset again until: {nextRun}", NextRunTimeAllowed = nextRun });
            }

            var btcUtxoDb = BitcoinUTXO.GetBitcoinUTXO();
            if (btcUtxoDb != null)
                btcUtxoDb.DeleteAllSafe();

            var btcAccounts = BitcoinAccount.GetBitcoinAccounts();

            if (btcAccounts?.Count() > 0)
            {
                var btcADb = BitcoinAccount.GetBitcoin();
                if (btcADb != null)
                {
                    foreach (var btcAccount in btcAccounts)
                    {
                        btcAccount.Balance = 0.0M;
                        btcADb.UpdateSafe(btcAccount);
                    }
                }
            }

            _ = Bitcoin.AccountCheck();

            Globals.LastRanBTCReset = DateTime.Now;

            return JsonConvert.SerializeObject(new { Success = true, Message = $"Bitcoin accounts reset." });
        }

        /// <summary>
        /// Get address UTXO List
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetAddressUTXOList/{address}")]
        public async Task<string> GetAddressUTXOList(string address)
        {
            if (address == null)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"address cannot be null." });
            }

            var utxoList = BitcoinUTXO.GetUTXOs(address);

            if (utxoList == null)
                return JsonConvert.SerializeObject(new { Success = true, Message = $"No UTXOs Found For this Address." });

            if(utxoList.Count == 0)
                return JsonConvert.SerializeObject(new { Success = true, Message = $"No UTXOs Found For this Address." });


            return JsonConvert.SerializeObject(new { Success = true, Message = $"UTXOs Found For this Address.", UTXOs = utxoList });
        }

        /// <summary>
        /// Get address send TX List
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetAddressTXList/{address}")]
        public async Task<string> GetAddressTXList(string address)
        {
            if (address == null)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"address cannot be null." });
            }

            var txList = BitcoinTransaction.GetTXs(address);

            if (txList?.Count() == 0)
                return JsonConvert.SerializeObject(new { Success = true, Message = $"No TXs Found For this Address." });

            return JsonConvert.SerializeObject(new { Success = true, Message = $"TXs Found For this Address.", TXs = txList });
        }

        /// <summary>
        /// Gets the last time the accounts were synced
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetLastAccounySync")]
        public async Task<string> GetLastAccounySync()
        {
            var nextCheckTime = Globals.BTCAccountLastCheckedDate.AddMinutes(4);
            return JsonConvert.SerializeObject(new { Success = true, Message = $"Sync Times", LastChecked = Globals.BTCAccountLastCheckedDate, NextCheck = nextCheckTime });
        }

        /// <summary>
        /// Send a transaction. Specify from, to, and amount
        /// </summary>
        /// <param name="faddr"></param>
        /// <param name="taddr"></param>
        /// <param name="amt"></param>
        /// <param name="feeRate"></param>
        /// <param name="overrideInternalSend"></param>
        /// <returns></returns>
        [HttpGet("SendTransaction/{faddr}/{taddr}/{amt}/{feeRate}/{overrideInternalSend?}")]
        public async Task<string> SendTransaction(string faddr, string taddr, decimal amt, int feeRate, bool overrideInternalSend = false)
        {
            var result = await TransactionService.SendTransaction(faddr, taddr, amt, feeRate, overrideInternalSend);

            return JsonConvert.SerializeObject(new { Success = result.Item1, Message = result.Item2 }); ;
        }

        /// <summary>
        /// Calcs fee for transaction
        /// </summary>
        /// <param name="faddr"></param>
        /// <param name="taddr"></param>
        /// <param name="amt"></param>
        /// <param name="feeRate"></param>
        /// <returns></returns>
        [HttpGet("CalculateFee/{faddr}/{taddr}/{amt}/{feeRate}")]
        public async Task<string> CalculateFee(string faddr, string taddr, decimal amt, int feeRate)
        {
            var result = await TransactionService.CalcuateFee(faddr, taddr, amt, feeRate);

            if (!result.Item1)
                return JsonConvert.SerializeObject(new { Success = result.Item1, Message = $"{result.Item2}", Fee = "0.0" }); 

            return JsonConvert.SerializeObject(new { Success = result.Item1, Message = "Fee Calculated", Fee = result.Item2 });
        }

        /// <summary>
        /// Create an BTC ADNR and associate it to address
        /// </summary>
        /// <param name="address"></param>
        /// <param name="btcAddress"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        [HttpGet("CreateAdnr/{address}/{btcAddress}/{name}")]
        public async Task<string> CreateAdnr(string address, string btcAddress, string name)
        {
            string output = "";

            try
            {
                var wallet = AccountData.GetSingleAccount(address);
                if (wallet != null)
                {
                    var addressFrom = wallet.Address;
                    var adnr = BitcoinAdnr.GetBitcoinAdnr();
                    if (adnr != null)
                    {
                        var adnrAddressCheck = adnr.FindOne(x => x.BTCAddress == btcAddress);
                        if (adnrAddressCheck != null)
                        {
                            output = JsonConvert.SerializeObject(new { Success = false, Message = $"This address already has a DNR associated with it: {adnrAddressCheck.Name}" });
                            return output;
                        }

                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            name = name.ToLower();

                            var limit = Globals.ADNRLimit;

                            if (name.Length > limit)
                            {
                                output = JsonConvert.SerializeObject(new { Success = false, Message = "A DNR may only be a max of 65 characters" });
                                return output;
                            }

                            var nameCharCheck = Regex.IsMatch(name, @"^[a-zA-Z0-9]+$");
                            if (!nameCharCheck)
                            {
                                output = JsonConvert.SerializeObject(new { Success = false, Message = "A DNR may only contain letters and numbers." });
                                return output;
                            }
                            else
                            {
                                var nameRBX = name.ToLower() + ".btc";
                                var nameCheck = adnr.FindOne(x => x.Name == nameRBX);
                                if (nameCheck == null)
                                {
                                    var result = await BitcoinAdnr.CreateAdnrTx(address, name, btcAddress);
                                    if (result.Item1 != null)
                                    {
                                        output = JsonConvert.SerializeObject(new { Success = true, Message = $"Transaction has been broadcasted.", Hash = result.Item1.Hash });
                                    }
                                    else
                                    {
                                        output = JsonConvert.SerializeObject(new { Success = false, Message = $"Transaction failed to broadcast. Error: {result.Item2}" });
                                    }
                                }
                                else
                                {
                                    output = JsonConvert.SerializeObject(new { Success = false, Message = $"This name already has a DNR associated with it: {nameCheck.Name}" });
                                    return output;
                                }
                            }

                        }
                        else
                        {
                            output = JsonConvert.SerializeObject(new { Success = false, Message = $"Name was empty." });
                        }
                    }
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Success = false, Message = $"Account with address: {address} was not found." });
                    return output;
                }
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error: {ex.ToString()}" });
            }

            return output;
        }

        /// <summary>
        /// Transfer ADNR from one address to another
        /// </summary>
        /// <param name="toAddress"></param>
        /// <param name="btcFromAddress"></param>
        /// <param name="btcToAddress"></param>
        /// <returns></returns>
        [HttpGet("TransferAdnr/{toAddress}/{btcFromAddress}/{btcToAddress}")]
        public async Task<string> TransferAdnr(string toAddress, string btcFromAddress, string btcToAddress)
        {
            string output = "";

            try
            {
                var adnr = BitcoinAdnr.GetBitcoinAdnr();
                if (adnr != null)
                {
                    var adnrCheck = adnr.FindOne(x => x.BTCAddress == btcFromAddress);
                    if (adnrCheck == null)
                    {
                        output = JsonConvert.SerializeObject(new { Success = false, Message = $"This address does not have a DNR associated with it." });
                        return output;
                    }
                    var wallet = AccountData.GetSingleAccount(adnrCheck.RBXAddress);
                    if (wallet == null)
                    {
                        output = JsonConvert.SerializeObject(new { Success = false, Message = $"Account with address: {adnrCheck.RBXAddress} was not found." });
                        return output;
                    }
                    var addressFrom = wallet.Address;
                    if (!string.IsNullOrWhiteSpace(toAddress))
                    {
                        var addrVerify = AddressValidateUtility.ValidateAddress(toAddress);
                        if (addrVerify == true)
                        {
                            var toAddrAdnr = adnr.FindOne(x => x.BTCAddress == btcToAddress);
                            if (toAddrAdnr == null)
                            {
                                var result = await BitcoinAdnr.TransferAdnrTx(addressFrom, toAddress, btcToAddress, btcFromAddress);
                                if (result.Item1 != null)
                                {
                                    output = JsonConvert.SerializeObject(new { Success = true, Message = $"Transaction has been broadcasted.", Hash = result.Item1.Hash });
                                }
                                else
                                {
                                    output = JsonConvert.SerializeObject(new { Success = false, Message = $"Transaction failed to broadcast. Error: {result.Item2}" });
                                }
                            }
                            else
                            {
                                output = JsonConvert.SerializeObject(new { Success = false, Message = $"To Address already has adnr associated to it." });
                            }
                        }
                        else
                        {
                            output = JsonConvert.SerializeObject(new { Success = false, Message = $"To Address is not a valid RBX address." });
                        }

                    }
                    else
                    {
                        output = JsonConvert.SerializeObject(new { Success = false, Message = $"Name was empty." });
                    }
                }
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error: {ex.ToString()}" });
            }

            return output;
        }

        /// <summary>
        /// Permanently remove ADNR from address.
        /// </summary>
        /// <param name="btcFromAddress"></param>
        /// <returns></returns>
        [HttpGet("DeleteAdnr/{btcFromAddress}")]
        public async Task<string> DeleteAdnr(string btcFromAddress)
        {
            string output = "";

            try
            {
                
                var adnr = BitcoinAdnr.GetBitcoinAdnr();
                if (adnr != null)
                {
                    var adnrCheck = adnr.FindOne(x => x.BTCAddress == btcFromAddress);
                    if (adnrCheck == null)
                    {
                        output = JsonConvert.SerializeObject(new { Success = false, Message = $"This address does not have a DNR associated with it: {adnrCheck.Name}" });
                        return output;
                    }
                    var wallet = AccountData.GetSingleAccount(adnrCheck.RBXAddress);
                    if (wallet == null)
                    {
                        output = JsonConvert.SerializeObject(new { Success = false, Message = $"Account with address: {adnrCheck.RBXAddress} was not found." });
                        return output;
                    }
                    var addressFrom = wallet.Address;
                    var result = await BitcoinAdnr.DeleteAdnrTx(addressFrom, btcFromAddress);
                    if (result.Item1 != null)
                    {
                        output = JsonConvert.SerializeObject(new { Success = true, Message = $"Transaction has been broadcasted.", Hash = result.Item1.Hash });
                    }
                    else
                    {
                        output = JsonConvert.SerializeObject(new { Success = false, Message = $"Transaction failed to broadcast. Error: {result.Item2}" });
                    }
                }
                
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error: {ex.ToString()}" });
            }

            return output;
        }

        /// <summary>
        /// Tokenize Bitcoin
        /// </summary>
        /// <returns></returns>
        [HttpPost("TokenizeBitcoin")]
        [ProducesResponseType(typeof(SwaggerResponse), StatusCodes.Status200OK)]
        public async Task<string> TokenizeBitcoin([FromBody] BTCTokenizePayload jsonData)
        {
            try
            {
                if (jsonData == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Payload body was null" });

                var payload = jsonData;

                if(payload == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Failed to deserialize payload" });

                var scUID = Guid.NewGuid().ToString().Replace("-", "") + ":" + TimeUtil.GetTime().ToString();

                var tokenizationDetails = await ArbiterService.GetTokenizationDetails(payload.RBXAddress, scUID);

                if(tokenizationDetails.Item1 == "FAIL")
                    return JsonConvert.SerializeObject(new { Success = false, Message = tokenizationDetails.Item2 });

                var scMain = await TokenizationService.CreateTokenizationScMain(payload.RBXAddress, payload.FileLocation, 
                    tokenizationDetails.Item1, tokenizationDetails.Item2, payload.Name, payload.Description);

                if (scMain == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = "Failed to generate vBTC token. Please check logs for more." });

                var featureList = payload.Features;
                if (featureList?.Count() > 0)
                {
                    var nonMultiAssetFeatures = featureList.Where(x => x.FeatureName != FeatureName.MultiAsset).ToList();
                    if (nonMultiAssetFeatures.Any())
                        return JsonConvert.SerializeObject(new { Success = false, Message = "vBTC Tokens may only contain multi-asset features." });

                    if(scMain.Features != null)
                    {
                        scMain.Features.AddRange(payload.Features);
                    }
                    
                }

                scMain.SmartContractUID = scUID; //premade scuid

                var createSC = await TokenizationService.CreateTokenizationSmartContract(scMain);

                if (!createSC.Item1)
                    return JsonConvert.SerializeObject(new { Success = false, Message = "Failed to write vBTC token contract. Please check logs for more." });

                var publishSc = await TokenizationService.MintSmartContract(createSC.Item2, true, TransactionType.TKNZ_MINT);

                if(!publishSc.Item1)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Failed To Produce a Valid TX. Reason: {publishSc.Item2}" });

                return JsonConvert.SerializeObject(new { Success = true, Message = "Transaction Success!", Hash = publishSc.Item2 });

            }
            catch(Exception ex)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error: Message: {ex}" });
            }
        }

        /// <summary>
        /// Get Tokenized BTC List
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetTokenizedBTCList")]
        public async Task<string> GetTokenizedBTCList()
        {
            var tknzList = TokenizedBitcoin.GetTokenizedList();
            return JsonConvert.SerializeObject(new { Success = true, Message = "If no tokenized elements TokenizedList will be 'NULL'", TokenizedList = tknzList });
        }

        /// <summary>
        /// Get Tokenized BTC List
        /// </summary>
        /// <returns></returns>
        [HttpGet("ReplaceByFee/{txid}/{feeRate}")]
        public async Task<string> ReplaceByFee(string txid, int feeRate)
        {
            if(string.IsNullOrEmpty(txid) || feeRate == 0)
                return JsonConvert.SerializeObject(new { Success = false, Message = "Incorrect URL parameters" });

            var result = await TransactionService.ReplaceByFeeTransaction(txid, feeRate);

            return result;
        }

        /// <summary>
        /// Broadcast transaction with hex
        /// </summary>
        /// <returns></returns>
        [HttpGet("Broadcast/{txHash}")]
        public async Task<string> Broadcast(string txHash)
        {
            var btcTran = NBitcoin.Transaction.Parse(txHash, Globals.BTCNetwork);

            if (btcTran == null)
                return JsonConvert.SerializeObject(new { Success = false, Message = "TX found, but failed to parse tx from hex signature." });

            _ = BroadcastService.BroadcastTx(btcTran);

            return JsonConvert.SerializeObject(new { Success = true, Message = "Broadcasting Transaction Again."});
        }

        /// <summary>
        /// Rebroadcast transaction
        /// </summary>
        /// <returns></returns>
        [HttpGet("Rebroadcast/{txid}")]
        public async Task<string> Rebroadcast(string txid)
        {
            if (string.IsNullOrEmpty(txid))
                return JsonConvert.SerializeObject(new { Success = false, Message = "Incorrect URL parameters" });

            var btcTransaction = await BitcoinTransaction.GetTX(txid);

            if(btcTransaction == null)
                return JsonConvert.SerializeObject(new { Success = false, Message = "Could not find transaction locally." });

            if(btcTransaction.Signature == null)
                return JsonConvert.SerializeObject(new { Success = false, Message = "Local TX found, but signed transaction missing." });

            var btcTran = NBitcoin.Transaction.Parse(btcTransaction.Signature, Globals.BTCNetwork);

            if(btcTran == null)
                return JsonConvert.SerializeObject(new { Success = false, Message = "Local TX found, but failed to parse tx from hex signature." });

            _ = BroadcastService.BroadcastTx(btcTran);

            return JsonConvert.SerializeObject(new { Success = true, Message = "Broadcasting Transaction Again.", btcTransaction.Hash });
        }

        /// <summary>
        /// Transfer amount to VFX
        /// </summary>
        /// <returns></returns>
        [HttpPost("TransferCoin")]
        [ProducesResponseType(typeof(SwaggerResponse), StatusCodes.Status200OK)]
        public async Task<string> TransferCoin([FromBody] BTCTokenizeTransaction jsonData)
        {
            try
            {
                var result = await TokenizationService.TransferCoin(jsonData);
                return result;

            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Error: {ex}" });
            }
        }

        /// <summary>
        /// Transfers ownership of the vBTC token
        /// </summary>
        /// <param name="scUID"></param>
        /// <param name="toAddress"></param>
        /// <param name="backupURL"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("TransferOwnership/{scUID}/{toAddress}")]
        [Route("TransferOwnership/{scUID}/{toAddress}/{**backupURL}")]
        [ProducesResponseType(typeof(SwaggerResponse), StatusCodes.Status200OK)]
        public async Task<string> TransferOwnership(string scUID, string toAddress, string? backupURL = "")
        {
            try
            {
                var result = await TokenizationService.TransferOwnership(scUID, toAddress, backupURL);
                return result;

            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Error: {ex}" });
            }
        }

        /// <summary>
        /// Withdrawal to BTC address
        /// </summary>
        /// <returns></returns>
        [HttpPost("WithdrawalCoin")]
        [ProducesResponseType(typeof(SwaggerResponse), StatusCodes.Status200OK)]
        public async Task<string> WithdrawalCoin([FromBody] BTCTokenizeTransaction jsonData)
        {
            try
            {
                if(jsonData == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"BTCTokenizeTransaction data was null" });

                if(string.IsNullOrEmpty(jsonData.FromAddress))
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"VFX From address cannot be null here." });

                var result = await TokenizationService.WithdrawalCoin(jsonData.FromAddress, jsonData.ToAddress, jsonData.SCUID, jsonData.Amount, jsonData.ChosenFeeRate);
                return result;
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Error: {ex}" });
            }
        }

        /// <summary>
        /// Get base vBTC image
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetDefaultImageBase")]
        [ProducesResponseType(typeof(SwaggerResponse), StatusCodes.Status200OK)]
        public async Task<string> GetDefaultImageBase()
        {
            try
            {
                var defaultImageLocation = NFTAssetFileUtility.GetvBTCDefaultLogoLocation();

                if(System.IO.File.Exists(defaultImageLocation))
                {
                    byte[] imageBytes = System.IO.File.ReadAllBytes(defaultImageLocation);
                    var imageBase = imageBytes.ToBase64();

                    return JsonConvert.SerializeObject(new { Success = true, 
                        Message = $"Success", 
                        EncodingFormat = "base64", 
                        ImageExtension = "png", 
                        ImageName = "defaultvBTC.png",
                        ImageBase = imageBase });
                }
                else
                {
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Could not find file in: {defaultImageLocation}" });
                }

            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Error: {ex}" });
            }
        }
    }
}
