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
            var blockHeight = Program.BlockHeight;
            var accounts = AccountData.GetAccounts();
            var localValidator = accounts.FindOne(x => x.IsValidating == true);
            var validator = localValidator != null ? localValidator.Address : "No Validator";
            var nodes = Program.Nodes;
            var nodeList = nodes.ToList();
            var lastBlock = Program.LastBlock;
            var adjudicator = Program.Adjudicate.ToString();
            var adjudicatorConnection = P2PClient.IsAdjConnected1.ToString();
            var fortisPoolCount = P2PAdjServer.FortisPool.Count().ToString();
            var isChainSynced = Program.IsChainSynced.ToString();
            var peerCount = await P2PServer.GetConnectedPeerCount();
            var valCount = await P2PAdjServer.GetConnectedValCount();
            var lastTaskSent = P2PClient.LastTaskSentTime.ToString();
            var lastTaskResult = P2PClient.LastTaskResultTime.ToString();
            var lastTaskBlockHeight = P2PClient.LastTaskBlockHeight.ToString();
            var lastTaskError = P2PClient.LastTaskError.ToString();

            var validatorAddress = "Validator Address: " + Program.ValidatorAddress;
            var isBlockCrafting = "Block Craft: " + Program.BlockCrafting.ToString();
            var isBlocksDownloading = "Blocks Downloading: " + Program.BlocksDownloading.ToString();
            var isChainSyncing = "Chain Sync State (True = done, false = blocks downloading): " + isChainSynced;
            var isCrafting = "Is Crafting: " + Program.IsCrafting.ToString();
            var isPeersConnecting = "Peers Connecting Startup: " + Program.PeersConnecting.ToString();
            var isStopAllTimers = "Stop all timers: " + Program.StopAllTimers.ToString();
            var isQueueProcessing = "Queue Processing: " + BlockQueueService.QueueProcessing;
            var isPeerConnected = "Peers connected: " + peersConnected.Item1.ToString();
            var peerConnectedCount = "Peers connected Count: " + peersConnected.Item2.ToString();
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

            var lastBlockInfo = "Height: " + lastBlock.Height.ToString() + " - Hash: " + lastBlock.Hash + " Timestamp: " + lastBlock.Timestamp
                + " - Validator: " + lastBlock.Validator;

            StringBuilder strBld = new StringBuilder();
            strBld.AppendLine(validatorAddress);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(isCorrupt);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(isBlockCrafting);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(isBlocksDownloading);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(isChainSyncing);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(isCrafting);
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
            nodeList.ForEach(x => {
                var ip = x.NodeIP;
                var lastcheck = x.NodeLastChecked != null ? x.NodeLastChecked.Value.ToLocalTime().ToLongTimeString() : "NA";
                var height = x.NodeHeight.ToString();
                var latency = x.NodeLatency.ToString();

                strBld.AppendLine("Node: " + ip + " - Last Checked: " + lastcheck + " - Height: " + height + " - Latency: " + latency);
                strBld.AppendLine("---------------------------------------------------------------------");
            });
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine("-------------------------------Block Info----------------------------");
            strBld.AppendLine(lastBlockInfo);
            return strBld.ToString();
        }
    }
}
