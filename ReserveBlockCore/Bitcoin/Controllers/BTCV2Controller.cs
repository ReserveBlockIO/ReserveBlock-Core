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
using System.Security.Principal;
using System.Text.RegularExpressions;

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

            ScriptPubKeyType scriptPubKeyType = addressFormat == Bitcoin.BitcoinAddressFormat.SegwitP2SH ? ScriptPubKeyType.SegwitP2SH :
                addressFormat == Bitcoin.BitcoinAddressFormat.Segwit ? ScriptPubKeyType.Segwit : ScriptPubKeyType.TaprootBIP86;

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
        /// Get Transaction Fee
        /// </summary>
        /// <param name="faddr"></param>
        /// <param name="taddr"></param>
        /// <param name="amt"></param>
        /// <param name="feeRate"></param>
        /// <returns></returns>
        [HttpGet("GetTransactionFee/{faddr}/{taddr}/{amt}/{feeRate}")]
        public async Task<string> GetTransactionFee(string faddr, string taddr, decimal amt, int feeRate)
        {
            var result = await TransactionService.GetTransactionFee(faddr, taddr, amt, feeRate);

            if(result.Item1)
            {
                var parseResult = decimal.TryParse(result.Item2, out var btcFee);
                var satParseResult = ulong.TryParse(result.Item2, out var satFee);
                return JsonConvert.SerializeObject(new { Success = result.Item1, Message = result.Item2, SatoshiFee = satParseResult ? satFee : 0, BitcoinFee = parseResult ? (btcFee * TransactionService.SatoshiMultiplier) : 0 });
            }
               
            return JsonConvert.SerializeObject(new { Success = result.Item1, Message = result.Item2 });
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
        public async Task<string> TokenizeBitcoin([FromBody] object jsonData)
        {
            try
            {
                if (jsonData == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Payload body was null" });

                var payload = JsonConvert.DeserializeObject<BTCTokenizePayload>(jsonData.ToString());

                if(payload == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Failed to deserialize payload" });

                //TODO:
                //Get Deposit Address - Get Proofs too
                var tokenizationDetails = await ArbiterService.GetTokenizationDetails(payload.RBXAddress);

                if(tokenizationDetails.Item1 == "FAIL")
                    return JsonConvert.SerializeObject(new { Success = false, Message = tokenizationDetails.Item1 });

                var scMain = await TokenizationService.CreateTokenizationScMain(payload.RBXAddress, payload.FileLocation, 
                    tokenizationDetails.Item1, tokenizationDetails.Item2, payload.Name, payload.Description);

                if (scMain == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = "Failed to generate vBTC token. Please check logs for more." });

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
            return JsonConvert.SerializeObject(new { Success = true, Message = "If no tokenized elements TokenizedList will be 'NULL'", TokenizedList = TokenizedBitcoin.GetTokenizedList() });
        }

        /// <summary>
        /// Unwrapps
        /// </summary>
        /// <param name="scUID"></param>
        /// <returns></returns>
        [HttpGet("RevealPrivateKey/{scUID}")]
        public async Task<string> RevealPrivateKey(string scUID)
        {
            return JsonConvert.SerializeObject(new { Success = true, Message = $"Coming Soon..." });
        }

    }
}
