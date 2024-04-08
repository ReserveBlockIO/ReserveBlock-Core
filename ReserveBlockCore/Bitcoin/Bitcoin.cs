using ReserveBlockCore.Bitcoin.Integrations;
using ReserveBlockCore.Bitcoin.Models;
using ReserveBlockCore.Bitcoin.Utilities;
using ReserveBlockCore.Commands;
using ReserveBlockCore.Services;
using Spectre.Console;

namespace ReserveBlockCore.Bitcoin
{
   
    public class Bitcoin
    {
        static bool exit = false;
        static SemaphoreSlim BalanceCheckLock = new SemaphoreSlim(1, 1);
        private enum CommandResult
        {
            MainMenu,
            BtcMenu,
            Nothing
        }
        public static async Task StartBitcoinProgram()
        {
            await BitcoinMenu();
            exit = false;

            Explorers.PopulateExplorerDictionary();

            _ = NodeFinder.GetNode(); // Get Node for later use now. This is to save time.
            _ = AccountCheck();

            while (!exit)
            {
                var command = Console.ReadLine();
                if (!string.IsNullOrEmpty(command))
                {
                    var result = await ProcessCommand(command);
                    if (result == CommandResult.BtcMenu)
                        await BitcoinMenu();
                    if (result == CommandResult.MainMenu)
                        exit = true;

                }
            }
            Console.WriteLine("Return you to main menu in 5 seconds...");
            await Task.Delay(5000);
            StartupService.MainMenu();
        }

        private static async Task<CommandResult> ProcessCommand(string command)
        {
            CommandResult result = CommandResult.Nothing;

            switch (command.ToLower())
            {
                case "/btc":
                    result = CommandResult.BtcMenu;
                    break;
                case "/menu":
                    result = CommandResult.MainMenu;
                    break;
                case "1": //create address
                    BitcoinCommand.CreateAddress();
                    break;
                case "2": //show address(es)
                    await BitcoinCommand.ShowBitcoinAccounts();
                    break;
                case "3": //send transactions
                    await BitcoinCommand.SendBitcoinTransaction();
                    break;
                case "4": //Tokenize
                    await BitcoinCommand.TokenizeBitcoin();
                    break;
                case "4a": //Tokenize Generate Deposit Address
                    await BitcoinCommand.GenerateBTCTokenAddress();
                    break;
                case "4b": //Tokenize Transfer
                    Console.WriteLine("No vBTC Tokens to transfer at this time");
                    break;
                case "5": //Import address
                    await BitcoinCommand.ImportAddress();
                    break;
                case "6": //Bitcoin ADNR
                    Globals.StopConsoleOutput = true;
                    await BitcoinCommand.CreateDnr();
                    Globals.StopConsoleOutput = false;
                    break;
                case "7": //Reset Accounts
                    await BitcoinCommand.ResetAccounts();
                    break;
                case "8": //Exit
                    result = CommandResult.MainMenu;
                    break;
                case "/printkeys":
                    BitcoinCommand.PrintKeys();
                    break;
                case "/printutxos":
                    await BitcoinCommand.PrintUTXOs();
                    break;
            }

            return result;
        }

        public static async Task AccountCheck()
        {
            while(!exit)
            {
                var delay = Task.Delay(new TimeSpan(0,4,0));
                await BalanceCheckLock.WaitAsync();

                Globals.BTCAccountLastCheckedDate = DateTime.Now;

                try
                {
                    var addressList = BitcoinAccount.GetBitcoin()?.FindAll().ToList();

                    if (addressList?.Count != 0)
                    {
                        foreach (var address in addressList)
                        {
                            await Explorers.GetAddressInfo(address.Address);
                            await Task.Delay(3000);
                        }
                    }

                    var tokenizeList = await TokenizedBitcoin.GetTokenizedList();
                    if(tokenizeList?.Count != 0)
                    {
                        foreach (var address in tokenizeList)
                        {
                            if(address.BTCAddress != null)
                            {
                                await Explorers.GetAddressInfo(address.BTCAddress, true);
                                await Task.Delay(5000);
                            }
                            
                        }
                    }
                }
                catch
                {

                }
                finally
                {
                    BalanceCheckLock.Release();
                }

                await delay;
            }
            
        }

        public static async Task BitcoinMenu()
        {
            Console.Clear();
            Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop);

            if (Globals.IsTestNet != true)
            {
                AnsiConsole.Write(
                new FigletText("Bitcoin + VerifiedX")
                .LeftJustified()
                .Color(new Color(247, 147, 26)));
            }
            else
            {
                AnsiConsole.Write(
                new FigletText("Bitcoin + VerifiedX - TestNet")
                .LeftJustified()
                .Color(new Color(50, 146, 57)));
            }

            if (Globals.IsTestNet != true)
            {
                Console.WriteLine("Bitcoin + VerifiedX Network - Mainnet");
            }
            else
            {
                Console.WriteLine("Bitcoin + VerifiedX Network - **TestNet**");
            }

            Console.WriteLine("|========================================|");
            Console.WriteLine("| 1. Create Address                      |");
            Console.WriteLine("| 2. Show Address(es)                    |");
            Console.WriteLine("| 3. Send Transaction                    |");
            Console.WriteLine("| 4. Tokenize Bitcoin                    |");
            Console.WriteLine("| 4a. Generate vBTC Deposit Address      |");
            Console.WriteLine("| 4b. Transfer vBTC                      |");
            Console.WriteLine("| 5. Import Address                      |");
            Console.WriteLine("| 6. Bitcoin ADNR Register               |");
            Console.WriteLine("| 7. Reset/Resync Accounts               |");
            Console.WriteLine("| 8. Exit Bitcoin Wallet                 |");
            Console.WriteLine("|========================================|");
            Console.WriteLine("|type /btc to come back to the main area |");
            Console.WriteLine("|type /menu to go back to RBX Wallet     |");
            Console.WriteLine("|========================================|");
        }

        public enum BitcoinAddressFormat
        {
            SegwitP2SH = 0,
            Segwit = 1,
            Taproot = 2
        }

    }
}
