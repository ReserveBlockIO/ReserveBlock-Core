using LiteDB;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using ReserveBlockCore.Commands;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System.Diagnostics;
using System.Net.Sockets;

namespace ReserveBlockCore
{
    class Program
    {

        #region Constants

        private static Timer? blockTimer;//for creating a new block at max every 30 seconds
        private static Timer? heightTimer; //timer for getting height from other nodes
        private static Timer? PeerCheckTimer;//checks currents peers and old peers and will request others to try. 
        private static Timer? ValidatorListTimer;//checks currents peers and old peers and will request others to try. 
        private static Timer? DBCommitTimer;//checks dbs and commits log files. 

        public static List<Block> MemBlocks = new List<Block>();
        public static List<Block> QueuedBlocks = new List<Block>();
        public static List<Transaction> MempoolList = new List<Transaction>();
        public static List<NodeInfo> Nodes = new List<NodeInfo>();
        public static List<Validators> InactiveValidators = new List<Validators>();
        public static bool BlocksDownloading = false;
        public static bool HeightCheckLock = false;
        public static bool InactiveNodeSendLock = false;
        public static bool IsCrafting = false;
        public static bool TestURL = false;
        public static bool StopAllTimers = false;
        public static bool PeersConnecting = false;
        public static int BlockValidateFailCount = 0;
        public static bool BlockCrafting = false;
        public static string ValidatorAddress = "";
        public static bool IsTestNet = false;
        public static int Port = 3338;
        public static int APIPort = 7292;
        public static string GenesisAddress = "RBdwbhyqwJCTnoNe1n7vTXPJqi5HKc6NTH";
        public static byte AddressPrefix = 0x3C; //address prefix 'R'
        public static bool PrintConsoleErrors = false;
        public static Process proc = new Process();
        public static string CLIVersion = "1.12.0";

        #endregion
        static async Task Main(string[] args)
        {
            var argList = args.ToList();
            
            if (args.Length != 0)
            {
                argList.ForEach(x => {
                    var argC = x.ToLower();
                    if (argC == "enableapi")
                    {
                        Startup.APIEnabled = true; //api disabled by default
                    }
                    if(argC == "hidecli")
                    {
                        ProcessStartInfo start = new ProcessStartInfo();
                        start.FileName = Directory.GetCurrentDirectory() + @"\RBXCore\ReserveBlockCore.exe";
                        start.WindowStyle = ProcessWindowStyle.Hidden; //Hides GUI
                        start.CreateNoWindow = true; //Hides console
                        start.Arguments = "enableapi";

                        proc.StartInfo = start;
                        proc.Start();

                        Environment.Exit(0);
                    }
                    if (argC == "gui")
                    {
                        //launch gui
                    }
                    if (argC == "testnet")
                    {
                        //Launch testnet
                        IsTestNet = true;
                        GenesisAddress = "xAfPR4w2cBsvmB7Ju5mToBLtJYuv1AZSyo";
                        Port = 13338;
                        APIPort = 17292;
                        AddressPrefix = 0x89; //address prefix 'x'
                    }
                    if (argC == "testurl")
                    {
                        //Launch testnet
                        TestURL = true;
                    }
                });
            }

            StartupService.AnotherInstanceCheck();
            StartupService.StartupDatabase();// initializes databases
            StartupService.SetBlockchainChainRef(); // sets blockchain reference id
            StartupService.SetBlockchainVersion(); //sets the block version for rules
            StartupService.SetupNodeDictionary();
            StartupService.ClearStaleMempool();
            StartupService.RunRules(); //rules for cleaning up wallet data.

            if (IsTestNet == true)
            {
                StartupService.SetBootstrapValidatorsTestNet();
            }
            else
            {
                StartupService.SetBootstrapValidators(); //sets initial validators from bootstrap list.
            }
             
            //StartupService.ResetStateTreis();

            StartupService.CheckLastBlock();
            //StartupService.ResetEntireChain(); //Might need to put this back in other spot.***************************

            PeersConnecting = true;
            BlocksDownloading = true;
            StopAllTimers = true;

            blockTimer = new Timer(blockBuilder_Elapsed); // 1 sec = 1000, 60 sec = 60000
            blockTimer.Change(60000, 5000); //waits 1 minute, then runs every 5 seconds for new blocks

            heightTimer = new Timer(blockHeightCheck_Elapsed); // 1 sec = 1000, 60 sec = 60000
            heightTimer.Change(60000, 10000); //waits 1 minute, then runs every 10 seconds for new blocks

            PeerCheckTimer = new Timer(peerCheckTimer_Elapsed); // 1 sec = 1000, 60 sec = 60000
            PeerCheckTimer.Change(90000, 4 * 10 * 6000); //waits 1.5 minute, then runs every 4 minutes

            ValidatorListTimer = new Timer(validatorListCheckTimer_Elapsed); // 1 sec = 1000, 60 sec = 60000
            ValidatorListTimer.Change(70000, 2 * 10 * 6000); //waits 1 minute, then runs every 2 minutes

            DBCommitTimer = new Timer(dbCommitCheckTimer_Elapsed); // 1 sec = 1000, 60 sec = 60000
            DBCommitTimer.Change(90000, 5 * 10 * 6000); //waits 1.5 minute, then runs every 5 minutes

            //add method to remove stale state trei records and stale validator records too

            //To update this go to project -> right click properties -> go To debug -> general -> open debug launch profiles
            

            string url = TestURL == false ? "http://*:" + APIPort : "https://*:7777"; //local API to connect to wallet. This can be changed, but be cautious. 
            string url2 = "http://*:" + Port; //this is port for signalr connect and all p2p functions
            //string url2 = "https://*:3338" //This is non http version. Must uncomment out app.UseHttpsRedirection() in startupp2p
            
            var commandLoopTask = Task.Run(() => CommandLoop(url));
            var commandLoopTask2 = Task.Run(() => CommandLoop2(url2));

            //for web API using Kestrel
            var builder = Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel()
                    .UseStartup<Startup>()
                    .UseUrls(url)
                    .ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
                });

