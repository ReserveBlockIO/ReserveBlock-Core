using NBitcoin;
using ReserveBlockCore.Bitcoin.Models;
using ReserveBlockCore.Bitcoin.Services;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using Spectre.Console;

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
                }
                else
                {
                    BitcoinAccount.ImportPrivateKeyWIF(privateKey, scriptPubKeyType);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("The key provided could not be restore. Please check format and ensure key is not incomplete or corrupt.");
            }
            
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
                var accountList = BitcoinAccount.GetBitcoinAccounts();
                var accountNumberList = new Dictionary<string, BitcoinAccount>();

                if (accountList?.Count() > 0)
                {
                    int count = 1;
                    var table = new Table();

                    table.Title("[green]Please select a Bitcoin account to tokenize.[/]").Centered();
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
                    AnsiConsole.MarkupLine($"Tokenizing: [green]{fromAddress}[/]");
                    
                    Console.WriteLine("\nPlease enter ADNR Name.");
                    string? adnrName = await ReadLineUtility.ReadLine();

                    if (string.IsNullOrEmpty(fromAddress) ||
                    string.IsNullOrEmpty(adnrName))
                    {

                        Console.WriteLine("\n\nError! Please input all fields: tokenize address and the name.\n");
                        return;
                    }

                    // FIND network MCP
                    if(adnrName != "")
                        ErrorLogUtility.LogError($"Network MCP Could not be located on testnet.", "BitcoinCommand.TokenizeBitcoin()");
                    Console.WriteLine("Error Tokenizing BTC. Please Review Logs and Try again.");
                    Console.WriteLine("Returning you to BTC Menu");
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
                ErrorLogUtility.LogError($"Error Tokenizing BTC. Error: {ex.ToString}", "BitcoinCommand.TokenizeBitcoin()");
            }
        }
    }
}
