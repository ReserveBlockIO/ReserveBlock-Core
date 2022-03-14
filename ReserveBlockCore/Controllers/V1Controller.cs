using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace ReserveBlockCore.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class V1Controller : ControllerBase
    {
        // GET: api/<V1>
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/<V1>/getgenesisblock
        [HttpGet("{id}")]
        public string Get(string id)
        {
            //use Id to get specific commands
            var output = "Command not recognized."; // this will only display if command not recognized.
            var command = id.ToLower();
            switch (command)
            {
                //This is initial example. Returns Genesis block in JSON format.
                case "getgenesisblock":
                    var genBlock = BlockchainData.GetGenesisBlock();
                    BlockchainData.PrintBlock(genBlock);
                    output = JsonConvert.SerializeObject(genBlock);
                    break;
            }

            return output;
        }
        [HttpGet("CheckStatus")]
        public async Task<string> CheckStatus()
        {
            //use Id to get specific commands
            var output = "Online"; // this will only display if command not recognized.
            

            return output;
        }
        [HttpGet("GetNewAddress")]
        public async Task<string> GetNewAddress()
        {
            //use Id to get specific commands
            var output = "Fail"; // this will only display if command not recognized.
            var account = AccountData.CreateNewAccount();

            output = account.Address + ":" + account.PrivateKey;

            return output;
        }
        [HttpGet("GetWalletInfo")]
        public async Task<string> GetWalletInfo()
        {
            //use Id to get specific commands
            var output = "Command not recognized."; // this will only display if command not recognized.
            var peerCount = "";
            var blockHeight = BlockchainData.GetHeight().ToString();

            var peersConnected = await P2PClient.ArePeersConnected();
            if (peersConnected.Item1 == true)
            {
                peerCount = peersConnected.Item2.ToString();
            }
            else
            {
                peerCount = "0";
            }

            output = blockHeight + ":" + peerCount + ":" + Program.BlocksDownloading.ToString();

            return output;
        }
        [HttpGet("GetAllAddresses")]
        public async Task<string> GetAllAddresses()
        {
            //use Id to get specific commands
            var output = "Command not recognized."; // this will only display if command not recognized.
            var accounts = AccountData.GetAccounts();

            if (accounts.Count() == 0)
            {
                output = "No Accounts";
            }
            else
            {
                var accountList = accounts.FindAll().ToList();
                output = JsonConvert.SerializeObject(accountList);
            }

            return output;
        }

        [HttpGet("GetValidatorAddresses")]
        public async Task<string> GetValidatorAddresses()
        {
            //use Id to get specific commands
            var output = "Command not recognized."; // this will only display if command not recognized.
            var accounts = AccountData.GetPossibleValidatorAccounts();

            if (accounts.Count() == 0)
            {
                output = "No Accounts";
            }
            else
            {
                var accountList = accounts.ToList();
                output = JsonConvert.SerializeObject(accountList);
            }

            return output;
        }

        [HttpGet("TurnOnValidator/{id}")]
        public async Task<string> TurnOnValidator(string id)
        {
            var output = "Command not recognized."; // this will only display if command not recognized.
            var validators = Validators.Validator.GetAll();
            var validator = validators.FindOne(x => x.Address == id);

            if(validator != null)
            {
                var accounts = AccountData.GetAccounts();
                var presentValidator = accounts.FindOne(x => x.IsValidating == true);
                if(presentValidator != null)
                {
                    output = "There is already a account flagged as validator in this wallet: " + presentValidator.Address;
                }
                else
                {
                    var account = AccountData.GetSingleAccount(id);
                    if (account != null)
                    {
                        account.IsValidating = true;
                        accounts.Update(account);
                        Program.ValidatorAddress = account.Address;
                        output = "Success! The requested account has been turned on: " + account.Address;
                    }
                    else
                    {
                        output = "The requested account was not found in wallet. You may need to import it.";
                    }
                }
            }
            else
            {
                output = "STV";
            }
            

            return output;
        }

        [HttpGet("TurnOffValidator/{id}")]
        public async Task<string> TurnOffValidator(string id)
        {
            var output = "Command not recognized."; // this will only display if command not recognized.

            var accounts = AccountData.GetAccounts();
            var presentValidator = accounts.FindOne(x => x.IsValidating == true);
            if (presentValidator != null)
            {
                presentValidator.IsValidating = false;
                accounts.Update(presentValidator);
                Program.ValidatorAddress = ""; 
                output = "The validator has been turned off: " + presentValidator.Address;
            }
            else
            {
                output = "There are currently no active validators running.";
            }
            
            return output;
        }

        [HttpGet("GetValidatorInfo/{id}")]
        public async Task<string> GetValidatorInfo(string id)
        {
            //use Id to get specific commands
            var output = "Command not recognized."; // this will only display if command not recognized.
            var validators = Validators.Validator.GetAll();
            var validator = validators.FindOne(x => x.Address == id);

            if(validator != null)
            {
                output = validator.UniqueName;
            }
            else
            {
                output = "Validator not on network yet.";
            }

            return output;
        }

        [HttpGet("GetAddressInfo/{id}")]
        public async Task<string> GetAddressInfo(string id)
        {
            //use Id to get specific commands
            var output = "Command not recognized."; // this will only display if command not recognized.
            var account = AccountData.GetSingleAccount(id);

            if (account == null)
            {
                output = "No Accounts";
            }
            else
            {
                output = JsonConvert.SerializeObject(account);
            }

            return output;
        }

        [HttpGet("ImportPrivateKey/{id}")]
        public async Task<string> ImportPrivateKey(string id)
        {
            //use Id to get specific commands
            var output = "Command not recognized."; // this will only display if command not recognized.
            var account = AccountData.RestoreAccount(id);

            if (account == null)
            {
                output = "NAC";
            }
            else if(account.Address == null || account.Address == "")
            {
                output = "NAC";
            }
            else
            {
                output = JsonConvert.SerializeObject(account);
            }

            return output;
        }

        //Sends block information - for like block explorers

        [HttpGet("SendBlock/{id}")]
        public async Task<string> SendBlock(string id)
        {
            //use Id to get specific commands
            var output = "Command not recognized."; // this will only display if command not recognized.

            long height = Convert.ToInt64(id);
            var block = BlockchainData.GetBlockByHeight(height);

            if (block == null)
            {
                output = "NNB";
            }
            else
            {
                output = JsonConvert.SerializeObject(block);
            }

            return output;
        }

        [HttpGet("GetLastBlock")]
        public async Task<string> GetLastBlock()
        {
            //use Id to get specific commands
            var output = "Command not recognized."; // this will only display if command not recognized.

            var block = BlockchainData.GetLastBlock();

            if (block == null)
            {
                output = "NNB";
            }
            else
            {
                output = JsonConvert.SerializeObject(block);
            }

            return output;
        }

        [HttpGet("GetAllTransactions")]
        public async Task<string> GetAllTransactions()
        {
            //use Id to get specific commands
            var output = "FAIL"; // this will only display if command not recognized.
            var transactions = TransactionData.GetAll();

            if (transactions.Count() == 0)
            {
                output = "No TX";
            }
            else
            {
                var transactionsList = transactions.FindAll().ToList();
                output = JsonConvert.SerializeObject(transactionsList);
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

            if(result.Contains("Success"))
            {
                output = result;
            }

            return output;
        }

        [HttpGet("StartValidating/{addr}/{uname}")]
        public async Task<string> StartValidating(string addr, string uname)
        {
            var output = false;
            var result = "FAIL";
            var address = addr;
            var uniqueName = uname;

            var valAccount = AccountData.GetPossibleValidatorAccounts();
            if (valAccount.Count() > 0)
            {
                var accountCheck = valAccount.Where(x => x.Address == address).FirstOrDefault();
                if(accountCheck != null)
                {
                    if(accountCheck.IsValidating)
                    {
                        result = "Node is already flagged as validator.";
                        return result;
                    }
                    try
                    {
                        var valResult = await ValidatorService.StartValidating(accountCheck, uniqueName);
                        result = valResult;
                    }
                    catch (Exception ex)
                    {

                    }
                    output = true;
                }
            }

            return result;
        }
        
        [HttpGet("SendExit")]
        public async Task SendExit()
        {
            //use Id to get specific commands
            var output = "Starting Stop"; // this will only display if command not recognized.

            Program.StopAllTimers = true;
            Thread.Sleep(1000);
            Environment.Exit(0);
        }

        [HttpGet("SendExitComplete")]
        public async Task<string> SendExitComplete()
        {
            var output = "SA"; 
            return output;
        }

    }
}
