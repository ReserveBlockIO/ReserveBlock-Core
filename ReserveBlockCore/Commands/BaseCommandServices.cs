using ReserveBlockCore.Models;
using ReserveBlockCore.Services;
using ReserveBlockCore.BIP39;
using ReserveBlockCore.P2P;
using Newtonsoft.Json;
using ReserveBlockCore.Utilities;
using System.Net;
using ReserveBlockCore.Data;
using System.Text.RegularExpressions;

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

        public static async Task<string> CreateDnr()
        {
            var output = "";
            Console.WriteLine("Please select the wallet you'd like to create a domain name registration for...");
            var accountList = AccountData.GetAccountsWithBalanceForAdnr();
            var accountNumberList = new Dictionary<string, Account>();
            if (accountList.Count() > 0)
            {
                int count = 1;
                Console.WriteLine("********************************************************************");
                Console.WriteLine("Please choose an address below by typing its # and pressing enter.");
                accountList.ToList().ForEach(x => {
                    accountNumberList.Add(count.ToString(), x);
                    Console.WriteLine("********************************************************************");
                    Console.WriteLine("\n#" + count.ToString());
                    Console.WriteLine("\nAddress :\n{0}", x.Address);
                    Console.WriteLine("\nAccount Balance:\n{0}", x.Balance);
                    Console.WriteLine("********************************************************************");
                    count++;
                });
                string walletChoice = "";
                walletChoice = Console.ReadLine();

                if(walletChoice != null && walletChoice != "")
                {
                    var keyCheck = accountNumberList.ContainsKey(walletChoice);

                    if (keyCheck == false)
                    {
                        Console.WriteLine($"Please choose a correct number. Error with entry given: {walletChoice}");
                        return output;
                    }
                    else
                    {
                        var wallet = accountNumberList[walletChoice];
                        var address = wallet.Address;
                        var adnr = Adnr.GetAdnr();
                        var adnrCheck = adnr.FindOne(x => x.Address == address);
                        if(adnrCheck != null)
                        {
                            Console.WriteLine($"This address already has a DNR associated with it: {adnrCheck.Name}");
                            return output;
                        }
                        bool nameFound = true;
                        while(nameFound)
                        {
                            Console.WriteLine($"You have selected the following wallet: {address}");
                            Console.WriteLine("Please enter the name you'd like for this wallet. Ex: (cryptoinvestor1) Please note '.rbx' will automatically be added. DO NOT INCLUDE IT.");
                            Console.WriteLine("type exit to leave this menu.");
                            var name = Console.ReadLine();
                            if(name != null && name != "")
                            {
                                var nameCharCheck = Regex.IsMatch(name, @"^[a-zA-Z0-9]+$");
                                if(!nameCharCheck)
                                {
                                    Console.WriteLine("A DNR may only contain letters and numbers.");
                                }
                                else
                                {
                                    var nameCheck = adnr.FindOne(x => x.Name == name);
                                    if (nameCheck == null)
                                    {
                                        nameFound = false;
                                        Console.WriteLine("Are you sure you want to create this DNR? 'y' for yes, 'n' for no.");
                                        var response = Console.ReadLine();
                                        if (response != null && response != "")
                                        {
                                            if (response.ToLower() == "y")
                                            {
                                                Console.WriteLine("Sending Transaction now.");
                                                var result = await Adnr.CreateAdnrTx(address, name);
                                                if(result.Item1 != null)
                                                {
                                                    Console.WriteLine("DNR Request has been sent to mempool. Sending you back to main menu.");
                                                    Console.WriteLine("3...");
                                                    Thread.Sleep(1000);
                                                    Console.WriteLine("2...");
                                                    Thread.Sleep(1000);
                                                    Console.WriteLine("1...");
                                                    Thread.Sleep(1000);
                                                    StartupService.MainMenu();
                                                }
                                                else
                                                {
                                                    Console.WriteLine("DNR Request failed to enter the mempool.");
                                                    Console.WriteLine($"Error: {result.Item2}");
                                                }
                                            }
                                            else
                                            {
                                                StartupService.MainMenu();
                                                Console.WriteLine("DNR Request has been cancelled.");
                                            }
                                        }

                                    }
                                }
                                
                            }
                            else
                            {
                                StartupService.MainMenu();
                                Console.WriteLine("DNR Request has been cancelled. Incorrect format inputted.");
                            }
                            
                        }
                        
                    }
                }
                return output;
                
            }
            else
            {
                Console.WriteLine("No eligible accounts were detected. You must have an account with at least 1 RBX to create a dnr.");
                return output;
            }

            
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
