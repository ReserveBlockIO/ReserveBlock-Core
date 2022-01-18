using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using ReserveBlockCore.Commands;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;



namespace ReserveBlockCore
{
    class Program
    {
        static async Task Main(string[] args)
        {
            StartupService.StartupDatabase();
            StartupService.StartupInitializeChain();

            string url = "https://localhost:12345";

            var commandLoopTask = Task.Run(() => CommandLoop(url));

            var builder = Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel()
                    .UseStartup<Startup>()
                    .UseUrls(url)
                    .ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
                });

            await Task.WhenAny(builder.RunConsoleAsync(), commandLoopTask);
        }

        private static void CommandLoop(string url)
        {
            StartupService.StartupMenu();
            Thread.Sleep(1000);
            StartupService.MainMenu();

            while (true)
            {
                var command = Console.ReadLine();

                if (command != "" || command != null)
                {
                    var commandResult = BaseCommand.ProcessCommand(command);

                    if (commandResult == "_EXIT")
                    {
                        Console.WriteLine("Closing and Exiting Wallet Application.");
                        Environment.Exit(0);
                    }

                    Console.WriteLine(commandResult);
                }
                else
                {
                    Console.WriteLine("Please enter a command...");
                }

            }
            
        }

    }
}



