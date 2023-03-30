using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ReserveBlockCore.BIP39;
using ReserveBlockCore.Config;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Extensions;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System.Globalization;
using System.Net;
using System.Numerics;
using System.Security;
using System.Security.Principal;
using System.Text;

namespace ReserveBlockCore.Controllers
{
    [ActionFilterController]
    [Route("api/[controller]")]
    [Route("api/[controller]/{somePassword?}")]
    [ApiController]
    public class V1Controller : ControllerBase
    {
        /// <summary>
        /// Check Status of API
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "RBX-Wallet", "API" };
        }

        /// <summary>
        /// Another check method for API
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// Unlock the API using Password
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Lock the API using Password
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Returns the state of a wallets encryption and if password is present
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetCheckEncryptionStatus")]
        public static async Task<string> GetCheckEncryptionStatus()
        {
            string output = "";
            if (Globals.IsWalletEncrypted == true)
            {
                if (Globals.EncryptPassword.Length != 0)
                {
                    output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Wallet has decryption password." });
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

        /// <summary>
        /// Check for GUI launchers to request password on startup if needed
        /// </summary>
        /// <returns></returns>
        [HttpGet("CheckPasswordNeeded")]
        public async Task<string> CheckPasswordNeeded()
        {
            var output = "false";

            if (Globals.GUIPasswordNeeded)
                output = "true";

            return output;
        }

        /// <summary>
        /// Returns the state of a wallets encryption
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetIsWalletEncrypted")]
        public async Task<string> GetIsWalletEncrypted()
        {
            var output = "false";

            if (Globals.IsWalletEncrypted)
                output = "true";

            return output;
        }

        /// <summary>
        /// Returns the state of a wallets encryption password existence. If 0 it is not entered.
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetIsEncryptedPasswordStored")]
        public async Task<string> GetIsEncryptedPasswordStored()
        {
            var output = "false";

            if (Globals.EncryptPassword.Length > 0)
                output = "true";

            return output;
        }

        /// <summary>
        /// Enter a password that will store in the Globals.EncryptPassword in order for wallet to function
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        [HttpGet("GetEncryptedPassword/{**password}")]
        public async Task<string> GetEncryptedPassword(string password)
        {
            var output = "False"; // this will only display if command not recognized.
            //use Id to get specific commands
            try
            {

                if (!string.IsNullOrEmpty(password))
                {
                    Globals.EncryptPassword = password.ToSecureString();
                    var account = AccountData.GetSingleAccount(Globals.ValidatorAddress);
                    BigInteger b1 = BigInteger.Parse(account.GetKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
                    PrivateKey privateKey = new PrivateKey("secp256k1", b1);

                    var randString = RandomStringUtility.GetRandomString(8);

                    var signature = SignatureService.CreateSignature(randString, privateKey, account.PublicKey);
                    var sigVerify = SignatureService.VerifySignature(account.Address, randString, signature);

                    if (sigVerify)
                    {
                        password = "";
                        Globals.GUIPasswordNeeded = false;
                        output = JsonConvert.SerializeObject(new { Result = "Success", Message = "" });
                    }
                    else
                    {
                        password = "";
                        Globals.EncryptPassword.Dispose();
                        Globals.EncryptPassword = new SecureString();
                        output = JsonConvert.SerializeObject(new { Result = "Fail", Message = "Password was incorrect. Please attempt again" });
                    }
                }
            }
            catch(Exception ex)
            {

            }

            return output;
        }

        /// <summary>
        /// Simple Status Check
        /// </summary>
        /// <returns></returns>
        [HttpGet("CheckStatus")]
        public async Task<string> CheckStatus()
        {
            //use Id to get specific commands
            var output = "Online"; // this will only display if command not recognized.


            return output;
        }

        /// <summary>
        /// Converts wallet to an HD Wallet. Must choose strength of 12 or 24.
        /// </summary>
        /// <param name="strength"></param>
        /// <returns></returns>
        [HttpGet("GetHDWallet/{strength}")]
        public async Task<string> GetHDWallet(int strength)
        {
            var output = "";
            try
            {
                var mnemonic = HDWallet.HDWalletData.CreateHDWallet(strength, BIP39Wordlist.English);

                Globals.HDWallet = mnemonic.Item1;

                output = JsonConvert.SerializeObject(new { Result = mnemonic.Item1, Message = mnemonic.Item2});
            }
            catch(Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Result = false, Message = $"Error: {ex.ToString()}" });
            }

            return output;
        }

        /// <summary>
        /// Restores and HD Wallet
        /// </summary>
        /// <param name="mnemonic"></param>
        /// <returns></returns>
        [HttpGet("GetRestoreHDWallet/{mnemonic}")]
        public async Task<string> GetRestoreHDWallet(string mnemonic)
        {
            var output = "";
            try
            {
                var mnemonicRestore = HDWallet.HDWalletData.RestoreHDWallet(mnemonic);

                output = JsonConvert.SerializeObject(new { Result = mnemonicRestore });
            }
            catch(Exception ex)
            {
                output = $"ERROR! Message: {ex.ToString()}";
            }

            return output;
        }

        /// <summary>
        /// This is a permanent process of encrypting a wallet with a user supplied password.
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        [HttpGet("GetEncryptWallet/{password}")]
        public async Task<string> GetEncryptWallet(string password)
        {
            var output = "";
            if(Globals.HDWallet == true)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"HD wallet cannot be encrypted at this time." });
            }
            else if (Globals.IsWalletEncrypted != true)
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

        /// <summary>
        /// Erases encryption password from all memory and release from RAM as well.
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetEncryptLock")]
        public async Task<string> GetEncryptLock()
        {
            var output = "Fail";

            if (string.IsNullOrEmpty(Globals.ValidatorAddress) && Globals.AdjudicateAccount == null)
            {
                Globals.EncryptPassword.Dispose();
                Globals.EncryptPassword = new SecureString();
                output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Wallet is locked." });
                return output;
            }

            output= JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Failed to lock wallet." });

            return output;
        }

        /// <summary>
        /// Decrypts wallet encryption and allows functions to work.
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        [HttpGet("GetDecryptWallet/{**password}")]
        public async Task<string> GetDecryptWallet(string password)
        {
            var output = "";

            try
            {
                if (!string.IsNullOrEmpty(password))
                {
                    Globals.EncryptPassword = password.ToSecureString();
                    var accounts = AccountData.GetAccounts();
                    if (accounts != null)
                    {
                        var account = accounts.Query().Where(x => x.Address != null).FirstOrDefault();
                        
                        if (account == null)
                            return JsonConvert.SerializeObject(new { Result = "Fail", Message = "No accounts in wallet." });
                        await Task.Delay(200);
                        var privKey = account.GetKey;
                        BigInteger b1 = BigInteger.Parse(privKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
                        PrivateKey privateKey = new PrivateKey("secp256k1", b1);

                        var randString = RandomStringUtility.GetRandomString(8);

                        var signature = SignatureService.CreateSignature(randString, privateKey, account.PublicKey);
                        var sigVerify = SignatureService.VerifySignature(account.Address, randString, signature);

                        if (sigVerify)
                        {
                            Globals.GUIPasswordNeeded = false;
                            password = "";
                            output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Password has been stored for {Globals.PasswordClearTime} minutes." });
                        }
                        else
                        {
                            password = "";
                            Globals.EncryptPassword.Dispose();
                            Globals.EncryptPassword = new SecureString();
                            output = JsonConvert.SerializeObject(new { Result = "Fail", Message = "Password was incorrect. Please attempt again" });
                        }
                    }
                }

                password = "0";
            }
            catch(Exception ex)
            {
                password = "";
                Globals.EncryptPassword.Dispose();
                Globals.EncryptPassword = new SecureString();
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Unknown Error. Error: {ex.ToString()}" });
            }

            return output;
        }

        /// <summary>
        /// Produces a new address
        /// </summary>
        /// <returns></returns>
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
                new { Address = account.Address, PrivateKey = account.GetKey}
            };

            LogUtility.Log("New Address Created: " + account.Address, "V1Controller.GetNewAddress()");

            output = JsonConvert.SerializeObject(newAddressInfo);
            //output = account.Address + ":" + account.PrivateKey;

            return output;
        }

        /// <summary>
        /// Spits out wallet info.
        /// </summary>
        /// <returns></returns>
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
                new { BlockHeight = blockHeight, PeerCount = peerCount, BlocksDownloading = (Globals.BlocksDownloadSlim.CurrentCount == 0).ToString(),
                    IsResyncing = Globals.IsResyncing.ToString(), IsChainSynced =  Globals.IsChainSynced.ToString(), 
                    ChainCorrupted = Globals.DatabaseCorruptionDetected.ToString(), DuplicateValIP = Globals.DuplicateAdjIP, 
                    DuplicateValAddress = Globals.DuplicateAdjAddr, NFTFilesReadyEPN = Globals.NFTFilesReadyEPN, 
                    ConnectedToMother = Globals.ConnectToMother.ToString(), UpToDate = Globals.UpToDate, BlockVersion = BlockVersionUtility.GetBlockVersion(Globals.LastBlock.Height),
                    TimeInSync = Globals.TimeInSync.ToString(), TimeSyncError = Globals.TimeSyncError }
            };

            output = JsonConvert.SerializeObject(walletInfo);

            //output = blockHeight + ":" + peerCount + ":" + Globals.BlocksDownloading.ToString() + ":" + Globals.IsResyncing.ToString() + ":" + Globals.IsChainSynced.ToString();

            return output;
        }

        /// <summary>
        /// Gets the latest CLI release and downloads to default RBX location and installs if requested
        /// </summary>
        /// <param name="runUpdate"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        [HttpGet("GetLatestRelease/{runUpdate}/{**fileName}")]
        public async Task<string> GetLatestRelease(bool runUpdate, string fileName)
        {
            var output = JsonConvert.SerializeObject(new { Result = "Fail", Message = "Update was not successful." });
            if (!Globals.UpToDate)
            {
                var result = await VersionControlService.DownloadLatestAndUpdate(fileName, runUpdate);

                if (result)
                    output = JsonConvert.SerializeObject(new { Result = "Success", Message = "Update was successful. Please Restart CLI." });
            }
            else
            {
                output = JsonConvert.SerializeObject(new { Result = "Success", Message = "CLI is up to date." });
            }
            
            return output;
        }

        /// <summary>
        /// Gets the latest CLI release and downloads to default RBX location and installs if requested
        /// </summary>
        /// <returns>
        /// Returns a list of strings
        /// </returns>
        [HttpGet("GetLatestReleaseFiles")]
        public async Task<string> GetLatestReleaseFiles()
        {
            var output = JsonConvert.SerializeObject(new { Result = "Fail", Message = "Unknown Issue." });

            var result = await VersionControlService.GetLatestDownloadFiles();

            if (result?.Count() > 0)
            {
                output = JsonConvert.SerializeObject(new { Result = "Success", Message = JsonConvert.SerializeObject(result) });
            }
            else
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = "No Files Found." });
            }

            return output;
        }

        /// <summary>
        /// Dumps out all addresses locally stored.
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetAllAddresses")]
        public async Task<string> GetAllAddresses()
        {
            var output = "Command not recognized."; // this will only display if command not recognized.
            var accounts = AccountData.GetAccounts();

            if (accounts.Count() == 0)
            {
                output = "No Accounts";
            }
            else
            {
                var accountList = accounts.Query().Where(x => true).ToEnumerable();
                output = JsonConvert.SerializeObject(accountList);
            }

            return output;
        }

        /// <summary>
        /// Dumps out network metrics
        /// </summary>
        /// <returns></returns>
        [HttpGet("NetworkMetrics")]
        public async Task<string> NetworkMetrics()
        {
            var output = "[]"; // this will only display if command not recognized.
            var currentTime = TimeUtil.GetTime();
            var currentDiff = (currentTime - Globals.LastBlockAddedTimestamp).ToString();

            output = JsonConvert.SerializeObject(new { BlockDiffAvg = BlockDiffService.CalculateAverage().ToString("#.##"), BlockLastReceived = Globals.LastBlockAddedTimestamp.ToLocalDateTimeFromUnix(),
            BlockLastDelay = Globals.BlockTimeDiff.ToString(), TimeSinceLastBlockSeconds = currentDiff, BlocksAveraged = $"{Globals.BlockDiffQueue.Count().ToString()}/3456"});

            return output;
        }

        /// <summary>
        /// Dumps out a validator owned address.
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetValidatorAddresses")]
        public async Task<string> GetValidatorAddresses()
        {
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

        /// <summary>
        /// Tells you if you are for sure validating by checking send/receive responses
        /// </summary>
        /// <returns></returns>
        [HttpGet("IsValidating")]
        public async Task<string> IsValidating()
        {
            var output = "false";

            if(!string.IsNullOrEmpty(Globals.ValidatorAddress))
            {
                if (Globals.ValidatorReceiving && Globals.ValidatorSending)
                {
                    output = "true";
                }
            }

            return output;
        }

        /// <summary>
        /// Turns stored validator information on.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
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
                        if(stateTreiBalance < Globals.ValidatorRequiredRBX)
                        {
                            output = $"The balance for this account is under {Globals.ValidatorRequiredRBX}.";
                        }
                        else
                        {
                            account.IsValidating = true;
                            accounts.UpdateSafe(account);
                            Globals.ValidatorAddress = account.Address;                            
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

        /// <summary>
        /// Turns validator off
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Dumps out validator information. In this case the Unique name to an address
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Returns the wallet info for a specific local address
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Returns the balance for a specific network address **For exchanges**
        /// </summary>
        /// <param name="rbxAddress"></param>
        /// <returns></returns>
        [HttpGet("GetChainBalance/{rbxAddress}")]
        public async Task<string> GetChainBalance(string rbxAddress)
        {
            //use Id to get specific commands
            var output = "Command not recognized."; // this will only display if command not recognized.
            var balance = AccountStateTrei.GetAccountBalance(rbxAddress);

            output = JsonConvert.SerializeObject(new { Account = rbxAddress, Balance = balance });

            return output;
        }

        /// <summary>
        /// Imports a private key.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="scan"></param>
        /// <returns></returns>
        [HttpGet("ImportPrivateKey/{id}/{scan?}")]
        public async Task<string> ImportPrivateKey(string id, bool scan = false)
        {
            //use Id to get specific commands
            var output = "Command not recognized."; // this will only display if command not recognized.
            if(Globals.IsWalletEncrypted == true)
            {
                if(Globals.EncryptPassword.Length > 0)
                {
                    var account = await AccountData.RestoreAccount(id, scan);

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
                var account = await AccountData.RestoreAccount(id, scan);

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

        /// <summary>
        /// Rescan for TXs
        /// </summary>
        /// <param name="walletAddr"></param>
        /// <returns></returns>
        [HttpGet("RescanForTx/{walletAddr}")]
        public async Task<string> RescanForTx(string walletAddr)
        {
            var output = "";
            var account = AccountData.GetSingleAccount(walletAddr);
            if(account != null)
            {
                _ = Task.Run(() => BlockchainRescanUtility.RescanForTransactions(account.Address));
                output = JsonConvert.SerializeObject(new { Success = true, Message = $"Rescan has started." });
            }
            else
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = $"Account was not found locally." });
            }

            return output;
        }

        /// <summary>
        /// Syncs account balances
        /// </summary>
        /// <returns></returns>
        [HttpGet("SyncBalances")]
        public async Task<string> SyncBalances()
        {
            var output = "";

            var accountsDb = AccountData.GetAccounts();
            var accounts = accountsDb.Query().Where(x => true).ToEnumerable();

            if (accounts.Count() > 0)
            {
                foreach(var account in accounts)
                {
                    var stateTrei = StateData.GetSpecificAccountStateTrei(account.Address);
                    if(stateTrei != null)
                    {
                        account.Balance = stateTrei.Balance;
                        accountsDb.UpdateSafe(account);
                    }
                }
                output = JsonConvert.SerializeObject(new { Success = true, Message = $"Balance resync completed" });
            }
            else
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = $"No Accounts were found locally." });
            }

            return output;
        }

        /// <summary>
        /// Sends back block information on a requested block
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Returns last block stored in wallet.
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// This is deprecated. Do not use.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
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

        /// <summary>
        /// **Deprecated use TX API - Returns all transactions
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
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

        /// <summary>
        /// **Deprecated. Use TX API For transactions
        /// </summary>
        /// <param name="faddr"></param>
        /// <param name="taddr"></param>
        /// <param name="amt"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Start validating with provided address and unique name
        /// </summary>
        /// <param name="addr"></param>
        /// <param name="uname"></param>
        /// <returns></returns>
        [HttpGet("StartValidating/{addr}/{uname}")]
        public async Task<string> StartValidating(string addr, string uname)
        {
            var output = false;
            var result = "No Potential Validator Accounts Found.";
            var address = addr;
            var uniqueName = uname;

            if(Globals.IsWalletEncrypted == true)
            {
                if (Globals.EncryptPassword.Length == 0)
                    return "Please type in your encrypted password before starting validating.";
            }

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
            else
            {
                result = "No eligible accounts were found that can validate.";
            }

            return result;
        }

        /// <summary>
        /// Change your validator name. Restart is required.
        /// </summary>
        /// <param name="uname"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Validator reset.
        /// </summary>
        /// <returns></returns>
        [HttpGet("ResetValidator")]
        public async Task<string> ResetValidator()
        {
            string output = "Failed!";

            var result = await ValidatorService.ValidatorErrorReset();
            if (result)
                output = "Success!";
            
            return output;
        }

        /// <summary>
        /// Validates an RBX Address or ADNR
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        [HttpGet("ValidateAddress/{**address}")]
        public async Task<string> ValidateAddress(string address)
        {
            string output = "";

            var result = AddressValidateUtility.ValidateAddress(address);

            output = result.ToString();

            return output;
        }

        /// <summary>
        /// Create a signature with provided message and address
        /// </summary>
        /// <param name="message"></param>
        /// <param name="address"></param>
        /// <returns></returns>
        [HttpGet("CreateSignature/{message}/{address}")]
        public async Task<string> CreateSignature(string message, string address)
        {
            string output;

            var account = AccountData.GetSingleAccount(address);
            if(account != null)
            {
                BigInteger b1 = BigInteger.Parse(account.GetKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
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

        /// <summary>
        /// Validate a signature with provided message and address and sigscript
        /// </summary>
        /// <param name="message"></param>
        /// <param name="address"></param>
        /// <param name="sigScript"></param>
        /// <returns></returns>
        [HttpGet("ValidateSignature/{message}/{address}/{**sigScript}")]
        public async Task<bool> ValidateSignature(string message, string address, string sigScript)
        {
            bool output;

            var result = SignatureService.VerifySignature(address, message, sigScript);
            output = result;

            return output;
        }

        /// <summary>
        /// Returns the mempool
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetMempool")]
        public async Task<string> GetMempool()
        {
            string output = "";
            var txs = TransactionData.GetMempool();
            output = JsonConvert.SerializeObject(txs);

            return output;
        }

        /// <summary>
        /// Dumps out the entire memblock cluster. This can be very large.
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetMemBlockCluster")]
        public async Task<string> GetMemBlockCluster()
        {
            string output = "";
            var blocks = Globals.MemBlocks;
            output = JsonConvert.SerializeObject(blocks);

            return output;
        }

        /// <summary>
        /// Finds a specific block by height.
        /// </summary>
        /// <param name="height"></param>
        /// <returns></returns>
        [HttpGet("GetBlockByHeight")]
        public async Task<string> GetBlockByHeight(long height)
        {
            string output = "";
            var block = BlockchainData.GetBlockByHeight(height);
            if(block != null)
                output = JsonConvert.SerializeObject(block);

            return output;
        }

        /// <summary>
        /// Finds a specific block by hash.
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        [HttpGet("GetBlockByHash")]
        public async Task<string> GetBlockByHash(string hash)
        {
            string output = "";
            var block = BlockchainData.GetBlockByHash(hash);
            if (block != null)
                output = JsonConvert.SerializeObject(block);

            return output;
        }

        /// <summary>
        /// Returns a task answer list
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetTaskAnswersList")]
        public async Task<string> GetTaskAnswersList()
        {
            string output = "";

            var taskAnswerList = Globals.TaskAnswerDictV3.Values.Select(x => new {
                Address = x.RBXAddress,
                Answer = x.Answer,
                IP = x.IPAddress,                    
            });
            output = JsonConvert.SerializeObject(taskAnswerList);            

            return output;
        }

        /// <summary>
        /// Returns master node list that is sent
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetMasternodesSent")]
        public async Task<string> GetMasternodesSent()
        {
            string output = "";
            var fortisPool = Globals.FortisPool.Values.Where(x => x.LastAnswerSendDate != null);
            output = JsonConvert.SerializeObject(fortisPool);

            return output;
        }

        /// <summary>
        /// Returns entire fortis pool (Masternode List)
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// Returns beacon pool
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetBeaconPool")]
        public async Task<string> GetBeaconPool()
        {
            string output = "";
            var beaconPool = Globals.BeaconPool.Values.ToList();

            output = JsonConvert.SerializeObject(beaconPool);

            return output;
        }

        /// <summary>
        /// Returns adj node information
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetValidatorPoolInfo")]
        public async Task<string> GetValidatorPoolInfo()
        {
            string output = "";
            var isConnected = Globals.AdjNodes.Values.Any(x => x.IsConnected);
            DateTime? connectDate = Globals.AdjNodes.Values.Select(x => x.AdjudicatorConnectDate).FirstOrDefault();

            var connectedInfo = new[]
            {
                new { ValidatorConnectedToPool = isConnected, PoolConnectDate = connectDate }
            };

            output = JsonConvert.SerializeObject(connectedInfo);

            return output;
        }

        /// <summary>
        /// Starts the mother process
        /// </summary>
        /// <returns></returns>
        [HttpPost("StartMother")]
        public async Task<string> StartMother([FromBody] object jsonData)
        {
            var output = "{}";

            try
            {
                var momPayload = JsonConvert.DeserializeObject<Mother.MotherStartPayload>(jsonData.ToString());

                if (momPayload != null)
                {
                    Mother mom = new Mother
                    {
                        Name = momPayload.Name,
                        Password = momPayload.Password
                    };

                    var result = Models.Mother.SaveMother(mom);

                    if (result.Item1 == true)
                    {
                        output = JsonConvert.SerializeObject(new { Result = "Success", Message = result.Item2 });
                    }
                    else
                    {
                        output = JsonConvert.SerializeObject(new { Result = "Fail", Message = result.Item2 });
                    }
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Result = "Fail", Message = "Mother Payload did not deserialized." });
                }
            }
            catch(Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Unknown Error. Error: {ex.ToString()}" });
            }

            
            
            return output;
        }

        /// <summary>
        /// Stops mother.
        /// </summary>
        /// <returns></returns>
        [HttpGet("StopMother")]
        public async Task<string> StopMother()
        {
            var output = "{}";

            try
            {
                var mom = Models.Mother.GetMother();

                if (mom != null)
                {
                    var result = Models.Mother.DeleteMother(mom);
                    if (result.Item1 == true)
                    {
                        output = JsonConvert.SerializeObject(new { Result = "Success", Message = result.Item2 });
                    }
                    else
                    {
                        output = JsonConvert.SerializeObject(new { Result = "Fail", Message = result.Item2 });
                    }
                }
            }
            catch(Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Unknown Error. Error: {ex.ToString()}" });
            }
            
            return output;
        }

        /// <summary>
        /// Returns json of mother or null
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetMother")]
        public async Task<string> GetMother()
        {
            var output = "{}";

            var mom = Models.Mother.GetMother();

            if (mom != null)
            {
                output = JsonConvert.SerializeObject(mom);
            }

            return output;
        }

        /// <summary>
        /// Joins the mothers house
        /// </summary>
        /// <returns></returns>
        [HttpPost("JoinMother")]
        public async Task<string> JoinMother([FromBody] object jsonData)
        {
            var output = "{}";

            try
            {
                var momJoinPayload = JsonConvert.DeserializeObject<Mother.MotherJoinPayload>(jsonData.ToString());
                Globals.MotherAddress = momJoinPayload.IPAddress;
                Globals.MotherPassword = momJoinPayload.Password.ToSecureString();
                Globals.ConnectToMother = true;

                var path = GetPathUtility.GetConfigPath();
                var fileExist = System.IO.File.Exists(path + "config.txt");

                if (fileExist)
                {
                    System.IO.File.AppendAllText(path + "config.txt", Environment.NewLine + $"MotherAddress={Globals.MotherAddress}");
                    System.IO.File.AppendAllText(path + "config.txt", Environment.NewLine + $"MotherPassword={Globals.MotherPassword.ToUnsecureString()}");
                }

                output = JsonConvert.SerializeObject(new { Result = "Success", Message = "Mother setup has been completed." });
            }
            catch(Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Unknown Error! Error: {ex.ToString()}" });
            }
           
            return output;
        }

        /// <summary>
        /// Returns mothers URL
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetMotherURL")]
        public async Task<string> GetMotherURL()
        {
            var output = "";

            var url = $"http://localhost:{Globals.APIPort}/api/v1/mother";

            if(Globals.TestURL)
                url = $"https://localhost:7777/api/v1/mother";

            output = url;

            return output;
        }

        /// <summary>
        /// Returns mothers kids
        /// </summary>
        /// <returns></returns>
        [HttpGet("MothersKids")]
        public async Task<string> MothersKids()
        {
            var output = "{}";

            var result = Globals.MothersKids.Values.ToList();

            if(result.Count > 0)
            {
                output = JsonConvert.SerializeObject(result);
            }

            return output;
        }

        /// <summary>
        /// Returns mother
        /// </summary>
        /// <returns></returns>
        [HttpGet("Mother")]
        public async Task<ContentResult> Mother()
        {
            var output = "{}";

            var mCompressed1 = @"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";
            var mCompressed2 = @"H4sIAAAAAAAACu1Y227TQBCdZyT+YekzJqJcHlpTRKFSK0GLEELiCeXahtiJZbslbpR/55zZuHZiN7GTpupDtMp6d3Z35uzs2cvEiClNrrwQB8nIV7lA+o7SqZzID/woP5Ln8kzuH92Rvtyg1BZPmhIhfZA91EaoO/Ja9rXN1iLxkb/PSXyMd+RtTuLJJfI30LHc8moETQnRZmCjVUtjVc2OXEkX5Q7ysLb2NE3kF3R4sNTBN4YXQjlHyYfWaU28jRni+kgyFriY1bslc45hIUZbV2dcxG7u0BMPdR1BtppJy1G1lBW0MkSJ/jbAZVAbycu79kiugbSNduK2PRKtrYPAhTav4AnONsJcHVgMYf1aAvQJUN9fmwOuaq1iyVHvd+Ffo6vR05UYo/YX7ezdhyxBC/fTUPsyd+ChLr7/kFNiZut2iXKmM9JxdsQmnE7TsVrhmlHrJppcYAtU06KXWroDyYmUBQGQ9zEbMjaB1PpuqPuUPgvQ6iFxdpMFjFNlbWpr3dVsqG93XMh75Uxvlk9YgQ5WxO7Qp8kIIp3HuWPF9k4ID2vRlgHKp9DIE+kKup8mMzK0eaw7dmyLHWd6c8+/Mfr6Avj4CAzhSRCVWj/Gb1rhHCkf/VtPlXOM3TFnW8z5rK+fIXS1VSPjAL5PjXxDHuvLPXxEFhXx/IQkj6U6p1br2ibD1tNQHkkF+jJlhMh5jLX2kCxwYcvAJ7zHe2qR53UMu5EcYFYNtLRg95V6k69jRjADyG8KcU0E6aTwLthT7Lw9uNqx2vgDnfZFOdD2RW7Eyhj7tTFxVpu/jzj/C8jsi52nh0FUPoaE9xBRhLo6zQ1WNh8x1o02GR89dEzdw8zIZBvjZbzwYS3byfexhXuS3uOOMRoPJRoh0QY121goAoZbSA/Qh/9QBNB2iHLWo6cxLHdeor18bRnNdhKtHs52ZPlLMcVARPWj8qpjqvdL4+gTnApf8M3+67Fx8X/CWr9vHhIAAA==";
            var mCompressed3 = @"H4sIAAAAAAAACo1UQU7DMBCcMxJ/yK1BAkO5UnpA4tArPyiOaSLaFDVJEUL8nVk7BtnUwVp5E+16Z2fHTha4RoUGRyxxjjMUoy2ieJjroHFg9g19VOftndmWCHu+KfoWW/o1IwXuuV4wMKZZ39hsiQtGP09ieTuy/sBnh5o1AxErPMFEyD33DIzeTWJp27XjbsETtj1jNR6JZrDjam1OEGUKTUwfVdjwrY/2PuADK+4tMSMDwwnFC9eroMOMkzpuU/zSnJyaEl/zKUxSihoq5mr+1zbWZW97KqvtJsBSXHIOTgNlmRj6V/qKnXK6TJ9gbq/pE/7K0FhQV7aTYRe5X1vOeupu5qnXsLZMTpd/CsVo4VckOJpInpeyN8yjl5j/3CxfnzKnTTp7ST/HLW5GS+GGOPLP+PtnkOgzOVb8Pn4jNfM7cl/iG/BcyLqCBAAA";

            var mDecompressed1 = mCompressed1.ToDecompress();
            var mDecompressed2 = mCompressed2.ToDecompress();
            var mDecompressed3 = mCompressed3.ToDecompress();

            StringBuilder mother = new StringBuilder();

            mother.AppendLine(mDecompressed1);

            var kids = Globals.MothersKids.Values.ToList();

            if (kids.Count > 0)
            {
                foreach (var kid in kids)
                {
                    var newKidCard = mDecompressed2;
                    newKidCard = newKidCard.Replace("{ValidatorName}", kid.ValidatorName);
                    newKidCard = newKidCard.Replace("{Balance}", $"{kid.Balance} RBX");
                    newKidCard = newKidCard.Replace("{IPAddress}", kid.IPAddress);
                    newKidCard = newKidCard.Replace("{BlockHeight}", kid.BlockHeight.ToString());
                    newKidCard = newKidCard.Replace("{IsValidatingYesNo}", kid.ActiveWithValidating ? "Yes" : "No");
                    newKidCard = newKidCard.Replace("{IsConnectedToMotherYesNo}", kid.ActiveWithMother ? "Yes" : "No");
                    newKidCard = newKidCard.Replace("{IsValidatingBg}", kid.ActiveWithValidating ? "bg-success" : "bg-danger");
                    newKidCard = newKidCard.Replace("{IsConnectedToMotherBg}", kid.ActiveWithMother ? "bg-success" : "bg-danger");
                    newKidCard = newKidCard.Replace("{Address}", kid.Address);

                    mother.AppendLine(newKidCard);
                }
                
            }

            mother.AppendLine(mDecompressed3);

            output = mother.ToString();

            return base.Content(output, "text/html");
        }

        /// <summary>
        /// First to the Egg!
        /// Congrats you have found an easter egg!
        /// </summary>
        /// <returns></returns>
        [HttpGet("Egg")]
        public async Task<ContentResult> Egg()
        {
            var gCompressed = @"H4sIAAAAAAAACrVZW28bRRSeZyT+gwkCOyRxYhoKtE4kKEEggUCFF1T1IfElMTi2u+s2XlX573zfOTs7l91xtoqrle2dmXO/zZnx
                                0HxmfjJ/mBfmb/OP+dNcmI65MWtza+bm3HxqPjHDhvHEXJpxOe7gGQJihs8cK+cYHQdjB5VjrohmLfVDvF+ZJegWeHtfrSvMBPSu
                                BfYZxgNzgucL8zyAuoVUGaBmZiFQJ976vbzZUYrPFSiMzH+gkQHiLeiMhdIV5NOVkOMYvHKzktVCIKei3SaCu8Qs5V+YI7HLBLLm
                                Aj/C+0JmsgjnX/CnvWagWQBvBIkUkt9pXNWUbyPwXZh3+M4bNF0Cayy4atEVpO4AcimyjvF+B3urtE3U6eXYn5wLo2NY2VrHOhdK
                                dif81sA8M3vmVHy7F3k9XCE8Z65B4RZQe2XU+XR9fjlWMmCtQEnnCZPVbHQmPl1i9q3QpXX74DLB74XoaWd/hEa/ArZnupUUXbNf
                                WspRtz7b4Fvp+xwt7RcBHGl+DdqOnk+Tsan+IbWBedrAk5Fr+flZ4MPlgLlERE9KOBcfmzIingL3sJrV+I5n7e8xnpDmO3zPxZaM
                                oQK6MkOXMk9bU+NJoA/tu8CYkdDBOyEzydIpftXG9P1CVkmVEUJd9I2Sky5nijI7M8yPJIvIz0o7LnW0vJ0+jFStHSkdqd0EkUSu
                                WVkVyHVa5vnco5t7svm2cfWH9mG0rQBPu7jcpfXmZZV4ZV43yMN6t0Hs+JCnsnofxcQl6LNKpXz9BPFW97XO+vT4mUp0OZt2yhh+
                                KXHN7LlFZjD+enijtw4rWfcD3vTNGtQyofI7IGirvtTQpfix581mHv2eUPoKn15JuYMK2Sn57cvqQTVW2e+3aKD8ViXdUMI3gKZn
                                1uYHwM6En8X8uYrLXkXDz1nSYP2mnAd4/NwcirdCfrFVUvvXthwPa04fY/qdcfCyzIWe+JqfsBa5Ohyv+JU41s/FdV/8cCAS+bPM
                                Niefv1Ik4IuEDWNetOFJgw1juHrddbpq5NiMbdrpmF95WXua5TjfyqGdfPVuxc/1Js5FSwsQri6f79PH2qBI2CCMmzYypns2H9pV
                                x77kQS6dylRi+31Z00IbH1ZVLeR6n8zXFL9wlzqPKMY1ebvWPt1VVYFSFtD9Zyw15y6o6qncn0Ib0v+r6tPUyuxZWGeoSXdL7bD4
                                rnI4ntaq/kwhMzaOjtAtxOPY2u0lXklNVE6+zCl7TmUHuZAd+gaSx1W/V0FTRu4T7IbjPcrPwXYWslStfdy4nXUsR5+rjUmfNu3C
                                J/QA57/EE/Jthi0adO08EM/cxaztY7xYjrOtvQH3nw46jG+qvbxef5ooFzujHGYXH40ZWtp2TrOSm4sP7SwG4qlZWYHb1AqFp/2a
                                rR6Pt/u8meMrYL1uEQXbsVNxkdq59ExQ91qqxreFd5IpHrvgh7HiiFXc0xaY7FNsbKUisY4T71xNGB8vMz5edoT7fzhyu6bLId2d
                                eHfAXplxz7PPEt+0VCFnHubUuDrbUZ68grJnw6YTE+nXz+W8ZRijwltav3m8eS7kOVp5E/dO9jrqXt8JJlHEqy4r2SMt9fAExxMp
                                rerujNxpkLXiGit6tuA5lZ3TXPL5SjzEHWkSwFJnnisUvit2CaVZgN66PGNSLmpfVHYLeToL2lMzc4LWYUVRvXI8Fouxw+7J3vno
                                vutTDmXx4cmxK++0MjktIBclUEztERiFnQbOmdeDpng7fvFtw42cg+agbWUgJH8LWdH9mnWA8U267Mq1c1MI1niNDXcq30+c/J3W
                                jGP6n12YRljcPTIXVR/62lXeJ+bbqjY31R6Feqijd5XqKJG922tUmM2qHe8gtmlWPwGkdfwuoWPxQToWH6TjpoWOfrTtRtPvd+rN
                                XfnS1rvdaMl7113683HeDE9sthKsRdO1V/vsrSyhHnOPo/fc8f3xUHpFVuQJVvz/OG7Q36hEen7hPbVWIfaWz8znwHTPc7m71vtf
                                2oUy2dtR7jLad9o6yDpKz13j4X3qL8JdfUgsV9PU1+F5V+cGiFpWIb2rHwSyHzdoxdmmO3xi2/+F/gcqZ+CqRhoAAA==";

            var gDecompressed = gCompressed.ToDecompress();

            return base.Content(gDecompressed, "text/html");
        }

        /// <summary>
        /// Returns Peer Info
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// Dumps out banned peers
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// Bans a Peer IP
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <returns></returns>
        [HttpGet("BanPeer/{**ipAddress}")]
        public async Task<string> BanPeer(string ipAddress)
        {
            BanService.BanPeer(ipAddress, "Banned from API", "V1Controller.BanPeer()");
            var result = $"Peer({ipAddress}) Banned!";
            return result;
        }

        /// <summary>
        /// Unbans a Peer IP
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <returns></returns>
        [HttpGet("UnbanPeer/{**ipAddress}")]
        public async Task<string> UnbanPeer(string ipAddress)
        {
            BanService.UnbanPeer(ipAddress);
            var result = $"Peer({ipAddress}) Unbanned!";

            return result;
        }

        /// <summary>
        /// Unbans all peers.
        /// </summary>
        /// <param name="unbanPerma"></param>
        /// <returns></returns>
        [HttpGet("UnbanAllPeers/{unbanPerma?}")]
        public async Task<string> UnbanAllPeers(bool unbanPerma = false)
        {
            var result = await Peers.UnbanAllPeers(unbanPerma);

            return result.ToString();
        }

        /// <summary>
        /// Dumps out debug information
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetDebugInfo")]
        public async Task<string> GetDebugInfo()
        {
            var output = await StaticVariableUtility.GetStaticVars();

            return output;
        }

        /// <summary>
        /// Dumps out connection history
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetConnectionHistory")]
        public async Task<string> GetConnectionHistory()
        {
            var output = await ConnectionHistory.Read();

            return output;
        }

        /// <summary>
        /// Dumps out client information
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetClientInfo")]
        public async Task<string> GetClientInfo()
        {
            var output = await StaticVariableUtility.GetClientInfo();

            return output;
        }

        /// <summary>
        /// Dumps out CLI version
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetCLIVersion")]
        public async Task<string> GetCLIVersion()
        {
            string output = "";

            output = Globals.CLIVersion;

            return output;
        }

        /// <summary>
        /// Dumps out RBX log
        /// </summary>
        /// <returns></returns>
        [HttpGet("ReadRBXLog")]
        public async Task<string> ReadRBXLog()
        {
            string output = "";

            output = await LogUtility.ReadLog();

            return output;
        }

        /// <summary>
        /// Clears the RBX log
        /// </summary>
        /// <returns></returns>
        [HttpGet("ClearRBXLog")]
        public async Task<string> ClearRBXLog()
        {
            string output = "";

            await LogUtility.ClearLog();

            output = "Log Cleared";
            return output;
        }

        /// <summary>
        /// Reads the validator log
        /// </summary>
        /// <returns></returns>
        [HttpGet("ReadValLog")]
        public async Task<string> ReadValLog()
        {
            string output = "";

            output = await ValidatorLogUtility.ReadLog();

            return output;
        }

        /// <summary>
        /// Clears the validator log
        /// </summary>
        /// <returns></returns>
        [HttpGet("ClearValLog")]
        public async Task<string> ClearValLog()
        {
            string output = "";

            await ValidatorLogUtility.ClearLog();

            output = "Log Cleared";
            return output;
        }

        /// <summary>
        /// Stops all wallet functions and exits
        /// </summary>
        /// <returns></returns>
        [HttpGet("SendExit")]
        public async Task SendExit()
        {
            //use Id to get specific commands
            var delay = Task.Delay(2000);
            LogUtility.Log("Send exit has been called. Closing Wallet.", "V1Controller.SendExit()");
            Globals.StopAllTimers = true;
            await delay;
            while (Globals.TreisUpdating)
            {
                await Task.Delay(300);
                //waiting for treis to stop
            }
            await Settings.InitiateShutdownUpdate();

            
            Environment.Exit(0);
        }

        /// <summary>
        /// Sets restart variable and then stops all wallet functions and exits
        /// </summary>
        /// <returns></returns>
        [HttpGet("SetRestartAndExit")]
        public async Task SetRestartAndExit()
        {
            //use Id to get specific commands
            var delay = Task.Delay(2000);
            LogUtility.Log("Send exit has been called. Closing Wallet.", "V1Controller.SendExit()");
            Globals.StopAllTimers = true;
            await delay;
            while (Globals.TreisUpdating)
            {
                await Task.Delay(300);
                //waiting for treis to stop
            }
            await Settings.InitiateShutdownUpdate();

            Environment.SetEnvironmentVariable("RBX-Restart", "1", EnvironmentVariableTarget.User);
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
