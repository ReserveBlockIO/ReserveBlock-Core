using ReserveBlockCore.Services;

namespace ReserveBlockCore.Utilities
{
    public class ReadLineUtility
    {
        public static async Task<string?> ReadLine()
        {
            var readLine = Console.ReadLine();

            if(readLine == "/menu")
            {
                MainMenuReturn();
                throw new Exception("/menu was been entered. Returning to main menu");
            }

            return readLine;
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
