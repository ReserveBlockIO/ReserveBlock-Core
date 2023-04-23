using Microsoft.AspNetCore.Hosting.Server;
using ReserveBlockCore.Models;
using ReserveBlockCore.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReserveBlockCore.Config
{
    public class Config
    {
        public int Port { get; set; }
        public int APIPort { get; set; }
		public bool TestNet { get; set; }
		public string? WalletPassword { get; set; }
		public bool AlwaysRequireWalletPassword { get; set; }
		public string? APIPassword { get; set; }
		public bool AlwaysRequireAPIPassword { get; set; }
		public string? APICallURL { get; set; }
		public int WalletUnlockTime { get; set; }
        public bool ChainCheckPoint { get; set; }
		public int ChainCheckPointInterval { get; set; }
        public int ChainCheckPointRetain { get; set; }
		public string ChainCheckpointLocation { get; set; }
		public bool APICallURLLogging { get; set; }
		public string? ValidatorAddress { get; set; }
		public string ValidatorName { get; set; }
		public int NFTTimeout { get; set; }
		public int PasswordClearTime { get; set; }
        public bool AutoDownloadNFTAsset { get; set; }
        public bool IgnoreIncomingNFTs { get; set; }
		public string? MotherAddress { get; set; }
		public string? MotherPassword { get; set; }
        public List<string> RejectAssetExtensionTypes { get; set; }
		public List<string> AllowedExtensionsTypes { get; set; }
		public string? CustomPath { get; set; }
		public bool LogAPI { get; set; }
		public bool RefuseToCallSeed { get; set; }
		public bool OpenAPI { get; set; }
		public bool RunUnsafeCode { get; set; }
		public int DSTClientPort { get; set; }
		public string? STUNServers { get; set; }
		public bool SelfSTUNServer { get; set; }
        public int SelfSTUNPort { get; set; }
        public static Config ReadConfigFile()
        {
            var path = GetPathUtility.GetConfigPath();

			Config config = new Config();

			using (StreamReader sr = new StreamReader(path + "config.txt"))
			{
				// Declare the dictionary outside the loop:
				var dict = new Dictionary<string, string>();

				// (This loop reads every line until EOF or the first blank line.)
				string line;
				while (!string.IsNullOrEmpty((line = sr.ReadLine())))
				{
					// Split each line around '=':
					var tmp = line.Split(new[] { '=' },
										 StringSplitOptions.RemoveEmptyEntries);
					// Add the key-value pair to the dictionary:
					dict[tmp[0]] = tmp[1];
				}

                config.CustomPath = dict.ContainsKey("CustomPath") ? dict["CustomPath"] : null;
                if (!string.IsNullOrEmpty(config.CustomPath))
                {
                    Globals.CustomPath = config.CustomPath;
                    _  = GetPathUtility.GetConfigPath();
                }
                // Assign the values that you need:
                config.Port = dict.ContainsKey("Port") ? Convert.ToInt32(dict["Port"]) : 3338;
				config.APIPort = dict.ContainsKey("APIPort") ? Convert.ToInt32(dict["APIPort"]) : 7292;
				config.TestNet = dict.ContainsKey("TestNet") ? Convert.ToBoolean(dict["TestNet"]) : false;
				config.NFTTimeout = dict.ContainsKey("NFTTimeout") ? Convert.ToInt32(dict["NFTTimeout"]) : 15;
				config.WalletPassword = dict.ContainsKey("WalletPassword") ? dict["WalletPassword"] : null;
				config.AlwaysRequireWalletPassword = dict.ContainsKey("AlwaysRequireWalletPassword") ? Convert.ToBoolean(dict["AlwaysRequireWalletPassword"]) : false;
				config.APIPassword = dict.ContainsKey("APIPassword") ? dict["APIPassword"] : null;
				config.AlwaysRequireAPIPassword = dict.ContainsKey("AlwaysRequireAPIPassword") ? Convert.ToBoolean(dict["AlwaysRequireAPIPassword"]) : false;
				config.APICallURL = dict.ContainsKey("APICallURL") ? dict["APICallURL"] : null;
				config.ValidatorAddress = dict.ContainsKey("ValidatorAddress") ? dict["ValidatorAddress"] : null;
				config.ValidatorName = dict.ContainsKey("ValidatorName") ? dict["ValidatorName"] : Guid.NewGuid().ToString();
				config.WalletUnlockTime = dict.ContainsKey("WalletUnlockTime") ? Convert.ToInt32(dict["WalletUnlockTime"]) : 15;
				config.ChainCheckPoint = dict.ContainsKey("ChainCheckPoint") ? Convert.ToBoolean(dict["ChainCheckPoint"]) : false;
				config.APICallURLLogging = dict.ContainsKey("APICallURLLogging") ? Convert.ToBoolean(dict["APICallURLLogging"]) : false;
				config.ChainCheckPointInterval = dict.ContainsKey("ChainCheckPointInternal") ? Convert.ToInt32(dict["ChainCheckPointInternal"]) : 12;
				config.ChainCheckPointRetain = dict.ContainsKey("ChainCheckPointRetain") ? Convert.ToInt32(dict["ChainCheckPointRetain"]) : 2;
				config.ChainCheckpointLocation = dict.ContainsKey("ChainCheckpointLocation") ? dict["ChainCheckpointLocation"] : GetPathUtility.GetCheckpointPath();
				config.PasswordClearTime = dict.ContainsKey("PasswordClearTime") ? Convert.ToInt32(dict["PasswordClearTime"]) : 10;
                config.LogAPI = dict.ContainsKey("LogAPI") ? Convert.ToBoolean(dict["LogAPI"]) : false;
                config.RefuseToCallSeed = dict.ContainsKey("RefuseToCallSeed") ? Convert.ToBoolean(dict["RefuseToCallSeed"]) : false;
                config.OpenAPI = dict.ContainsKey("OpenAPI") ? Convert.ToBoolean(dict["OpenAPI"]) : false;
                config.RunUnsafeCode = dict.ContainsKey("RunUnsafeCode") ? Convert.ToBoolean(dict["RunUnsafeCode"]) : false;
                config.DSTClientPort = dict.ContainsKey("DSTClientPort") ? Convert.ToInt32(dict["DSTClientPort"]) : 3341;
                config.STUNServers = dict.ContainsKey("STUNServers") ? dict["STUNServers"] : null;
                config.SelfSTUNServer = dict.ContainsKey("STUN") ? Convert.ToBoolean(dict["STUN"]) : false;
                config.SelfSTUNPort = dict.ContainsKey("SelfSTUNPort") ? Convert.ToInt32(dict["SelfSTUNPort"]) : 3340;


                config.AutoDownloadNFTAsset = dict.ContainsKey("AutoDownloadNFTAsset") ? Convert.ToBoolean(dict["AutoDownloadNFTAsset"]) : false;
                config.IgnoreIncomingNFTs = dict.ContainsKey("IgnoreIncomingNFTs") ? Convert.ToBoolean(dict["IgnoreIncomingNFTs"]) : false;
				config.RejectAssetExtensionTypes = new List<string>();

				var rejExtList = new List<string> { ".exe", ".pif", ".application", ".gadget", ".msi", ".msp", ".com", ".scr", ".hta",
					".cpl", ".msc", ".jar", ".bat", ".cmd", ".vb", ".vbs", ".vbe", ".js", ".jse", ".ws", ".wsf" , ".wsc", ".wsh", ".ps1",
					".ps1xml", ".ps2", ".ps2xml", ".psc1", ".psc2", ".msh", ".msh1", ".msh2", ".mshxml", ".msh1xml", ".msh2xml", ".scf",
					".lnk", ".inf", ".reg", ".doc", ".xls", ".ppt", ".docm", ".dotm", ".xlsm", ".xltm", ".xlam", ".pptm", ".potm", ".ppam",
					".ppsm", ".sldm", ".sys", ".dll", ".zip", ".rar"};

				var knownVirusMalwareExt = new List<string> {".xnxx", ".ozd", ".aur", ".boo", ".386", ".sop", ".dxz", ".hlp", ".tsa", ".exe1", 
					".bkd", "exe_.", ".rhk", ".vbx", ".lik", ".osa", ".9", ".cih", ".mjz", ".dlb", ".php3", ".dyz", ".wsc", ".dom", ".hlw", 
					".s7p", ".cla", ".mjg", ".mfu", ".dyv", ".kcd", ".spam", ".bup", ".rsc_tmp", ".mcq", ".upa", ".bxz", ".dli", ".txs", 
					".xir", ".cxq", ".fnr", ".xdu", ".xlv", ".wlpginstall", ".ska", ".tti", ".cfxxe", ".dllx", ".smtmp", ".vexe", ".qrn", 
					".xtbl", ".fag", ".oar", ".ceo", ".tko", ".uzy", ".bll", ".dbd", ".plc", ".smm", ".ssy", ".blf", ".zvz", ".cc", ".ce0", 
					".nls", ".ctbl", ".crypt1", ".hsq", ".iws", ".vzr", ".lkh", ".ezt", ".rna", ".aepl", ".hts", ".atm", ".fuj", ".aut", 
					".fjl", ".delf", ".buk", ".bmw", ".capxml", ".bps", ".cyw", ".iva", ".pid", ".lpaq5", ".dx", ".bqf", ".qit", ".pr", ".lok", 
					".xnt"};

                config.MotherAddress = dict.ContainsKey("MotherAddress") ? dict["MotherAddress"] : null;
                config.MotherPassword = dict.ContainsKey("MotherPassword") ? dict["MotherPassword"] : null;

                if (dict.ContainsKey("RejectAssetExtensionTypes"))
				{
					string rejectedExtensions = dict["RejectAssetExtensionTypes"].ToString();
					var rejExtListConfig = rejectedExtensions.Split(',');
					foreach (var rejExt in rejExtListConfig)
					{
						config.RejectAssetExtensionTypes.Add(rejExt);
					}

					config.RejectAssetExtensionTypes.AddRange(rejExtList);
                    config.RejectAssetExtensionTypes.AddRange(knownVirusMalwareExt);
                }
				else
				{
                    config.RejectAssetExtensionTypes.AddRange(rejExtList);
                    config.RejectAssetExtensionTypes.AddRange(knownVirusMalwareExt);
                }
				if (dict.ContainsKey("AllowedExtensionsTypes"))
				{
                    string allowedExtensions = dict["AllowedExtensionsTypes"].ToString();
                    var allowedExtensionsList = allowedExtensions.Split(',');
					foreach(var allowedExtension in allowedExtensionsList)
					{
						if(config.RejectAssetExtensionTypes.Contains(allowedExtension))
						{
							config.RejectAssetExtensionTypes.Remove(allowedExtension);
						}
					}
                }
            }

			return config;
		}

		public static void ProcessConfig(Config config)
        {
			Globals.Port = config.Port;
			Globals.APIPort = config.APIPort;
			Globals.APICallURL = config.APICallURL;
			Globals.APICallURLLogging = config.APICallURLLogging;
			Globals.NFTTimeout = config.NFTTimeout;
			Globals.PasswordClearTime = config.PasswordClearTime;
			if (config.RejectAssetExtensionTypes != null)
				foreach (var type in config.RejectAssetExtensionTypes)
					Globals.RejectAssetExtensionTypes.Add(type);
			Globals.IgnoreIncomingNFTs = config.IgnoreIncomingNFTs;
			Globals.AutoDownloadNFTAsset = config.AutoDownloadNFTAsset;
			Globals.LogAPI = config.LogAPI;
			Globals.RefuseToCallSeed = config.RefuseToCallSeed;
			Globals.OpenAPI = Globals.OpenAPI != true ? config.OpenAPI : true;
			Globals.RunUnsafeCode = config.RunUnsafeCode;
			Globals.DSTClientPort = config.DSTClientPort;
            Globals.SelfSTUNPort = config.SelfSTUNPort;
			Globals.SelfSTUNServer = Globals.SelfSTUNServer == true ? true : config.SelfSTUNServer;
			
			if (config.STUNServers?.Count() > 0)
			{
				var serverList = config.STUNServers.Split(',');
				foreach( var server in serverList)
				{
                    Globals.STUNServers.Add(new StunServer { ServerIPPort = server, Group = 999, IsNetwork = false });
                }
			}
			else
			{
				var port = Globals.IsTestNet ? 13340 : Globals.MinorVer == 5 && Globals.MajorVer == 3 ? 3440 : Globals.SelfSTUNPort; //needs to be 3340 **patched  DSTServer.cs Line: 20**

				if(!Globals.IsTestNet)
				{
                    Globals.STUNServers.Add(new StunServer { ServerIPPort = $"162.248.14.123:{port}", Group = 1, IsNetwork = true});
                    Globals.STUNServers.Add(new StunServer { ServerIPPort = $"144.126.149.104:{port}", Group = 1, IsNetwork = true });
                    Globals.STUNServers.Add(new StunServer { ServerIPPort = $"144.126.150.118:{port}", Group = 2, IsNetwork = true });
                    Globals.STUNServers.Add(new StunServer { ServerIPPort = $"89.117.21.39:{port}", Group = 2, IsNetwork = true });
                    Globals.STUNServers.Add(new StunServer { ServerIPPort = $"89.117.21.40:{port}", Group = 3, IsNetwork = true });
                    Globals.STUNServers.Add(new StunServer { ServerIPPort = $"209.126.11.92:{port}", Group = 3, IsNetwork = true });
                    Globals.STUNServers.Add(new StunServer { ServerIPPort = $"149.102.144.58:{port}", Group = 4, IsNetwork = true });
                    Globals.STUNServers.Add(new StunServer { ServerIPPort = $"194.233.77.39:{port}", Group = 4, IsNetwork = true });
                    Globals.STUNServers.Add(new StunServer { ServerIPPort = $"185.188.249.117:{port}", Group = 5, IsNetwork = true });
                    Globals.STUNServers.Add(new StunServer { ServerIPPort = $"154.26.155.35:{port}", Group = 5, IsNetwork = true });

					//failover
                    Globals.STUNServers.Add(new StunServer { ServerIPPort = $"173.254.253.106:{port}", Group = 0, IsNetwork = true });
                }
				else
				{
                    Globals.STUNServers.Add(new StunServer { ServerIPPort = $"162.251.121.150:{port}", Group = 1, IsNetwork = true });
                }
            }

            if (config.TestNet == true)
            {
                Globals.ADJPort = 13339;
                Globals.IsTestNet = true;
				Globals.GenesisAddress = "xAfPR4w2cBsvmB7Ju5mToBLtJYuv1AZSyo";
				Globals.Port = 13338;
				Globals.APIPort = 17292;
				Globals.AddressPrefix = 0x89; //address prefix 'x'
				Globals.V1ValHeight = 200;
				Globals.TXHeightRule1 = 200;
				Globals.TXHeightRule2 = 200;
				Globals.DSTClientPort = 13341;
                Globals.SelfSTUNPort = 13340;
            }

			if (!string.IsNullOrWhiteSpace(config.WalletPassword))
			{
				Globals.WalletPassword = config.WalletPassword.ToEncrypt();
				Globals.CLIWalletUnlockTime = DateTime.UtcNow;
				Globals.WalletUnlockTime = config.WalletUnlockTime;
				Globals.AlwaysRequireWalletPassword = config.AlwaysRequireWalletPassword;
			}

			if (!string.IsNullOrWhiteSpace(config.APIPassword))
            {
				//create API Password method that locks password in encrypted string
				Globals.APIPassword = config.APIPassword.ToEncrypt();
				Globals.APIUnlockTime = DateTime.UtcNow;
				Globals.WalletUnlockTime = config.WalletUnlockTime;
				Globals.AlwaysRequireAPIPassword = config.AlwaysRequireAPIPassword;

			}
			if(config.ChainCheckPoint == true)
            {
				//establish chain checkpoint parameters here.
				Globals.ChainCheckPointInterval = config.ChainCheckPointInterval;
				Globals.ChainCheckPointRetain = config.ChainCheckPointRetain;
				Globals.ChainCheckpointLocation = config.ChainCheckpointLocation;
            }

			if(!string.IsNullOrWhiteSpace(config.ValidatorAddress))
            {
				Globals.ConfigValidator = config.ValidatorAddress;
				Globals.ConfigValidatorName = config.ValidatorName;
            }

			if(!string.IsNullOrEmpty(config.MotherAddress))
			{
				Globals.MotherAddress = config.MotherAddress;
				Globals.MotherPassword = config.MotherPassword != null ? config.MotherPassword.ToSecureString() : null;
				Globals.ConnectToMother = true;
			}
			
        }

		public static async void EstablishConfigFile()
		{
			var path = GetPathUtility.GetConfigPath();
			var fileExist = File.Exists(path + "config.txt");

			if(!fileExist)
            {
				if (Globals.IsTestNet == false)
				{
					File.AppendAllText(path + "config.txt", "Port=3338");
					File.AppendAllText(path + "config.txt", Environment.NewLine + "APIPort=7292");
					File.AppendAllText(path + "config.txt", Environment.NewLine + "TestNet=false");
					File.AppendAllText(path + "config.txt", Environment.NewLine + "NFTTimeout=15");
                    File.AppendAllText(path + "config.txt", Environment.NewLine + "AutoDownloadNFTAsset=true");
                }
                else
                {
					File.AppendAllText(path + "config.txt", "Port=13338");
					File.AppendAllText(path + "config.txt", Environment.NewLine + "APIPort=17292");
					File.AppendAllText(path + "config.txt", Environment.NewLine + "TestNet=true");
					File.AppendAllText(path + "config.txt", Environment.NewLine + "NFTTimeout=15");
                    File.AppendAllText(path + "config.txt", Environment.NewLine + "AutoDownloadNFTAsset=true");
                }
				
			}
		}
    }
}
