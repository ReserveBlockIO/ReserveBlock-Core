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
		public bool TestNet{ get; set; }
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
			}

			return config;
		}

		public static void ProcessConfig(Config config)
        {
			Program.Port = config.Port;
			Program.APIPort = config.APIPort;
			Program.APICallURL = config.APICallURL;
			Program.APICallURLLogging = config.APICallURLLogging;

			if(config.TestNet == true)
            {
				Program.IsTestNet = true;
				Program.GenesisAddress = "xAfPR4w2cBsvmB7Ju5mToBLtJYuv1AZSyo";
				Program.Port = 13338;
				Program.APIPort = 17292;
				Program.AddressPrefix = 0x89; //address prefix 'x'
			}

			if (config.WalletPassword != null)
			{
				Program.WalletPassword = config.WalletPassword.ToEncrypt();
				Program.CLIWalletUnlockTime = DateTime.UtcNow;
				Program.WalletUnlockTime = config.WalletUnlockTime;
				Program.AlwaysRequireWalletPassword = config.AlwaysRequireWalletPassword;
			}

			if (config.APIPassword != null)
            {
				//create API Password method that locks password in encrypted string
				Program.APIPassword = config.APIPassword.ToEncrypt();
				Program.APIUnlockTime = DateTime.UtcNow;
				Program.WalletUnlockTime = config.WalletUnlockTime;
				Program.AlwaysRequireAPIPassword = config.AlwaysRequireAPIPassword;

			}
			if(config.ChainCheckPoint == true)
            {
				//establish chain checkpoint parameters here.
				Program.ChainCheckPointInterval = config.ChainCheckPointInterval;
				Program.ChainCheckPointRetain = config.ChainCheckPointRetain;
				Program.ChainCheckpointLocation = config.ChainCheckpointLocation;
            }

			if(config.ValidatorAddress != null)
            {
				Program.ConfigValidator = config.ValidatorAddress;
				Program.ConfigValidatorName = config.ValidatorName;
            }
        }

		public static async void EstablishConfigFile()
		{
			var path = GetPathUtility.GetConfigPath();
			var fileExist = File.Exists(path + "config.txt");

			if(!fileExist)
            {
				if (Program.IsTestNet == false)
				{
					File.AppendAllText(path + "config.txt", "Port=3338");
					File.AppendAllText(path + "config.txt", Environment.NewLine + "APIPort=7292");
					File.AppendAllText(path + "config.txt", Environment.NewLine + "TestNet=false");
				}
                else
                {
					File.AppendAllText(path + "config.txt", "Port=13338");
					File.AppendAllText(path + "config.txt", Environment.NewLine + "APIPort=17292");
					File.AppendAllText(path + "config.txt", Environment.NewLine + "TestNet=true");
				}
				
			}
		}
    }
}
