using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ReserveBlockCore.BIP39;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System.Globalization;
using System.Numerics;

namespace ReserveBlockCore.Controllers
{
    [ActionFilterController]
    [Route("api/[controller]")]
    [Route("api/[controller]/{somePassword?}")]
    [ApiController]
    public class V1Controller : ControllerBase
    {
        // GET: api/<V1>
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "RBX-Wallet", "API" };
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

        [HttpGet("UnlockWallet/{password}")]
        public async Task<string> UnlockWallet(string password)
        {
            var output = "";

            if (Program.APIPassword != null)
            {
                if (password != null)
                {
                    var passCheck = Program.APIPassword.ToDecrypt(password);
                    if (passCheck == password && passCheck != "Fail")
                    {
                        Program.APIUnlockTime = DateTime.UtcNow.AddMinutes(Program.WalletUnlockTime);
                        var successResult = new[]
                        {
                            new { Result = "Success", Message = $"Wallet has been unlocked for {Program.WalletUnlockTime} mins."}
                        };

                        output = JsonConvert.SerializeObject(successResult);
                    }
                    else
                    {
                        var failResult = new[]
                        {
                            new { Result = "Fail", Message = "Incorrect Password."}
                        };

                        output = JsonConvert.SerializeObject(failResult);
                    }
                }
            }
            else
            {
                var failResult = new[]
                {
                    new { Result = "Fail", Message = "No password has been configured."}
                };

                output = JsonConvert.SerializeObject(failResult);
            }

            return output;
        }

