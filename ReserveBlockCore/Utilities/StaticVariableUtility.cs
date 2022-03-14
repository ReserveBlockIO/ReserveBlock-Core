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
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
        }
    }
}
