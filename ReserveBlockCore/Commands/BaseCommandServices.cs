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
using Microsoft.AspNetCore.HttpOverrides;
using System.IO;
using System.Globalization;
using System.Numerics;
using ReserveBlockCore.EllipticCurve;
using System;

namespace ReserveBlockCore.Commands
{
    public class BaseCommandServices
    {
        public static async void UnlockWallet()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(Globals.WalletPassword))
                {
                    Console.WriteLine("Please type in password to unlock wallet.");
                    var password = await ReadLineUtility.ReadLine();
                    if (!string.IsNullOrWhiteSpace(password))
                    {
                        var passCheck = Globals.WalletPassword.ToDecrypt(password);
                        if (passCheck == password)
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
            catch (Exception ex) { }
            
        }
        public static async void PrintKeys()
        {
            var accounts = AccountData.GetAccounts();

            var accountList = accounts.Query().Where(x => true).ToEnumerable();

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

                foreach(var account in accountList)
                {
                    table.AddRow($"[blue]{account.Address}[/]", $"[green]{account.GetKey}[/]");
                }
  
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
            else if(Globals.IsWalletEncrypted == false)
            {
                AnsiConsole.MarkupLine("[red]******************************************WARNING******************************************[/]");
                AnsiConsole.MarkupLine("[yellow]****************************************PLEASE READ****************************************[/]");
                Console.WriteLine("You are about to encrypt your wallet. Please note this will encrypt ALL private keys currently in wallet and all future keys.");
                Console.WriteLine("If you forget this password there is no way to recover your keys. Please use this feature fully understanding this.");
                Console.WriteLine("This is a new wallet feature. It is recommended a non-encrypted version or private keys be backed up before starting this process.");
                Console.WriteLine("Please do not use the equal sign ('=') in your password.");
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
                        if(password.ToUnsecureString().Contains("="))
                        {
                            MainMenuReturn();
                            Console.WriteLine("Passwords may not contain an equal sign '='.");
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

        public static async Task FindTXByHash()
        {
            var coreCount = Environment.ProcessorCount;
            if (coreCount >= 4)
            {
                Console.WriteLine("Please enter the TX Hash you are looking for...");
                var txHash = Console.ReadLine();
                if (!string.IsNullOrEmpty(txHash))
                {
                    try
                    {
                        txHash = txHash.Replace(" ", "");//removes any whitespace before or after in case left in.
                        var blocks = BlockchainData.GetBlocks();
                        var height = Convert.ToInt32(Globals.LastBlock.Height);
                        bool resultFound = false;

                        var integerList = Enumerable.Range(0, height + 1);
                        Parallel.ForEach(integerList, new ParallelOptions { MaxDegreeOfParallelism = coreCount == 4 ? 2 : 4 }, (blockHeight, loopState) =>
                        {
                            var block = blocks.Query().Where(x => x.Height == blockHeight).FirstOrDefault();
                            if (block != null)
                            {
                                var txs = block.Transactions.ToList();
                                var result = txs.Where(x => x.Hash == txHash).FirstOrDefault();
                                if (result != null)
                                {
                                    resultFound = true;
                                    Console.WriteLine($"Hash Found in block {result.Height}");
                                    Console.WriteLine($"TXHash: {result.Hash}");
                                    Console.WriteLine($"To: {result.ToAddress}");
                                    Console.WriteLine($"From: {result.FromAddress}");
                                    Console.WriteLine($"Amount: {result.Amount} RBX");
                                    Console.WriteLine($"Fee {result.Fee} RBX");
                                    Console.WriteLine($"TX Rating {result.TransactionRating}");
                                    Console.WriteLine($"TX Type: {result.TransactionType}");
                                    Console.WriteLine($"Timestamp : {result.Timestamp}");
                                    Console.WriteLine($"Signature: {result.Signature}");
                                    Console.WriteLine($"----------------------------Data----------------------------");
                                    Console.WriteLine($"Data: {result.Data}");
                                    loopState.Break();

                                }
                            }
                        });

                        if (!resultFound)
                            Console.WriteLine("No transaction found with that hash.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error Performing Query: {ex.ToString()}");
                    }

                }
            }
            else
            {
                Console.WriteLine("The current system does not have enough physical/logical cores to safely run a query of this magnitude.");
                Console.WriteLine("To ensure wallet integrity this query was cancelled.");
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
                        var accounts = AccountData.GetAccounts();
                        if (accounts != null)
                        {
                            var account = accounts.Query().Where(x => x.Address != null).FirstOrDefault();

                            if (account == null)
                                break;

                            Globals.EncryptPassword = pwd;
                            await Task.Delay(200);
                            var privKey = account.GetKey;
                            BigInteger b1 = BigInteger.Parse(privKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
                            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

                            var randString = RandomStringUtility.GetRandomString(8);

                            var signature = SignatureService.CreateSignature(randString, privateKey, account.PublicKey);
                            var sigVerify = SignatureService.VerifySignature(account.Address, randString, signature);

                            if (sigVerify)
                            {
                                Console.WriteLine($"Password has been entered and will be stored for {Globals.PasswordClearTime} minutes.");
                                break;
                            }
                            else
                            {
                                Globals.EncryptPassword.Dispose();
                                Globals.EncryptPassword = new SecureString();
                                Console.WriteLine("Failed to decrypt wallet. Password was incorrect!");
                                break;
                            }
                        }
                        else
                        {
                            break;
                        }
                        
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
            }
            else
            {
                Console.WriteLine("Wallet already has password for decryption.");
            }
        }

        public static async void StartMother()
        {
            var mom = Mother.GetMother();
            bool firstSave = false;
            if(mom == null)
            {
                Console.WriteLine("Please enter your name mom...");
                var momName = Console.ReadLine();
                if (!string.IsNullOrEmpty(momName))
                {
                    Console.WriteLine("Please enter your connection password mom...");
                    var password = Console.ReadLine();
                    Console.WriteLine("Please confirm your connection password mom...");
                    var passwordConfirm = Console.ReadLine();
                    if (password == passwordConfirm)
                    {
                        Mother nMom = new Mother
                        {
                            Name = momName,
                            Password = password,
                            StartDate = DateTime.Now,
                        };

                        Mother.SaveMother(nMom);
                        firstSave = true;
                        Console.WriteLine("Thank you mom. Please restart wallet to start service. Type /mother to start the service up.");

                    }
                    else
                    {
                        Console.WriteLine("Passwords did not match mom!");
                    }
                }
                else
                {
                    Console.WriteLine("Name cannot be blank mom!");
                }
            }

            //start mother program here!
            if(!firstSave)
                await Mothering.Mother.StartMotherProgram();
        }

        public static async void SetTrilliumOutput()
        {
            Globals.ShowTrilliumOutput ^= true;
            Globals.ShowTrilliumDiagnosticBag ^= true;

            Console.WriteLine($"Trillium Output Set to: {Globals.ShowTrilliumOutput}");
            Console.WriteLine($"Trillium Diagnostic Output Set to: {Globals.ShowTrilliumDiagnosticBag}");
        }

        public static async void ResetValidator()
        {
            Globals.ValidatorAddress = "";
            var result = await ValidatorService.ValidatorErrorReset();
            if (result)
            {
                foreach(var node in Globals.AdjNodes.Values)
                {
                    node.LastTaskErrorCount = 0;
                    node.LastTaskError = false;
                    node.LastWinningTaskError = false;
                }
                    
                ValidatorLogUtility.Log("ValidatorErrorReset() called manually. Results: Success!", "Program.validatorListCheckTimer_Elapsed()");
            }
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
                                FailCount = 0,
                                BanCount= 0
                            };

                            peers.InsertSafe(nPeer);

                            StartupService.MainMenu();
                            Console.WriteLine("Success! Peer added.");
                            Console.WriteLine("Returned to main menu...");
                        }
                        else
                        {
                            var peerRec = peers.FindOne(x => x.PeerIP == peer);
                            peerRec.IsOutgoing = !peerRec.IsOutgoing;
                            peers.UpdateSafe(peerRec);

                            StartupService.MainMenu();
                            Console.WriteLine("Peer already exist...");
                            Console.WriteLine($"Peer Outgoing has been set to {peerRec.IsOutgoing}");
                            Console.WriteLine("Returned to main menu...");
                        }
                    }
                    else
                    {                        
                        StartupService.MainMenu();
                        Console.WriteLine("Failed to add. Please input a valid IP...");
                        Console.WriteLine("Returned to main menu...");
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Unexpected Error. Error Message: {ex.ToString()}");
                    Console.WriteLine("Type /menu to return to main menu.");
                }
            }
        }

        public static async void BanPeer()
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
                        BanService.BanPeer(peer, "Manual Ban", "BaseCommandService.BanPeer()");
                        StartupService.MainMenu();
                        Console.WriteLine("Success! Peer has been Banned.");
                        Console.WriteLine("Returned to main menu...");
                    }
                    else
                    {
                        StartupService.MainMenu();
                        Console.WriteLine("Failed to process. Please input a valid IP...");
                        Console.WriteLine("Returned to main menu.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected Error. Error Message: {ex.ToString()}");
                    Console.WriteLine("Type /menu to return to main menu.");
                }
            }
        }

        public static async void UnbanPeer()
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
                        BanService.UnbanPeer(peer);
                        StartupService.MainMenu();
                        Console.WriteLine("Success! Peer has been unbanned.");
                        Console.WriteLine("Returned to main menu...");
                    }
                    else
                    {
                        StartupService.MainMenu();
                        Console.WriteLine("Failed to process. Please input a valid IP...");
                        Console.WriteLine("Returned to main menu...");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected Error. Error Message: {ex.ToString()}");
                    Console.WriteLine("Type /menu to return to main menu.");
                }
            }
        }

        public static async Task ResyncBlocks()
        {
            Console.WriteLine("Resync Blocks? y/n");
            var reconnect = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(reconnect))
            {
                if (reconnect == "y")
                {
                    await BlockDownloadService.GetAllBlocks();
                }
                else
                {
                    MainMenuReturn();
                }
            }
        }

        public static async Task BlockDetails()
        {
            try
            {
                Console.WriteLine("Please enter the block you want all details on.");
                var blockStr = await ReadLineUtility.ReadLine();
                if (!string.IsNullOrWhiteSpace(blockStr))
                {
                    int.TryParse(blockStr, out var blockNumber);

                    if(blockNumber > 0)
                    {
                        var block = BlockchainData.GetBlockByHeight(blockNumber);

                        if (block != null)
                        {
                            var blockLocalTime = TimeUtil.ToDateTime(block.Timestamp);
                            var txList = block.Transactions.ToList();
                            var blockTable = new Table();
                            blockTable.Title("[yellow]Block Info[/]").Centered();
                            // Add some columns
                            blockTable.AddColumn(new TableColumn(new Panel($"Block {block.Height}")));
                            blockTable.AddColumn(new TableColumn(new Panel("Details")));

                            blockTable.AddRow("[blue]Version[/]", $"[green]{block.Version}[/]");
                            blockTable.AddRow("[blue]Previous Hash[/]", $"[green]{block.PrevHash}[/]");
                            blockTable.AddRow("[blue]Hash[/]", $"[green]{block.Hash}[/]");
                            blockTable.AddRow("[blue]Merkle Root[/]", $"[green]{block.MerkleRoot}[/]");
                            blockTable.AddRow("[blue]State Root[/]", $"[green]{block.StateRoot}[/]");
                            blockTable.AddRow("[blue]Timestamp[/]", $"[green]{block.Timestamp} - Local: {blockLocalTime}[/]");
                            blockTable.AddRow("[blue]Validator[/]", $"[green]{block.Validator}[/]");
                            blockTable.AddRow("[blue]Number of Tx(s)[/]", $"[green]{block.NumberOfTransactions}[/]");
                            blockTable.AddRow("[blue]Size[/]", $"[green]{block.Size}[/]");
                            blockTable.AddRow("[blue]Craft Time[/]", $"[green]{block.BCraftTime}[/]");
                            blockTable.AddRow("[blue]Chain Ref[/]", $"[green]{block.ChainRefId}[/]");

                            blockTable.Border(TableBorder.Rounded);

                            AnsiConsole.Write(blockTable);

                            var txTable = new Table();
                            txTable.Title("[yellow]Transaction Info[/]").Centered();
                            // Add some columns
                            txTable.AddColumn(new TableColumn(new Panel("Hash")));
                            txTable.AddColumn(new TableColumn(new Panel("From")));
                            txTable.AddColumn(new TableColumn(new Panel("To")));
                            txTable.AddColumn(new TableColumn(new Panel("Amount")));
                            txTable.AddColumn(new TableColumn(new Panel("Fee")));
                            txTable.AddColumn(new TableColumn(new Panel("Timestamp")));
                            txTable.AddColumn(new TableColumn(new Panel("Transaction Type")));
                            txTable.AddColumn(new TableColumn(new Panel("Transaction Rating")));

                            txList.ForEach(x => {
                                txTable.AddRow($"{x.Hash}", $"[blue]{x.FromAddress}[/]", $"[red]{x.ToAddress}[/]", $"[green]{x.Amount}[/]",
                                    $"{x.Fee}", $"[yellow]{x.Timestamp}[/]", $"{x.TransactionType}", $"{x.TransactionRating}");
                            });

                            txTable.Border(TableBorder.Rounded);

                            AnsiConsole.Write(txTable);

                        }
                        else
                        {
                            ConsoleWriterService.Output($"Could not find block with height: {blockStr}");
                        }
                    }
                    
                }
            }
            catch(Exception ex) { }
        }

        public static async void CreateBeacon()
        {
            if(Globals.SelfBeacon != null)
            {
                var selfBeacon = Globals.SelfBeacon;
                Console.WriteLine("You are already a beacon! Please use /switchbeacon if you wish to turn beacon feature off.");
                Console.WriteLine($"|=================================~Beacon Info~=================================|");
                Console.WriteLine($"| Beacon UID: {selfBeacon.BeaconUID}");
                Console.WriteLine($"|-------------------------------------------------------------------------------|");
                Console.WriteLine($"| Beacon IP: {selfBeacon.IPAddress}");
                Console.WriteLine($"|-------------------------------------------------------------------------------|");
                Console.WriteLine($"| Beacon Name: {selfBeacon.Name}");
                Console.WriteLine($"|-------------------------------------------------------------------------------|");
                Console.WriteLine($"| Private Beacon? {selfBeacon.IsPrivateBeacon}");
                Console.WriteLine($"|-------------------------------------------------------------------------------|");
                Console.WriteLine($"| File Cache Period: {selfBeacon.FileCachePeriodDays}");
                Console.WriteLine($"|-------------------------------------------------------------------------------|");
                Console.WriteLine($"| Auto Delete? {selfBeacon.AutoDeleteAfterDownload}");
                Console.WriteLine($"|-------------------------------------------------------------------------------|");
                Console.WriteLine($"| Active? {selfBeacon.SelfBeaconActive}");
                Console.WriteLine($"|===============================================================================|");
            }
            else
            {
                Console.WriteLine("Please give your beacon a name...");
                var name = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    Console.WriteLine("Do you want to auto delete file cache after receiver downloads? (y for yes and n for no)");
                    var autoDeleteString = Console.ReadLine();
                    bool autoDelete = false;
                    if (!string.IsNullOrWhiteSpace(autoDeleteString))
                    {
                        if (autoDeleteString == "y")
                            autoDelete = true;
                    }

                    Console.WriteLine("Do you want this beacon to be private meaning only you can upload to it? (y for yes and n for no)");
                    var isPrivateString = Console.ReadLine();
                    bool isPrivate = false;
                    if (!string.IsNullOrWhiteSpace(isPrivateString))
                    {
                        if (isPrivateString == "y")
                            isPrivate = true;
                    }

                    Console.WriteLine("Do you want to cache files for a limited time? Please input a number. (0 is infinite, 5 is 5 days, 10 is 10 days, etc.)");
                    var cachePeriodString = Console.ReadLine();
                    int fileCachePeriod = 0;
                    if (!string.IsNullOrWhiteSpace(cachePeriodString))
                    {
                        int.TryParse(cachePeriodString, out fileCachePeriod);
                    }

                    var ip = P2PClient.MostLikelyIP();

                    if (ip == "NA")
                    {
                        Console.WriteLine("Could not get external IP. Please ensure you are connected to peers and that you are not blocking ports.");
                    }

                    var bUID = Guid.NewGuid().ToString().Substring(0, 12).Replace("-", "") + ":" + TimeUtil.GetTime().ToString();

                    BeaconInfo.BeaconInfoJson beaconLoc1 = new BeaconInfo.BeaconInfoJson
                    {
                        IPAddress = ip,
                        Port = Globals.Port + 20000,
                        Name = name,
                        BeaconUID = bUID
                    };

                    var beaconLocJson1 = JsonConvert.SerializeObject(beaconLoc1);

                    Beacons beacon = new Beacons
                    {
                        IPAddress = ip,
                        Name = name,
                        Port = Globals.Port + 20000,
                        BeaconUID = bUID,
                        AutoDeleteAfterDownload = autoDelete,
                        FileCachePeriodDays = fileCachePeriod,
                        IsPrivateBeacon = isPrivate,
                        SelfBeacon = true,
                        SelfBeaconActive = true,
                        BeaconLocator = beaconLocJson1.ToBase64(),
                    };

                    var result = Beacons.SaveBeacon(beacon);

                    if (result)
                    {
                        Console.WriteLine("Beacon Inserted. Please restart wallet for beacon to activate.");
                        await StartupService.SetSelfBeacon();
                        Globals.Beacons[beacon.IPAddress] = beacon;
                    }

                    Console.WriteLine(result);
                }
            }
        }

        public static async void SwitchBeaconState()
        {
            var result = Beacons.SetBeaconActiveState();

            if (result == null)
            {
                Console.WriteLine("Error turning beacon on/off");
            }
            else
            {
                var beaconState = result.Value ? "on" : "off";
                Console.WriteLine($"Beacon has been turned {beaconState}.");
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
                    Globals.HDWallet = mnemonic.Item1;

                    return mnemonic.Item2;
                }
                else if(strengthStr == "24")
                {
                    var strength = Convert.ToInt32(strengthStr);
                    var mnemonic = HDWallet.HDWalletData.CreateHDWallet(strength, BIP39Wordlist.English);
                    Globals.HDWallet = mnemonic.Item1;

                    return mnemonic.Item2;
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
                    var isValidating = Globals.ValidatorReceiving && Globals.ValidatorSending ? "[green]Yes[/]" : "[red]No[/]";
                    var isValidatingSending = Globals.ValidatorSending ? "[green]Yes[/]" : "[red]No[/]";
                    var isValidatingReceiving = Globals.ValidatorReceiving ? "[green]Yes[/]" : "[red]No[/]";
                    Console.WriteLine($"Validator Name: {validator.UniqueName}");
                    Console.WriteLine($"Validator Address: {validator.Address}");
                    Console.WriteLine($"Validator Amount: {account.Balance}");
                    AnsiConsole.MarkupLine($"Validating? {isValidating}");
                    AnsiConsole.MarkupLine($"Validator Sending? {isValidatingSending}");
                    AnsiConsole.MarkupLine($"Validator Receiving? {isValidatingReceiving}");


                    foreach (var node in Globals.AdjNodes.Values)
                    {
                        if(node.IsConnected)
                        {
                            AnsiConsole.MarkupLine($"Last Task Received Time: [yellow]{node.LastTaskResultTime}[/] from [purple]{node.Address}[/]");
                            AnsiConsole.MarkupLine($"Last Task Sent Time: [yellow]{node.LastTaskSentTime}[/] from [purple]{node.Address}[/]");
                        }
                        
                    }
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

        public static async Task AdjudicatorInfo()
        {
            var taskSelectedNumbersV3 = Globals.TaskSelectedNumbersV3.Values.ToList();

            if (Globals.AdjudicateAccount == null)
            {
                var consensusNodes = Globals.AdjNodes.Values.ToList();
                if (consensusNodes.Count() > 0)
                {
                    ConsoleWriterService.Output("*******************************Consensus Nodes*******************************");
                    foreach (var cNode in consensusNodes)
                    {
                        var line = $"IP: {cNode.IpAddress} | Address: {cNode.Address} | IsConnected? {cNode.IsConnected}";
                        ConsoleWriterService.Output(line);
                    }
                    ConsoleWriterService.Output("******************************************************************************");
                }
                else
                {
                    ConsoleWriterService.Output("Empty");
                }
            }
            else
            {
                var adjConsensusNodes = Globals.Nodes.Values.ToList();
                var Now = TimeUtil.GetMillisecondTime();
                if (adjConsensusNodes.Count() > 0)
                {
                    ConsoleWriterService.Output("*******************************Consensus Nodes*******************************");
                    foreach (var cNode in adjConsensusNodes)
                    {
                        var line = $"IP: {cNode.NodeIP} | Address: {cNode.Address} | IsConnected? {cNode.IsConnected} ({Now - cNode.LastMethodCodeTime < ConsensusClient.HeartBeatTimeout})";
                        ConsoleWriterService.Output(line);
                    }
                    ConsoleWriterService.Output("******************************************************************************");
                }
                else
                {
                    ConsoleWriterService.Output("Empty");
                }
            }

            if(taskSelectedNumbersV3.Count() > 0)
            {
                ConsoleWriterService.Output("*******************************Task Answers V3********************************");
                foreach (var taskNum in taskSelectedNumbersV3)
                {
                    var taskLine = $"Address: {taskNum.RBXAddress} |  IP Address: {taskNum.IPAddress} | Answer: {taskNum.Answer}";
                    ConsoleWriterService.Output(taskLine);
                }
                ConsoleWriterService.Output("******************************************************************************");
            }
            else
            {
                ConsoleWriterService.Output("Empty 2");
            }
        }

        public static async Task BenchIP()
        {
            var benches = AdjBench.GetBench().FindAll().ToList();
            if(benches.Count == 0)
            {
                Console.WriteLine("The bench database is empty.");
                return;
            }

            var count = 0;
            benches.ToList().ForEach(x => {                
                Console.WriteLine("********************************************************************");
                Console.WriteLine("Please choose an address below to update the ip address for.");

                Console.WriteLine("\n #" + count.ToString());
                Console.WriteLine("\nAddress :\n{0}", x.RBXAddress);
                Console.WriteLine("\nIP Address:\n{0}", x.IPAddress);
                count++;
            });

            int index = 0;
            var benchChoice = await ReadLineUtility.ReadLine();
            while (!int.TryParse(benchChoice, out index))
            {
                Console.WriteLine("You must choose an address. Type a number from above and press enter please.");
                benchChoice = await ReadLineUtility.ReadLine();
            }
            var bench = benches[index];
            Console.WriteLine("********************************************************************");
            Console.WriteLine("The chosen bench address is:");
            string benchAddress = bench.RBXAddress;
            Console.WriteLine(benchAddress);
            Console.WriteLine("Are you sure you want to update the ip address for this address? (Type 'y' for yes and 'n' for no.)");
            var confirmChoice = await ReadLineUtility.ReadLine();

            if (confirmChoice == null)
            {
                Console.WriteLine("You must only type 'y' or 'n'. Please choose the correct option. (Type 'y' for yes and 'n' for no.)");
                Console.WriteLine("Returning you to main menu...");
                await Task.Delay(5000);
                StartupService.MainMenu();
            }
            else if (confirmChoice.ToLower() == "n")
            {
                Console.WriteLine("Returning you to main menu in 3 seconds...");
                await Task.Delay(3000);
                StartupService.MainMenu();
            }
            else if (confirmChoice.ToLower() == "y")
            {
                Console.Clear();
                Console.WriteLine("Please type an ip address");
                var ipAddress = await ReadLineUtility.ReadLine();
                
                if (string.IsNullOrWhiteSpace(ipAddress) || ipAddress.Where(x => x == '.').Count() != 3)
                {
                    Console.WriteLine("Not a valid ip address");
                    StartupService.MainMenu();                  
                    Console.WriteLine("Returned to main menu.");
                }
                else
                {
                    bench.IPAddress = ipAddress;
                    AdjBench.GetBench().UpdateSafe(bench);
                    if (Globals.AdjBench.TryGetValue(bench.RBXAddress, out var cacheBench))
                        cacheBench.IPAddress = ipAddress;
                    StartupService.MainMenu();
                    Console.WriteLine("Ip updated.");
                }
            }
            else
            {
                StartupService.MainMenu();
                Console.WriteLine("Unexpected input detected.");
                Console.WriteLine("Returned to main menu.");
            }
        }

        public static async Task AddSigner()
        {
            var benches = AdjBench.GetBench().FindAll().ToList();
            if (benches.Count == 0)
            {
                Console.WriteLine("The bench database is empty.");
                return;
            }

            var count = 0;
            benches.ToList().ForEach(x => {
                Console.WriteLine("********************************************************************");
                Console.WriteLine("Please choose an address below to add as a signer.");

                Console.WriteLine("\n #" + count.ToString());
                Console.WriteLine("\nAddress :\n{0}", x.RBXAddress);
                Console.WriteLine("\nIP Address:\n{0}", x.IPAddress);
                count++;
            });

            int index = 0;
            var benchChoice = await ReadLineUtility.ReadLine();
            while (!int.TryParse(benchChoice, out index))
            {
                Console.WriteLine("You must choose an address. Type a number from above and press enter please.");
                benchChoice = await ReadLineUtility.ReadLine();
            }
            var bench = benches[index];
            Console.WriteLine("********************************************************************");
            Console.WriteLine("The chosen bench address is:");
            string benchAddress = bench.RBXAddress;
            Console.WriteLine(benchAddress);

            Console.WriteLine("Specify a start height");
            long height = 0;
            var StartHeight = await ReadLineUtility.ReadLine();
            if (string.IsNullOrWhiteSpace(StartHeight) || long.TryParse(StartHeight, out height))
            {
                Console.WriteLine("Invalid height.");
                return;
            }

            var newSigner = new Signer
            {
                Address = benchAddress,
                StartHeight = height,
                EndHeight = null
            };
            Signer.GetSigners().InsertSafe(newSigner);
            Signer.Signers[(benchAddress, height)] = null;

            Console.WriteLine("Signer Added.");
        }

        public static async Task RemoveSigner()
        {
            var CurrentSigners = Signer.Signers.Where(x => x.Value == null).ToList();
            if (CurrentSigners.Count == 0)
            {
                Console.WriteLine("There are no current signers.");
                return;
            }

            var count = 0;
            CurrentSigners.ToList().ForEach(x => {
                Console.WriteLine("********************************************************************");
                Console.WriteLine("Please choose an address below to add an end height to.");

                Console.WriteLine("\n #" + count.ToString());
                Console.WriteLine("\nAddress :\n{0}", x.Key.Address);
                Console.WriteLine("\nStart Height:\n{0}", x.Key.StartHeight);
                count++;
            });

            int index = 0;
            var signerChoice = await ReadLineUtility.ReadLine();
            while (!int.TryParse(signerChoice, out index))
            {
                Console.WriteLine("You must choose an address. Type a number from above and press enter please.");
                signerChoice = await ReadLineUtility.ReadLine();
            }
            var signer = CurrentSigners[index];
            Console.WriteLine("********************************************************************");
            Console.WriteLine("The chosen address is:");
            string Address = signer.Key.Address;
            Console.WriteLine(Address);
            
            Console.WriteLine("Specify a end height");
            long height = 0;
            var EndHeight = await ReadLineUtility.ReadLine();
            if (string.IsNullOrWhiteSpace(EndHeight) || long.TryParse(EndHeight, out height))
            {
                Console.WriteLine("Invalid height.");
                return;
            }
            var dbSigner = Signer.GetSigners().FindOne(x => x.Address == Address && x.StartHeight == signer.Key.StartHeight);
            dbSigner.EndHeight = height;            
            Signer.GetSigners().UpdateSafe(dbSigner);
            Signer.Signers[signer.Key] = height;

            Console.WriteLine("Signer Updated.");
        }

        public static async Task PeerInfo()
        {
            var peerNodes = Globals.Nodes.Values.ToList();

            if (peerNodes.Count() > 0)
            {
                ConsoleWriterService.Output("*********************************************Peer Nodes*********************************************");
                foreach (var node in peerNodes)
                {
                    var line = $"IP: {node.NodeIP} | BlockHeight: {node.NodeHeight} | IsConnected? {node.IsConnected} | Latency: {node.NodeLatency} ";
                    ConsoleWriterService.Output(line);
                }
                ConsoleWriterService.Output("****************************************************************************************************");
            }
            else
            {
                ConsoleWriterService.Output("Empty");
            }

        }
        public static async Task ConsensusNodeInfo()
        {
            var conState = ConsensusServer.GetState();
            Console.WriteLine("*******************************Consensus State********************************");
            
            var conStateLine = $"Next Height: {Globals.LastBlock.Height + 1} | Status: {conState.Status} | Answer: {conState.Answer} | Method Code: {conState.MethodCode}";
            Console.WriteLine(conStateLine);
            LogUtility.LogQueue(conStateLine, "", "cinfo.txt", true);

            Console.WriteLine("******************************************************************************");

            var conMessage = string.Join("\r\n", ConsensusServer.Messages.Select(x => x.Value.Select(y => x.Key.Height + " " + x.Key.MethodCode + " " + y.Key + " " + y.Value.Message + " " + y.Value.Signature))
                .SelectMany(x => x));
            LogUtility.LogQueue(conMessage, "", "cinfo.txt", true);

            Console.WriteLine("*****************************Consensus Messages*******************************");

            Console.WriteLine(conMessage);            

            Console.WriteLine("******************************************************************************");

            var hashMessage = string.Join("\r\n", ConsensusServer.Hashes.Select(x => x.Value.Select(y => x.Key.Height + " " + x.Key.MethodCode + " " + y.Key + " " + y.Value.Hash + " " + y.Value.Signature))
                            .SelectMany(x => x));
            LogUtility.LogQueue(hashMessage, "", "cinfo.txt", true);

            Console.WriteLine("*****************************Consensus Hashes*******************************");

            Console.WriteLine(hashMessage);

            Console.WriteLine("******************************************************************************");

            var addressesToWaitFor = ConsensusClient.AddressesToWaitFor(Globals.LastBlock.Height + 1, conState.MethodCode, ConsensusClient.HeartBeatTimeout).ToArray();

            LogUtility.LogQueue(JsonConvert.SerializeObject(addressesToWaitFor), "", "cinfo.txt", true);
            Console.WriteLine("*****************************Addresses To Wait For*******************************");

            Console.WriteLine(JsonConvert.SerializeObject(addressesToWaitFor));

            Console.WriteLine("******************************************************************************");

            Console.WriteLine("*****************************Consensus Dump*******************************");

            Console.WriteLine(JsonConvert.SerializeObject(JsonConvert.SerializeObject(Globals.ConsensusDump)));

            LogUtility.LogQueue(JsonConvert.SerializeObject(Globals.ConsensusDump), "", "cinfo.txt", true);
            Console.WriteLine("******************************************************************************");

            Console.WriteLine("*****************************Node Dump*******************************");

            Console.WriteLine("Now: " + TimeUtil.GetMillisecondTime() + "\r\n");

            LogUtility.LogQueue(JsonConvert.SerializeObject(JsonConvert.SerializeObject(Globals.Nodes.Values)), "", "cinfo.txt", true);
            Console.WriteLine(JsonConvert.SerializeObject(JsonConvert.SerializeObject(Globals.Nodes.Values)));

            Console.WriteLine("******************************************************************************");
        }


        public static async Task<string> CreateDnr()
        {
            var output = "";
            Console.WriteLine("Please select the wallet you'd like to create a domain name registration for...");
            var accountList = AccountData.GetAccountsWithBalanceForAdnr();
            var accountNumberList = new Dictionary<string, Account>();
            if (accountList.Count() > 0)
            {
                try
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
                    string? walletChoice = "";
                    walletChoice = await ReadLineUtility.ReadLine();

                    if (!string.IsNullOrEmpty(walletChoice))
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
                            if (adnrCheck != null)
                            {
                                Console.WriteLine($"This address already has a DNR associated with it: {adnrCheck.Name}");
                                return output;
                            }
                            bool nameFound = true;
                            while (nameFound)
                            {
                                Console.WriteLine($"You have selected the following wallet: {address}");
                                Console.WriteLine("Please enter the name you'd like for this wallet. Ex: (cryptoinvestor1) Please note '.rbx' will automatically be added. DO NOT INCLUDE IT.");
                                Console.WriteLine("type exit to leave this menu.");
                                var name = await ReadLineUtility.ReadLine();
                                if (!string.IsNullOrWhiteSpace(name) && name != "exit")
                                {
                                    var nameCharCheck = Regex.IsMatch(name, @"^[a-zA-Z0-9]+$");
                                    if (!nameCharCheck)
                                    {
                                        Console.WriteLine("-->ERROR! A DNR may only contain letters and numbers. ERROR!<--");
                                    }
                                    else
                                    {
                                        var nameRBX = name.ToLower() + ".rbx";
                                        var nameCheck = adnr.FindOne(x => x.Name == nameRBX);
                                        if (nameCheck == null)
                                        {
                                            nameFound = false;
                                            Console.WriteLine("Are you sure you want to create this DNR? 'y' for yes, 'n' for no.");
                                            var response = await ReadLineUtility.ReadLine();
                                            if (!string.IsNullOrWhiteSpace(response))
                                            {
                                                if (response.ToLower() == "y")
                                                {
                                                    Console.WriteLine("Sending Transaction now.");
                                                    var result = await Adnr.CreateAdnrTx(address, name);
                                                    if (result.Item1 != null)
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
                catch(Exception ex)
                {
                    output = "DNR Request has been cancelled.";
                    return output;
                }
                
            }
            else
            {
                Console.WriteLine("No eligible accounts were detected. You must have an account with at least 1 RBX to create a dnr.");
                return output;
            }

        }

        public static async Task SyncTreis()
        {
            Console.WriteLine("Beginning Trei Sync");
            await StateTreiSyncService.SyncAccountStateTrei();
            Console.WriteLine("Trei Sync has completed. Please check error log for report on balances updated.");
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
                var transactionsList = transactions.Query().OrderByDescending(x => x.Timestamp).ToEnumerable().Take(10);
                foreach(var tx in transactionsList)
                {
                    var txStr = $"TxId: {tx.Hash} - From: {tx.FromAddress} - To: {tx.ToAddress} - Amount: {tx.Amount} - Time: {tx.Timestamp}";
                    Console.WriteLine(txStr);
                }
                
            }
        }

        public static void RPS()
        {
            Random random = new();
            int w1 = 0;
            int d1 = 0;
            int l1 = 0;
            while (true)
            {
                Console.Clear();
                Console.WriteLine(("Um9jaywgUGFwZXIsIFNjaXNzb3Jz").ToStringFromBase64());
                Console.WriteLine();
            GetInput:
                Console.Write(("Q2hvb3NlIFtyXW9jaywgW3BdYXBlciwgW3NdY2lzc29ycywgb3IgW2VdeGl0Og==").ToStringFromBase64());
                int pm;

                switch (Console.ReadLine()!.ToLower())
                {
                    case "r": pm = 0; break;
                    case "p": pm = 1; break;
                    case "s": pm = 2; break;
                    case "e": Console.Clear(); return;
                    default: Console.WriteLine(("SW52YWxpZCBJbnB1dC4gVHJ5IEFnYWluLi4u").ToStringFromBase64()); goto GetInput;
                }
                int cm = (int)random.Next(3);
                Console.WriteLine(("VGhlIGNvbXB1dGVyIGNob3NlIA==").ToStringFromBase64() + 
                    (cm == 0 ? ("cm9jaw==").ToStringFromBase64() : 
                    cm == 1 ? ("cGFwZXI=").ToStringFromBase64() : 
                    ("c2Npc3NvcnM=").ToStringFromBase64()) + ".");
                switch (pm, cm)
                {
                    case (0, 1):
                    case (1, 2):
                    case (2, 0):
                        Console.WriteLine(("WW91IGxvc2Uu").ToStringFromBase64());
                        l1++;
                        break;
                    case (0, 2):
                    case (1, 0):
                    case (2, 1):
                        Console.WriteLine(("WW91IHdpbi4=").ToStringFromBase64());
                        w1++;
                        break;
                    default:
                        Console.WriteLine(("VGhpcyBnYW1lIHdhcyBhIGRyYXcu").ToStringFromBase64());
                        d1++;
                        break;
                }
                Console.WriteLine(("U2NvcmU6IA==").ToStringFromBase64() + 
                    w1 + 
                    ("IHdpbnMsIA==").ToStringFromBase64() + 
                    l1 +
                    ("IGxvc3Nlcywg").ToStringFromBase64() + 
                    d1 +
                    ("IGRyYXdz").ToStringFromBase64());
                Console.WriteLine(("UHJlc3MgRW50ZXIgVG8gQ29udGludWUuLi4=").ToStringFromBase64());
                Console.ReadLine();
            }
        }

        public static void PrintBlock()
        {
            try
            {
                Console.WriteLine("Please enter the block height");
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
                ConsoleWriterService.Output($"Unexpected error. Please try again. Error Message: {ex.ToString()}");
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
            var programPath = Directory.GetCurrentDirectory();
            var refProgramPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string strWorkPath = Path.GetDirectoryName(refProgramPath);
            

            var threadCount = Environment.ProcessorCount;

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
            table.AddRow("[blue]Thread Count[/]", $"[green]{threadCount}[/]");
            table.AddRow("[blue].Net Core[/]", $"[green]{netFramework}[/]");
            table.AddRow("[blue]External IP[/]", $"[green]{mostLikelyIP}[/]");
            table.AddRow("[blue]HD Wallet?[/]", $"[green]{Globals.HDWallet}[/]");
            table.AddRow("[blue]Program Path[/]", $"[green]{strWorkPath}[/]");
            table.AddRow("[blue]Database Folder Path[/]", $"[green]{path}[/]");
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
            table.AddRow("[blue]/update[/]", "[green]This will download latest CLI client to RBX folder location under 'Download'.[/]");
            table.AddRow("[blue]/mempool[/]", "[green]This will print out the current state of the mempool.[/]");
            table.AddRow("[blue]/recp[/]", "[green]This will attempt to perform a reconnect to peers.[/]");
            table.AddRow("[blue]/optlog[/]", "[green]Turns on optional logging for adjudicators.[/]");
            table.AddRow("[blue]/beacon[/]", "[green]Starts the process for creating a beacon.[/]");
            table.AddRow("[blue]/switchbeacon[/]", "[green]This will turn a beacon on and off.[/]");
            table.AddRow("[blue]/unlock[/]", $"[green]This will unlock your wallet for {Globals.WalletUnlockTime} minutes.[/]");
            table.AddRow("[blue]/addpeer[/]", "[green]This will allow a user to add a peer manually.[/]");
            table.AddRow("[blue]/banpeer[/]", "[green]Bans a peer by their IP address.[/]");
            table.AddRow("[blue]/unbanpeer[/]", "[green]Unbans a peer by their IP address.[/]");
            table.AddRow("[blue]/creatednr[/]", "[green]Creates an address domain name registrar.[/]");
            table.AddRow("[blue]/deletednr[/]", "[green]Deletes a domain name registrar.[/]");
            table.AddRow("[blue]/transferdnr[/]", "[green]transfers a domain name registrar.[/]");
            table.AddRow("[blue]/printkeys[/]", "[green]Prints all private keys associated to a wallet.[/]");
            table.AddRow("[blue]/encrypt[/]", "[green]Encrypts the wallets private keys.[/]");
            table.AddRow("[blue]/decrypt[/]", "[green]Decrypts the wallets private keys.[/]");
            table.AddRow("[blue]/setto[/]", "[green]This will let you set output for trillium to show on screen.[/]");
            table.AddRow("[blue]/trillium[/]", "[green]This will let you execute Trillium code.[/]");
            table.AddRow("[blue]/val[/]", "[green]This will show you your current validator information.[/]");
            table.AddRow("[blue]/resetval[/]", "[green]Resets all validator and reconnects them.[/]");
            table.AddRow("[blue]/findtx[/]", "[green]This is a heavy query to find a specific TX in all blocks.[/]");
            table.AddRow("[blue]/vote[/]", "[green]This will start the voting program.[/]");
            table.AddRow("[blue]/resblocks[/]", "[green]Resyncs the blocks to ensure you are at max height.[/]");
            table.AddRow("[blue]/mother[/]", "[green]This will create a mother host.[/]");
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
            table.AddRow("[blue]13[/]", "[green]This will open the voting program.[/]");
            table.AddRow("[blue]14[/]", "[green]This is the proper way to shutdown wallet with this command.[/]");

            table.Border(TableBorder.Rounded);

            AnsiConsole.Write(table);
        }

        private static async void MainMenuReturn()
        {
            var delay = Task.Delay(3000);
            Console.WriteLine("Returning you to main menu in 3 seconds.");
            await delay;
            StartupService.MainMenu();
        }
    }
}
