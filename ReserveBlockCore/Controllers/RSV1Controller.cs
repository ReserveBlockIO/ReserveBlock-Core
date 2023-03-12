using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Utilities;

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
