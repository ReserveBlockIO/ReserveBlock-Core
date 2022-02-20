using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using ReserveBlockCore.Commands;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;

namespace ReserveBlockCore
{
    class Program
    {
        private static Timer blockTimer;//for creating a new block at max every 30 seconds
        private static Timer mempoolShareTimer;//Sharing and checking mempool with other nodes
        private static Timer BlockHeightTimer; //Checking Height of other nodes to see if new block is needed
        private static Timer PeerCheckTimer;//checks currents peers and old peers and will request others to try. 
        private static int MempoolCount = 0;
        public static List<Transaction> MempoolList = new List<Transaction>();
        private static bool BlocksDownloading = false;
        static async Task Main(string[] args)
        {
            var argList = args.ToList();

            blockTimer = new Timer(blockBuilder_Elapsed); // 1 sec = 1000, 60 sec = 60000
            blockTimer.Change(60000, 30000); //waits 1 minute, then runs every 30 seconds for new blocks

            mempoolShareTimer = new Timer(mempoolBroadcast_Elapsed); // 1 sec = 1000, 60 sec = 60000
            mempoolShareTimer.Change(60000, 5000); //waits 1 minute, then runs every 5 seconds for new tx's

            BlockHeightTimer = new Timer(blockHeightCheck_Elapsed); // 1 sec = 1000, 60 sec = 60000
            BlockHeightTimer.Change(60000, 3000); //waits 1 minute, then runs every 3 seconds for new block heights

            PeerCheckTimer = new Timer(peerCheckTimer_Elapsed); // 1 sec = 1000, 60 sec = 60000
            PeerCheckTimer.Change(60000, 300000); //waits 1 minute, then runs every 3 seconds for new block heights


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
            string url2 = "http://*:3338"; //this is port for signalr connect and all p2p functions
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

            StartupService.TestConnect();
            StartupService.StartupPeers();
            Thread.Sleep(5000);
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
        private static async void blockBuilder_Elapsed(object sender)
        {
            var localValidator = Validators.Validator.GetLocalValidator();
            //If no validators are detected then no need to run this code
            if(localValidator.Count != 0)
            {
                var validator = Validators.Validator.GetBlockValidator(); //need create consensus on who should actually do this. 
                //if validator is NaN then there are no validators on network and block creation will stop. 
                if (validator != "NaN")
                {
                    var accounts = DbContext.DB_Wallet.GetCollection<Account>(DbContext.RSRV_ACCOUNTS);
                    var account = accounts.Query().Where(x => x.Address == validator).FirstOrDefault();

                    if (account != null)
                    {
                        //craft new block
                        await BlockchainData.CraftNewBlock(validator);
                    }
                }
            }
        }

        private static void mempoolBroadcast_Elapsed(object sender)
        {
            var mempoolCount = TransactionData.GetPool().FindAll().Count();
            

            if(mempoolCount > MempoolCount)
            {
                //rebroadcast
                //reset count
                MempoolCount = mempoolCount;
            }
            else if(mempoolCount < MempoolCount)
            {
                //block added
                //reset count
                MempoolCount = mempoolCount;
            }
            else
            {
                //do nothing
            }
            
        }

        private static async void blockHeightCheck_Elapsed(object sender)
        {
            //if blocks are currently downloading this will stop it from running again.
            if (BlocksDownloading != true)
            {

                var result = await P2PClient.GetCurrentHeight();
                if (result.Item1 == true)
                {
                    BlocksDownloading = true;
                    BlocksDownloading = await BlockDownloadService.GetAllBlocks(result.Item2);
                }
            }
        }

        private static async void peerCheckTimer_Elapsed(object sender)
        {
            var peerCheck = await P2PClient.PeerHealthCheck();

            if(peerCheck == true)
            {
                //health check pass
            }
            else
            {
                //potentially no connected nodes.
            }
        }
    }
}



