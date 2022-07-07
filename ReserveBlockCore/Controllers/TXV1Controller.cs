using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System.Text.RegularExpressions;

namespace ReserveBlockCore.Controllers
{
    [ActionFilterController]
    [Route("txapi/[controller]")]
    [Route("txapi/[controller]/{somePassword?}")]
    [ApiController]
    public class TXV1Controller : ControllerBase
    {

        //Step 1.
        [HttpGet("GetTimestamp")]
        public async Task<string> GetTimestamp()
        {
            //use Id to get specific commands
            var output = "FAIL"; // this will only display if command not recognized.

            var timestamp = TimeUtil.GetTime();

            output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Timestamp Acquired.", Timestamp = timestamp });

            return output;
        }

        //Step 2.
        [HttpGet("GetAddressNonce/{address}")]
        public async Task<string> GetAddressNonce(string address)
        {
            //use Id to get specific commands
            var output = "FAIL"; // this will only display if command not recognized.

            var nextNonce = AccountStateTrei.GetNextNonce(address);

            output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Next Nonce Found.", Nonce = nextNonce });

            return output;
        }

        //Step 3.
        [HttpPost("GetRawTxFee")]
        public async Task<string> GetRawTxFee([FromBody] object jsonData)
        {
            var output = "";
            try
            {
                var tx = JsonConvert.DeserializeObject<Transaction>(jsonData.ToString());

                var nTx = new Transaction
                {
                    Timestamp = tx.Timestamp,
                    FromAddress = tx.FromAddress,
                    ToAddress = tx.ToAddress,
                    Amount = tx.Amount + 0.0M,
                    Fee = 0,
                    Nonce = AccountStateTrei.GetNextNonce(tx.FromAddress),
                    TransactionType = tx.TransactionType,
                };

                //Calculate fee for tx.
                nTx.Fee = FeeCalcService.CalculateTXFee(nTx);

                output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"TX Fee Calculated", Fee = nTx.Fee });
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Failed to calcuate Fee. Error: {ex.Message}" });
            }

            return output;
        }

        //Step 4.
        [HttpPost("GetTxHash")]
        public async Task<string> GetTxHash([FromBody] object jsonData)
        {
            var output = "";
            try
            {
                var tx = JsonConvert.DeserializeObject<Transaction>(jsonData.ToString());

                tx.Build();

                output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"TX Fee Calculated", Hash = tx.Hash });
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Failed to create Hash. Error: {ex.Message}" });
            }

            return output;
        }

        //Step 5.
        //You create the signature now in your application.

        //Step 6.
        [HttpGet("ValidateSignature/{message}/{address}/{**sigScript}")]
        public async Task<string> ValidateSignature(string message, string address, string sigScript)
        {
            string output;

            try
            {
                var result = SignatureService.VerifySignature(address, message, sigScript);

                if (result == true)
                {
                    output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Signature Verified." });
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Signature Not Verified." });
                }
            }
            catch(Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Signature Not Verified. Unknown Error: {ex.Message}" });
            }
            
            return output;
        }

        //If validation was true
        //Step 7.
        [HttpPost("VerifyRawTransaction")]
        public async Task<string> VerifyRawTransaction([FromBody] object jsonData)
        {
            var output = "";
            try
            {
                var transaction = JsonConvert.DeserializeObject<Transaction>(jsonData.ToString());

                if (transaction != null)
                {
                    var result = await TransactionValidatorService.VerifyTXDetailed(transaction);
                    if (result.Item1 == true)
                    {

                        output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Transaction has been verified.", Hash = transaction.Hash });
                    }
                    else
                    {

                        output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Transaction was not verified. Error: {result.Item2}" });
                    }
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Failed to deserialize transaction. Please try again." });
                }

            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Error - {ex.Message}. Please Try Again." });
            }

            return output;
        }

        //Step 8.
        [HttpPost("SendRawTransaction")]
        public async Task<string> SendRawTransaction([FromBody] object jsonData)
        {
            var output = "";
            try
            {
                var transaction = JsonConvert.DeserializeObject<Transaction>(jsonData.ToString());

                if (transaction != null)
                {
                    var result = await TransactionValidatorService.VerifyTX(transaction);
                    if (result == true)
                    {
                        TransactionData.AddToPool(transaction);
                        P2PClient.SendTXMempool(transaction);//send out to mempool

                        output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Transaction has been broadcasted.", Hash = transaction.Hash });
                    }
                    else
                    {
                        output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Transaction was not verified." });
                    }
                }
                else
                {

                    output = JsonConvert.SerializeObject(new { Result = "Fail", Message = "Failed to deserialize transaction. Please try again." });
                }

            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Error - {ex.Message}. Please Try Again." });
            }

            return output;
        }

        [HttpPost("TestRawTransaction")]
        public string TestRawTransaction([FromBody] object jsonData)
        {
            var output = jsonData.ToString();
            try
            {
                var tx = JsonConvert.DeserializeObject<Transaction>(jsonData.ToString());

                var json = JsonConvert.SerializeObject(tx);

                output = json;
            }
            catch (Exception ex)
            {
                output = $"Error - {ex.Message}. Please Try Again.";
            }

            return output;
        }

        [HttpGet("CreateDnr/{address}/{name}")]
        public async Task<string> CreateDnr(string address, string name)
        {
            string output = "";

            try
            {
                var wallet = AccountData.GetSingleAccount(address);
                if(wallet != null)
                {
                    var addressFrom = wallet.Address;
                    var adnr = Adnr.GetAdnr();
                    if(adnr != null)
                    {
                        var adnrCheck = adnr.FindOne(x => x.Address == addressFrom);
                        if (adnrCheck != null)
                        {
                            output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"This address already has a DNR associated with it: {adnrCheck.Name}" });
                            return output;
                        }
                        if (name != null && name != "")
                        {
                            var nameCharCheck = Regex.IsMatch(name, @"^[a-zA-Z0-9]+$");
                            if (!nameCharCheck)
                            {
                                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = "A DNR may only contain letters and numbers." });
                                return output;
                            }
                            else
                            {
                                var nameCheck = adnr.FindOne(x => x.Name == name);
                                if (nameCheck == null)
                                {
                                    var result = await Adnr.CreateAdnrTx(address, name);
                                    if (result.Item1 != null)
                                    {
                                        output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Transaction has been broadcasted.", Hash = result.Item1.Hash });
                                    }
                                    else
                                    {
                                        output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Transaction failed to broadcast. Error: {result.Item2}" });
                                    }
                                }
                            }

                        }
                        else
                        {
                            output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Name was empty." });
                        }
                    }
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Account with address: {address} was not found." });
                    return output;
                }
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Unknown Error: {ex.Message}" });
            }

            return output;
        }

        [HttpGet("SendTransaction/{faddr}/{taddr}/{amt}")]
        public async Task<string> SendTransaction(string faddr, string taddr, string amt)
        {
            var output = "FAIL";
            var fromAddress = faddr;
            var toAddress = taddr;
            var strAmount = amt;

            var addrCheck = AddressValidateUtility.ValidateAddress(toAddress);

            if (addrCheck == false)
            {
                output = "This is not a valid RBX address to send to. Please verify again.";
                return output;
            }

            decimal amount = new decimal();

            try
            {
                amount = decimal.Parse(strAmount);
            }
            catch
            {
                return output;
            }

            var result = WalletService.SendTXOut(fromAddress, toAddress, amount);

            if (result.Contains("Success"))
            {
                output = result;
            }

            return output;
        }
    }
}
