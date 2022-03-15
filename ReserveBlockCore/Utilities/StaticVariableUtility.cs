using ReserveBlockCore.Data;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;

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
    }
}
