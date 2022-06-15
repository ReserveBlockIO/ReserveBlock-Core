global using ReserveBlockCore.Extensions;

using LiteDB;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using ReserveBlockCore.Commands;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;
using ReserveBlockCore.Trillium;
using ReserveBlockCore.Utilities;
using System.Diagnostics;
using System.Net.Sockets;
using System.Reflection;

namespace ReserveBlockCore
{
    class Program
    {
        #region Constants

        private static Timer? heightTimer; //timer for getting height from other nodes
        private static Timer? PeerCheckTimer;//checks currents peers and old peers and will request others to try. 
        private static Timer? ValidatorListTimer;//checks currents peers and old peers and will request others to try. 
        private static Timer? DBCommitTimer;//checks dbs and commits log files. 


        public static List<Block> MemBlocks = new List<Block>();
        public static List<Block> QueuedBlocks = new List<Block>();
        public static List<Transaction> MempoolList = new List<Transaction>();
        public static List<NodeInfo> Nodes = new List<NodeInfo>();
        public static List<Validators> InactiveValidators = new List<Validators>();
        public static List<Validators> MasternodePool = new List<Validators>();
        public static long BlockHeight = -1;
        public static Block LastBlock = new Block();
        public static Adjudicators? LeadAdjudicator = null;
        public static Guid AdjudicatorKey = Adjudicators.AdjudicatorData.GetAdjudicatorKey();
        public static bool Adjudicate = false;
        public static bool AdjudicateLock = false;
        public static long LastAdjudicateTime = 0;
        public static bool BlocksDownloading = false;
        public static bool HeightCheckLock = false;
        public static bool InactiveNodeSendLock = false;
        public static bool IsCrafting = false;
        public static bool IsResyncing = false;
        public static bool TestURL = false;
        public static bool StopAllTimers = false;
        public static bool PeersConnecting = false;
        public static bool DatabaseCorruptionDetected = false;
        public static bool BlockCrafting = false;
        public static bool RemoteCraftLock = false;
        public static bool IsChainSynced = false;
        public static bool OptionalLogging = false;
        public static DateTime? RemoteCraftLockTime = null;
        public static string ValidatorAddress = "";
        public static bool IsTestNet = false;
        public static int Port = 3338;
        public static int APIPort = 7292;
        public static string GenesisAddress = "RBdwbhyqwJCTnoNe1n7vTXPJqi5HKc6NTH";
        public static byte AddressPrefix = 0x3C; //address prefix 'R'
        public static bool PrintConsoleErrors = false;
        public static Process proc = new Process();
        public static int MajorVer = 1;
        public static int MinorVer = 22;
        public static int BuildVer = 0;
        public static string CLIVersion = "";
        public static bool HDWallet = false;

        private readonly IHubContext<P2PAdjServer> _hubContext;

        private Program(IHubContext<P2PAdjServer> hubContext)
        {
            _hubContext = hubContext;
        }

        #endregion