        [HttpGet("LockWallet/{password}")]
        public async Task<string> LockWallet(string password)
        {
            var output = "";

            if (Program.APIPassword != null)
            {
                if (password != null)
                {
                    var passCheck = Program.APIPassword.ToDecrypt(password);
                    if (passCheck == password && passCheck != "Fail")
                    {
                        Program.APIUnlockTime = DateTime.UtcNow;
                        var successResult = new[]
                        {
                            new { Result = "Success", Message = $"Wallet has been locked."}
                        };

                        output = JsonConvert.SerializeObject(successResult);
                    }
                    else
                    {
                        var failResult = new[]
                        {
                            new { Result = "Fail", Message = "Incorrect Password."}
                        };

                        output = JsonConvert.SerializeObject(failResult);
                    }
                }
            }
            else
            {
                var failResult = new[]
                {
                    new { Result = "Fail", Message = "No password has been configured."}
                };

                output = JsonConvert.SerializeObject(failResult);
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

        [HttpGet("GetHDWallet/{strength}")]
        public async Task<string> GetHDWallet(int strength)
        {
            var output = "";
            var mnemonic = HDWallet.HDWalletData.CreateHDWallet(strength, BIP39Wordlist.English);

            Program.HDWallet = true;

            var newHDWalletInfo = new[]
            {
                new { Result = mnemonic}
            };

            output = JsonConvert.SerializeObject(newHDWalletInfo);

            return output;
        }

        [HttpGet("GetRestoreHDWallet/{mnemonic}")]
        public async Task<string> GetRestoreHDWallet(string mnemonic)
        {
            var output = "";
            var mnemonicRestore = HDWallet.HDWalletData.RestoreHDWallet(mnemonic);

            var newHDWalletInfo = new[]
            {
                new { Result = mnemonicRestore}
            };

            output = JsonConvert.SerializeObject(newHDWalletInfo);

            return output;
        }

        [HttpGet("GetNewAddress")]
        public async Task<string> GetNewAddress()
        {
            //use Id to get specific commands
            Account account = null; 
            var output = "Fail"; // this will only display if command not recognized.
            if(Program.HDWallet == true)
            {
                account = HDWallet.HDWalletData.GenerateAddress();
            }
            else
            {
                account = AccountData.CreateNewAccount();
            }
            
            var newAddressInfo = new[]
            {
                new { Address = account.Address, PrivateKey = account.PrivateKey}
            };

            LogUtility.Log("New Address Created: " + account.Address, "V1Controller.GetNewAddress()");

            output = JsonConvert.SerializeObject(newAddressInfo);
            //output = account.Address + ":" + account.PrivateKey;

            return output;
        }
        [HttpGet("GetWalletInfo")]
        public async Task<string> GetWalletInfo()
        {
            //use Id to get specific commands
            var output = "Command not recognized."; // this will only display if command not recognized.
            var peerCount = "";
            var blockHeight = Program.BlockHeight.ToString();

            var peersConnected = await P2PClient.ArePeersConnected();
            if (peersConnected.Item1 == true)
            {
                peerCount = peersConnected.Item2.ToString();
            }
            else
            {
                peerCount = "0";
            }


            var walletInfo = new[]
            {
                new { BlockHeight = blockHeight, PeerCount = peerCount, BlocksDownloading = Program.BlocksDownloading.ToString(), 
                    IsResyncing = Program.IsResyncing.ToString(), IsChainSynced =  Program.IsChainSynced.ToString(), ChainCorrupted = Program.DatabaseCorruptionDetected.ToString()}
            };

            output = JsonConvert.SerializeObject(walletInfo);

            //output = blockHeight + ":" + peerCount + ":" + Program.BlocksDownloading.ToString() + ":" + Program.IsResyncing.ToString() + ":" + Program.IsChainSynced.ToString();

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
                        accounts.UpdateSafe(account);
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
                accounts.UpdateSafe(presentValidator);
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

            var block = Program.LastBlock;

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

        [HttpGet("GetRollbackBlocks/{id}")]
        public async Task<string> GetRollbackBlocks(string id)
        {
            //use Id to get specific commands
            var output = "Command not recognized."; // this will only display if command not recognized.

            int num = Convert.ToInt32(id);

            //This needs refactor
            //var result = await BlockRollbackUtility.RollbackBlocks(num);
            var result = false;


            if(result == true)
            {
                output = "Process has completed.";
            }
            else
            {
                output = "Process has failed. You will need to re-download all blocks.";
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
            var result = "No Potential Validator Accounts Found.";
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
                        ErrorLogUtility.LogError(ex.Message, "V1Controller.StartValidating - result: " + result);
                        result = $"Unknown Error Occured: {ex.Message}";
                    }
                    output = true;
                }
                else
                {
                    result = "Account provided was not found in wallet.";
                }
            }

            return result;
        }

        [HttpGet("ChangeValidatorName/{uname}")]
        public async Task<string> ChangeValidatorName(string uname)
        {
            string output = "";
            
            if(Program.ValidatorAddress != "")
            {
                var validatorTable = Validators.Validator.GetAll();
                var validator = validatorTable.FindOne(x => x.Address == Program.ValidatorAddress);
                validator.UniqueName = uname;
                validatorTable.UpdateSafe(validator);

                output = "Validator Unique Name Updated. Please restart wallet.";
            }
            return output;
        }

        [HttpGet("CreateSignature/{message}/{address}")]
        public async Task<string> CreateSignature(string message, string address)
        {
            string output;

            var account = AccountData.GetSingleAccount(address);
            if(account != null)
            {
                BigInteger b1 = BigInteger.Parse(account.PrivateKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
                PrivateKey privateKey = new PrivateKey("secp256k1", b1);

                var signature = SignatureService.CreateSignature(message, privateKey, account.PublicKey);
                output = signature;
            }
            else
            {
                output = "ERROR - Account not associated with wallet.";
            }
            
            return output;
        }

        [HttpGet("ValidateSignature/{message}/{address}/{**sigScript}")]
        public async Task<bool> ValidateSignature(string message, string address, string sigScript)
        {
            bool output;

            var result = SignatureService.VerifySignature(address, message, sigScript);
            output = result;

            return output;
        }

        [HttpGet("GetMempool")]
        public async Task<string> GetMempool()
        {
            string output = "";
            var txs = TransactionData.GetMempool();
            output = JsonConvert.SerializeObject(txs);

            return output;
        }

        [HttpGet("GetMemBlockCluster")]
        public async Task<string> GetMemBlockCluster()
        {
            string output = "";
            var blocks = Program.MemBlocks;
            output = JsonConvert.SerializeObject(blocks);

            return output;
        }

        [HttpGet("GetTaskAnswersList")]
        public async Task<string> GetTaskAnswersList()
        {
            string output = "";
            var taskAnswerList = P2PAdjServer.TaskAnswerList.Select(x => new {
                Address = x.Address,
                Answer = x.Answer,
                BlockHeight = x.Block != null ? x.Block.Height : 0,
                SubmitTime = x.SubmitTime
                
            });
            output = JsonConvert.SerializeObject(taskAnswerList);

            return output;
        }

        [HttpGet("GetMasternodesSent")]
        public async Task<string> GetMasternodesSent()
        {
            string output = "";
            var currentTime = DateTime.Now.AddMinutes(-15);
            var fortisPool = P2PAdjServer.FortisPool.Where(x => x.LastAnswerSendDate >= currentTime);
            output = JsonConvert.SerializeObject(fortisPool);

            return output;
        }

        [HttpGet("GetMasternodes")]
        public async Task<string> GetMasternodes()
        {
            string output = "";
            var validators = P2PAdjServer.FortisPool.ToList();

            output = JsonConvert.SerializeObject(validators);

            return output;
        }

        [HttpGet("GetValidatorPoolInfo")]
        public async Task<string> GetValidatorPoolInfo()
        {
            string output = "";
            var isConnected = P2PClient.IsAdjConnected1;
            DateTime? connectDate = P2PClient.AdjudicatorConnectDate != null ? P2PClient.AdjudicatorConnectDate.Value : null;

            var connectedInfo = new[]
            {
                new { ValidatorConnectedToPool = isConnected, PoolConnectDate = connectDate }
            };

            output = JsonConvert.SerializeObject(connectedInfo);

            return output;
        }

        [HttpGet("GetPeerInfo")]
        public async Task<string> GetPeerInfo()
        {
            string output = "";

            var nodeInfoList = Program.Nodes;

            output = JsonConvert.SerializeObject(nodeInfoList);

            return output;

        }

        [HttpGet("GetDebugInfo")]
        public async Task<string> GetDebugInfo()
        {
            var output = await StaticVariableUtility.GetStaticVars();

            return output;
        }

        [HttpGet("GetCLIVersion")]
        public async Task<string> GetCLIVersion()
        {
            string output = "";

            output = Program.CLIVersion;

            return output;
        }

        [HttpGet("ReadRBXLog")]
        public async Task<string> ReadRBXLog()
        {
            string output = "";

            output = await LogUtility.ReadLog();

            return output;
        }

        [HttpGet("ClearRBXLog")]
        public async Task<string> ClearRBXLog()
        {
            string output = "";

            await LogUtility.ClearLog();

            output = "Log Cleared";
            return output;
        }

        [HttpGet("ReadValLog")]
        public async Task<string> ReadValLog()
        {
            string output = "";

            output = await ValidatorLogUtility.ReadLog();

            return output;
        }

        [HttpGet("ClearValLog")]
        public async Task<string> ClearValLog()
        {
            string output = "";

            await ValidatorLogUtility.ClearLog();

            output = "Log Cleared";
            return output;
        }

        [HttpGet("SendExit")]
        public async Task SendExit()
        {
            //use Id to get specific commands
            var output = "Starting Stop"; // this will only display if command not recognized.
            LogUtility.Log("Send exit has been called. Closing Wallet.", "V1Controller.SendExit()");
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
