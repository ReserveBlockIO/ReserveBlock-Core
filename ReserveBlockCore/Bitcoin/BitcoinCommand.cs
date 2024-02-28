using NBitcoin;
using ReserveBlockCore.Bitcoin.Models;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;
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
            Console.WriteLine("Please paste in your private key");
            var privateKey = Console.ReadLine();

            if(privateKey == null)
            {
                Console.WriteLine("Key cannot be blank.");
                await Bitcoin.BitcoinMenu();
                return;
            }
            if(privateKey?.Length < 50)
            {
                Console.WriteLine("Incorrect key format. Please try again.");
                await Bitcoin.BitcoinMenu();
            }

            //hex key
            if(privateKey?.Length > 58)
            {
                BitcoinAccount.ImportPrivateKey(privateKey);
            }
            else
            {
                BitcoinAccount.ImportPrivateKeyWIF(privateKey);
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
    }
}