        #region Main
        static async Task Main(string[] args)
        {
            DateTime originDate = new DateTime(2022, 1, 1);
            DateTime currentDate = DateTime.Now;

            var dateDiff = (int)Math.Round((currentDate - originDate).TotalDays);
            BuildVer = dateDiff;

            CLIVersion = MajorVer.ToString() + "." + MinorVer.ToString() + "." + BuildVer.ToString() + "-pre";
            LogUtility.Log("", "Main", true);
            var logCLIVer = CLIVersion.ToString();

            LogUtility.Log($"RBX Wallet - {logCLIVer}", "Main");

            NFTLogUtility.Log("", "Main", true);
            NFTLogUtility.Log($"RBX NFT ver. - {logCLIVer}", "Main");

            StartupService.AnotherInstanceCheck();

            StartupService.StartupDatabase();// initializes databases

            StartupService.HDWalletCheck();// checks for HD wallet

            //To update this go to project -> right click properties -> go To debug -> general -> open debug launch profiles
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

                    if(argC.Contains("privkey"))
                    {
                        try
                        {
                            var keySplit = argC.Split(new char[] { '=' });
                            var privateKey = keySplit[1];
                            var account = AccountData.RestoreAccount(privateKey);
                            if(account != null)
                            {
                                Console.WriteLine("Account Loaded: " + account.Address);
                            }
                            
                        }
                        catch (Exception ex)
                        {
                            //bad key
                        }
                    }
                });
            }

            //THis is for adjudicator start. This might need to be removed.
            P2PAdjServer.CurrentTaskQuestion = await TaskQuestionUtility.CreateTaskQuestion("rndNum");

            //Temporary for TestNet------------------------------------
            SeedNodeService.SeedNodes();
            var nodeIp = await SeedNodeService.PingSeedNode();
            await SeedNodeService.GetSeedNodePeers(nodeIp);
            //Temporary for TestNet------------------------------------


            StartupService.SetBlockchainChainRef(); // sets blockchain reference id
            StartupService.CheckBlockRefVerToDb();

            StartupService.SetBlockchainVersion(); //sets the block version for rules
            StartupService.SetBlockHeight();
            StartupService.SetLastBlock();

            StartupService.SetupNodeDictionary();
            StartupService.ClearStaleMempool();
            StartupService.SetValidator();
            

            StartupService.RunRules(); //rules for cleaning up wallet data.
            StartupService.ClearValidatorDups();

            if (IsTestNet == true)
            {
                
            }
            else
            {
                
                StartupService.SetBootstrapAdjudicator(); //sets initial validators from bootstrap list.
            }

            

            PeersConnecting = true;
            BlocksDownloading = true;
            StopAllTimers = true;

            //blockTimer = new Timer(blockBuilder_Elapsed); // 1 sec = 1000, 60 sec = 60000
            //blockTimer.Change(60000, 10000); //waits 1 minute, then runs every 10 seconds for new blocks

            heightTimer = new Timer(blockHeightCheck_Elapsed); // 1 sec = 1000, 60 sec = 60000
            heightTimer.Change(60000, 30000); //waits 1 minute, then runs every 30 seconds for new blocks

            PeerCheckTimer = new Timer(peerCheckTimer_Elapsed); // 1 sec = 1000, 60 sec = 60000
            PeerCheckTimer.Change(90000, 4 * 10 * 6000); //waits 1.5 minute, then runs every 4 minutes

            ValidatorListTimer = new Timer(validatorListCheckTimer_Elapsed); // 1 sec = 1000, 60 sec = 60000
            ValidatorListTimer.Change(70000, 2 * 10 * 6000); //waits 1 minute, then runs every 2 minutes

            DBCommitTimer = new Timer(dbCommitCheckTimer_Elapsed); // 1 sec = 1000, 60 sec = 60000
            DBCommitTimer.Change(90000, 3 * 10 * 6000); //waits 1.5 minute, then runs every 5 minutes

            //add method to remove stale state trei records and stale validator records too

            
            

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
                    webBuilder.ConfigureKestrel(options => { 
                        
                        
                    });
                });

            builder.RunConsoleAsync();
            builder2.RunConsoleAsync();

            LogUtility.Log("Wallet Starting...", "Program:Before CheckLastBlock()");

            StartupService.CheckLastBlock();
            StartupService.CheckForDuplicateBlocks();//Commenting this out as duplicate blocks should not happen.

            try
            {
                await StartupService.StartupPeers();
                PeersConnecting = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            await StartupService.SetLeadAdjudicator();
            StartupService.SetSelfAdjudicator();
            await StartupService.DownloadBlocksOnStart(); //download blocks from peers on start.

            await StartupService.ConnectoToAdjudicator();
            
            StartupService.StartupMemBlocks();

            Thread.Sleep(3000);

            Task.WaitAll(commandLoopTask, commandLoopTask2);

            LogUtility.Log("Wallet Started and Running...", "Program:Before Task.WaitAll(commandLoopTask, commandLoopTask2)");

            //await Task.WhenAny(builder2.RunConsoleAsync(), commandLoopTask2);
            //await Task.WhenAny(builder.RunConsoleAsync(), commandLoopTask);
        }

        #endregion

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
                    string commandResult = "";
                    if (command.Contains(","))
                    {
                        var splitCommand = command.Split(',');
                        commandResult = BaseCommand.ProcessCommand(splitCommand[0], splitCommand[1]);
                    }
                    else
                    {
                        commandResult = BaseCommand.ProcessCommand(command);
                    }
                    

                    if (commandResult == "_EXIT")
                    {
                        StopAllTimers = true;
                        Console.WriteLine("Closing and Exiting Wallet Application.");
                        Thread.Sleep(2000);
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
            //Console.ReadKey();
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
                            if (ValidatorAddress == "")
                            {
                                var nodes = Nodes.ToList();
                                var maxHeightNode = nodes.OrderByDescending(x => x.NodeHeight).FirstOrDefault();
                                if (maxHeightNode != null)
                                {
                                    var maxHeight = maxHeightNode.NodeHeight;

                                    if (maxHeight > BlockHeight)
                                    {
                                        if (BlocksDownloading == false)
                                        {
                                            BlocksDownloading = true;
                                            var setDownload = await BlockDownloadService.GetAllBlocks(maxHeight);
                                            BlocksDownloading = setDownload;
                                        }
                                    }
                                }
                            }
                            
                            //testing purposes only.
                            //Nodes.ForEach(x =>
                            //{
                            //    Console.WriteLine(x.NodeIP);
                            //    Console.WriteLine(x.NodeHeight.ToString());
                            //    Console.WriteLine(x.NodeLastChecked != null ? x.NodeLastChecked.Value.ToLocalTime() : "N/A");
                            //    Console.WriteLine((x.NodeLatency / 10).ToString() + " ms");

                            //});

                        }
                        HeightCheckLock = false;
                    }
                }
            }
            try
            {
                DebugUtility.WriteToDebugFile();
            }
            catch (Exception ex)
            {

            }
            
        }

        #endregion

        #region Peer Online Check
        private static async void peerCheckTimer_Elapsed(object sender)
        {
            if (StopAllTimers == false)
            {
                try
                {
                    var peersConnected = await P2PClient.ArePeersConnected();

                    if (peersConnected.Item1 != true)
                    {
                        Console.WriteLine("You have lost connection to all peers. Attempting to reconnect...");
                        LogUtility.Log("Connection to Peers Lost", "peerCheckTimer_Elapsed()");
                        await StartupService.StartupPeers();
                        //potentially no connected nodes.
                    }
                    else
                    {
                        if (peersConnected.Item2 != 6)
                        {
                            bool result = false;
                            //Get more nodes!
                            result = await P2PClient.ConnectToPeers();
                        }
                    }
                }
                catch(Exception ex)
                {
                    ErrorLogUtility.LogError(ex.Message, "Program.peerCheckTimer_Elapsed()");
                }
                
            }
            
        }

        #endregion

        #region Validator Checks
        private static async void validatorListCheckTimer_Elapsed(object sender)
        {
            if (StopAllTimers == false)
            {
                ValidatorService.ClearDuplicates();

                var peersConnected = await P2PClient.ArePeersConnected();

                if (peersConnected.Item1 != true)
                {
                    Console.WriteLine("You have lost connection to all peers. Attempting to reconnect...");
                    LogUtility.Log("Connection to Peers Lost", "validatorListCheckTimer_Elapsed()");
                    await StartupService.StartupPeers();
                    //potentially no connected nodes.
                }
                else
                {
                    if(Program.ValidatorAddress != "")
                    {
                        //Check connection to head val and update.
                        var connection = P2PClient.IsAdjConnected1;
                        if (connection != true)
                        {
                            Console.WriteLine("You have lost connection to the adjudicator. Attempting to reconnect...");
                            LogUtility.Log("Connection to Adj Lost", "validatorListCheckTimer_Elapsed()");
                            await StartupService.ConnectoToAdjudicator();
                        }
                    }


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



