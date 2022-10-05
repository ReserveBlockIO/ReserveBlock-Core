using ReserveBlockCore.Models;
using ReserveBlockCore.Services;
using ReserveBlockCore.BIP39;
using ReserveBlockCore.P2P;
using Newtonsoft.Json;
using ReserveBlockCore.Utilities;
using System.Net;
using ReserveBlockCore.Data;
using System.Text.RegularExpressions;
using Spectre.Console;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Security;
using Microsoft.AspNetCore.Mvc.Formatters.Xml;
using ReserveBlockCore.Beacon;

namespace ReserveBlockCore.Commands
{
    public class BaseCommandServices
    {
        public static async void UnlockWallet()
        {
            if(!string.IsNullOrWhiteSpace(Globals.WalletPassword))
            {
                Console.WriteLine("Please type in password to unlock wallet.");
                var password = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(password))
                {
                    var passCheck = Globals.WalletPassword.ToDecrypt(password);
                    if(passCheck == password)
                    {
                        Globals.CLIWalletUnlockTime = DateTime.UtcNow.AddMinutes(Globals.WalletUnlockTime);
                        Console.WriteLine($"Wallet has been unlocked for {Globals.WalletUnlockTime} mins.");
                    }
                    else
                    {
                        Console.WriteLine("Incorrect Password.");
                    }
                }
            }
            else
            {
                Console.WriteLine("No password has been configured");
            }
            
        }
        public static async void PrintKeys()
        {
            var accounts = AccountData.GetAccounts();

            var accountList = accounts.FindAll().ToList();

            if (accountList.Count() > 0)
            {
                Console.Clear();
                Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop);

                AnsiConsole.Write(
                new FigletText("RBX Private Keys")
                .Centered()
                .Color(Color.Green));

                var table = new Table();

                table.Title("[yellow]RBX Private Keys[/]").Centered();
                table.AddColumn(new TableColumn(new Panel("Address")));
                table.AddColumn(new TableColumn(new Panel("Private Key"))).Centered();

                accountList.ForEach(x => {
                    table.AddRow($"[blue]{x.Address}[/]", $"[green]{x.PrivateKey}[/]");
                });

                table.Border(TableBorder.Rounded);

                AnsiConsole.Write(table);
            }
            else
            {

            }
        }
        public static async Task EncryptWallet()
        {
            if(Globals.HDWallet == true)
            {
                Console.WriteLine("Wallet Encryption is not currently compatible with HD wallets.");
                Console.WriteLine("This will be released in a future wallet update.");
            }
            else if(Globals.ValidatorAddress != "")
            {
                Console.WriteLine("Validators are required to sign their task and cannot have their keys encrypted");
                Console.WriteLine("This may be addressed at a later time.");
            }
            else if(Globals.IsWalletEncrypted == false)
            {
                AnsiConsole.MarkupLine("[red]******************************************WARNING******************************************[/]");
                AnsiConsole.MarkupLine("[yellow]****************************************PLEASE READ****************************************[/]");
                Console.WriteLine("You are about to encrypt your wallet. Please note this will encrypt ALL private keys currently in wallet and all future keys.");
                Console.WriteLine("If you forget this password there is no way to recover your keys. Please use this feature fully understanding this.");
                Console.WriteLine("This is a new wallet feature. It is recommended a non-encrypted version or private keys be backed up before starting this process.");
                AnsiConsole.MarkupLine("Are you sure you want to do this? ('[bold green]y[/]' for yes and '[bold red]n[/]' for no).");
                var confirmation = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(confirmation) && confirmation.ToLower() == "y")
                {
                    Console.WriteLine("Please type a password to encrypt wallet with. Please note for security you will not see this password in the CLI.");
                    var password = new SecureString();
                    while (true)
                    {
                        ConsoleKeyInfo i = Console.ReadKey(true);
                        if (i.Key == ConsoleKey.Enter)
                        {
                            break;
                        }
                        else if (i.Key == ConsoleKey.Backspace)
                        {
                            if (password.Length > 0)
                            {
                                password.RemoveAt(password.Length - 1);
                                Console.Write("\b \b");
                            }
                        }
                        else if (i.KeyChar != '\u0000') // KeyChar == '\u0000' if the key pressed does not correspond to a printable character, e.g. F1, Pause-Break, etc
                        {
                            password.AppendChar(i.KeyChar);
                            Console.Write("");
                        }
                    }
                    if (password.Length != 0)
                    {
                        if (password.Length > 32)
                        {
                            MainMenuReturn();
                            Console.WriteLine("Passwords cannot be larger than 32 characters.");
                        }
                        else
                        {
                            Console.WriteLine("------------------------------------------------");
                            Console.WriteLine("Please confirm password");
                            var passwordConfirmed = new SecureString();
                            while (true)
                            {
                                ConsoleKeyInfo i = Console.ReadKey(true);
                                if (i.Key == ConsoleKey.Enter)
                                {
                                    break;
                                }
                                else if (i.Key == ConsoleKey.Backspace)
                                {
                                    if (passwordConfirmed.Length > 0)
                                    {
                                        passwordConfirmed.RemoveAt(passwordConfirmed.Length - 1);
                                        Console.Write("\b \b");
                                    }
                                }
                                else if (i.KeyChar != '\u0000') // KeyChar == '\u0000' if the key pressed does not correspond to a printable character, e.g. F1, Pause-Break, etc
                                {
                                    passwordConfirmed.AppendChar(i.KeyChar);
                                    Console.Write("");
                                }
                            }
                            if (passwordConfirmed.Length != 0)
                            {
                                if (password.SecureStringCompare(passwordConfirmed))
                                {
                                    Globals.EncryptPassword = passwordConfirmed;
                                    passwordConfirmed = new SecureString(); //clear password
                                    password = new SecureString(); //clear password

                                    Console.WriteLine("Encrypting Wallet. Please do not close wallet as this may take a few moments.");
                                    await Keystore.GenerateKeystoreAddresses();
                                    Globals.IsWalletEncrypted = true;

                                    Console.WriteLine("Encrypting Wallet has completed...");
                                    MainMenuReturn();
                                }
                                else
                                {
                                    Console.WriteLine("Encrypting wallet failed. Passwords did not match. Please try again.");
                                    MainMenuReturn();
                                }
                            }
                            else
                            {
                                MainMenuReturn();
                                Console.WriteLine("Passwords cannot be blank.");
                            }
                        }
                    }
                    else
                    {
                        MainMenuReturn();
                        Console.WriteLine("Passwords cannot be blank.");
                    }
                }
                else
                {
                    MainMenuReturn();
                    Console.WriteLine("Unexpected response. Please try again...");
                }
            }
            else
            {
                MainMenuReturn();
                Console.WriteLine("Wallet is already encrypted.");
            }
        }

        public static async Task DecryptWallet()
        {
            if(Globals.EncryptPassword.Length == 0)
            {
                Console.WriteLine($"Please enter your encryption password to decrypt wallet for {Globals.PasswordClearTime} minutes.");
                var pwd = new SecureString();
                while (true)
                {
                    ConsoleKeyInfo i = Console.ReadKey(true);
                    if (i.Key == ConsoleKey.Enter)
                    {
                        Globals.EncryptPassword = pwd;
                        break;
                    }
                    else if (i.Key == ConsoleKey.Backspace)
                    {
                        if (pwd.Length > 0)
                        {
                            pwd.RemoveAt(pwd.Length - 1);
                            Console.Write("\b \b");
                        }
                    }
                    else if (i.KeyChar != '\u0000') // KeyChar == '\u0000' if the key pressed does not correspond to a printable character, e.g. F1, Pause-Break, etc
                    {
                        pwd.AppendChar(i.KeyChar);
                        Console.Write("");
                    }
                }

                Console.WriteLine("Password has been entered.");
            }
            else
            {
                Console.WriteLine("Wallet already has password for decryption.");
            }
        }

        public static async void ResetValidator()
        {
            await ValidatorService.DoMasterNodeStop();
        }
        public static async void AddPeer()
        {
            IPAddress ip;
            Console.WriteLine("Please input the IP of the peer...");
            var peer = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(peer))
            {
                try
                {
                    bool ValidateIP = IPAddress.TryParse(peer, out ip);
                    if (ValidateIP)
                    {
                        var peers = Peers.GetAll();
                        var peerExist = peers.Exists(x => x.PeerIP == peer);
                        if (!peerExist)
                        {
                            Peers nPeer = new Peers
                            {
                                IsIncoming = false,
                                IsOutgoing = true,
                                PeerIP = peer,
                                FailCount = 0
                            };

                            peers.InsertSafe(nPeer);

                            Console.WriteLine("Success! Peer added.");
                            Console.WriteLine("Returning you to main menu...");
                            Thread.Sleep(4000);
                            StartupService.MainMenu();

                        }
                        else
                        {
                            var peerRec = peers.FindOne(x => x.PeerIP == peer);
                            peerRec.IsOutgoing = !peerRec.IsOutgoing;
                            peers.UpdateSafe(peerRec);

                            Console.WriteLine("Peer already exist...");
                            Console.WriteLine($"Peer Outgoing has been set to {peerRec.IsOutgoing}");
                            Console.WriteLine("Returning you to main menu...");
                            Thread.Sleep(4000);
                            StartupService.MainMenu();
                        }
                    }
                    else
                    {
                        Console.WriteLine("Failed to add. Please input a valid IP...");
                        Console.WriteLine("Returning you to main menu...");
                        Thread.Sleep(4000);
                        StartupService.MainMenu();
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Unexpected Error. Error Message: {ex.Message}");
                    Console.WriteLine("Type /menu to return to main menu.");
                }
            }
        }
        public static async void ReconnectPeers()
        {
            Console.WriteLine("Re-establish Peers? y/n");
            var reconnect = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(reconnect))
            {
                if (reconnect == "y")
                {
                    await StartupService.StartupPeers();
                }
            }
        }

        public static async void CreateBeacon()
        {
            Console.WriteLine("Please give your beacon a name...");
            var name = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(name))
            {
                var ip = P2PClient.MostLikelyIP();

                if (ip == "NA")
                {
                     Console.WriteLine("Could not get external IP. Please ensure you are connected to peers and that you are not blocking ports.");
                }

                var bUID = Guid.NewGuid().ToString().Substring(0,12).Replace("-", "") + ":" + TimeUtil.GetTime().ToString();

                BeaconInfo.BeaconInfoJson beaconLoc = new BeaconInfo.BeaconInfoJson
                {
                    IPAddress = ip,
                    Port = Globals.IsTestNet != true ? Globals.Port + 10000 : Globals.Port + 20000,
                    Name = name,
                    BeaconUID = bUID
                };

                var beaconLocJson = JsonConvert.SerializeObject(beaconLoc);

                BeaconInfo bInfo = new BeaconInfo();
                bInfo.Name = name;
                bInfo.IsBeaconActive = true;
                bInfo.BeaconLocator = beaconLocJson.ToBase64();
                bInfo.BeaconUID = bUID;

                var result = BeaconInfo.SaveBeaconInfo(bInfo);

                Console.WriteLine(result);
            }
        }

        public static async void SwitchBeaconState()
        {
            var result = BeaconInfo.SetBeaconActiveState();

            if (result == null)
            {
                Console.WriteLine("Error turning beacon on/off");
            }
            else
            {
                if(result.Value == true)
                {
                    Console.WriteLine("Beacon has been turned on.");
                }
                else
                {
                    Console.WriteLine("Beacon has been turned off.");
                }
            }
        }

        public static async void CreateDecShop()
        {
            Console.WriteLine("Please select the wallet you'd like to use to pay for shop registration...");
            var accountList = AccountData.GetAccountsWithBalance();
            var accountNumberList = new Dictionary<string, Account>();
            if (accountList.Count() > 0)
            {
                int count = 1;
                Console.WriteLine("********************************************************************");
                Console.WriteLine("Please choose an address below by typing its # and pressing enter.");
                accountList.ToList().ForEach(x => {
                    accountNumberList.Add(count.ToString(), x);
                    Console.WriteLine("********************************************************************");
                    Console.WriteLine("\n#" + count.ToString());
                    Console.WriteLine("\nAddress :\n{0}", x.Address);
                    Console.WriteLine("\nAccount Balance:\n{0}", x.Balance);
                    Console.WriteLine("********************************************************************");
                    count++;
                });
                string walletChoice = "";
                walletChoice = Console.ReadLine();

                if (!string.IsNullOrWhiteSpace(walletChoice))
                {
                    var keyCheck = accountNumberList.ContainsKey(walletChoice);

                    if (keyCheck == false)
                    {
                        Console.WriteLine($"Please choose a correct number. Error with entry given: {walletChoice}");
                        MainMenuReturn();
                    }
                    else
                    {
                        var wallet = accountNumberList[walletChoice];
                        var address = wallet.Address;
                        Console.WriteLine("Please give your shop a name...");
                        var name = Console.ReadLine();
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            Console.WriteLine("Please give your shop a description (Max length of 512 characters)...");
                            var desc = Console.ReadLine();
                            if (!string.IsNullOrWhiteSpace(desc) && desc.Length > 512)
                            {
                                var ip = P2PClient.MostLikelyIP();

                                if (ip == "NA")
                                {
                                    Console.WriteLine("Could not get external IP. Please ensure you are connected to peers and that you are not blocking ports.");
                                    MainMenuReturn();
                                }
                                else
                                {
                                    var sUID = Guid.NewGuid().ToString().Substring(0, 10).Replace("-", "") + ":" + TimeUtil.GetTime().ToString();

                                    DecShop.DecShopInfoJson decShopLoc = new DecShop.DecShopInfoJson
                                    {
                                        IPAddress = ip,
                                        Port = Globals.Port,
                                        Name = name,
                                        ShopUID = sUID
                                    };

                                    var decShopLocJson = JsonConvert.SerializeObject(decShopLoc);

                                    DecShop dsInfo = new DecShop();
                                    dsInfo.Name = name;
                                    dsInfo.Description = desc;
                                    dsInfo.Locator = decShopLocJson.ToBase64();
                                    dsInfo.IsOffline = false;
                                    dsInfo.ShopUID = sUID;
                                    dsInfo.Address = address;

                                    var result = await DecShop.SaveMyDecShopInfo(dsInfo);
                                    //publish to chain now.
                                    Console.WriteLine(result);
                                    MainMenuReturn();
                                }

                            }
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("No eligible accounts were detected. You must have an account with at least 1 RBX to create a shop.");
                MainMenuReturn();
            }
        }

        public static string CreateHDWallet()
        {
            Console.WriteLine("How many words do you want? (12 or 24)");
            var strengthStr = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(strengthStr))
            {
                if (strengthStr == "12")
                {
                    var strength = Convert.ToInt32(strengthStr);
                    var mnemonic = HDWallet.HDWalletData.CreateHDWallet(strength, BIP39Wordlist.English);
                    Globals.HDWallet = true;

                    return mnemonic;
                }
                else if(strengthStr == "24")
                {
                    var strength = Convert.ToInt32(strengthStr);
                    var mnemonic = HDWallet.HDWalletData.CreateHDWallet(strength, BIP39Wordlist.English);
                    Globals.HDWallet = true;

                    return mnemonic;
                }
                else
                {
                    return "Unexpected entry detected. Please try again.";
                }
            }
            return "Unexpected entry detected. Please try again.";
        }
        public static async Task ValidatorInfo()
        {
            var account = AccountData.GetLocalValidator();
            if(account != null)
            {
                var validator = Validators.Validator.GetAll().FindOne(x => x.Address == account.Address);
                if(validator != null)
                {
                    Console.WriteLine($"Validator Name: {validator.UniqueName}");
                    Console.WriteLine($"Validator Address: {validator.Address}");
                    Console.WriteLine($"Validator Amount: {account.Balance}");
                    Console.WriteLine($"Validating? {account.IsValidating}");
                }
                else
                {
                    Console.WriteLine("Account found, but validator not registered locally.");
                }
            }
            else
            {
                Console.WriteLine("No accounts detected as validators.");
            }
        }

        public static async Task<string> CreateDnr()
        {
            var output = "";
            Console.WriteLine("Please select the wallet you'd like to create a domain name registration for...");
            var accountList = AccountData.GetAccountsWithBalanceForAdnr();
            var accountNumberList = new Dictionary<string, Account>();
            if (accountList.Count() > 0)
            {
                int count = 1;
                Console.WriteLine("********************************************************************");
                Console.WriteLine("Please choose an address below by typing its # and pressing enter.");
                accountList.ToList().ForEach(x => {
                    accountNumberList.Add(count.ToString(), x);
                    Console.WriteLine("********************************************************************");
                    Console.WriteLine("\n#" + count.ToString());
                    Console.WriteLine("\nAddress :\n{0}", x.Address);
                    Console.WriteLine("\nAccount Balance:\n{0}", x.Balance);
                    Console.WriteLine("********************************************************************");
                    count++;
                });
                string walletChoice = "";
                walletChoice = Console.ReadLine();

                if(!string.IsNullOrWhiteSpace(walletChoice))
                {
                    var keyCheck = accountNumberList.ContainsKey(walletChoice);

                    if (keyCheck == false)
                    {
                        Console.WriteLine($"Please choose a correct number. Error with entry given: {walletChoice}");
                        return output;
                    }
                    else
                    {
                        var wallet = accountNumberList[walletChoice];
                        var address = wallet.Address;
                        var adnr = Adnr.GetAdnr();
                        var adnrCheck = adnr.FindOne(x => x.Address == address);
                        if(adnrCheck != null)
                        {
                            Console.WriteLine($"This address already has a DNR associated with it: {adnrCheck.Name}");
                            return output;
                        }
                        bool nameFound = true;
                        while(nameFound)
                        {
                            Console.WriteLine($"You have selected the following wallet: {address}");
                            Console.WriteLine("Please enter the name you'd like for this wallet. Ex: (cryptoinvestor1) Please note '.rbx' will automatically be added. DO NOT INCLUDE IT.");
                            Console.WriteLine("type exit to leave this menu.");
                            var name = Console.ReadLine();
                            if(!string.IsNullOrWhiteSpace(name) && name != "exit")
                            {
                                var nameCharCheck = Regex.IsMatch(name, @"^[a-zA-Z0-9]+$");
                                if(!nameCharCheck)
                                {
                                    Console.WriteLine("-->ERROR! A DNR may only contain letters and numbers. ERROR!<--");
                                }
                                else
                                {
                                    var nameCheck = adnr.FindOne(x => x.Name == name);
                                    if (nameCheck == null)
                                    {
                                        nameFound = false;
                                        Console.WriteLine("Are you sure you want to create this DNR? 'y' for yes, 'n' for no.");
                                        var response = Console.ReadLine();
                                        if (!string.IsNullOrWhiteSpace(response))
                                        {
                                            if (response.ToLower() == "y")
                                            {
                                                Console.WriteLine("Sending Transaction now.");
                                                var result = await Adnr.CreateAdnrTx(address, name);
                                                if(result.Item1 != null)
                                                {
                                                    Console.WriteLine("DNR Request has been sent to mempool.");
                                                    MainMenuReturn();
                                                }
                                                else
                                                {
                                                    Console.WriteLine("DNR Request failed to enter the mempool.");
                                                    Console.WriteLine($"Error: {result.Item2}");
                                                }
                                            }
                                            else
                                            {
                                                StartupService.MainMenu();
                                                Console.WriteLine("DNR Request has been cancelled.");
                                            }
                                        }

                                    }
                                    else
                                    {
                                        StartupService.MainMenu();
                                        Console.WriteLine("DNR Request has been cancelled. Name already belongs to another address.");
                                    }
                                }
                                
                            }
                            else
                            {
                                StartupService.MainMenu();
                                Console.WriteLine("DNR Request has been cancelled. Incorrect format inputted.");
                            }
                            
                        }
                        
                    }
                }
                return output;
                
            }
            else
            {
                Console.WriteLine("No eligible accounts were detected. You must have an account with at least 1 RBX to create a dnr.");
                return output;
            }

        }

        public static async Task<string> TransferDnr()
        {
            var output = "";
            Console.WriteLine("Please select the wallet you'd like to transfer a domain name registration from...");
            var accountList = AccountData.GetAccountsWithAdnr();
            var accountNumberList = new Dictionary<string, Account>();
            if (accountList.Count() > 0)
            {
                int count = 1;
                Console.WriteLine("********************************************************************");
                Console.WriteLine("Please choose an address below by typing its # and pressing enter.");
                accountList.ToList().ForEach(x => {
                    accountNumberList.Add(count.ToString(), x);
                    Console.WriteLine("********************************************************************");
                    Console.WriteLine("********************************************************************");
                    Console.WriteLine("\n#" + count.ToString());
                    Console.WriteLine("\nAddress :\n{0}", x.Address);
                    Console.WriteLine("\nAccount Balance:\n{0}", x.Balance);
                    Console.WriteLine("\nAccount Adnr:\n{0}", x.ADNR);
                    Console.WriteLine("********************************************************************");
                    Console.WriteLine("********************************************************************");
                    count++;
                });
                string walletChoice = "";
                walletChoice = Console.ReadLine();

                if (!string.IsNullOrWhiteSpace(walletChoice))
                {
                    var keyCheck = accountNumberList.ContainsKey(walletChoice);

                    if (keyCheck == false)
                    {
                        Console.WriteLine($"Cancelled! Please choose a correct number. Error with entry given: {walletChoice}");
                        return output;
                    }
                    else
                    {
                        var wallet = accountNumberList[walletChoice];
                        var address = wallet.Address;
                        var adnr = Adnr.GetAdnr();
                        var adnrCheck = adnr.FindOne(x => x.Address == address);
                        if (adnrCheck == null)
                        {
                            Console.WriteLine($"Cancelled! This address does not have a DNR associated with it: {adnrCheck.Name}");
                            return output;
                        }
                        bool nameFound = true;
                        while (nameFound)
                        {
                            Console.WriteLine($"You have selected the following wallet: {address}");
                            Console.WriteLine("Please enter the address you'd like to transfer too. BE SURE YOU WANT TO DO THIS! Once a transfer is processed it cannot be reversed.");
                            Console.WriteLine("type exit to leave this menu.");
                            var toAddr = Console.ReadLine();
                            if (!string.IsNullOrWhiteSpace(toAddr) && toAddr != "exit")
                            {

                                var addrVerify = AddressValidateUtility.ValidateAddress(toAddr);
                                if (addrVerify == true)
                                {
                                    var toAddrAdnr = adnr.FindOne(x => x.Address == toAddr);
                                    if(toAddrAdnr != null)
                                    {
                                        nameFound = false;
                                        Console.WriteLine("This address already has an ADNR associated with it");
                                        MainMenuReturn();
                                    }
                                    else
                                    {
                                        nameFound = false;
                                        Console.WriteLine("Are you sure you want to transfer this DNR? 'y' for yes, 'n' for no.");
                                        var response = Console.ReadLine();
                                        if (!string.IsNullOrWhiteSpace(response))
                                        {
                                            if (response.ToLower() == "y")
                                            {
                                                Console.WriteLine("Sending Transaction now.");
                                                var result = await Adnr.TransferAdnrTx(address, toAddr);
                                                if (result.Item1 != null)
                                                {
                                                    Console.WriteLine("DNR Transfer Request has been sent to mempool.");
                                                    MainMenuReturn();
                                                }
                                                else
                                                {
                                                    Console.WriteLine("DNR Transfer Request failed to enter the mempool.");
                                                    Console.WriteLine($"Error: {result.Item2}");
                                                }
                                            }
                                            else
                                            {
                                                StartupService.MainMenu();
                                                Console.WriteLine("DNR Transfer Request has been cancelled.");
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("Invalid RBX address has been entered. Please try again.");
                                }
                            }
                            else
                            {
                                nameFound = false;
                                StartupService.MainMenu();
                                Console.WriteLine("DNR Request has been cancelled. Incorrect format inputted.");
                            }

                        }

                    }
                }
                return output;

            }
            else
            {
                Console.WriteLine("No eligible accounts were detected. You must have an account with at least 1 RBX to create a dnr.");
                return output;
            }

        }

        public static async Task CreateAddress()
        {
            if (Globals.HDWallet == true)
            {
                var hdAccount = HDWallet.HDWalletData.GenerateAddress();
                if (hdAccount != null)
                {
                    Console.WriteLine("-----------------------HD Wallet Address Created------------------------");
                    Console.WriteLine($"New Address: {hdAccount.Address}");
                    Console.WriteLine("----------------------Type /menu to return to menu----------------------");
                }
                else
                {
                    Console.WriteLine("You have not created an HD wallet. Please Use command '2hd' and press enter.");
                }
            }
            else
            {
                if (Globals.IsWalletEncrypted == true)
                {
                    var keysAvail = await Keystore.KeystoreCheck();
                    if (keysAvail > 0)
                    {
                        var account = await Keystore.GetNextKeystore();
                        if (account != null)
                        {
                            AccountData.WalletInfo(account);
                        }
                        else
                        {
                            Console.WriteLine("There was an issue creating a new address. Please try again...");
                        }
                    }
                    else
                    {
                        if(Globals.EncryptPassword.Length == 0)
                        {
                            Console.WriteLine("Please input your encryption password to create a new address");
                            var password = Console.ReadLine();
                            if (!string.IsNullOrEmpty(password))
                            {
                                Globals.EncryptPassword = password.ToSecureString();
                                Console.WriteLine("Creating new addresses. Please wait...");
                                await Keystore.GenerateKeystoreAddresses(false);
                                var account = await Keystore.GetNextKeystore();
                                if (account != null)
                                {
                                    AccountData.WalletInfo(account);
                                }
                                else
                                {
                                    Console.WriteLine("There was an issue creating a new address. Please try again...");
                                }
                                password = "0";
                            }
                            else
                            {
                                Console.WriteLine("Password cannot be blank. Returning you to main menu.");
                                MainMenuReturn();
                            }
                        }
                        else
                        {
                            Console.WriteLine("Creating new addresses. Please wait...");
                            await Keystore.GenerateKeystoreAddresses(false);
                            var account = await Keystore.GetNextKeystore();
                            if (account != null)
                            {
                                AccountData.WalletInfo(account);
                            }
                            else
                            {
                                Console.WriteLine("There was an issue creating a new address. Please try again...");
                            }
                        }
                        
                        
                    }
                }
                else
                {
                    var account = new Account().Build();
                    AccountData.WalletInfo(account);
                }

            }
        }

        public static async Task<string> DeleteDnr()
        {
            var output = "";
            Console.WriteLine("Please select the wallet you'd like to delete a domain name registration for...");
            var accountList = AccountData.GetAccountsWithBalanceForAdnr();
            var accountNumberList = new Dictionary<string, Account>();
            if (accountList.Count() > 0)
            {
                int count = 1;
                Console.WriteLine("********************************************************************");
                Console.WriteLine("Please choose an address below by typing its # and pressing enter.");
                accountList.ToList().ForEach(x => {
                    accountNumberList.Add(count.ToString(), x);
                    Console.WriteLine("********************************************************************");
                    Console.WriteLine("\n#" + count.ToString());
                    Console.WriteLine("\nAddress :\n{0}", x.Address);
                    Console.WriteLine("\nAccount Balance:\n{0}", x.Balance);
                    Console.WriteLine("********************************************************************");
                    count++;
                });
                string walletChoice = "";
                walletChoice = Console.ReadLine();

                if (!string.IsNullOrWhiteSpace(walletChoice))
                {
                    var keyCheck = accountNumberList.ContainsKey(walletChoice);

                    if (keyCheck == false)
                    {
                        Console.WriteLine($"Please choose a correct number. Error with entry given: {walletChoice}");
                        return output;
                    }
                    else
                    {
                        var wallet = accountNumberList[walletChoice];
                        var address = wallet.Address;
                        var adnr = Adnr.GetAdnr();
                        var adnrCheck = adnr.FindOne(x => x.Address == address);
                        if (adnrCheck == null)
                        {
                            Console.WriteLine($"This address does not have a DNR associated with it: {adnrCheck.Name}");
                            return output;
                        }

                        Console.WriteLine($"You have selected the following wallet: {address}");
                        Console.WriteLine("Are you sure you want to create this DNR? 'y' for yes, 'n' for no.");
                        var response = Console.ReadLine();
                        if (!string.IsNullOrWhiteSpace(response))
                        {
                            if (response.ToLower() == "y")
                            {
                                Console.WriteLine("Sending Transaction now...");
                                var result = await Adnr.DeleteAdnrTx(address);
                                if (result.Item1 != null)
                                {
                                    Console.WriteLine("DNR Delete Request has been sent to mempool.");
                                    MainMenuReturn();
                                }
                                else
                                {
                                    Console.WriteLine("DNR Request failed to enter the mempool.");
                                    Console.WriteLine($"Error: {result.Item2}");
                                }
                            }
                            else
                            {
                                StartupService.MainMenu();
                                Console.WriteLine("DNR Request has been cancelled.");
                            }
                        }
                    }
                }
                return output;
            }
            else
            {
                Console.WriteLine("No eligible accounts were detected. You must have an account with at least 1 RBX to create a dnr.");
                return output;
            }

        }

        public static string RestoreHDWallet()
        {
            Console.WriteLine("Please paste your Mnemonic Below...");
            var mnemonicStr = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(mnemonicStr))
            {
                var mnemonicResult = HDWallet.HDWalletData.RestoreHDWallet(mnemonicStr);
                if(mnemonicResult.Contains("Restored"))
                {
                    Globals.HDWallet = true;
                }
                
                return mnemonicResult;
                
            }
            return "Unexpected entry detected. Please try again.";
        }

        public static void GetLatestTx()
        {
            var transactions = TransactionData.GetAll();

            if (transactions.Count() == 0)
            {
                Console.WriteLine("No TXs...");
            }
            else
            {
                var transactionsList = transactions.FindAll().OrderByDescending(x => x.Timestamp).Take(10).ToList();
                foreach(var tx in transactionsList)
                {
                    var txStr = $"TxId: {tx.Hash} - From: {tx.FromAddress} - To: {tx.ToAddress} - Amount: {tx.Amount} - Time: {tx.Timestamp}";
                    Console.WriteLine(txStr);
                }
                
            }
        }

        public static void PrintBlock()
        {
            try
            {
                ConsoleWriterService.Output("Please enter the block height");
                var blockHeightStr = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(blockHeightStr))
                {
                    long blockHeight = 0;
                    long.TryParse(blockHeightStr, out blockHeight);

                    var block = BlockchainData.GetBlockByHeight(blockHeight);

                    if (block != null)
                    {
                        BlockchainData.PrintBlock(block);
                    }
                    else
                    {
                        ConsoleWriterService.Output($"Could not find block with height: {blockHeight}");
                    }
                }
            }
            catch(Exception ex)
            {
                ConsoleWriterService.Output($"Unexpected error. Please try again. Error Message: {ex.Message}");
            }
            
        }
        public static void PrintInfo()
        {
            var network = Globals.IsTestNet == true ? "TestNet" : "MainNet";
            var mostLikelyIP = P2PClient.MostLikelyIP();

            var databaseLocation = Globals.IsTestNet != true ? "Databases" : "DatabasesTestNet";
            var mainFolderPath = Globals.IsTestNet != true ? "RBX" : "RBXTest";

            var osDesc = RuntimeInformation.OSDescription;
            var processArch = RuntimeInformation.ProcessArchitecture;
            var netFramework = RuntimeInformation.FrameworkDescription;

            string path = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                path = homeDirectory + Path.DirectorySeparatorChar + mainFolderPath.ToLower() + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
            }
            else
            {
                if (Debugger.IsAttached)
                {
                    path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "DBs" + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                }
                else
                {
                    path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + mainFolderPath + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                }
            }

            Console.Clear();
            Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop);

            AnsiConsole.Write(
            new FigletText("RBX Info")
            .Centered()
            .Color(Color.Green));

            var table = new Table();

            table.Title("[yellow]RBX Info[/]").Centered();
            table.AddColumn(new TableColumn(new Panel("Title")));
            table.AddColumn(new TableColumn(new Panel("Description"))).Centered();

            table.AddRow("[blue]CLI Version[/]", $"[green]{Globals.CLIVersion}[/]");
            table.AddRow("[blue]Network[/]", $"[green]{network}[/]");
            table.AddRow("[blue]Port[/]", $"[green]{Globals.Port}[/]");
            table.AddRow("[blue]OS[/]", $"[green]{osDesc}[/]");
            table.AddRow("[blue]Processor Architecture[/]", $"[green]{processArch}[/]");
            table.AddRow("[blue].Net Core[/]", $"[green]{netFramework}[/]");
            table.AddRow("[blue]External IP[/]", $"[green]{mostLikelyIP}[/]");
            table.AddRow("[blue]HD Wallet?[/]", $"[green]{Globals.HDWallet}[/]");
            table.AddRow("[blue]Folder Path[/]", $"[green]{path}[/]");
            table.AddRow("[blue]System Time[/]", $"[green]{DateTime.Now}[/]");
            table.AddRow("[blue]Timestamp[/]", $"[green]{TimeUtil.GetTime()}[/]");

            table.Border(TableBorder.Rounded);

            AnsiConsole.Write(table);

        }
        public static void PrintHelpMenu()
        {
            Console.Clear();
            Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop);

            AnsiConsole.Write(
            new FigletText("RBX Help")
            .Centered()
            .Color(Color.Green));

            var table = new Table();

            table.Title("[yellow]RBX Wallet Commands[/]").Centered();
            table.AddColumn(new TableColumn(new Panel("Command")));
            table.AddColumn(new TableColumn(new Panel("Description"))).Centered();


            table.AddRow("[blue]/help[/]", "[green]This will print out the RBX wallet help menu.[/]");
            table.AddRow("[blue]/info[/]", "[green]This will print out the RBX wallet client information.[/]");
            table.AddRow("[blue]/debug[/]", "[green]This will print out the debug information for the current state of the wallet.[/]");
            table.AddRow("[blue]/stopco[/]", "[green]This will stop the automatic printout of text in CLI.[/]");
            table.AddRow("[blue]/exit[/]", "[green]This will close the wallet.[/]");
            table.AddRow("[blue]/menu[/]", "[green]This will return you to the main menu[/]");
            table.AddRow("[blue]/clear[/]", "[green]This will clear the current console window.[/]");
            table.AddRow("[blue]/mempool[/]", "[green]This will print out the current state of the mempool.[/]");
            table.AddRow("[blue]/recp[/]", "[green]This will attempt to perform a reconnect to peers.[/]");
            table.AddRow("[blue]/optlog[/]", "[green]Turns on optional logging for adjudicators.[/]");
            table.AddRow("[blue]/beacon[/]", "[green]Starts the process for creating a beacon.[/]");
            table.AddRow("[blue]/switchbeacon[/]", "[green]This will turn a beacon on and off.[/]");
            table.AddRow("[blue]/unlock[/]", $"[green]This will unlock your wallet for {Globals.WalletUnlockTime} minutes.[/]");
            table.AddRow("[blue]/addpeer[/]", "[green]This will allow a user to add a peer manually.[/]");
            table.AddRow("[blue]/creatednr[/]", "[green]Creates an address domain name registrar.[/]");
            table.AddRow("[blue]/deletednr[/]", "[green]Deletes a domain name registrar.[/]");
            table.AddRow("[blue]/transferdnr[/]", "[green]transfers a domain name registrar.[/]");
            table.AddRow("[blue]/printkeys[/]", "[green]Prints all private keys associated to a wallet.[/]");
            table.AddRow("[blue]/encrypt[/]", "[green]Encrypts the wallets private keys.[/]");
            table.AddRow("[blue]/trillium[/]", "[green]This will let you execute Trillium code.[/]");
            table.AddRow("[blue]/val[/]", "[green]This will show you your current validator information.[/]");
            table.AddRow("[blue]/resetval[/]", "[green]Resets all validator information in databases.[/]");
            table.AddRow("[blue]1[/]", "[green]This will print out the Genesis block[/]");
            table.AddRow("[blue]2[/]", "[green]This will create a new account.[/]");
            table.AddRow("[blue]2hd[/]", "[green]This will create an HD wallet.[/]");
            table.AddRow("[blue]3[/]", "[green]This will restore an account with a provided key.[/]");
            table.AddRow("[blue]3hd[/]", "[green]Restores an HD wallet with a provided Mnemonic (12 or 24 words).[/]");
            table.AddRow("[blue]4[/]", "[green]This will start an RBX transactions for coins only.[/]");
            table.AddRow("[blue]5[/]", "[green]This will print out the most recent block synced to wallet.[/]");
            table.AddRow("[blue]6[/]", "[green]This will print out your most recent 10 transactions.[/]");
            table.AddRow("[blue]7[/]", "[green]This will print out your wallet accounts.[/]");
            table.AddRow("[blue]8[/]", "[green]This will start the masternode process.[/]");
            table.AddRow("[blue]9[/]", "[green]This will print out a specific block.[/]");
            table.AddRow("[blue]10[/]", "[green]This will turn the wallet API on and off.[/]");
            table.AddRow("[blue]11[/]", "[green]This will stop your masternode.[/]");
            table.AddRow("[blue]12[/]", "[green]Reserved command. Coming soon.[/]");
            table.AddRow("[blue]13[/]", "[green]This will also exit the wallet.[/]");

            table.Border(TableBorder.Rounded);

            AnsiConsole.Write(table);
        }

        private static void MainMenuReturn()
        {
            Console.WriteLine("Returning you to main menu in 3 seconds.");
            Console.WriteLine("3...");
            Thread.Sleep(1000);
            Console.WriteLine("2...");
            Thread.Sleep(1000);
            Console.WriteLine("1...");
            Thread.Sleep(1000);
            StartupService.MainMenu();
        }
    }
}
