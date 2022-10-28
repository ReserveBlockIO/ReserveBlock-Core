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
using System.Net;
using System.Numerics;
using System.Security;

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

            if (!string.IsNullOrWhiteSpace(Globals.APIPassword))
            {
                if (password != null)
                {
                    var passCheck = Globals.APIPassword.ToDecrypt(password);
                    if (passCheck == password && passCheck != "Fail")
                    {
                        Globals.APIUnlockTime = DateTime.UtcNow.AddMinutes(Globals.WalletUnlockTime);
                        var successResult = new[]
                        {
                            new { Result = "Success", Message = $"Wallet has been unlocked for {Globals.WalletUnlockTime} mins."}
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

            if (!string.IsNullOrWhiteSpace(Globals.APIPassword))
            {
                if (password != null)
                {
                    var passCheck = Globals.APIPassword.ToDecrypt(password);
                    if (passCheck == password && passCheck != "Fail")
                    {
                        Globals.APIUnlockTime = DateTime.UtcNow;
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

        public static async Task<string> GetCheckEncryptionStatus()
        {
            string output = "";
            if (Globals.IsWalletEncrypted == true)
            {
                if (Globals.EncryptPassword.Length != 0)
                {
                    output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Wallet is has decryption password." });
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Wallet does not have decryption password." });
                }

                return output;
            }
            else
            {
                output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Wallet does not need decryption password." });
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

            Globals.HDWallet = true;

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

        //its recommended that you verify password before submitting to this endpoint.
        [HttpGet("GetEncryptWallet/{password}")]
        public async Task<string> GetEncryptWallet(string password)
        {
            var output = "";
            if (Globals.IsWalletEncrypted != true)
            {
                try
                {
                    Globals.EncryptPassword = password.ToSecureString();
                    await Keystore.GenerateKeystoreAddresses();
                    Globals.IsWalletEncrypted = true;

                    password = "0";
                    output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Wallet Encrypted." });
                }
                catch (Exception ex)
                {
                    output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"There was an error encrypting your wallet. Error: {ex.ToString()}" });
                }
            }
            else
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Wallet is already encrypted." });
            }
            
            return output;
        }

        [HttpGet("GetDecryptWallet/{password}")]
        public async Task<string> GetDecryptWallet(string password)
        {
            var output = "";

            Globals.EncryptPassword = password.ToSecureString();
            password = "0";

            output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Password has been stored for {Globals.PasswordClearTime} minutes." });

            return output;
        }


        [HttpGet("GetNewAddress")]
        public async Task<string> GetNewAddress()
        {
            //use Id to get specific commands
            Account account = null;
            var output = "Fail"; // this will only display if command not recognized.
            if (Globals.HDWallet == true)
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
            var blockHeight = Globals.LastBlock.Height.ToString();

            var peersConnected = await P2PClient.ArePeersConnected();
            if (peersConnected)
            {
                peerCount = Globals.Nodes.Count.ToString();
            }
            else
            {
                peerCount = "0";
            }


            var walletInfo = new[]
            {
                new { BlockHeight = blockHeight, PeerCount = peerCount, BlocksDownloading = Globals.BlocksDownloading.ToString(),
                    IsResyncing = Globals.IsResyncing.ToString(), IsChainSynced =  Globals.IsChainSynced.ToString(), ChainCorrupted = Globals.DatabaseCorruptionDetected.ToString()}
            };

            output = JsonConvert.SerializeObject(walletInfo);

            //output = blockHeight + ":" + peerCount + ":" + Globals.BlocksDownloading.ToString() + ":" + Globals.IsResyncing.ToString() + ":" + Globals.IsChainSynced.ToString();

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
                        var stateTreiBalance = AccountStateTrei.GetAccountBalance(account.Address);
                        if(stateTreiBalance < 1000)
                        {
                            output = "The balance for this account is under 1000.";
                        }
                        else
                        {
                            account.IsValidating = true;
                            accounts.UpdateSafe(account);
                            Globals.ValidatorAddress = account.Address;
                            await StartupService.ConnectoToAdjudicator();
                            output = "Success! The requested account has been turned on: " + account.Address;
                        }
                    }
                    else
                    {
                        output = "The requested account was not found in wallet. You may need to import it.";
                    }
                }
            }
            else
            {
                output = "No Validator account has been found. Please create one.";
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
                
                await ValidatorService.DoMasterNodeStop();
                
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
            if(Globals.IsWalletEncrypted == true)
            {
                if(Globals.EncryptPassword.Length > 0)
                {
                    var account = await AccountData.RestoreAccount(id);

                    if (account == null)
                    {
                        output = "NAC";
                    }
                    else if (account.Address == null || account.Address == "")
                    {
                        output = "NAC";
                    }
                    else
                    {
                        output = JsonConvert.SerializeObject(account);
                    }
                }
                else
                {
                    output = "Please type in wallet encryption password.";
                }
            }
            else
            {
                var account = await AccountData.RestoreAccount(id);

                if (account == null)
                {
                    output = "NAC";
                }
                else if (account.Address == null || account.Address == "")
                {
                    output = "NAC";
                }
                else
                {
                    output = JsonConvert.SerializeObject(account);
                }
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

            var block = Globals.LastBlock;

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

            if (Globals.IsWalletEncrypted == true)
            {
                if(Globals.EncryptPassword.Length > 0 )
                {
                    var result = await WalletService.SendTXOut(fromAddress, toAddress, amount);

                    output = result;
                }
                else
                {
                    output = "FAIL. Please type in wallet encryption password first.";
                }
            }
            else
            {
                var result = await WalletService.SendTXOut(fromAddress, toAddress, amount);
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
            if(Globals.IsWalletEncrypted == false)
            {
                var valAccount = AccountData.GetPossibleValidatorAccounts();
                if (valAccount.Count() > 0)
                {
                    var accountCheck = valAccount.Where(x => x.Address == address).FirstOrDefault();
                    if (accountCheck != null)
                    {
                        if (accountCheck.IsValidating)
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
                            ErrorLogUtility.LogError(ex.ToString(), "V1Controller.StartValidating - result: " + result);
                            result = $"Unknown Error Occured: {ex.ToString()}";
                        }
                        output = true;
                    }
                    else
                    {
                        result = "Account provided was not found in wallet.";
                    }
                }
            }
            else
            {
                result = "Cannot start validating on an encrypted wallet.";
            }

            return result;
        }

        [HttpGet("ChangeValidatorName/{uname}")]
        public async Task<string> ChangeValidatorName(string uname)
        {
            string output = "";
            
            if(!string.IsNullOrWhiteSpace(Globals.ValidatorAddress))
            {
                var validatorTable = Validators.Validator.GetAll();
                var validator = validatorTable.FindOne(x => x.Address == Globals.ValidatorAddress);
                validator.UniqueName = uname;
                validatorTable.UpdateSafe(validator);

                output = "Validator Unique Name Updated. Please restart wallet.";
            }
            return output;
        }

        [HttpGet("ResetValidator")]
        public async Task<string> ResetValidator()
        {
            string output = "Failed!";

            var result = await ValidatorService.ValidatorErrorReset();
            if (result)
                output = "Success!";
            
            return output;
        }

        [HttpGet("CreateSignature/{message}/{address}")]
        public async Task<string> CreateSignature(string message, string address)
        {
            string output;

            var account = AccountData.GetSingleAccount(address);
            if(account != null)
            {
                var accPrivateKey = GetPrivateKeyUtility.GetPrivateKey(account.PrivateKey, account.Address);

                BigInteger b1 = BigInteger.Parse(accPrivateKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
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
            var blocks = Globals.MemBlocks;
            output = JsonConvert.SerializeObject(blocks);

            return output;
        }

        [HttpGet("GetTaskAnswersList")]
        public async Task<string> GetTaskAnswersList()
        {
            string output = "";
            var taskAnswerList = Globals.TaskAnswerDict.Values.Select(x => new {
                Address = x.Address,
                Answer = x.Answer,
                BlockHeight = x.Block != null ? x.Block.Height : 0,
                SubmitTime = x.SubmitTime
                
            });
            output = JsonConvert.SerializeObject(taskAnswerList);

            return output;
        }

        [HttpGet("GetTaskAnswersListNew")]
        public async Task<string> GetTaskAnswersListNew()
        {
            string output = "";
            var taskAnswerList = Globals.TaskAnswerDict_New.Values.Select(x => new {
                Address = x.Address,
                Answer = x.Answer,
                NextBlockHeight = x.NextBlockHeight,
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
            var fortisPool = Globals.FortisPool.Values.Where(x => x.LastAnswerSendDate != null);
            output = JsonConvert.SerializeObject(fortisPool);

            return output;
        }

        [HttpGet("GetMasternodes")]
        public async Task<string> GetMasternodes()
        {
            string output = "";
            var validators = Globals.FortisPool.Values.Select(x => new
            {
                x.Context.ConnectionId,
                x.ConnectDate,
                x.LastAnswerSendDate,
                x.IpAddress,
                x.Address,
                x.UniqueName,
                x.WalletVersion
            }).ToList();

            output = JsonConvert.SerializeObject(validators);

            return output;
        }

        [HttpGet("GetBeaconPool")]
        public async Task<string> GetBeaconPool()
        {
            string output = "";
            var beaconPool = Globals.BeaconPool.Values.ToList();

            output = JsonConvert.SerializeObject(beaconPool);

            return output;
        }

        [HttpGet("GetValidatorPoolInfo")]
        public async Task<string> GetValidatorPoolInfo()
        {
            string output = "";
            var isConnected = P2PClient.IsAdjConnected1;
            DateTime? connectDate = Globals.AdjudicatorConnectDate != null ? Globals.AdjudicatorConnectDate.Value : null;

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

            var nodeInfoList = Globals.Nodes.Select(x => new
            {
                x.Value.NodeIP,
                x.Value.NodeLatency,
                x.Value.NodeHeight,
                x.Value.NodeLastChecked
            })
            .ToArray();

            output = JsonConvert.SerializeObject(nodeInfoList);

            return output;

        }

        [HttpGet("ListBannedPeers")]
        public async Task<string> ListBannedPeers()
        {
            var output = "";
            var bannedPeers = Peers.ListBannedPeers();

            if(bannedPeers.Count() > 0)
            {
                output = JsonConvert.SerializeObject(bannedPeers);
            }
            else
            {
                output = "No banned peers";
            }

            return output;
        }

        [HttpGet("UnbanPeer/{**ipAddress}")]
        public async Task<string> UnbanPeer(string ipAddress)
        {
            var result = await Peers.UnbanPeer(ipAddress);

            return result;
        }

        [HttpGet("UnbanAllPeers")]
        public async Task<string> UnbanAllPeers()
        {
            var result = await Peers.UnbanAllPeers();

            return result.ToString();
        }

        [HttpGet("GetDebugInfo")]
        public async Task<string> GetDebugInfo()
        {
            var output = await StaticVariableUtility.GetStaticVars();

            return output;
        }

        [HttpGet("GetConnectionHistory")]
        public async Task<string> GetConnectionHistory()
        {
            var output = await ConnectionHistory.Read();

            return output;
        }

        [HttpGet("GetClientInfo")]
        public async Task<string> GetClientInfo()
        {
            var output = await StaticVariableUtility.GetClientInfo();

            return output;
        }

        [HttpGet("GetCLIVersion")]
        public async Task<string> GetCLIVersion()
        {
            string output = "";

            output = Globals.CLIVersion;

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
            Globals.StopAllTimers = true;
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
