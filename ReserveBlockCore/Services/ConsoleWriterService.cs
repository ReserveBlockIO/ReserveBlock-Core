namespace ReserveBlockCore.Services
{
    public class ConsoleWriterService
    {
        public static void Output(string text)
        {
            if(Program.StopConsoleOutput != true)
            {
                Console.WriteLine(text);
            }
        }
    }
}
