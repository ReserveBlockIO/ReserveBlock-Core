using ReserveBlockCore.Models;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using Spectre.Console;

namespace ReserveBlockCore.Mothering
{
    public class Mother
    {
        public static async Task StartMotherProgram()
        {
            await MotherMenu();

            bool exit = false;
            while (!exit)
            {
                var command = Console.ReadLine();
                if (!string.IsNullOrEmpty(command))
                {
                    var result = ProcessCommand(command);
                    if (result == CommandResult.MotherMenu)
                        await MotherMenu();
                    if (result == CommandResult.MainMenu)
                        exit = true;

                }
            }

            StartupService.MainMenu();
            Console.WriteLine("Returned to main menu.");
        }

        private static CommandResult ProcessCommand(string command)
        {
            CommandResult result = CommandResult.Nothing;

            switch (command.ToLower())
            {
                case "/mm":
                    result = CommandResult.MotherMenu;
                    break;
                case "/menu":
                    result = CommandResult.MainMenu;
                    break;
                case "1":
                    GetKidsStatic();
                    result = CommandResult.Nothing;
                    break;
                case "2":
                    //Live Feed
                    break;
                case "3":
                    //Delete Mother :(
                    break;
                case "4":
                    result = CommandResult.MainMenu;
                    break;
                default:
                    result = CommandResult.Nothing;
                    break;
            }

            return result;
        }

        private enum CommandResult
        {
            MainMenu,
            MotherMenu,
            Nothing
        }

        private static void GetKidsStatic()
        {
            var kids = Globals.MothersKids.Values.ToList();
            if (kids.Count > 0)
            {
                Console.Clear();
                Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop);

                var table = new Table();

                table.Title("[yellow]Mothers Kids (Validators)[/]").Centered();
                table.AddColumn(new TableColumn(new Panel("IP Address")));
                table.AddColumn(new TableColumn(new Panel("Name")));
                table.AddColumn(new TableColumn(new Panel("Address")));
                table.AddColumn(new TableColumn(new Panel("Balance")));
                table.AddColumn(new TableColumn(new Panel("Active With Mother")));
                table.AddColumn(new TableColumn(new Panel("Active Validating")));

                foreach (var kid in kids)
                {
                    table.AddRow($"[yellow]{kid.IPAddress}[/]",
                        $"[blue]{kid.ValidatorName}[/]",
                        $"[purple]{kid.Address}[/]",
                        $"[green]{kid.Balance}[/]",
                        kid.ActiveWithMother ? $"[green]Yes[/]" : $"[red]No![/]",
                        kid.ActiveWithValidating ? $"[green]Yes[/]" : $"[red]No![/]"
                        );
                }

                table.Border(TableBorder.Rounded);

                AnsiConsole.Write(table);

                Console.WriteLine("|============================================|");
                Console.WriteLine("|type /mm to come back to mother             |");
                Console.WriteLine("|type /menu to come back to main area        |");
                Console.WriteLine("|============================================|");
            }
        }

        private static async Task MotherMenu()
        {
            Console.Clear();
            Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop);

            if (Globals.IsTestNet != true)
            {
                AnsiConsole.Write(
                new FigletText("Mother")
                .LeftAligned()
                .Color(Color.Blue));
            }
            else
            {
                AnsiConsole.Write(
                new FigletText("Welcome Mother")
                .LeftAligned()
                .Color(Color.Green));
            }

            Console.WriteLine("|============================================|");
            Console.WriteLine("| 1. Show Kids (Display connected Vals)      |");
            Console.WriteLine("| 2. Show Live Feed (Auto Updates)           |");
            Console.WriteLine("| 3. Disable Mother (Requires Wallet restart)|");
            Console.WriteLine("| 4. Exit Mother's Program                   |");
            Console.WriteLine("|============================================|");
            Console.WriteLine("|type /mm to come back to mother             |");
            Console.WriteLine("|type /menu to come back to main area        |");
            Console.WriteLine("|============================================|");
        }
    }
}
