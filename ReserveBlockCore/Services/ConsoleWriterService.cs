namespace ReserveBlockCore.Services
{
    public class ConsoleWriterService
    {
        public static void Output(string text)
        {
            if(Globals.StopConsoleOutput != true)
            {
                Console.WriteLine(text);
            }
        }
    }
}
