using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;
using System.Net;
using System.Security.Principal;

namespace ReserveBlockCore.Controllers
{
    [ActionFilterController]
    [Route("tkapi/[controller]")]
    [Route("tkapi/[controller]/{somePassword?}")]
    [ApiController]
    public class TKV2Controller : ControllerBase
    {
        /// <summary>
        /// Check Status of API
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "RBX-Wallet", "Token API Standard V2" };
        }

        /// <summary>
        /// Creates a transaction to send a desired token from one account to another
        /// </summary>
        /// <param name="scUID"></param>
        /// <param name="fromAddress"></param>
        /// <param name="toAddress"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("TransferToken/{scUID}/{fromAddress}/{toAddress}/{amount}")]
        public async Task<string> TransferToken(string scUID, string fromAddress, string toAddress, decimal amount)
        {
            try
            {
                var sc = SmartContractStateTrei.GetSmartContractState(scUID);

                if (sc == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Could not locate the requested Smart Contract." });

                if (sc.IsToken == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Smart Contract is not a token contract." });

                if (sc.IsToken.Value == false)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Smart Contract is not a token contract." });

                if (sc.TokenDetails != null && sc.TokenDetails.IsPaused)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Contract has been paused." });

                toAddress = toAddress.Replace(" ", "").ToAddressNormalize();

                var account = AccountData.GetSingleAccount(fromAddress);

                if (account == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Account does not exist locally." });

                var stateAccount = StateData.GetSpecificAccountStateTrei(fromAddress);

                if (stateAccount == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Account does not exist at the state level." });

                if (stateAccount.TokenAccounts.Count == 0)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Account does not have any token accounts." });

                var tokenAccount = stateAccount.TokenAccounts.Where(x => x.SmartContractUID == scUID).FirstOrDefault();

                if (tokenAccount == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Account does not own any of the token {sc.TokenDetails?.TokenName}." });

                if (tokenAccount.Balance < amount)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Insufficient Balance. Current Balance: {tokenAccount.Balance} - Attempted Send of: {amount}." });

                var result = await TokenContractService.TransferToken(sc, tokenAccount, fromAddress, toAddress, amount);

                return JsonConvert.SerializeObject(new { Success = result.Item1, Message = $"Result: {result.Item2}" });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error: {ex.ToString()}" });
            }
        }
        /// <summary>
        /// Creates a transaction to burn tokens for own balance.
        /// </summary>
        /// <param name="scUID"></param>
        /// <param name="fromAddress"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("BurnToken/{scUID}/{fromAddress}/{amount}")]
        public async Task<string> BurnToken(string scUID, string fromAddress, decimal amount)
        {
            try
            {
                var sc = SmartContractStateTrei.GetSmartContractState(scUID);
                if (sc == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Could not locate the requested Smart Contract." });

                if (sc.IsToken == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Smart Contract is not a token contract." });

                if (sc.IsToken.Value == false)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Smart Contract is not a token contract." });

                if (sc.TokenDetails != null && sc.TokenDetails.IsPaused)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Contract has been paused." });

                var account = AccountData.GetSingleAccount(fromAddress);

                if (account == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Account does not exist locally." });

                var stateAccount = StateData.GetSpecificAccountStateTrei(fromAddress);

                if (stateAccount == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Account does not exist at the state level." });

                if (stateAccount.TokenAccounts.Count == 0)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Account does not have any token accounts." });

                var tokenAccount = stateAccount.TokenAccounts.Where(x => x.SmartContractUID == scUID).FirstOrDefault();

                if (tokenAccount == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Account does not own any of the token {sc.TokenDetails?.TokenName}." });

                if (tokenAccount.Balance < amount)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Insufficient Balance. Current Balance: {tokenAccount.Balance} - Attempted Send of: {amount}." });

                var result = await TokenContractService.BurnToken(sc, tokenAccount, fromAddress, amount);

                return JsonConvert.SerializeObject(new { Success = result.Item1, Message = $"Result: {result.Item2}" });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error: {ex.ToString()}" });
            }
        }

        /// <summary>
        /// Creates a transaction to mint new tokens.
        /// </summary>
        /// <param name="scUID"></param>
        /// <param name="fromAddress"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("TokenMint/{scUID}/{fromAddress}/{amount}")]
        public async Task<string> TokenMint(string scUID, string fromAddress, decimal amount)
        {
            try
            {
                var sc = SmartContractStateTrei.GetSmartContractState(scUID);
                if (sc == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Could not locate the requested Smart Contract." });

                if (sc.IsToken == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Smart Contract is not a token contract." });

                if (sc.IsToken.Value == false)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Smart Contract is not a token contract." });

                if (sc.TokenDetails != null && sc.TokenDetails.IsPaused)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Contract has been paused." });

                var account = AccountData.GetSingleAccount(fromAddress);

                if (account == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Account does not exist locally." });

                if (account.Address != sc.TokenDetails.ContractOwner)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Account does not own this token contract." });

                if (sc.TokenDetails.StartingSupply > 0.0M)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Token supply was not set to infinite." });

                var result = await TokenContractService.TokenMint(sc, fromAddress, amount);

                return JsonConvert.SerializeObject(new { Success = result.Item1, Message = $"Result: {result.Item2}" });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error: {ex.ToString()}" });
            }
        }

        /// <summary>
        /// Pauses contract from doing anything else. Only owner can pause and unpause.
        /// </summary>
        /// <param name="scUID"></param>
        /// <param name="fromAddress"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("PauseTokenContract/{scUID}/{fromAddress}")]
        public async Task<string> PauseTokenContract(string scUID, string fromAddress)
        {
            try
            {
                bool pause;
                var sc = SmartContractStateTrei.GetSmartContractState(scUID);
                if (sc == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Could not locate the requested Smart Contract." });

                if (sc.IsToken == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Smart Contract is not a token contract." });

                if (sc.IsToken.Value == false)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Smart Contract is not a token contract." });

                if (sc.TokenDetails == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Token details are null." });

                pause = !sc.TokenDetails.IsPaused;

                var account = AccountData.GetSingleAccount(fromAddress);

                if (account == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Account does not exist locally." });

                if (account.Address != sc.TokenDetails.ContractOwner)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Account does not own this token contract." });

                var result = await TokenContractService.PauseTokenContract(sc, fromAddress, pause);

                return JsonConvert.SerializeObject(new { Success = result.Item1, Message = $"Result: {result.Item2}" });
            }
            catch(Exception ex)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error: {ex.ToString()}" });
            }
        }

        /// <summary>
        /// Change contract owner. Only current owner can do this.
        /// </summary>
        /// <param name="scUID"></param>
        /// <param name="fromAddress"></param>
        /// <param name="banAddress"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("BanAddress/{scUID}/{fromAddress}/{banAddress}")]
        public async Task<string> BanAddress(string scUID, string fromAddress, string banAddress)
        {
            try
            {
                var sc = SmartContractStateTrei.GetSmartContractState(scUID);
                if (sc == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Could not locate the requested Smart Contract." });

                if (sc.IsToken == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Smart Contract is not a token contract." });

                if (sc.IsToken.Value == false)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Smart Contract is not a token contract." });

                if (sc.TokenDetails == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Token details are null." });

                var account = AccountData.GetSingleAccount(fromAddress);

                if (account == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Account does not exist locally." });

                if (account.Address != sc.TokenDetails.ContractOwner)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Account does not own this token contract." });

                banAddress = banAddress.Replace(" ", "").ToAddressNormalize();

                var result = await TokenContractService.BanAddress(sc, fromAddress, banAddress);

                return JsonConvert.SerializeObject(new { Success = result.Item1, Message = $"Result: {result.Item2}" });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error: {ex.ToString()}" });
            }
        }

        /// <summary>
        /// Change contract owner. Only current owner can do this.
        /// </summary>
        /// <param name="scUID"></param>
        /// <param name="fromAddress"></param>
        /// <param name="toAddress"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("ChangeTokenContractOwnership/{scUID}/{fromAddress}/{toAddress}")]
        public async Task<string> ChangeTokenContractOwnership(string scUID, string fromAddress, string toAddress)
        {
            try
            {
                var sc = SmartContractStateTrei.GetSmartContractState(scUID);
                if (sc == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Could not locate the requested Smart Contract." });

                if (sc.IsToken == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Smart Contract is not a token contract." });

                if (sc.IsToken.Value == false)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Smart Contract is not a token contract." });

                if (sc.TokenDetails == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Token details are null." });

                var account = AccountData.GetSingleAccount(fromAddress);

                if (account == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Account does not exist locally." });

                if (account.Address != sc.TokenDetails.ContractOwner)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Account does not own this token contract." });

                toAddress = toAddress.Replace(" ", "").ToAddressNormalize();

                var result = await TokenContractService.ChangeTokenContractOwnership(sc, fromAddress, toAddress);

                return JsonConvert.SerializeObject(new { Success = result.Item1, Message = $"Result: {result.Item2}" });

            }
            catch(Exception ex)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error: {ex.ToString()}" });
            }
        }
    }
}
