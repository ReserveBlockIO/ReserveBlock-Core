using ReserveBlockCore.Models;
using ReserveBlockCore.Services;
using ReserveBlockCore.BIP39;
using ReserveBlockCore.P2P;
using Newtonsoft.Json;
using ReserveBlockCore.Utilities;

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

        public static async void CreateBeacon()
        {
            Console.WriteLine("Please give your beacon a name...");
            var name = Console.ReadLine();
            if (name != null)
            {
                var ip = P2PClient.ReportedIPs.Count() != 0 ?
                P2PClient.ReportedIPs.GroupBy(x => x).OrderByDescending(y => y.Count()).Select(y => y.Key).First().ToString() :
                "NA";

                if (ip == "NA")
                {
                     Console.WriteLine("Could not get external IP. Please ensure you are connected to peers and that you are not blocking ports.");
                }

                var bUID = Guid.NewGuid().ToString().Substring(0,12).Replace("-", "") + ":" + TimeUtil.GetTime().ToString();

                BeaconInfo.BeaconInfoJson beaconLoc = new BeaconInfo.BeaconInfoJson
                {
                    IPAddress = ip,
                    Port = Program.Port,
                    Name = name,
                    BeaconUID = bUID
                };

                var beaconLocJson = JsonConvert.SerializeObject(beaconLoc);

                BeaconInfo bInfo = new BeaconInfo();
                bInfo.Name = name;
                bInfo.IsBeaconActive = true;
                bInfo.BeaconLocator = beaconLocJson.ToBase64();
                bInfo.BeaconUID = bUID;

                var result = BeaconInfo.SaveBeaconInfo(bInfo);

                Console.WriteLine(result);
            }
        }

        public static async void SwitchBeaconState()
        {
            var result = BeaconInfo.SetBeaconActiveState();

            if (result == null)
            {
                Console.WriteLine("Error turning beacon on/off");
            }
            else
            {
                if(result.Value == true)
                {
                    Console.WriteLine("Beacon has been turned on.");
                }
                else
                {
                    Console.WriteLine("Beacon has been turned off.");
                }
            }
        }

        public static string CreateHDWallet()
        {
            Console.WriteLine("How many words do you want? (12 or 24)");
            var strengthStr = Console.ReadLine();
            if (strengthStr != null)
            {
                if (strengthStr == "12")
                {
                    var strength = Convert.ToInt32(strengthStr);
                    var mnemonic = HDWallet.HDWalletData.CreateHDWallet(strength, BIP39Wordlist.English);
                    Program.HDWallet = true;

                    return mnemonic;
                }
                else if(strengthStr == "24")
                {
                    var strength = Convert.ToInt32(strengthStr);
                    var mnemonic = HDWallet.HDWalletData.CreateHDWallet(strength, BIP39Wordlist.English);
                    Program.HDWallet = true;

                    return mnemonic;
                }
                else
                {
                    return "Unexpected entry detected. Please try again.";
                }
            }
            return "Unexpected entry detected. Please try again.";
        }

        public static string RestoreHDWallet()
        {
            Console.WriteLine("Please paste your Mnemonic Below...");
            var mnemonicStr = Console.ReadLine();
            if (mnemonicStr != null)
            {
                var mnemonicResult = HDWallet.HDWalletData.RestoreHDWallet(mnemonicStr);
                if(mnemonicResult.Contains("Restored"))
                {
                    Program.HDWallet = true;
                }
                
                return mnemonicResult;
                
            }
            return "Unexpected entry detected. Please try again.";
        }
    }
}
