using ImageMagick;
using NBitcoin;
using ReserveBlockCore.Bitcoin.Models;
using ReserveBlockCore.Bitcoin.Services;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using Spectre.Console;
using System.Text.RegularExpressions;

namespace ReserveBlockCore.Bitcoin
{
    public class BitcoinCommand
    {
        public static void CreateAddress()
        {
            var account = BitcoinAccount.CreateAddress();
            BitcoinAccount.PrintAccountInfo(account);
        }

        public static async Task ImportAddress()
        {
            try
            {
                Console.WriteLine("Please paste in your private key");
                var privateKey = Console.ReadLine();

                if (privateKey == null)
                {
                    Console.WriteLine("Key cannot be blank.");
                    await Bitcoin.BitcoinMenu();
                    return;
                }
                if (privateKey?.Length < 50)
                {
                    Console.WriteLine("Incorrect key format. Please try again.");
                    await Bitcoin.BitcoinMenu();
                }

                Console.WriteLine("Please choose your address format.");
                Console.WriteLine("0 - SegwitP2SH");
                Console.WriteLine("1 - Native Segwit");
                Console.WriteLine("2 - Taproot");
                Console.WriteLine("Press enter to use default.");
                var addressFormatString = Console.ReadLine();

                ScriptPubKeyType scriptPubKeyType = Globals.ScriptPubKeyType;

                if (!string.IsNullOrWhiteSpace(addressFormatString))
                {
                    var parseAttempt = int.TryParse(addressFormatString, out int addressFormat);
                    if (parseAttempt)
                    {
                        var addressFormatEnum = (Bitcoin.BitcoinAddressFormat)addressFormat;
                        scriptPubKeyType = addressFormatEnum == Bitcoin.BitcoinAddressFormat.SegwitP2SH ? ScriptPubKeyType.SegwitP2SH :
                            addressFormatEnum == Bitcoin.BitcoinAddressFormat.Segwit ? ScriptPubKeyType.Segwit : ScriptPubKeyType.TaprootBIP86;
                    }
                }

                //hex key
                if (privateKey?.Length > 58)
                {
                    BitcoinAccount.ImportPrivateKey(privateKey, scriptPubKeyType);
                    await ReturnToMenu("Private key has been imported!");
                }
                else
                {
                    BitcoinAccount.ImportPrivateKeyWIF(privateKey, scriptPubKeyType);
                    await ReturnToMenu("Private key has been imported!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("The key provided could not be restore. Please check format and ensure key is not incomplete or corrupt.");
            }
            
        }

        public static async Task ReturnToMenu(string message = "Returning to BTC menu.")
        {
            Console.WriteLine(message);
            Console.WriteLine("......3");
            Thread.Sleep(1000);
            Console.WriteLine("......2");
            Thread.Sleep(1000);
            Console.WriteLine("......1");
            Thread.Sleep(1000);
            await Bitcoin.BitcoinMenu();
        }

        public static async Task ShowBitcoinAccounts()
        {
            Console.Clear();
            var accounts = BitcoinAccount.GetBitcoin();

            var accountList = accounts.FindAll().ToList();

            if (accountList.Count() > 0)
            {
                Console.Clear();
                Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop);

                AnsiConsole.Write(
                new FigletText("BTC Accounts")
                .Centered()
                .Color(Color.Green));

                var table = new Table();

                table.Title("[yellow]BTC Wallet Accounts[/]").Centered();
                table.AddColumn(new TableColumn(new Panel("Address")));
                table.AddColumn(new TableColumn(new Panel("Balance"))).Centered();

                accountList.ForEach(x => {
                    table.AddRow($"[blue]{x.Address}[/]", $"[green]{x.Balance}[/]");
                });


                table.Border(TableBorder.Rounded);

                AnsiConsole.Write(table);

                Console.WriteLine("Please type /btc to return to mainscreen.");
            }
            else
            {
                Console.WriteLine("No Accounts Found. Returning you to BTC Menu");
                Console.WriteLine("......3");
                Thread.Sleep(1000);
                Console.WriteLine("......2");
                Thread.Sleep(1000);
                Console.WriteLine("......1");
                Thread.Sleep(1000);
                await Bitcoin.BitcoinMenu();
            }

        }

        public static async Task SendBitcoinTransaction()
        {
            Console.Clear();
            Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop);

            try
            {
                var accountList = BitcoinAccount.GetBitcoinAccounts();
                var accountNumberList = new Dictionary<string, BitcoinAccount>();

                if (accountList?.Count() > 0)
                {
                    int count = 1;
                    Console.WriteLine("Please select a Bitcoin account to send from");
                    Console.WriteLine("********************************************************************");

                    var table = new Table();

                    table.Title("[yellow]BTC Wallet Accounts[/]").Centered();
                    table.AddColumn(new TableColumn(new Panel("#")));
                    table.AddColumn(new TableColumn(new Panel("Address")));
                    table.AddColumn(new TableColumn(new Panel("Balance"))).Centered();

                    accountList.ToList().ForEach(x => {
                        accountNumberList.Add(count.ToString(), x);
                        table.AddRow($"[yellow]{count}[/]", $"[blue]{x.Address}[/]", $"[green]{x.Balance}[/]");
                        count++;
                    });


                    table.Border(TableBorder.Rounded);

                    AnsiConsole.Write(table);

                    string? walletChoice = "";
                    walletChoice = await ReadLineUtility.ReadLine();
                    while (string.IsNullOrEmpty(walletChoice))
                    {
                        Console.WriteLine("Entry not recognized. Please try it again. Sorry for trouble!");
                        walletChoice = await ReadLineUtility.ReadLine();
                    }
                    var wallet = accountNumberList[walletChoice];
                    Console.WriteLine("********************************************************************");
                    Console.WriteLine("From Address address:");
                    string fromAddress = wallet.Address;
                    Console.WriteLine(fromAddress);

                    Console.WriteLine("\nPlease enter the recipient address!:");
                    string? toAddress = await ReadLineUtility.ReadLine();

                    Console.WriteLine("\nPlease enter the amount (number)!:");
                    string? strAmount = await ReadLineUtility.ReadLine();

                    if (string.IsNullOrEmpty(fromAddress) ||
                    string.IsNullOrEmpty(toAddress) ||
                    string.IsNullOrEmpty(strAmount))
                    {

                        Console.WriteLine("\n\nError! Please input all fields: sender, recipient, and the amount.\n");
                        return;
                    }

                    decimal amount = new decimal();

                    try
                    {
                        amount = decimal.Parse(strAmount);
                    }
                    catch
                    {
                        Console.WriteLine("\nError! You have entered an incorrect value for  the amount!");
                        return;
                    }

                    Console.WriteLine("\nPlease choose your base tx fee (Satoshi's per byte).");
                    Console.WriteLine("\nSlow - 10 s/byte");
                    Console.WriteLine("\nGeneral - 30 s/byte");
                    Console.WriteLine("\nFast - 100 s/byte");

                    var baseFee = Console.ReadLine();

                    var baseFeeParse = int.TryParse(baseFee, out int feeResult);

                    if(!baseFeeParse)
                    {
                        Console.WriteLine("\nBase fee was not in the correct format.");
                        return;
                    }

                    bool overrideInternal = false;

                    Console.WriteLine("\nDo you want to allow same wallet transfers? (Y for yes, N for no.)");
                    var overrideAnswer = Console.ReadLine();
                    overrideInternal = overrideAnswer?.ToLower() == "y" ? true : false;

                    var result = await TransactionService.SendTransaction(fromAddress, toAddress, amount, feeResult, overrideInternal);
                    Console.WriteLine(result.Item2);

                    if(!result.Item1)
                    {
                        ErrorLogUtility.LogError($"Error Sending BTC. Error: {result.Item2}", "BitcoinCommand.SendBitcoinTransaction()");
                    }
                }
                else
                {
                    Console.WriteLine("No Accounts Found. Returning you to BTC Menu");
                    Console.WriteLine("......3");
                    Thread.Sleep(1000);
                    Console.WriteLine("......2");
                    Thread.Sleep(1000);
                    Console.WriteLine("......1");
                    Thread.Sleep(1000);
                    await Bitcoin.BitcoinMenu();
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"ERROR: {ex}");
            }

        }

