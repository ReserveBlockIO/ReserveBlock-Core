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

        #region Constants

        private static Timer? blockTimer;//for creating a new block at max every 30 seconds
        private static Timer? BlockHeightTimer; //Checking Height of other nodes to see if new block is needed
        private static Timer? PeerCheckTimer;//checks currents peers and old peers and will request others to try. 
        private static Timer? ValidatorListTimer;//checks currents peers and old peers and will request others to try. 
        private static Timer? DBCommitTimer;//checks dbs and commits log files. 
        public static List<Block> MemBlocks = new List<Block>();
        public static List<Block> QueuedBlocks = new List<Block>();
        public static List<Transaction> MempoolList = new List<Transaction>();
        public static bool BlocksDownloading = false;
        public static bool IsCrafting = false;
        public static bool TestURL = false;
        public static bool StopAllTimers = false;
        public static bool PeersConnecting = false;

        #endregion

        static async Task Main(string[] args)
        {
            var argList = args.ToList();

            StartupService.StartupDatabase();// initializes databases
            StartupService.SetBlockchainChainRef(); // sets blockchain reference id
            StartupService.SetBootstrapValidators(); //sets initial validators from bootstrap list.
            
            PeersConnecting = true;
            BlocksDownloading = true;
            StopAllTimers = true;
            

            blockTimer = new Timer(blockBuilder_Elapsed); // 1 sec = 1000, 60 sec = 60000
            blockTimer.Change(60000, 10000); //waits 1 minute, then runs every 10 seconds for new blocks

            //BlockHeightTimer = new Timer(blockHeightCheck_Elapsed); // 1 sec = 1000, 60 sec = 60000
            //BlockHeightTimer.Change(60000, 15000); //waits 1 minute, then runs every 37 seconds for new block heights

            PeerCheckTimer = new Timer(peerCheckTimer_Elapsed); // 1 sec = 1000, 60 sec = 60000
            PeerCheckTimer.Change(90000, 1 * 10 * 6000); //waits 1.5 minute, then runs every 60 seconds

            ValidatorListTimer = new Timer(validatorListCheckTimer_Elapsed); // 1 sec = 1000, 60 sec = 60000
            ValidatorListTimer.Change(90000, 60 * 10 * 6000); //waits 1 minute, then runs every 1 hour

            DBCommitTimer = new Timer(dbCommitCheckTimer_Elapsed); // 1 sec = 1000, 60 sec = 60000
            DBCommitTimer.Change(90000, 5 * 10 * 6000); //waits 1.5 minute, then runs every 5 minutes

            //add method to remove stale state trei records and stale validator records too

            //To update this go to project -> right click properties -> go To debug -> general -> open debug launch profiles
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
                    if (argC == "testurl")
                    {
                        //Launch testnet
                        TestURL = true;
                    }
                });
            }

            string url = TestURL == false ? "http://*:8080" : "https://*:7777"; //local API to connect to wallet. This can be changed, but be cautious. 
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

            StartupService.CheckForDuplicateBlocks();//Check for duplicate block adds due to back close
            try
            {

                await StartupService.StartupPeers();
                PeersConnecting = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            await StartupService.DownloadBlocksOnStart(); //download blocks from peers on start.
            StartupService.StartupMemBlocks();//adds last 15 blocks to memory for stale tx searching

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
                        StopAllTimers = true;
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
            if(StopAllTimers == false)
            {
                var localValidator = Validators.Validator.GetLocalValidator();
                var lastBlock = BlockchainData.GetLastBlock();
                var currentUnixTime = Utilities.TimeUtil.GetTime();
                var timeDiff = (currentUnixTime - lastBlock.Timestamp) / 60.0M;
                //If no validators are detected then no need to run this code
                if (IsCrafting == false)
                {
                    if (localValidator.Count != 0)
                    {
                        IsCrafting = true;
                        var validator = Validators.Validator.GetBlockValidator(); 
                                                                                  
                        if (validator != "NaN")
                        {
                            if (lastBlock.Height != 0)
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
                                        await BlockchainData.CraftNewBlock(mainVal);
                                    }
                                }
                                if (timeDiff >= 1.5M && timeDiff < 2.0M)
                                {
                                    var accounts = DbContext.DB_Wallet.GetCollection<Account>(DbContext.RSRV_ACCOUNTS);
                                    var account = accounts.Query().Where(x => x.Address == secondaryVal).FirstOrDefault();

                                    if (account != null)
                                    {
                                        //craft new block
                                        await BlockchainData.CraftNewBlock(secondaryVal);
                                    }
                                }
                                if (timeDiff > 2.20M)
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
                    IsCrafting = false;
                }
            }
        }
        private static async void blockHeightCheck_Elapsed(object sender)
        {
            if (StopAllTimers == false)
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
            
        }

        private static async void peerCheckTimer_Elapsed(object sender)
        {
            if (StopAllTimers == false)
            {
                var peersConnected = await P2PClient.ArePeersConnected();

                if (peersConnected.Item1 != true)
                {
                    Console.WriteLine("You have lost connection to all peers. Attempting to reconnect...");
                    await StartupService.StartupPeers();
                    //potentially no connected nodes.
                }
            }
            
        }

        private static async void validatorListCheckTimer_Elapsed(object sender)
        {
            if (StopAllTimers == false)
            {
                var peersConnected = await P2PClient.ArePeersConnected();

                if (peersConnected.Item1 != true)
                {
                    Console.WriteLine("You have lost connection to all peers. Attempting to reconnect...");
                    await StartupService.StartupPeers();
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

        private static async void dbCommitCheckTimer_Elapsed(object sender)
        {
            if (StopAllTimers == false)
            {
                //if blocks are currently downloading this will stop it from running again.
                try
                {
                    DbContext.DB.Checkpoint();
                }
                catch (Exception ex)
                {
                    //error saving from db cache
                }
                try
                {
                    DbContext.DB_AccountStateTrei.Checkpoint();
                }
                catch (Exception ex)
                {
                    //error saving from db cache
                }
                try
                {
                    DbContext.DB_Banlist.Checkpoint();
                }
                catch (Exception ex)
                {
                    //error saving from db cache
                }
                try
                {
                    DbContext.DB_Peers.Checkpoint();
                }
                catch (Exception ex)
                {
                    //error saving from db cache
                }
                try
                {
                    DbContext.DB_Wallet.Checkpoint();
                }
                catch (Exception ex)
                {
                    //error saving from db cache
                }
                try
                {
                    DbContext.DB_WorldStateTrei.Checkpoint();
                }
                catch (Exception ex)
                {
                    //error saving from db cache
                }
            }
            
        }
    }

   
}