            //for p2p using signalr
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

            StartupService.CheckForDuplicateBlocks();//Check for duplicate block adds due to back close. This will also reset chain

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
            await StartupService.BroadcastValidatorOnline();
            StartupService.StartupMemBlocks();//adds last 15 blocks to memory for stale tx searching

            Thread.Sleep(3000);

            Task.WaitAll(commandLoopTask, commandLoopTask2);

            //await Task.WhenAny(builder2.RunConsoleAsync(), commandLoopTask2);
            //await Task.WhenAny(builder.RunConsoleAsync(), commandLoopTask);
        }

        #region Command Loops
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

        #endregion

        #region Block Building
        private static async void blockBuilder_Elapsed(object sender)
        {
            if(StopAllTimers == false)
            {
                //process block queue first
                await BlockQueueService.ProcessBlockQueue();
                var localValidator = Validators.Validator.GetLocalValidator();
                var lastBlock = BlockchainData.GetLastBlock();
                var currentUnixTime = TimeUtil.GetTime();
                var timeDiff = (currentUnixTime - lastBlock.Timestamp) / 60.0M;
                var validators = Validators.Validator.GetAll();
                //If no validators are detected then no need to run this code
                if (IsCrafting == false)
                {
                    if (localValidator.Count == 0) // Change back to != 0
                    {
                        IsCrafting = true;
                        var nextValidators = Validators.Validator.GetBlockValidator(); 
                                                                                  
                        if (nextValidators != "NaN")
                        {
                            var nextVals = nextValidators.Split(':');
                            var mainVal = nextVals[0];
                            var secondaryVal = nextVals[1];

                            if (lastBlock.Height != 0)
                            {
                                if (timeDiff >= 0.52M && timeDiff < 1.04M)
                                {
                                    if(mainVal == ValidatorAddress)
                                    {
                                        var accounts = DbContext.DB_Wallet.GetCollection<Account>(DbContext.RSRV_ACCOUNTS);
                                        var account = accounts.Query().Where(x => x.Address == mainVal).FirstOrDefault();

                                        if (account != null)
                                        {
                                            //craft new block
                                            await BlockchainData.CraftNewBlock(mainVal);
                                        }
                                    }
                                    
                                }
                                if (timeDiff >= 1.5M && timeDiff < 2.0M)
                                {
                                    //CALL OUT TO FIRST NODE AND MAKE SURE THEY DID NOT MAKE BLOCK!!!!
                                    if(secondaryVal == ValidatorAddress)
                                    {
                                        bool craftBlock = false;
                                        try
                                        {
                                            var mainValidator = validators.FindOne(x => x.Address == mainVal);
                                            if (mainValidator != null)
                                            {
                                                var result = await P2PClient.CallCrafter(mainValidator);
                                                if (result == true)
                                                {
                                                    var height = lastBlock.Height;
                                                    BlockCrafting = true;
                                                    var block = await P2PClient.GetNewlyCraftedBlock(height, mainValidator);
                                                    if (block != null)
                                                    {
                                                        var blocks = BlockchainData.GetBlocks();
                                                        var blockFromChain = blocks.FindOne(x => x.Height == block.Height);
                                                        if (blockFromChain == null)
                                                        {
                                                            var blockResult = await BlockValidatorService.ValidateBlock(block);
                                                            if (blockResult == true)
                                                            {
                                                                //block added and found!
                                                                BlockCrafting = false;
                                                            }
                                                            else
                                                            {
                                                                craftBlock = true;
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        //if peer is online but not crafting that should not happen.
                                                        craftBlock = true;
                                                    }
                                                }
                                                else
                                                {
                                                    craftBlock = true;
                                                }
                                            }
                                            else
                                            {
                                                await P2PClient.GetMasternodes();
                                                validators = Validators.Validator.GetAll();
                                                mainValidator = validators.FindOne(x => x.Address == mainVal);
                                                if (mainValidator != null)
                                                {
                                                    var result = await P2PClient.CallCrafter(mainValidator);
                                                    if (result == true)
                                                    {
                                                        var height = lastBlock.Height;
                                                        BlockCrafting = true;
                                                        var block = await P2PClient.GetNewlyCraftedBlock(height, mainValidator);
                                                        if (block != null)
                                                        {
                                                            var blocks = BlockchainData.GetBlocks();
                                                            var blockFromChain = blocks.FindOne(x => x.Height == block.Height);
                                                            if (blockFromChain == null)
                                                            {
                                                                var blockResult = await BlockValidatorService.ValidateBlock(block);
                                                                if (blockResult == true)
                                                                {
                                                                    //block added and found!
                                                                    BlockCrafting = false;
                                                                }
                                                                else
                                                                {
                                                                    craftBlock = true;
                                                                }
                                                            }
                                                        }
                                                        else
                                                        {
                                                            //Peer can be online, but not making the block.
                                                            craftBlock = true;
                                                        }

                                                    }
                                                    else
                                                    {
                                                        craftBlock = true;
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            craftBlock = true;
                                            BlockCrafting = false;
                                        }

                                        var accounts = DbContext.DB_Wallet.GetCollection<Account>(DbContext.RSRV_ACCOUNTS);
                                        var account = accounts.FindOne(x => x.Address == secondaryVal);

                                        if (account != null && craftBlock == true)
                                        {
                                            //craft new block
                                            await BlockchainData.CraftNewBlock(secondaryVal);
                                            BlockCrafting = false;
                                        }
                                        BlockCrafting = false;
                                    }
                                    
                                }
                                if (timeDiff > 2.20M)
                                {
                                    //CALL OUT TO FIRST AND SECOND NODE AND MAKE SURE THEY DID NOT MAKE BLOCK!!!!
                                    //This will eventually be randomized and chosen through network, but for launch hard coding so blocks don't freeze after 2 mins of 
                                    //non-responsive nodes. Though they are checked before being selected, but can still go offline in 30 seconds after check.
                                    bool craftBlock = false;
                                    try
                                    {
                                        await P2PClient.GetMasternodes(); //ensure we have validators
                                        var mainValidator = validators.FindOne(x => x.Address == mainVal);
                                        var secondValidator = validators.FindOne(x => x.Address == secondaryVal);

                                        if (mainValidator != null)
                                        {
                                            var result = await P2PClient.CallCrafter(mainValidator);
                                            if (result == true)
                                            {
                                                var height = lastBlock.Height;
                                                BlockCrafting = true;
                                                var block = await P2PClient.GetNewlyCraftedBlock(height, mainValidator);
                                                if (block != null)
                                                {
                                                    var blocks = BlockchainData.GetBlocks();
                                                    var blockFromChain = blocks.FindOne(x => x.Height == block.Height);
                                                    if (blockFromChain == null)
                                                    {
                                                        var blockResult = await BlockValidatorService.ValidateBlock(block);
                                                        if (blockResult == true)
                                                        {
                                                            //block added and found!
                                                            BlockCrafting = false;
                                                        }
                                                        else
                                                        {
                                                            craftBlock = true;
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    //if peer is online but not crafting that should not happen.
                                                    craftBlock = true;
                                                }
                                            }
                                            else
                                            {
                                                craftBlock = true;
                                            }
                                        }
                                        
                                        if (secondValidator != null)
                                        {
                                            var result = await P2PClient.CallCrafter(secondValidator);
                                            if (result == true)
                                            {
                                                var height = lastBlock.Height;
                                                BlockCrafting = true;
                                                var block = await P2PClient.GetNewlyCraftedBlock(height, secondValidator);
                                                if (block != null)
                                                {
                                                    var blocks = BlockchainData.GetBlocks();
                                                    var blockFromChain = blocks.FindOne(x => x.Height == block.Height);
                                                    if (blockFromChain == null)
                                                    {
                                                        var blockResult = await BlockValidatorService.ValidateBlock(block);
                                                        if (blockResult == true)
                                                        {
                                                            //block added and found!
                                                            BlockCrafting = false;
                                                            craftBlock = false;
                                                        }
                                                        else
                                                        {
                                                            craftBlock = true;
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    //Peer can be online, but not making the block.
                                                    craftBlock = true;
                                                }

                                            }
                                            else
                                            {
                                                craftBlock = true;
                                            }
                                        }
                                        else
                                        {
                                            craftBlock = true;
                                        }
                                        
                                    }
                                    catch (Exception ex)
                                    {
                                        craftBlock = true;
                                    }
                                    var backupValidator = Program.GenesisAddress;
                                    var accounts = DbContext.DB_Wallet.GetCollection<Account>(DbContext.RSRV_ACCOUNTS);
                                    var account = accounts.Query().Where(x => x.Address == backupValidator).FirstOrDefault();

                                    if (account != null && craftBlock == true)
                                    {
                                        //craft new block
                                        await BlockchainData.CraftNewBlock(backupValidator);
                                        BlockCrafting = false;
                                    }

                                    BlockCrafting = false;
                                }
                            }
                            else
                            {
                                var accounts = DbContext.DB_Wallet.GetCollection<Account>(DbContext.RSRV_ACCOUNTS);
                                var account = accounts.Query().Where(x => x.Address == mainVal).FirstOrDefault();

                                if (account != null)
                                {
                                    //craft new block
                                    await BlockchainData.CraftNewBlock(mainVal);
                                }
                            }

                        }
                    }
                    IsCrafting = false;
                }
            }
        }

#endregion

        #region Block Height Check
        private static async void blockHeightCheck_Elapsed(object sender)
        {
            if (StopAllTimers == false)
            {
                //if blocks are currently downloading this will stop it from running again.
                if (BlocksDownloading != true)
                {
                    if(HeightCheckLock == false)
                    {
                        HeightCheckLock = true;
                        var nodeHeightDict = await P2PClient.GetNodeHeight();
                        if(nodeHeightDict == false)
                        {
                            //do some reconnect logic possibly here.
                        }
                        else
                        {
                            //testing purposes only.
                            //Nodes.ForEach(x =>
                            //{
                            //    Console.WriteLine(x.NodeIP);
                            //    Console.WriteLine(x.NodeHeight.ToString());
                            //    Console.WriteLine(x.NodeLastChecked != null ? x.NodeLastChecked.Value.ToLocalTime() : "N/A");
                            //    Console.WriteLine(x.NodeLatency.ToString() + " ms");
                                
                            //});
                        }
                        HeightCheckLock = false;
                    }
                }
            }
            
        }

        #endregion

        #region Peer Online Check
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
                else
                {
                    if(peersConnected.Item2 != 6)
                    {
                        bool result = false;
                        //Get more nodes!
                        result = await P2PClient.ConnectToPeers();
                    }
                }
            }
            
        }

        #endregion

        #region Validator Checks
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

                    if(InactiveValidators.Count() > 0)
                    {
                        if(InactiveNodeSendLock == false)
                        {
                            InactiveNodeSendLock = true;
                            await P2PClient.SendInactiveNodes(InactiveValidators);
                            InactiveNodeSendLock = false;
                        }
                    }

                    await StartupService.BroadcastValidatorOnline();
                }
            }
            
        }

        #endregion

        #region DB Commits
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

        #endregion
    }


}