        public static async Task ResetAccounts()
        {
            Console.WriteLine("\nAre you sure you want to do this? (Y for yes, N for no.)");
            var answer = Console.ReadLine();
            if (!string.IsNullOrEmpty(answer))
            {
                if (answer.ToLower() == "y")
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
                    Console.WriteLine("Reset Started. Please give a few minutes for items to refresh. Returning to menu in......");
                    Console.WriteLine("......3");
                    Thread.Sleep(1000);
                    Console.WriteLine("......2");
                    Thread.Sleep(1000);
                    Console.WriteLine("......1");
                    Thread.Sleep(1000);
                    await Bitcoin.BitcoinMenu();
                }
            }
        }

        public static async Task PrintUTXOs()
        {
            var accountList = BitcoinAccount.GetBitcoinAccounts();

            if (accountList?.Count() > 0)
            {
                Console.Clear();
                Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop);

                AnsiConsole.Write(
                new FigletText("Bitcoin UTXOs")
                .Centered()
                .Color(Color.Green));

                foreach(var account in accountList)
                {
                    var table = new Table();

                    table.Title($"[hotpink]Address: {account.Address}[/]").Centered();
                    table.AddColumn(new TableColumn(new Panel("TxId")));
                    table.AddColumn(new TableColumn(new Panel("Value"))).Centered();
                    table.AddColumn(new TableColumn(new Panel("Vout"))).Centered();
                    table.AddColumn(new TableColumn(new Panel("Is Used"))).Centered();

                    var utxoList = BitcoinUTXO.GetUTXOs(account.Address);
                    if(utxoList?.Count() > 0)
                    {
                        foreach(var utxo in utxoList)
                        {
                            table.AddRow($"[blue]{utxo.TxId}[/]", $"[green]{utxo.Value}[/]", $"[yellow]{utxo.Vout}[/]" ,$"[purple]{utxo.IsUsed}[/]");
                        }
                        
                    }

                    table.Border(TableBorder.Rounded);

                    AnsiConsole.Write(table);
                }

                
            }
            else
            {

            }
        }

        public static async void PrintKeys()
        {
            var accountList = BitcoinAccount.GetBitcoinAccounts();

            if (accountList?.Count() > 0)
            {
                Console.Clear();
                Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop);

                AnsiConsole.Write(
                new FigletText("Bitcoin Private Keys")
                .Centered()
                .Color(Color.Green));

                var table = new Table();

                table.Title("[yellow]Bitcoin Private Keys[/]").Centered();
                table.AddColumn(new TableColumn(new Panel("Address")));
                table.AddColumn(new TableColumn(new Panel("Private Key"))).Centered();

                foreach (var account in accountList)
                {
                    table.AddRow($"[blue]{account.Address}[/]", $"[green]{account.PrivateKey}[/]");
                }

                table.Border(TableBorder.Rounded);

                AnsiConsole.Write(table);
            }
            else
            {

            }
        }

        public static async Task TokenizeBitcoin()
        {
            Console.Clear();
            Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop);

            try
            {
                var accountList = AccountData.GetAccountsWithBalance();
                var accountNumberList = new Dictionary<string, Account>();

                if (accountList?.Count() > 0)
                {
                    int count = 1;
                    var table = new Table();

                    table.Title("[green]Please select an VBX address to tokeninze your BTC under.[/]").Centered();
                    table.AddColumn(new TableColumn(new Panel("#")));
                    table.AddColumn(new TableColumn(new Panel("Address")));
                    table.AddColumn(new TableColumn(new Panel("Balance"))).Centered();

                    accountList.ToList().ForEach(x => {
                        accountNumberList.Add(count.ToString(), x);
                        table.AddRow($"[yellow]{count}[/]", $"[blue]{x.Address}[/]", $"[green]{x.Balance}[/]");
                        count++;
                    });

                    table.Border(TableBorder.Rounded);

                    AnsiConsole.Write(table);

                    string? walletChoice = "";
                    walletChoice = await ReadLineUtility.ReadLine();
                    while (string.IsNullOrEmpty(walletChoice))
                    {
                        Console.WriteLine("Entry not recognized. Please try it again. Sorry for trouble!");
                        walletChoice = await ReadLineUtility.ReadLine();
                    }
                    var wallet = accountNumberList[walletChoice];
                    Console.WriteLine("********************************************************************");
                    string fromAddress = wallet.Address;
                    AnsiConsole.MarkupLine($"Creating vBTC token for: [green]{fromAddress}[/]");

                    
                    Console.WriteLine("\nPlease enter path for vBTC image. Press Enter for Default image.");
                    string? fileLocation = await ReadLineUtility.ReadLine();

                    if (string.IsNullOrEmpty(fileLocation))
                        fileLocation = "default";

                    Console.WriteLine("\nPlease enter vBTC token Name. Press enter for default 'vBTC Token'");
                    string? tokenNameInput = await ReadLineUtility.ReadLine();
                    string tokenName = string.IsNullOrWhiteSpace(tokenNameInput) ? "vBTC Token" : tokenNameInput;

                    Console.WriteLine("\nPlease enter vBTC token description. Press enter for default 'vBTC Token'");
                    string? tokenDescInput = await ReadLineUtility.ReadLine();
                    string tokenDesc = string.IsNullOrWhiteSpace(tokenDescInput) ? "vBTC Token" : tokenDescInput;

                    if (string.IsNullOrEmpty(fromAddress) || string.IsNullOrEmpty(fileLocation))
                    {
                        Console.WriteLine("\n\nError! Please input all fields: tokenize address and the name.\n");
                        await ReturnToMenu();
                        return;
                    }

                    Console.WriteLine("Generating vBTC token contract.");
                    var scMain = await TokenizationService.CreateTokenizationScMain(fromAddress, fileLocation, tokenName, tokenDesc);
                    if(scMain == null) 
                    {
                        await ReturnToMenu("Failed to generate vBTC token. Please check logs for more.");
                        return;
                    }
                    var createSC = await TokenizationService.CreateTokenizationSmartContract(scMain);

                    if(!createSC.Item1)
                    {
                        await ReturnToMenu("Failed to write vBTC token contract. Please check logs for more.");
                        return;
                    }

                    var publishSc = await TokenizationService.MintSmartContract(createSC.Item2);

                    if(!publishSc.Item1)
                    {
                        await ReturnToMenu($"Failed to publish vBTC token contract. Reason: {publishSc.Item2}. Please check logs for more.");
                        return;
                    }

                    AnsiConsole.MarkupLine($"[green]{publishSc.Item2}[/]");
                    await ReturnToMenu();
                }
                else
                {
                    await ReturnToMenu("No Accounts Found. Returning you to BTC Menu");
                    return;
                }
            }
            catch(Exception ex) 
            {
                ErrorLogUtility.LogError($"Error Tokenizing BTC. Error: {ex.ToString}", "BitcoinCommand.TokenizeBitcoin()");
            }
        }

        public static async Task GenerateBTCTokenAddress()
        {
            var publishedTokens = await TokenizedBitcoin.GetTokenPublishedNoAddressList();

            if(!publishedTokens.Any())
            {
                await ReturnToMenu("No Tokens Found ready for generation.");
                return;
            } 
            var btcAccountNumberList = new Dictionary<string, TokenizedBitcoin>();
            int count = 1;
            var table = new Table();

            table.Title("[green]Please select a token to generate a deposit address.[/]").Centered();
            table.AddColumn(new TableColumn(new Panel("#")));
            table.AddColumn(new TableColumn(new Panel("Token Name")));
            table.AddColumn(new TableColumn(new Panel("Token Desc."))).Centered();
            table.AddColumn(new TableColumn(new Panel("RBX Address")));
            table.AddColumn(new TableColumn(new Panel("SmartContractUID")));




             publishedTokens.ToList().ForEach(x => {
                btcAccountNumberList.Add(count.ToString(), x);
                 var tokDesc = x.TokenDescription.Length > 21 ? x.TokenDescription.Substring(0, 20) : x.TokenDescription.Substring(0, x.TokenDescription.Length - 1);
                table.AddRow($"[yellow]{count}[/]", 
                    $"[green]{x.TokenName}[/]", 
                    $"[green]{tokDesc}[/]", 
                    $"[blue]{x.RBXAddress}[/]", 
                    $"[purple]{x.SmartContractUID}[/]");
                count++;
            });

            table.Border(TableBorder.Rounded);

            AnsiConsole.Write(table);

            string? walletChoice = "";
            walletChoice = await ReadLineUtility.ReadLine();
            while (string.IsNullOrEmpty(walletChoice))
            {
                Console.WriteLine("Entry not recognized. Please try it again. Sorry for trouble!");
                walletChoice = await ReadLineUtility.ReadLine();
            }
            var wallet = btcAccountNumberList[walletChoice];
            Console.WriteLine("********************************************************************");
            Console.WriteLine("From Address address:");
            string fromAddress = wallet.RBXAddress;
            Console.WriteLine(fromAddress);

            AnsiConsole.MarkupLine("Starting MPC Connection");
            await Task.Delay(1000);
            AnsiConsole.MarkupLine("Connected!");
            await AnsiConsole.Progress()
                .Columns(new ProgressColumn[] {
                    new TaskDescriptionColumn(),    // Task description
                    new ProgressBarColumn(),        // Progress bar
                    new PercentageColumn(),         // Percentage
                    new RemainingTimeColumn(),      // Remaining time
                    new SpinnerColumn()             // Spinner
                    })
                .StartAsync(async ctx =>
                {
                    // Define tasks
                    var task1 = ctx.AddTask("[green]Initiating MPC Protocols[/]");
                    var task2 = ctx.AddTask("[blue]Share Generation Progress[/]");
                    var task3 = ctx.AddTask("[purple]Connection Stabilizer Running[/]");

                    while (!ctx.IsFinished)
                    {
                        task1.Increment(2.5);
                        task2.Increment(1.5);
                        task2.Increment(5.5);
                        await Task.Delay(200);
                    }
                });
        }

        public static async Task<string> CreateDnr()
        {
            var output = "";
            Console.WriteLine("Please select the wallet you'd like to create a domain name registration for...");
            var accountList = AccountData.GetAccountsWithBalanceForAdnr();
            var btcAccountList = BitcoinAccount.GetBitcoinAccounts();
            var accountNumberList = new Dictionary<string, Account>();
            var btcAccountNumberList = new Dictionary<string, BitcoinAccount>();

            if (accountList.Count() > 0)
            {
                try
                {
                    int count = 1;
                    var table = new Table();

                    table.Title("[green]Please select a account to own the BTC ADNR.[/]").Centered();
                    table.AddColumn(new TableColumn(new Panel("#")));
                    table.AddColumn(new TableColumn(new Panel("Address")));
                    table.AddColumn(new TableColumn(new Panel("Balance"))).Centered();

                    accountList.ToList().ForEach(x => {
                        accountNumberList.Add(count.ToString(), x);
                        table.AddRow($"[yellow]{count}[/]", $"[blue]{x.Address}[/]", $"[green]{x.Balance}[/]");
                        count++;
                    });

                    table.Border(TableBorder.Rounded);

                    AnsiConsole.Write(table);
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

                            var table2 = new Table();

                            table2.Title("[green]Please select a account to own the BTC ADNR.[/]").Centered();
                            table2.AddColumn(new TableColumn(new Panel("#")));
                            table2.AddColumn(new TableColumn(new Panel("Address")));
                            table2.AddColumn(new TableColumn(new Panel("Balance"))).Centered();

                            btcAccountList.ToList().ForEach(x => {
                                btcAccountNumberList.Add(count.ToString(), x);
                                table2.AddRow($"[yellow]{count}[/]", $"[blue]{x.Address}[/]", $"[green]{x.Balance}[/]");
                                count++;
                            });

                            table2.Border(TableBorder.Rounded);

                            AnsiConsole.Write(table2);
                            string? btcWalletChoice = "";
                            btcWalletChoice = await ReadLineUtility.ReadLine();
                            if (string.IsNullOrEmpty(btcWalletChoice))
                            {
                                Console.WriteLine($"Incorrect input for BTC Address");
                                return output;
                            }

                            var btcWallet = btcAccountNumberList[btcWalletChoice];

                            var address = wallet.Address;
                            var btcAddress = btcWallet.Address;

                            var adnr = BitcoinAdnr.GetBitcoinAdnr();
                            var adnrCheck = adnr.FindOne(x => x.BTCAddress == btcAddress);
                            if (adnrCheck != null)
                            {
                                Console.WriteLine($"This address already has a DNR associated with it: {adnrCheck.Name}");
                                return output;
                            }
                            bool nameFound = true;
                            while (nameFound)
                            {
                                Console.WriteLine($"You have selected the following wallet: {btcAddress}");
                                Console.WriteLine("Please enter the name you'd like for this wallet. Ex: (cryptoinvestor1) Please note '.btc' will automatically be added. DO NOT INCLUDE IT.");
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
                                        var nameRBX = name.ToLower() + ".btc";
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
                                                    var result = await BitcoinAdnr.CreateAdnrTx(address, name, btcAddress);
                                                    if (result.Item1 != null)
                                                    {
                                                        Console.WriteLine("DNR Request has been sent to mempool.");
                                                        Console.WriteLine("......3");
                                                        Thread.Sleep(1000);
                                                        Console.WriteLine("......2");
                                                        Thread.Sleep(1000);
                                                        Console.WriteLine("......1");
                                                        Thread.Sleep(1000);
                                                        await Bitcoin.BitcoinMenu();
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
                catch (Exception ex)
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
    }
}
