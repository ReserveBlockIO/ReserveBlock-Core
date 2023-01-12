using Microsoft.AspNetCore.SignalR;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
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
            var Source = new CancellationTokenSource();
            Source.Cancel();
            CancelledToken = Source.Token;
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
        public static Block LastBlock = new Block { Height = -1 };
        public static Adjudicators? LeadAdjudicator = null;
        public static Guid AdjudicatorKey = Adjudicators.AdjudicatorData.GetAdjudicatorKey();
        public static BeaconReference BeaconReference = new BeaconReference();
        public static Process proc = new Process();

        public static DateTime? RemoteCraftLockTime = null;        
        public static DateTime? CLIWalletUnlockTime = null;
        public static DateTime? APIUnlockTime = null;
        public static DateTime? ExplorerValDataLastSend = null;

        public const int ADNRLimit = 65;
        public static int BlockLock = 400000;
        public static long LastAdjudicateTime = 0;
        public static SemaphoreSlim BlocksDownloadSlim = new SemaphoreSlim(1, 1);
        public static int WalletUnlockTime = 0;
        public static int ChainCheckPointInterval = 0;
        public static int ChainCheckPointRetain = 0;
        public static int PasswordClearTime = 10;
        public static int NFTTimeout = 0;
        public static int Port = 3338;
        public static int APIPort = 7292;
        public static int MajorVer = 2;
        public static int MinorVer = 1; //change this to 1 for main release
        public static int BuildVer = 0;

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

        public static bool AlwaysRequireWalletPassword = false;
        public static bool AlwaysRequireAPIPassword = false;
        public static bool StopConsoleOutput = false;        
        public static int AdjudicateLock = 0;
        public static bool AdjudicateLockV2 = false;
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
        public static bool GUIPasswordNeeded = false;
        public static bool TreisUpdating = false;
        public static bool DuplicateAdjIP = false;
        public static bool DuplicateAdjAddr = false;
        public static bool ExplorerValDataLastSendSuccess = false;
        public static bool LogAPI = false;
        public static bool NFTFilesReadyEPN = false; // nft files ready, encryption password needed
        public static bool NFTsDownloading = false;

        public static CancellationToken CancelledToken;

        public static ConcurrentQueue<Block> MemBlocks = new ConcurrentQueue<Block>();
        public static ConcurrentDictionary<string, NodeInfo> Nodes = new ConcurrentDictionary<string, NodeInfo>(); // IP Address
        public static ConcurrentDictionary<string, Validators> InactiveValidators = new ConcurrentDictionary<string, Validators>(); // RBX address        
        public static ConcurrentDictionary<string, string> Locators = new ConcurrentDictionary<string, string>(); // BeaconUID
        public static ConcurrentDictionary<string, Mother.Kids> MothersKids = new ConcurrentDictionary<string, Mother.Kids>(); //Mothers Children
        public static ConcurrentDictionary<string, HubCallerContext> MothersKidsContext = new ConcurrentDictionary<string, HubCallerContext>(); //Mothers Children
        public static ConcurrentBag<string> RejectAssetExtensionTypes = new ConcurrentBag<string>();

        public static SecureString EncryptPassword = new SecureString();
        public static SecureString? MotherPassword = null;

        public static IHttpClientFactory HttpClientFactory;        

        #endregion

        #region P2P Client Variables

        public const int MaxPeers = 8;
        public static ConcurrentDictionary<string, int> ReportedIPs = new ConcurrentDictionary<string, int>();
        public static ConcurrentDictionary<string, bool> BannedIPs;        

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

        public static (long, TaskNumberAnswerV2?, DateTime) CurrentTaskNumberAnswerV2;
        public static (long Height, int Answer, string Signature, DateTime Time) CurrentTaskNumberAnswerV3;
        public static TaskWinner CurrentWinner;
        public static string VerifySecret = "";        

        public static ConcurrentDictionary<string, ConcurrentDictionary<string, (DateTime Time, string Request, string Response)>> ConsensusDump = new ConcurrentDictionary<string, ConcurrentDictionary<string, (DateTime Time, string Request, string Response)>>();

        public static ConcurrentDictionary<string, TaskWinner> TaskWinnerDictV2 = new ConcurrentDictionary<string, TaskWinner>(); // RBX address
        public static ConcurrentDictionary<(string RBXAddress, long Height), Block> TaskWinnerDictV3 = new ConcurrentDictionary<(string RBXAddress, long Height), Block>(); // RBX address
        public static ConcurrentDictionary<string, TaskNumberAnswerV2> TaskSelectedNumbersV2 = new ConcurrentDictionary<string, TaskNumberAnswerV2>(); // RBX address
        public static ConcurrentDictionary<(string RBXAddress, long Height), (string IPAddress, string RBXAddress, int Answer)> TaskSelectedNumbersV3 = new ConcurrentDictionary<(string RBXAddres, long height), (string IPAddress, string RBXAddress, int Answer)>();
        public static ConcurrentDictionary<string, TaskNumberAnswerV2> TaskAnswerDict_New = new ConcurrentDictionary<string, TaskNumberAnswerV2>(); // RBX address
        public static ConcurrentDictionary<(string RBXAddress, long Height), (string IPAddress, string RBXAddress, int Answer, string Signature)> TaskAnswerDictV3 = new ConcurrentDictionary<(string RBXAddres, long height), (string IPAddress, string RBXAddress, int Answer, string Signature)>();
        public static ConcurrentDictionary<string, TaskNumberAnswerV2> RejectedTaskAnswerDict_New = new ConcurrentDictionary<string, TaskNumberAnswerV2>(); // RBX address
        public static ConcurrentDictionary<string, Transaction> BroadcastedTrxDict = new ConcurrentDictionary<string, Transaction>(); // TX Hash
        public static ConcurrentDictionary<string, TransactionBroadcast> ConsensusBroadcastedTrxDict = new ConcurrentDictionary<string, TransactionBroadcast>(); //TX Hash
        public static ConcurrentDictionary<string, DuplicateValidators> DuplicatesBroadcastedDict= new ConcurrentDictionary<string, DuplicateValidators>();

        #endregion

        #region DbContext Variables
        public static ConcurrentDictionary<int, bool> HasTransactionDict = new ConcurrentDictionary<int, bool>();
        #endregion

    }
}
