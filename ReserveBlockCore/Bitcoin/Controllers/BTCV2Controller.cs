using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Newtonsoft.Json;
using ReserveBlockCore.Bitcoin.Models;
using ReserveBlockCore.Controllers;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Utilities;
using System.Security.Principal;

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

        /// <summary>
        /// Imports Address from Key
        /// </summary>
        /// <returns></returns>
        [HttpGet("ImportPrivateKey/{privateKey}")]
        public async Task<string> ImportPrivateKey(string privateKey)
        {
            if (privateKey == null)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Key cannot be null." }); ;
            }
            if (privateKey?.Length < 50)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Incorrect key format. Please try again." }); ;
            }

            //hex key
            if (privateKey?.Length > 58)
            {
                BitcoinAccount.ImportPrivateKey(privateKey);
            }
            else
            {
                BitcoinAccount.ImportPrivateKeyWIF(privateKey);
            }

            LogUtility.Log("Key Import Successful.", "BTCV2Controller.GetNewAddress()");

            return JsonConvert.SerializeObject(new { Success = true, Message = $"New address has been imported." });
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

            return JsonConvert.SerializeObject(new { Success = true, Message = $"UTXOs Found For this Address.", BitcoinAccount = btcAccount });
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
                return JsonConvert.SerializeObject(new { Success = false, Message = $"address cannot be null." }); ;
            }

            var utxoList = BitcoinUTXO.GetUTXOs(address);

            if (utxoList == null)
                return JsonConvert.SerializeObject(new { Success = true, Message = $"No UTXOs Found For this Address." });

            if(utxoList.Count == 0)
                return JsonConvert.SerializeObject(new { Success = true, Message = $"No UTXOs Found For this Address." });


            return JsonConvert.SerializeObject(new { Success = true, Message = $"UTXOs Found For this Address.", UTXOs = utxoList });
        }
    }
}
