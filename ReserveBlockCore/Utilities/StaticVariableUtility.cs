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
            Console.WriteLine("Validator Address: " + Program.ValidatorAddress);
            Console.WriteLine("Block Craft: " + Program.BlockCrafting.ToString());
            Console.WriteLine("Blocks Downloading: " + Program.BlocksDownloading.ToString());
            Console.WriteLine("Is Crafting: " + Program.IsCrafting.ToString());
            Console.WriteLine("Peers Connecting: " + Program.PeersConnecting.ToString());
            Console.WriteLine("Stop all timers: " + Program.StopAllTimers.ToString());
            Console.WriteLine("Queue Processing: " + BlockQueueService.QueueProcessing);
            var peersConnected = await P2PClient.ArePeersConnected();
            Console.WriteLine("Peers connected: " + peersConnected.Item1.ToString());
            Console.WriteLine("Peers connected Count: " + peersConnected.Item2.ToString());
            var blockHeight = BlockchainData.GetHeight();
            Console.WriteLine("Block Height: " + blockHeight.ToString());
            Program.PrintConsoleErrors = Program.PrintConsoleErrors == false ? true : false;
            Console.WriteLine("Showing Block Download Errors: " + Program.PrintConsoleErrors.ToString());
            Console.WriteLine("Re-establish Peers? y/n");
            var reconnect = Console.ReadLine();
            if(reconnect != null)
            {
                if (reconnect == "y")
                {
                    await StartupService.StartupPeers();
                }
            }
            Console.WriteLine("Force Redownload Block? y/n");
            var blockDownload = Console.ReadLine();
            if(blockDownload != null)
            {
                if(blockDownload == "y")
                {
                    Console.WriteLine("Blocks Downloading...");
                    await StartupService.DownloadBlocksOnStart();
                    Console.WriteLine("Blocks Done...");
                }
            }
            
            Console.WriteLine("End.");
        }

        public static async Task<string> GetStaticVars()
        {
            var peersConnected = await P2PClient.ArePeersConnected();
            var blockHeight = BlockchainData.GetHeight();
            var accounts = AccountData.GetAccounts();
            var localValidator = accounts.FindOne(x => x.IsValidating == true);
            var validator = localValidator != null ? localValidator.Address : "No Validator";
            var nodes = Program.Nodes;
            var lastBlock = BlockchainData.GetLastBlock();

            var validatorAddress = "Validator Address: " + Program.ValidatorAddress;
            var isBlockCrafting = "Block Craft: " + Program.BlockCrafting.ToString();
            var isBlocksDownloading = "Blocks Downloading: " + Program.BlocksDownloading.ToString();
            var isCrafting = "Is Crafting: " + Program.IsCrafting.ToString();
            var isPeersConnecting = "Peers Connecting Startup: " + Program.PeersConnecting.ToString();
            var isStopAllTimers = "Stop all timers: " + Program.StopAllTimers.ToString();
            var isQueueProcessing = "Queue Processing: " + BlockQueueService.QueueProcessing;
            var isPeerConnected = "Peers connected: " + peersConnected.Item1.ToString();
            var peerConnectedCount = "Peers connected Count: " + peersConnected.Item2.ToString();
            var blockHeightStr = "Block Height: " + blockHeight.ToString();
            var validatorStr = "Validator Address From DB: " + validator;
            var remoteLock = "Remote Lock: " + Program.RemoteCraftLock.ToString();
            var lastBlockInfo = "Height: " + lastBlock.Height.ToString() + " - Hash: " + lastBlock.Hash + " Timestamp: " + lastBlock.Timestamp
                + " - Validator: " + lastBlock.Validator + " - Next Validators: " + lastBlock.NextValidators;

            StringBuilder strBld = new StringBuilder();
            strBld.AppendLine(validatorAddress);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(isBlockCrafting);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(isBlocksDownloading);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(isCrafting);
            strBld.AppendLine("---------------------------------------------------------------------");
            strBld.AppendLine(isPeersConnecting);
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
            strBld.AppendLine("-------------------------------Node Info-----------------------------");
            nodes.ForEach(x => {
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
