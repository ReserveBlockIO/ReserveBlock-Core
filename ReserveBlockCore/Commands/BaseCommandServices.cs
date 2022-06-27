using ReserveBlockCore.Models;
using ReserveBlockCore.Services;
using ReserveBlockCore.BIP39;
using ReserveBlockCore.P2P;
using Newtonsoft.Json;
using ReserveBlockCore.Utilities;
using System.Net;

namespace ReserveBlockCore.Commands
{
    public class BaseCommandServices
    {
        public static async void UnlockWallet()
        {
            if(Program.APIPassword != null)
            {
                Console.WriteLine("Please type in password to unlock wallet.");
                var password = Console.ReadLine();
                if (password != null)
                {
                    var passCheck = Program.APIPassword.ToDecrypt(password);
                    if(passCheck == password)
                    {
                        Program.APIUnlockTime = DateTime.UtcNow.AddMinutes(Program.WalletUnlockTime);
                        Console.WriteLine($"Wallet has been unlocked for {Program.WalletUnlockTime} mins.");
                    }
                    else
                    {
                        Console.WriteLine("Incorrect Password.");
                    }
                }
            }
            else
            {
                Console.WriteLine("No password has been configured");
            }
            
        }

        public static async void AddPeer()
        {
            IPAddress ip;
            Console.WriteLine("Please input the IP of the peer...");
            var peer = Console.ReadLine();
            if (peer != null)
            {
                try
                {
                    bool ValidateIP = IPAddress.TryParse(peer, out ip);
                    if (ValidateIP)
                    {
                        var peers = Peers.GetAll();
                        var peerExist = peers.Exists(x => x.PeerIP == peer);
                        if (!peerExist)
                        {
                            Peers nPeer = new Peers
                            {
                                IsIncoming = false,
                                IsOutgoing = true,
                                PeerIP = peer,
                                FailCount = 0
                            };

                            peers.Insert(nPeer);

                            Console.WriteLine("Success! Peer added.");
                            Console.WriteLine("Returning you to main menu...");
                            Thread.Sleep(4000);
                            StartupService.MainMenu();

                        }
                        else
                        {
                            Console.WriteLine("Failed to add. Peer already exist...");
                            Console.WriteLine("Returning you to main menu...");
                            Thread.Sleep(4000);
                            StartupService.MainMenu();
                        }
                    }
                    else
                    {
                        Console.WriteLine("Failed to add. Please input a valid IP...");
                        Console.WriteLine("Returning you to main menu...");
                        Thread.Sleep(4000);
                        StartupService.MainMenu();
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Unexpected Error. Error Message: {ex.Message}");
                    Console.WriteLine("Type /menu to return to main menu.");
                }
            }
        }
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
