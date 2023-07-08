using Microsoft.AspNetCore.SignalR;
using ReserveBlockCore.Data;
using ReserveBlockCore.DST;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.DST;
using ReserveBlockCore.Utilities;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Security;

namespace ReserveBlockCore
{
    public static class Globals
    {
        static Globals()
        {
            var Source = new CancellationTokenSource();
            Source.Cancel();
            CancelledToken = Source.Token;
        }

        public class MethodCallCount
        {
            public int Enters { get; set; }
            public int Exits { get; set; }
            public int Exceptions { get; set; }
        }


        #region Timers
        public static bool IsTestNet = false;

        public static string LeadAddress = "RBXpH37qVvNwzLjtcZiwEnb3aPNG815TUY";      
        public static Timer? ValidatorListTimer;//checks currents peers and old peers and will request others to try. 
        public static Timer? DBCommitTimer;//checks dbs and commits log files. 
        public static Timer? ConnectionHistoryTimer;//process connections and history of them        

        #endregion

        #region Global General Variables
        public static byte AddressPrefix = 0x3C; //address prefix 'R'        
        public static ConcurrentDictionary<string, AdjNodeInfo> AdjNodes = new ConcurrentDictionary<string, AdjNodeInfo>(); // IP Address        
        public static ConcurrentDictionary<string, bool> Signers = new ConcurrentDictionary<string, bool>();
        public static ConcurrentDictionary<string, MethodCallCount> MethodDict = new ConcurrentDictionary<string, MethodCallCount>();
        public static ConcurrentDictionary<string, ReserveTransactions> ReserveTransactionsDict = new ConcurrentDictionary<string, ReserveTransactions>();
        public static string SignerCache = "";
        public static string IpAddressCache = "";
        public static object SignerCacheLock = new object();
        public static string FortisPoolCache = "";
        public static Block LastBlock = new Block { Height = -1 };
        public static Adjudicators? LeadAdjudicator = null;
        public static Guid AdjudicatorKey = Adjudicators.AdjudicatorData.GetAdjudicatorKey();
        public static BeaconReference BeaconReference = new BeaconReference();
        public static Beacons? SelfBeacon = null;
        public static long LastBlockAddedTimestamp = TimeUtil.GetTime();
        public static long BlockTimeDiff = 0;
        public static Block? LastWonBlock = null;
        public static Process GUIProcess;
        public static Blockchain Blockchain { get; set; }

        public static DateTime? RemoteCraftLockTime = null;        
        public static DateTime? CLIWalletUnlockTime = null;
        public static DateTime? APIUnlockTime = null;
        public static DateTime? ExplorerValDataLastSend = null;

        public const int ValidatorRequiredRBX = 12000;
        public const decimal ADNRRequiredRBX = 5.0M;
        public const decimal ADNRTransferRequiredRBX = 1.0M;
        public const decimal ADNRDeleteRequiredRBX = 0.0M;
        public const decimal TopicRequiredRBX = 10.0M;
        public const decimal DecShopRequiredRBX = 10.0M;
        public const decimal DecShopUpdateRequiredRBX = 1.0M;
        public const decimal DecShopDeleteRequiredRBX = 1.0M; //0
        public const decimal RSRVAccountRegisterRBX = 4.0M;

