using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Newtonsoft.Json;
using ReserveBlockCore.Bitcoin.Models;
using ReserveBlockCore.Controllers;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Utilities;

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
    }
}
