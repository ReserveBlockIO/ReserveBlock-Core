using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using Spectre.Console;

namespace ReserveBlockCore.Controllers
{
    [ActionFilterController]
    [Route("rsapi/[controller]")]
    [Route("rsapi/[controller]/{somePassword?}")]
    [ApiController]
    public class RSV1Controller : ControllerBase
    {
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
                            output = JsonConvert.SerializeObject(new { Success = true, Message = "Reserve Account Created", ReserveAccount = result });
                            return output;
                        }
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
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error. Error: {ex.ToString()}" });
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
        /// Restores a reserve address
        /// </summary>
        /// <returns></returns>
        [HttpPost("RestoreReserveAddress")]
        public async Task<string> RestoreReserveAddress([FromBody] object jsonData)
        {
            var output = "";

            try
            {
                if (jsonData != null)
                {
                    var rsrvAccountPayload = JsonConvert.DeserializeObject<ReserveAccount.ReserveAccountRestorePayload>(jsonData.ToString());
                    if (rsrvAccountPayload != null)
                    {
                        var result = ReserveAccount.RestoreReserveAccount(rsrvAccountPayload.RestoreCode, rsrvAccountPayload.Password, rsrvAccountPayload.StoreRecoveryAccount, rsrvAccountPayload.RescanForTx);
                        if (result != null)
                        {
                            output = JsonConvert.SerializeObject(new { Success = true, Message = "Reserve Account Restored", ReserveAccount = result });
                            return output;
                        }
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
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error. Error: {ex.ToString()}" });
            }

            return output;
        }
    }
}
