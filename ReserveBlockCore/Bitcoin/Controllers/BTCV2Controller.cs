using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Newtonsoft.Json;
using ReserveBlockCore.Bitcoin.Models;
using ReserveBlockCore.Bitcoin.Services;
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
    }
}
