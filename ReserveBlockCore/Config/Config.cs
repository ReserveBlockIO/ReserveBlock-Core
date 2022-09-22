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
		public bool NFTIgnore { get; set; }
		public int PasswordClearTime { get; set; }

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

			if(config.TestNet == true)
            {
				Globals.IsTestNet = true;
				Globals.GenesisAddress = "xAfPR4w2cBsvmB7Ju5mToBLtJYuv1AZSyo";
				Globals.Port = 13338;
				Globals.APIPort = 17292;
				Globals.AddressPrefix = 0x89; //address prefix 'x'
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
				}
                else
                {
					File.AppendAllText(path + "config.txt", "Port=13338");
					File.AppendAllText(path + "config.txt", Environment.NewLine + "APIPort=17292");
					File.AppendAllText(path + "config.txt", Environment.NewLine + "TestNet=true");
					File.AppendAllText(path + "config.txt", Environment.NewLine + "NFTTimeout=15");
				}
				
			}
		}
    }
}
