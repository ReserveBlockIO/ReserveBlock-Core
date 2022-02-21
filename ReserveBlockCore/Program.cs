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
        private static Timer BlockHeightTimer; //Checking Height of other nodes to see if new block is needed
        private static Timer PeerCheckTimer;//checks currents peers and old peers and will request others to try. 
        private static Timer ValidatorListTimer;//checks currents peers and old peers and will request others to try. 
        private static int MempoolCount = 0;
        public static List<Transaction> MempoolList = new List<Transaction>();
        public static bool BlocksDownloading = false;
        static async Task Main(string[] args)
        {
            var argList = args.ToList();

            blockTimer = new Timer(blockBuilder_Elapsed); // 1 sec = 1000, 60 sec = 60000
            blockTimer.Change(60000, 100000); //waits 1 minute, then runs every 10 seconds for new blocks

            BlockHeightTimer = new Timer(blockHeightCheck_Elapsed); // 1 sec = 1000, 60 sec = 60000
            BlockHeightTimer.Change(60000, 3000); //waits 1 minute, then runs every 3 seconds for new block heights

            PeerCheckTimer = new Timer(peerCheckTimer_Elapsed); // 1 sec = 1000, 60 sec = 60000
            PeerCheckTimer.Change(60000, 1 * 10 * 6000); //waits 1 minute, then runs every 60 seconds

            ValidatorListTimer = new Timer(validatorListCheckTimer_Elapsed); // 1 sec = 1000, 60 sec = 60000
            ValidatorListTimer.Change(60000, 60 * 10 * 6000); //waits 1 minute, then runs every 1 hour


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

            StartupService.StartupPeers();

            Thread.Sleep(5000);

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
            var lastBlock = BlockchainData.GetLastBlock();
            var currentUnixTime = Utilities.TimeUtil.GetTime();
            var timeDiff = (currentUnixTime - lastBlock.Timestamp) / 60.0M;
            //If no validators are detected then no need to run this code
            if (localValidator.Count != 0)
            {
                var validator = Validators.Validator.GetBlockValidator(); //need create consensus on who should actually do this. 
                //if validator is NaN then there are no validators on network and block creation will stop. 
                if (validator != "NaN")
                {
                    if(lastBlock.Height != 0)
                    {
                        var nextVals = validator.Split(':');
                        var mainVal = nextVals[0];
                        var secondaryVal = nextVals[1];

                        if (timeDiff >= 0.52M && timeDiff < 1.04M)
                        {

                            var accounts = DbContext.DB_Wallet.GetCollection<Account>(DbContext.RSRV_ACCOUNTS);
                            var account = accounts.Query().Where(x => x.Address == mainVal).FirstOrDefault();

                            if (account != null)
                            {
                                //craft new block
                                await BlockchainData.CraftNewBlock(validator);
                            }
                        }
                        if(timeDiff >= 1.04M && timeDiff < 2.0M)
                        {
                            var accounts = DbContext.DB_Wallet.GetCollection<Account>(DbContext.RSRV_ACCOUNTS);
                            var account = accounts.Query().Where(x => x.Address == secondaryVal).FirstOrDefault();

                            if (account != null)
                            {
                                //craft new block
                                await BlockchainData.CraftNewBlock(validator);
                            }
                        }
                        if(timeDiff > 2.0M)
                        {
                            //This will eventually be randomized and chosen through network, but for launch hard coding so blocks don't freeze after 2 mins of 
                            //non-responsive nodes. Though they are checked before being selected, but can still go offline in 30 seconds after check.
                            var backupValidator = "RBdwbhyqwJCTnoNe1n7vTXPJqi5HKc6NTH";
                            var accounts = DbContext.DB_Wallet.GetCollection<Account>(DbContext.RSRV_ACCOUNTS);
                            var account = accounts.Query().Where(x => x.Address == backupValidator).FirstOrDefault();

                            if (account != null)
                            {
                                //craft new block
                                await BlockchainData.CraftNewBlock(backupValidator);
                            }
                        }
                    }
                    else
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
            var peersConnected = await P2PClient.ArePeersConnected();

            if (peersConnected.Item1 != true)
            {
                Console.WriteLine("You have lost connection to all peers. Attempting to reconnect...");
                StartupService.StartupPeers();
                //potentially no connected nodes.
            }
        }

        private static async void validatorListCheckTimer_Elapsed(object sender)
        {
            var peersConnected = await P2PClient.ArePeersConnected();

            if (peersConnected.Item1 != true)
            {
                Console.WriteLine("You have lost connection to all peers. Attempting to reconnect...");
                StartupService.StartupPeers();
                //potentially no connected nodes.
            }
            else
            {
                var getMasterNodes = await P2PClient.GetMasternodes();
                if (getMasterNodes == true)
                {
                    Console.WriteLine("Masternode List Updated!");
                }
            }
        }
    }
}



