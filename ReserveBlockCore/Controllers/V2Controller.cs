using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using System.Security.Principal;

namespace ReserveBlockCore.Controllers
{
    [ActionFilterController]
    [Route("api/[controller]")]
    [Route("api/[controller]/{somePassword?}")]
    [ApiController]
    public class V2Controller : ControllerBase
    {
        /// <summary>
        /// Check Status of API
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "RBX-Wallet", "API V2" };
        }

        /// <summary>
        /// Gets RBX and Token Balances
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("GetBalances")]
        public async Task<string> GetBalances()
        {
            var output = "Command not recognized."; // this will only display if command not recognized.
            var accounts = AccountData.GetAccounts();
            var rAccounts = ReserveAccount.GetReserveAccounts();

            if (accounts.Count() == 0)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"No Accounts Found" });
            }
            else
            {
                var accountList = accounts.Query().Where(x => true).ToEnumerable();
                List<AccountBalance> accountBalanceList = new List<AccountBalance>();
                foreach (var account in accountList)
                {
                    List<TokenAccount> tokenAccounts = new List<TokenAccount>();
                    var stateAccount = StateData.GetSpecificAccountStateTrei(account.Address);
                    if (stateAccount != null)
                    {
                        AccountBalance accountBalance = new AccountBalance
                        {
                            Address = account.Address,
                            RBXBalance = account.Balance,
                            RBXLockedBalance = account.LockedBalance,
                            TokenAccounts = stateAccount.TokenAccounts?.Count > 0 ? stateAccount.TokenAccounts : tokenAccounts
                        };
                        accountBalanceList.Add(accountBalance);
                    }
                    else
                    {
                        AccountBalance accountBalance = new AccountBalance
                        {
                            Address = account.Address,
                            RBXBalance = 0.0M,
                            RBXLockedBalance = 0.0M,
                            TokenAccounts = tokenAccounts
                        };
                        accountBalanceList.Add(accountBalance);
                    }
                }

                if(rAccounts?.Count > 0)
                {
                    foreach(var rAccount in rAccounts)
                    {
                        List<TokenAccount> tokenAccounts = new List<TokenAccount>();
                        var stateAccount = StateData.GetSpecificAccountStateTrei(rAccount.Address);
                        if (stateAccount != null)
                        {
                            AccountBalance accountBalance = new AccountBalance
                            {
                                Address = rAccount.Address,
                                RBXBalance = rAccount.AvailableBalance,
                                TokenAccounts = stateAccount.TokenAccounts?.Count > 0 ? stateAccount.TokenAccounts : tokenAccounts
                            };
                            accountBalanceList.Add(accountBalance);
                        }
                        else
                        {
                            AccountBalance accountBalance = new AccountBalance
                            {
                                Address = rAccount.Address,
                                RBXBalance = 0.0M,
                                RBXLockedBalance = 0.0M,
                                TokenAccounts = tokenAccounts
                            };
                            accountBalanceList.Add(accountBalance);
                        }
                    }
                }

                return JsonConvert.SerializeObject(new { Success = true, Message = $"Accounts Found", AccountBalances = accountBalanceList });
            }
        }

        /// <summary>
        /// Gets RBX and Token Balances from State for specific address. Local or Remote
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("GetStateBalance/{address}")]
        public async Task<string> GetStateBalance(string address)
        {
            var stateAccount = StateData.GetSpecificAccountStateTrei(address);
            List<TokenAccount> tokenAccounts = new List<TokenAccount>();

            if (stateAccount == null)
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Account not found." });

            AccountBalance accountBalance = new AccountBalance
            {
                Address = stateAccount.Key,
                RBXBalance = stateAccount.Balance,
                RBXLockedBalance= stateAccount.LockedBalance,
                TokenAccounts = stateAccount.TokenAccounts?.Count > 0 ? stateAccount.TokenAccounts : tokenAccounts
            };

            return JsonConvert.SerializeObject(new { Success = true, Message = $"Account Found", AccountBalance = accountBalance });

        }
    }
}
