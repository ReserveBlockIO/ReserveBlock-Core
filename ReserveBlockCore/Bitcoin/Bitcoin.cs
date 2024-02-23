using ReserveBlockCore.Services;
using Spectre.Console;

namespace ReserveBlockCore.Bitcoin
{
    public class Bitcoin
    {
        private enum CommandResult
        {
            MainMenu,
            BtcMenu,
            Nothing
        }
        public static async Task StartBitcoinProgram()
        {
            await BitcoinMenu();

            bool exit = false;
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
                case "1":
                    BitcoinCommand.CreateAddress();
                    break;
                case "2":
                    await BitcoinCommand.ShowBitcoinAccounts();
                    break;
                case "3":
                    
                    break;
                case "4":
                    
                    break;
                case "5":
                    
                    break;
                case "6":
                    
                    break;
                case "7":
                    result = CommandResult.MainMenu;
                    break;
            }

            return result;
        }

        public static async Task BitcoinMenu()
        {
            Console.Clear();
            Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop);

            if (Globals.IsTestNet != true)
            {
                AnsiConsole.Write(
                new FigletText("Bitcoin")
                .LeftJustified()
                .Color(new Color(247, 147, 26)));
            }
            else
            {
                AnsiConsole.Write(
                new FigletText("Bitcoin - TestNet")
                .LeftJustified()
                .Color(new Color(50, 146, 57)));
            }

            if (Globals.IsTestNet != true)
            {
                Console.WriteLine("Bitcoin Network - Mainnet");
            }
            else
            {
                Console.WriteLine("Bitcoin Network - **TestNet**");
            }

            Console.WriteLine("|========================================|");
            Console.WriteLine("| 1. Create Address                      |");
            Console.WriteLine("| 2. Show Address(es)                    |");
            Console.WriteLine("| 3. Send Transaction                    |");
            Console.WriteLine("| 4. Tokenize Bitcoin                    |");
            Console.WriteLine("| 5. Import Address                      |");
            Console.WriteLine("| 6. Bitcoin ADNR Register               |");
            Console.WriteLine("| 7. Exit Bitcoin Wallet                 |");
            Console.WriteLine("|========================================|");
            Console.WriteLine("|type /btc to come back to the vote area |");
            Console.WriteLine("|type /menu to go back to RBX Wallet     |");
            Console.WriteLine("|========================================|");
        }
    }
}
