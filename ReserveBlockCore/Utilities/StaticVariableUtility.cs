using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;
using System.Text;

namespace ReserveBlockCore.Utilities
{
    public class StaticVariableUtility
    {
        public static async void PrintStaticVariables()
        {
            var staticVars = await GetStaticVars();

            Console.WriteLine(staticVars);
            
            Console.WriteLine("End.");
        }
        public static async Task<string> GetStaticVars()
        {
            var peersConnected = await P2PClient.ArePeersConnected();
            var blockHeight = Program.LastBlock.Height;
            var accounts = AccountData.GetAccounts();
            var localValidator = accounts.FindOne(x => x.IsValidating == true);
            var validator = localValidator != null ? localValidator.Address : "No Validator";
            var nodes = Program.Nodes;            
            var lastBlock = Program.LastBlock;
            var adjudicator = Program.Adjudicate.ToString();
            var adjudicatorConnection = P2PClient.IsAdjConnected1.ToString();
            var fortisPoolCount = P2PAdjServer.FortisPool.Count().ToString();
            var isChainSynced = Program.IsChainSynced.ToString();
            var peerCount = P2PServer.GetConnectedPeerCount();
            var valCount = await P2PAdjServer.GetConnectedValCount();
            var lastTaskSent = P2PClient.LastTaskSentTime.ToString();
            var lastTaskResult = P2PClient.LastTaskResultTime.ToString();
            var lastTaskBlockHeight = P2PClient.LastTaskBlockHeight.ToString();
            var lastTaskError = P2PClient.LastTaskError.ToString();
            var hdWallet = Program.HDWallet.ToString();
            var reportedIPs = string.Join(",", P2PClient.ReportedIPs.Select(x => Enumerable.Repeat(x.Key, x.Value))
                .SelectMany(x => x));
            var mostLikelyIP = P2PClient.MostLikelyIP();
            var balance = "Total Balance: " + accounts.FindAll().Sum(x => x.Balance);
            var validatorAddress = "Validator Address: " + Program.ValidatorAddress;            
            var isBlocksDownloading = "Blocks Downloading: " + (Program.BlocksDownloading == 1).ToString();
            var isChainSyncing = "Chain Sync State (True = done, false = blocks downloading): " + isChainSynced;            
            var isPeersConnecting = "Peers Connecting Startup: " + (!Program.Nodes.Any()).ToString();
            var isStopAllTimers = "Stop all timers: " + Program.StopAllTimers.ToString();
            var isQueueProcessing = "Queue Processing: " + (Program.BlocksDownloading == 1);
            var isPeerConnected = "Peers connected: " + peersConnected.ToString();
            var peerConnectedCount = "Peers connected Count: " + P2PServer.GetConnectedPeerCount().ToString();
            var peerConnectedToMe = "Peers connected to you: " + peerCount.ToString();
            var blockHeightStr = "Block Height: " + blockHeight.ToString();
            var validatorStr = "Validator Address From DB: " + validator;
            var remoteLock = "Remote Lock: " + Program.RemoteCraftLock.ToString();
            var remoteLockTime = "Remote Lock Time: " + (Program.RemoteCraftLockTime == null ?  "NA" : Program.RemoteCraftLockTime.Value.ToShortTimeString());
            var isResyncing = "Chain Resyncing? : " + Program.IsResyncing.ToString();
            var isCorrupt = "Database Corruption Detected? : " + Program.DatabaseCorruptionDetected.ToString();
            var adjudicatorText = "Is Adjudicating?: " + adjudicator;
            var adjConnection = "Adjudicator Connected?: " + adjudicatorConnection;
            var fortisPoolText = "*Only for Adjudicators* Fortis Pool Count: " + fortisPoolCount.ToString();
            var valCountText = "*Only for Adjudicators* Validator Pool Count: " + valCount.ToString();
            var lastTaskSentText = "*Only for Validators* Most Recent Task (Unsolved) Sent at: " + lastTaskSent;
            var lastTaskResultText = "*Only for Validators* Latest Task (Solved) Result Received at: " + lastTaskResult;
            var lastTaskBlockHeightText = "*Only for Validators* Last Task Block Height : " + lastTaskBlockHeight;
            var lastTaskErrorText = "*Only for Validators* Last Task Error : " + lastTaskError;
            var hdWalletText = $"HD Wallet? : {hdWallet}";
            var reportedIPText = $"Reported IPs: {reportedIPs}";
            var externalIPText = $"External IP: {mostLikelyIP}";

            var lastBlockInfo = "Height: " + lastBlock.Height.ToString() + " - Hash: " + lastBlock.Hash + " Timestamp: " + lastBlock.Timestamp
                + " - Validator: " + lastBlock.Validator;

            StringBuilder strBld = new StringBuilder();
            strBld.AppendLine(validatorAddress);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(hdWalletText);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(isCorrupt);
            strBld.AppendLine("---------------------------------------------------------------------");            
            strBld.AppendLine(isBlocksDownloading);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(isChainSyncing);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(balance);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(isPeersConnecting);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(peerConnectedToMe);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(isStopAllTimers);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(isQueueProcessing);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(isPeerConnected);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(peerConnectedCount);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(blockHeightStr);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(validatorStr);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(remoteLock);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(remoteLockTime);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(isResyncing);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(adjudicatorText);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(adjConnection);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(fortisPoolText);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(valCountText);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(lastTaskResultText);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(lastTaskSentText);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(lastTaskBlockHeightText);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(lastTaskErrorText);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine("-------------------------------Node Info-----------------------------");
            nodes.Values.ToList().ForEach(x => {
                var ip = x.NodeIP;
                var lastcheck = x.NodeLastChecked != null ? x.NodeLastChecked.Value.ToLocalTime().ToLongTimeString() : "NA";
                var height = x.NodeHeight.ToString();
                var latency = x.NodeLatency.ToString();

                strBld.AppendLine("Node: " + ip + " - Last Checked: " + lastcheck + " - Height: " + height + " - Latency: " + latency);
                strBld.AppendLine("---------------------------------------------------------------------");
            });
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(reportedIPText);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(externalIPText);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine("-------------------------------Block Info----------------------------");
            strBld.AppendLine(lastBlockInfo);
            strBld.AppendLine("---------------------------------------------------------------------");
               
            return strBld.ToString();
        }
    }
}
