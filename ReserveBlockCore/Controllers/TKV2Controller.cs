using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
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
        /// Gets in memory token list for all tokens associated with wallet accounts.
        /// </summary>
        /// <param name="scUID"></param>
        /// <param name="getAll"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("GetTokens/{scUID}/{getAll}")]
        public async Task<string> GetTokens(string scUID, bool getAll)
        {
            if(getAll)
            {
                var tokenList = Globals.Tokens.Values.ToList(); 
                if(tokenList.Count == 0)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"No tokens found in memory. Please restart wallet and try again." });

                return JsonConvert.SerializeObject(new { Success = true, Message = $"Token Found", Tokens = tokenList });
            }
            else
            {
                if(Globals.Tokens.TryGetValue(scUID, out var token))
                {
                    return JsonConvert.SerializeObject(new { Success = true, Message = $"Token Found", Token = token });
                }
                else
                {
                    var tokenState = SmartContractStateTrei.GetSmartContractState(scUID);
                    if(tokenState == null)
                        return JsonConvert.SerializeObject(new { Success = false, Message = $"Could not locate the requested Smart Contract." });
                    
                    if(tokenState.TokenDetails == null )
                        return JsonConvert.SerializeObject(new { Success = false, Message = $"Could not locate the token details." });

                    //Adding to memory to alleviate next query.
                    Globals.Tokens.TryAdd(scUID, tokenState.TokenDetails);

                    return JsonConvert.SerializeObject(new { Success = true, Message = $"Token Found", Token = tokenState.TokenDetails });
                }
            }
        }

        /// <summary>
        /// Gets in memory token list for all tokens and updates it.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("GetTokensUpdate")]
        public async Task<string> GetTokensUpdate()
        {
            Globals.Tokens.Clear();
            Globals.Tokens = new ConcurrentDictionary<string, TokenDetails>();

            var accounts = AccountData.GetAccounts().Query().Where(x => true).ToList();
            var rAccounts = ReserveAccount.GetReserveAccounts();

            if (accounts?.Count > 0)
            {
                foreach (var account in accounts)
                {
                    var stateAccount = StateData.GetSpecificAccountStateTrei(account.Address);
                    if (stateAccount != null)
                    {
                        if (stateAccount.TokenAccounts?.Count > 0)
                        {
                            var tokenAccountList = stateAccount.TokenAccounts;
                            foreach (var tokenAccount in tokenAccountList)
                            {
                                var tokenContract = SmartContractStateTrei.GetSmartContractState(tokenAccount.SmartContractUID);
                                if (tokenContract != null)
                                {
                                    var tokenDetails = tokenContract.TokenDetails;
                                    if (tokenDetails != null)
                                    {
                                        Globals.Tokens.TryAdd(tokenAccount.SmartContractUID, tokenDetails);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (rAccounts?.Count > 0)
            {
                foreach (var rAccount in rAccounts)
                {
                    var stateAccount = StateData.GetSpecificAccountStateTrei(rAccount.Address);
                    if (stateAccount != null)
                    {
                        if (stateAccount.TokenAccounts?.Count > 0)
                        {
                            var tokenAccountList = stateAccount.TokenAccounts;
                            foreach (var tokenAccount in tokenAccountList)
                            {
                                var tokenContract = SmartContractStateTrei.GetSmartContractState(tokenAccount.SmartContractUID);
                                if (tokenContract != null)
                                {
                                    var tokenDetails = tokenContract.TokenDetails;
                                    if (tokenDetails != null)
                                    {
                                        Globals.Tokens.TryAdd(tokenAccount.SmartContractUID, tokenDetails);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return JsonConvert.SerializeObject(new { Success = true, Message = $"Token List Updated." });
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

                if (stateAccount.TokenAccounts?.Count == 0)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Account does not have any token accounts." });

                var tokenAccount = stateAccount.TokenAccounts?.Where(x => x.SmartContractUID == scUID).FirstOrDefault();

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

                if (stateAccount.TokenAccounts?.Count == 0)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Account does not have any token accounts." });

                var tokenAccount = stateAccount.TokenAccounts?.Where(x => x.SmartContractUID == scUID).FirstOrDefault();

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

                if(sc.TokenDetails == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Token Details are null." });

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

        /// <summary>
        /// Post a vote topic for a token community.
        /// </summary>
        /// <param name="jsonData"></param>
        /// <returns></returns>
        [HttpPost("CreateTokenTopic")]
        public async Task<string> CreateTokenTopic([FromBody] object jsonData)
        {
            try
            {
                if (jsonData != null)
                {
                    var topicCreate = JsonConvert.DeserializeObject<TokenVoteTopic.TopicCreate>(jsonData.ToString());
                    var scUID = topicCreate.SmartContractUID;
                    var fromAddress = topicCreate.FromAddress;

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

                    var topic = new TokenVoteTopic
                    {
                        TopicName = topicCreate.TopicName,
                        TopicDescription = topicCreate.TopicDescription,
                        SmartContractUID = topicCreate.SmartContractUID,
                        MinimumVoteRequirement = topicCreate.MinimumVoteRequirement,
                    };

                    var buildResult = topic.Build(topicCreate.VotingEndDays);

                    if (!buildResult)
                        return JsonConvert.SerializeObject(new { Success = false, Message = $"Failed to create topic." });

                    var result = await TokenContractService.CreateTokenVoteTopic(sc, fromAddress, topic);

                    return JsonConvert.SerializeObject(new { Success = result.Item1, Message = $"Result: {result.Item2}" });
                }
            }
            catch(Exception ex)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error: {ex.ToString()}" });
            }
            return JsonConvert.SerializeObject(new { Success = false, Message = $"End of Method." });
        }

        /// <summary>
        /// Cast your vote on a specific token topic. (yes or no)
        /// </summary>
        /// <param name="scUID"></param>
        /// <param name="fromAddress"></param>
        /// <param name="topicUID"></param>
        /// <param name="voteType"></param>
        /// <returns></returns>
        [HttpGet("CastTokenTopicVote/{scUID}/{fromAddress}/{topicUID}/{voteType}")]
        public async Task<string> CastTokenTopicVote(string scUID, string fromAddress, string topicUID, VoteType voteType)
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

                var topic = sc.TokenDetails?.TokenTopicList?.Where(x => x.TopicUID == topicUID).FirstOrDefault();

                if(topic == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Topic was not found." });

                var account = AccountData.GetSingleAccount(fromAddress);

                if (account == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Account does not exist locally." });

                var stateAccount = StateData.GetSpecificAccountStateTrei(fromAddress);

                if (stateAccount == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Account does not exist at the state level." });

                if (stateAccount.TokenAccounts?.Count == 0)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Account does not have any token accounts." });

                var tokenAccount = stateAccount.TokenAccounts?.Where(x => x.SmartContractUID == scUID).FirstOrDefault();

                if (tokenAccount == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Account does not own any of the token {sc.TokenDetails?.TokenName}." });

                if(tokenAccount.Balance < topic.MinimumVoteRequirement)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"You do not meet the minimum required to vote." });

                var result = await TokenContractService.CastTokenVoteTopic(sc, tokenAccount, fromAddress, topicUID, voteType);

                return JsonConvert.SerializeObject(new { Success = result.Item1, Message = $"Result: {result.Item2}" });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error: {ex.ToString()}" });
            }
        }

        /// <summary>
        /// Gets your vote result.  Success variable = Vote
        /// </summary>
        /// <param name="scUID"></param>
        /// <returns></returns>
        [HttpGet("GetVoteBySmartContractUID/{scUID}")]
        public async Task<string> GetVoteBySmartContractUID(string scUID)
        {
            var vote = TokenVote.GetSpecificTopicVotesBySCUID(scUID);
            if(vote == null)
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Could not find vote with SCUID : {scUID}" });

            return JsonConvert.SerializeObject(new { Success = true, Message = $"Vote Found!", Vote = vote });
        }

        /// <summary>
        /// Gets your vote result.  Success variable = Vote
        /// </summary>
        /// <param name="topicUID"></param>
        /// <returns></returns>
        [HttpGet("GetVoteByTopic/{topicUID}")]
        public async Task<string> GetVoteByTopic(string topicUID)
        {
            var vote = TokenVote.GetSpecificTopicVotesByTUID(topicUID);
            if (vote == null)
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Could not find vote with TUID : {topicUID}" });

            return JsonConvert.SerializeObject(new { Success = true, Message = $"Vote Found!", Vote = vote });
        }

        /// <summary>
        /// Gets your vote result. Success variable = VoteList
        /// </summary>
        /// <param name="fromAddress"></param>
        /// <returns></returns>
        [HttpGet("GetVotesByAddress/{fromAddress}")]
        public async Task<string> GetVotesByAddress(string fromAddress)
        {
            var votes = TokenVote.GetSpecificAddressVotes(fromAddress);
            if (votes?.Count() == 0)
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Could not find votes with Address : {fromAddress}" });

            return JsonConvert.SerializeObject(new { Success = true, Message = $"Vote Found!", VoteList = votes });
        }

        /// <summary>
        /// Gets all vote results for owner. Success variable = VoteList
        /// </summary>
        /// <param name="tUID"></param>
        /// <returns></returns>
        [HttpGet("GetVotesByAddress/{fromAddress}")]
        public async Task<string> GetTokenOwnerVoteList(string tUID)
        {
            var votes = TokenVote.GetOwnerVoteList(tUID);
            if (votes?.Count() == 0)
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Could not find votes with TUID : {tUID}" });

            return JsonConvert.SerializeObject(new { Success = true, Message = $"Vote Found!", VoteList = votes });
        }

    }
}
