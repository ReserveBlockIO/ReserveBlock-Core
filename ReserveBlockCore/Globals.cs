using Microsoft.AspNetCore.SignalR;
using ReserveBlockCore.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security;

namespace ReserveBlockCore
{
    public static class Globals
    {
        static Globals()
        {
            //var peerDb = Peers.GetAll();
            //BannedIPs = new ConcurrentDictionary<string, bool>(
            //    peerDb.Find(x => x.IsBanned).ToArray().ToDictionary(x => x.PeerIP, x => true));
        }

        #region Timers

        public static Timer? heightTimer; //timer for getting height from other nodes
        public static Timer? PeerCheckTimer;//checks currents peers and old peers and will request others to try. 
        public static Timer? ValidatorListTimer;//checks currents peers and old peers and will request others to try. 
        public static Timer? DBCommitTimer;//checks dbs and commits log files. 

        #endregion

        #region Global General Variables

        public static int BlockLock = 350000;
        public static ConcurrentQueue<Block> MemBlocks = new ConcurrentQueue<Block>();
        public static ConcurrentDictionary<string, NodeInfo> Nodes = new ConcurrentDictionary<string, NodeInfo>();
        public static List<Validators> InactiveValidators = new List<Validators>();
        public static List<Validators> MasternodePool = new List<Validators>();
        public static List<string> Locators = new List<string>();
        public static bool StopConsoleOutput = false;
        public static Block LastBlock = new Block { Height = -1 };
        public static Adjudicators? LeadAdjudicator = null;
        public static Guid AdjudicatorKey = Adjudicators.AdjudicatorData.GetAdjudicatorKey();
        public static bool Adjudicate = false;
        public static bool AdjudicateLock = false;
        public static long LastAdjudicateTime = 0;
        public static int BlocksDownloading = 0;
        public static bool HeightCheckLock = false;
        public static bool InactiveNodeSendLock = false;
        public static bool IsCrafting = false;
        public static bool IsResyncing = false;
        public static bool TestURL = false;
        public static bool StopAllTimers = false;
        public static bool DatabaseCorruptionDetected = false;
        public static bool RemoteCraftLock = false;
        public static bool IsChainSynced = false;
        public static bool OptionalLogging = false;
        public static DateTime? RemoteCraftLockTime = null;
        public static string ValidatorAddress = "";
        public static bool IsTestNet = false;
        public static int NFTTimeout = 0;
        public static int Port = 3338;
        public static int APIPort = 7292;
        public static string? WalletPassword = null;
        public static bool AlwaysRequireWalletPassword = false;
        public static string? APIPassword = null;
        public static bool AlwaysRequireAPIPassword = false;
        public static DateTime? CLIWalletUnlockTime = null;
        public static DateTime? APIUnlockTime = null;
        public static int WalletUnlockTime = 0;
        public static string? APICallURL = null;
        public static bool APICallURLLogging = false;
        public static bool ChainCheckPoint = false;
        public static int ChainCheckPointInterval = 0;
        public static int ChainCheckPointRetain = 0;
        public static string ChainCheckpointLocation = "";
        public static string ConfigValidator = "";
        public static string ConfigValidatorName = "";
        public static string GenesisAddress = "RBdwbhyqwJCTnoNe1n7vTXPJqi5HKc6NTH";
        public static byte AddressPrefix = 0x3C; //address prefix 'R'
        public static bool PrintConsoleErrors = false;
        public static Process proc = new Process();
        public static int MajorVer = 2;
        public static int MinorVer = 0; //change this to 1 for main release
        public static int BuildVer = 0;
        public static string CLIVersion = "";
        public static bool HDWallet = false;
        public static bool IsWalletEncrypted = false;
        public static SecureString EncryptPassword = new SecureString();
        public static int PasswordClearTime = 10;
        public static bool AutoDownloadNFTAsset = false;
        public static bool IgnoreIncomingNFTs = false;
        public static List<string> RejectAssetExtensionTypes = new List<string>();
        public static BeaconReference BeaconReference = new BeaconReference();

        #endregion

        #region P2P Client Variables

        public const int MaxPeers = 8;
        public static ConcurrentDictionary<string, int> ReportedIPs = new ConcurrentDictionary<string, int>();
        public static ConcurrentDictionary<string, bool> BannedIPs;
        public static long LastSentBlockHeight = -1;
        public static DateTime? AdjudicatorConnectDate = null;
        public static DateTime? LastTaskSentTime = null;
        public static DateTime? LastWinningTaskSentTime = null;
        public static DateTime? LastTaskResultTime = null;
        public static long LastTaskBlockHeight = 0;
        public static bool LastTaskError = false;
        public static bool LastWinningTaskError = false;
        public static long LastWinningTaskBlockHeight = 0;
        public static CancellationTokenSource source = new CancellationTokenSource(10000);

        #endregion

        #region P2P Server Variables

        public static ConcurrentDictionary<string, HubCallerContext> P2PPeerList = new ConcurrentDictionary<string, HubCallerContext>();
        public static ConcurrentDictionary<string, HubCallerContext> BeaconPeerList = new ConcurrentDictionary<string, HubCallerContext>();
        public static ConcurrentDictionary<string, HubCallerContext> AdjPeerList = new ConcurrentDictionary<string, HubCallerContext>();
        public static ConcurrentDictionary<string, MessageLock> MessageLocks = new ConcurrentDictionary<string, MessageLock>();
        public static ConcurrentDictionary<string, int> TxRebroadcastDict = new ConcurrentDictionary<string, int>();

        #endregion

        #region P2P Adj Server Variables

        public static List<FortisPool> FortisPool = new List<FortisPool>();
        public static ConcurrentDictionary<string, FortisPool> FortisPoolDict = new ConcurrentDictionary<string, FortisPool>();
        public static List<BeaconPool> BeaconPool = new List<BeaconPool>();

        public static TaskQuestion? CurrentTaskQuestion = null;
        public static TaskNumberAnswer? CurrentTaskNumberAnswer = null;
        public static string VerifySecret = "";

        public static List<TaskWinner> TaskWinnerList = new List<TaskWinner>();
        public static List<TaskNumberAnswer> TaskSelectedNumbers = new List<TaskNumberAnswer>();
        public static List<TaskAnswer> TaskAnswerList = new List<TaskAnswer>();
        public static List<TaskNumberAnswer> TaskAnswerList_New = new List<TaskNumberAnswer>();
        public static List<TaskAnswer> RejectedTaskAnswerList = new List<TaskAnswer>();
        public static List<TaskNumberAnswer> RejectedTaskAnswerList_New = new List<TaskNumberAnswer>();
        public static List<Transaction> BroadcastedTrxList = new List<Transaction>();        

        #endregion

        #region DbContext Variables
        public static ConcurrentDictionary<int, bool> HasTransactionDict = new ConcurrentDictionary<int, bool>();
        #endregion

    }
}
