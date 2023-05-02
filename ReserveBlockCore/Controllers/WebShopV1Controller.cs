using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ReserveBlockCore.Data;
using ReserveBlockCore.DST;
using ReserveBlockCore.Models;
using System.Diagnostics;

namespace ReserveBlockCore.Controllers
{
    [ActionFilterController]
    [Route("wsapi/[controller]")]
    [Route("wsapi/[controller]/{somePassword?}")]
    [ApiController]
    public class WebShopV1Controller : ControllerBase
    {
        /// <summary>
        /// Check Status of API
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "RBX-Wallet Web Shop API", "WS API" };
        }

        /// <summary>
        /// Connects to a shop : 'rbx://someurlgoeshere'
        /// </summary>
        /// <param name="address"></param>
        /// <param name="url"></param>
        /// <returns></returns>
        [HttpGet("ConnectToDecShop/{address}/{**url}")]
        public async Task<bool> ConnectToDecShop(string address, string url)
        {
            var decshop = await DecShop.GetDecShopStateTreiLeafByURL(url);

            if (decshop != null)
            {
                //removes current connection to shop
                await DSTMultiClient.DisconnectFromShop(url);
                var connectionResult = await DSTMultiClient.ConnectToShop(url, address);

                //if connectionResult == true create some looping event.

                if (connectionResult.Item1)
                    _ = DSTMultiClient.GetShopData(address, connectionResult.Item2);

                return connectionResult.Item1;
            }

            return false;
        }
    }
}