        public const int ADNRLimit = 65;
        public static int BlockLock = 1079488;
        public static long V3Height = 579015;
        public static long V1ValHeight = 832000;
        public static long TXHeightRule1 = 820457; //March 31th, 2023 at 03:44 UTC
        public static long TXHeightRule2 = 847847; //around April 7, 2023 at 18:30 UTC
        public static long TXHeightRule3 = 1079488; //around June 13th, 2023 at 19:30 UTC
        public static long LastAdjudicateTime = 0;
        public static SemaphoreSlim BlocksDownloadSlim = new SemaphoreSlim(1, 1);
        public static SemaphoreSlim BlocksDownloadV2Slim = new SemaphoreSlim(1, 1);
        public static int WalletUnlockTime = 0;
        public static int ChainCheckPointInterval = 0;
        public static int ChainCheckPointRetain = 0;
        public static int PasswordClearTime = 10;
        public static int NFTTimeout = 0;
        public static int Port = 3338;
        public static int ADJPort = 3339;
        public static int SelfSTUNPort = 3340;
        public static int DSTClientPort = 3341;
        public static int APIPort = 7292;
        public static int MajorVer = 4;
        public static int MinorVer = 0;
        public static int RevisionVer = 0;
        public static int BuildVer = 0;
        public static int SCVersion = 1;
        public static int ValidatorIssueCount = 0;
        public static bool ValidatorSending = true;
        public static bool ValidatorReceiving = true;
        public static bool ValidatorBalanceGood = true;
        public static List<string> ValidatorErrorMessages = new List<string>();
        public static long ValidatorLastBlockHeight = 0;
        public static string GitHubVersion = $"beta{MajorVer}.{MinorVer}.{RevisionVer}";
        public static string GitHubApiURL = "https://api.github.com/";
        public static string GitHubRBXRepoURL = "repos/ReserveBlockIO/ReserveBlock-Core/releases/latest";
        public static string GitHubLatestReleaseVersion = "";
        public static ConcurrentDictionary<string, string> GitHubLatestReleaseAssetsDict = new ConcurrentDictionary<string, string>();
        public static bool UpToDate = true;
        public static string StartArguments = "";
        public static DateTime NewUpdateLastChecked = DateTime.UtcNow.AddHours(-2);
        public static SecureString? APIToken = null;
        public static int TimeSyncDiff = 0;
        public static DateTime TimeSyncLastDate = DateTime.Now;
        public static decimal StartMemory = 0;
        public static decimal CurrentMemory = 0;
        public static decimal ProjectedMemory = 0;
        public static long SystemMemory = 1;

        public static string Platform = "";
        public static string ValidatorAddress = "";
        public static string? WalletPassword = null;
        public static string? APIPassword = null;
        public static string? APICallURL = null;
        public static string ChainCheckpointLocation = "";
        public static string ConfigValidator = "";
        public static string ConfigValidatorName = "";
        public static string GenesisAddress = "RBdwbhyqwJCTnoNe1n7vTXPJqi5HKc6NTH";
        public static string CLIVersion = "";
        public static string? MotherAddress = null;
        public static string? CustomPath = null;

        public static bool Lock = true;
        public static bool AlwaysRequireWalletPassword = false;
        public static bool AlwaysRequireAPIPassword = false;
        public static bool StopConsoleOutput = false;        
        public static int AdjudicateLock = 0;        
        public static Account AdjudicateAccount;
        public static PrivateKey AdjudicatePrivateKey;
        public static bool APICallURLLogging = false;
        public static bool ChainCheckPoint = false;
        public static bool PrintConsoleErrors = false;
        public static bool HDWallet = false;
        public static bool IsWalletEncrypted = false;
        public static bool AutoDownloadNFTAsset = false;
        public static bool IgnoreIncomingNFTs = false;
        public static bool ShowTrilliumOutput = false;
        public static bool ShowTrilliumDiagnosticBag = false;
        public static bool ConnectToMother = false;        
        public static bool InactiveNodeSendLock = false;
        public static bool IsCrafting = false;
        public static bool IsResyncing = false;
        public static bool TestURL = false;
        public static bool StopAllTimers = false;
        public static bool DatabaseCorruptionDetected = false;
        public static bool RemoteCraftLock = false;
        public static bool IsChainSynced = false;
        public static bool OptionalLogging = false;
        public static bool AdjPoolCheckLock = false;
        public static bool GUI = false;
        public static bool RunUnsafeCode = false;
        public static bool GUIPasswordNeeded = false;
        public static bool TreisUpdating = false;
        public static bool DuplicateAdjIP = false;
        public static bool DuplicateAdjAddr = false;
        public static bool ExplorerValDataLastSendSuccess = false;
        public static bool LogAPI = false;
        public static bool RefuseToCallSeed = false;
        public static bool OpenAPI = false;
        public static bool NFTFilesReadyEPN = false; // nft files ready, encryption password needed
        public static bool NFTsDownloading = false;
        public static bool TimeInSync = true;
        public static bool TimeSyncError = false;
        public static bool BasicCLI = false;
        public static bool MemoryOverload = false;
        public static bool SelfSTUNServer = false;
        public static bool ShowSTUNMessagesInConsole = false;
        public static bool STUNServerRunning = false;
        public static bool LogMemory = false;
        public static bool UseV2BlockDownload = false;
        
        public static CancellationToken CancelledToken;

