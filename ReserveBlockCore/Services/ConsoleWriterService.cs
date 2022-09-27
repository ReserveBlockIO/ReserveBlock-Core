using Spectre.Console;

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

        public static void OutputSameLine(string text)
        {
            if (Globals.StopConsoleOutput != true)
            {
                Console.Write($"\r{text}");
            }
        }

        public static void OutputMarked(string text)
        {
            if (Globals.StopConsoleOutput != true)
            {
                AnsiConsole.MarkupLine($"{text}");
            }
        }

        public static void OutputSameLineMarked(string text)
        {
            if (Globals.StopConsoleOutput != true)
            {
                AnsiConsole.Markup($"\r{text}");
            }
        }
    }
}
