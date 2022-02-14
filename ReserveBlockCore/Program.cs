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
            
            
            //add method to remove stale state trei records and stale validator records too


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

            string url = "https://localhost:8001"; //local API to connect to wallet. This can be changed, but be cautious. 
            string url2 = "https://*:3338"; //this is port for signalr connect and all p2p functions
            //string url2 = "https://*:3338" //This is non http version. Must comment out app.UseHttpsRedirection() in startupp2p

            var commandLoopTask = Task.Run(() => CommandLoop(url));
            var commandLoopTask2 = Task.Run(() => CommandLoop2(url2));

            var builder = Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel()
                    .UseStartup<Startup>()
                    .UseUrls(url)
                    .ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
                });

            var builder2 = Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel()
                    .UseStartup<StartupP2P>()
                    .UseUrls(url2)
                    .ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
                });

            builder.RunConsoleAsync();
            builder2.RunConsoleAsync();

            StartupService.StartupDatabase();
            StartupService.SetBlockchainChainRef();
            //StartupService.StartupPeers();
            //StartupService.DownloadBlocks();
            StartupService.StartupInitializeChain();

            Task.WaitAll(commandLoopTask, commandLoopTask2);

            
            //await Task.WhenAny(builder2.RunConsoleAsync(), commandLoopTask2);
            //await Task.WhenAny(builder.RunConsoleAsync(), commandLoopTask);

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
        private static void CommandLoop2(string url)
        {
            Console.ReadKey();
        }
        private static void blockBuilder_Elapsed(object sender)
        {
            var validator = Validators.Validator.GetBlockValidator();
            //if validator is NaN then there are no validators on network and block creation will stop. 
            if(validator != "NaN")
            {
                var accounts = DbContext.DB_Wallet.GetCollection<Account>(DbContext.RSRV_ACCOUNTS);
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