        public static ConcurrentDictionary<string, long> MemBlocks = new ConcurrentDictionary<string, long>();
        public static ConcurrentDictionary<string, NodeInfo> Nodes = new ConcurrentDictionary<string, NodeInfo>(); // IP Address
        public static ConcurrentDictionary<string, AdjBench> AdjBench = new ConcurrentDictionary<string, AdjBench>(); // IP Address:Key
        public static ConcurrentDictionary<string, Validators> InactiveValidators = new ConcurrentDictionary<string, Validators>(); // RBX address        
        //public static ConcurrentDictionary<string, string> Locators = new ConcurrentDictionary<string, string>(); // BeaconUID
        public static ConcurrentDictionary<string, Mother.Kids> MothersKids = new ConcurrentDictionary<string, Mother.Kids>(); //Mothers Children
        public static ConcurrentDictionary<string, HubCallerContext> MothersKidsContext = new ConcurrentDictionary<string, HubCallerContext>(); //Mothers Children
        public static ConcurrentDictionary<string, Beacons> Beacons = new ConcurrentDictionary<string, Beacons>();
        public static ConcurrentBag<string> RejectAssetExtensionTypes = new ConcurrentBag<string>();
        public static ConcurrentDictionary<string, BeaconNodeInfo> Beacon = new ConcurrentDictionary<string, BeaconNodeInfo>();
        public static ConcurrentQueue<int> BlockDiffQueue = new ConcurrentQueue<int>();
        public static ConcurrentDictionary<string, long> ActiveValidatorDict = new ConcurrentDictionary<string, long>();
        public static ConcurrentBag<StunServer> STUNServers = new ConcurrentBag<StunServer>();


        public static SecureString EncryptPassword = new SecureString();
        public static SecureString DecryptPassword = new SecureString();
        public static SecureString? MotherPassword = null;

        public static IHttpClientFactory HttpClientFactory;        

        #endregion

        #region P2P Client Variables

        public const int MaxPeers = 14;
        public static ConcurrentDictionary<string, int> ReportedIPs = new ConcurrentDictionary<string, int>();
        public static ConcurrentDictionary<string, Peers> BannedIPs;        

        #endregion

        #region P2P Server Variables

        public static ConcurrentDictionary<string, HubCallerContext> P2PPeerDict = new ConcurrentDictionary<string, HubCallerContext>();
        public static ConcurrentDictionary<string, HubCallerContext> BeaconPeerDict = new ConcurrentDictionary<string, HubCallerContext>();        
        public static ConcurrentDictionary<string, MessageLock> MessageLocks = new ConcurrentDictionary<string, MessageLock>();
        public static ConcurrentDictionary<string, int> TxRebroadcastDict = new ConcurrentDictionary<string, int>();

        #endregion

        #region P2P Adj Server Variables

        public static ConcurrentMultiDictionary<string, string, FortisPool> FortisPool = new ConcurrentMultiDictionary<string, string, FortisPool>(); // IP address, RBX address        
        public static ConcurrentMultiDictionary<string, string, BeaconPool> BeaconPool = new ConcurrentMultiDictionary<string, string, BeaconPool>(); // IP address, Reference
        public static ConcurrentDictionary<string, ConnectionHistory.ConnectionHistoryQueue> ConnectionHistoryDict = new ConcurrentDictionary<string, ConnectionHistory.ConnectionHistoryQueue>();
        public static ConcurrentBag<ConnectionHistory> ConnectionHistoryList = new ConcurrentBag<ConnectionHistory>();
        public static ConcurrentDictionary<string, long> Signatures = new ConcurrentDictionary<string, long>();
        
        public static (long Height, int Answer, long Time) CurrentTaskNumberAnswerV3;
        public static TaskWinner CurrentWinner;
        public static string VerifySecret = "";        

        public static ConcurrentDictionary<string, ConcurrentDictionary<string, (DateTime Time, string Request, string Response)>> ConsensusDump = new ConcurrentDictionary<string, ConcurrentDictionary<string, (DateTime Time, string Request, string Response)>>();
        public static long ConsensusStartHeight = -1;
        public static long ConsensusSucceses = 0;
        
