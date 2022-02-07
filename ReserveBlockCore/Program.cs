using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using ReserveBlockCore.Commands;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;

namespace ReserveBlockCore
{
    class Program
    {
        private static Timer blockTimer;
        static async Task Main(string[] args)
        {
            var argList = args.ToList();

            //blockTimer = new Timer(blockBuilder_Elapsed); // 1 sec = 1000, 60 sec = 60000
            //blockTimer.Change(60000, 30000); //waits 1 minute, then runs every 30 seconds for new blocks

            if (args.Length != 0)
            {
                argList.ForEach(x => {
                    var argC = x.ToLower();
                    if(argC == "enableapi")
                    {
                        Startup.APIEnabled = true; //api disabled by default
                    }
                    if(argC == "hidecli")
                    {
                        //maybe add hidden cli feature
                    }
                    if(argC == "gui")
                    {
                        //launch gui
                    }
                    if(argC == "testnet")
                    {
                        //Launch testnet
                        Startup.IsTestNet = true;
                    }
                });
            }
            StartupService.StartupDatabase();
            StartupService.StartupInitializeChain();

            string url = "https://localhost:7777"; //local API to connect to wallet. This can be changed, but be cautious. 

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

        private static void blockBuilder_Elapsed(object sender)
        {
            var validator = Validators.Validator.GetBlockValidator();
            //if validator is NaN then there are no validators on network and block creation will stop. 
            if(validator != "NaN")
            {
                var accounts = DbContext.DB.GetCollection<Account>(DbContext.RSRV_ACCOUNTS);
                var account = accounts.Query().Where(x => x.Address == validator).FirstOrDefault();

                if (account != null)
                {
                    //craft new block
                    BlockchainData.CraftNewBlock(validator);
                }
            }
               

            
        }

    }
}



