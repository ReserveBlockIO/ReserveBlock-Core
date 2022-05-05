using ReserveBlockCore.Services;

namespace ReserveBlockCore.Commands
{
    public class BaseCommandServices
    {
        public static async void ReconnectPeers()
        {
            Console.WriteLine("Re-establish Peers? y/n");
            var reconnect = Console.ReadLine();
            if (reconnect != null)
            {
                if (reconnect == "y")
                {
                    await StartupService.StartupPeers();
                }
            }
        }
    }
}