        public static ConcurrentDictionary<(string RBXAddress, long Height), Block> TaskWinnerDictV3 = new ConcurrentDictionary<(string RBXAddress, long Height), Block>(); // RBX address        
        public static ConcurrentDictionary<(string RBXAddress, long Height), (string IPAddress, string RBXAddress, int Answer)> TaskSelectedNumbersV3 = new ConcurrentDictionary<(string RBXAddres, long height), (string IPAddress, string RBXAddress, int Answer)>();        
        public static ConcurrentDictionary<(string RBXAddress, long Height), (string IPAddress, string RBXAddress, int Answer)> TaskAnswerDictV3 = new ConcurrentDictionary<(string RBXAddres, long height), (string IPAddress, string RBXAddress, int Answer)>();        
        public static ConcurrentDictionary<string, Transaction> BroadcastedTrxDict = new ConcurrentDictionary<string, Transaction>(); // TX Hash
        public static ConcurrentDictionary<string, TransactionBroadcast> ConsensusBroadcastedTrxDict = new ConcurrentDictionary<string, TransactionBroadcast>(); //TX Hash
        public static ConcurrentDictionary<string, DuplicateValidators> DuplicatesBroadcastedDict= new ConcurrentDictionary<string, DuplicateValidators>();

        #endregion        

        #region Bad TX Ignore List

        public static List<string> BadADNRTxList = new List<string> { "9ebe7eb08abcf35f7e5cad6a5346babcb045f0e52732cdfddd021296331c2056"};
        public static List<string> BadNFTTxList = new List<string>() { "70e34dd1b5d646addc5328f971b4ab370095985dcf4bce1d0e1ea222824daa6d" };
        public static List<string> BadTopicTxList = new List<string>();
        public static List<string> BadVoteTxList = new List<string>();
        public static List<string> BadTxList = new List<string> { "9065618ff356dc1dcef8cd5413ffe826f8ab45ca8b6bb9c8f9853d1de0b576ae", "b05b230c9f7fb6f9014c0a9a4a5b1c9ddaf36a96462635d628272b8c62e2e5b3" };
        public static List<string> BadDSTList = new List<string> { "8f9eec99c69ace2ad758048ceb281c38099173ca95a97c31114f2d136b34916a", 
        "a898112b2770ca2182d330d71f8830ad7eeb2b7ac9030cf33312ebeefd72c8a5",
        "152250f2673234765ab61e3f46e2ef94a80e50cf24bcaaf0ad5e0341f8b5626a",
        "241546578f04a3dbcf9e9195352750f7ff087ba39840759fe38e56e22f9d6139",};

        public static List<string> BadNodeList = new List<string>();

        #endregion

        #region DST Variables
        public const decimal BidModifier = 100000000M;
        public const decimal BidMinimum = 0.00000001M;
        public static ConcurrentDictionary<string, DSTConnection> ConnectedClients = new ConcurrentDictionary<string, DSTConnection>();
        public static ConcurrentDictionary<string, DSTConnection> ConnectedShops = new ConcurrentDictionary<string, DSTConnection>();
        public static DSTConnection? STUNServer = null;
        public static ConcurrentQueue<Message> ClientMessageQueue = new ConcurrentQueue<Message>();
        public static ConcurrentQueue<Message> ServerMessageQueue = new ConcurrentQueue<Message>();
        public static ConcurrentDictionary<string, List<Chat.ChatMessage>> ChatMessageDict = new ConcurrentDictionary<string, List<Chat.ChatMessage>>();
        public static ConcurrentDictionary<string, IPEndPoint> ShopChatUsers = new ConcurrentDictionary<string, IPEndPoint>();
        public static ConcurrentDictionary<string, MessageState> ClientMessageDict = new ConcurrentDictionary<string, MessageState>();
        public static ConcurrentDictionary<string, Message> ServerMessageDict = new ConcurrentDictionary<string, Message>();
        public static DecShopData? DecShopData = null;
        public static ConcurrentDictionary<string, DecShopData> MultiDecShopData = new ConcurrentDictionary<string, DecShopData>();
        public static ConcurrentQueue<BidQueue> BidQueue = new ConcurrentQueue<BidQueue>();
        public static ConcurrentQueue<BidQueue> BuyNowQueue = new ConcurrentQueue<BidQueue>();
        public static ConcurrentDictionary<IPEndPoint, int> AssetAckEndpoint = new ConcurrentDictionary<IPEndPoint, int>();
        public static ConcurrentDictionary<string, (bool, int)> PingResultDict = new ConcurrentDictionary<string, (bool, int)>();
        public static bool AssetDownloadLock = false;
        public static readonly HashSet<string> ValidExtensions = new HashSet<string>()
        {
            "png",
            "jpg",
            "jpeg",
            "jp2",
            "gif",
            "tif",
            "tiff",
            "webp",
            "bmp",
            "pdf"
            // Other possible extensions
        };

        #endregion

    }
}
