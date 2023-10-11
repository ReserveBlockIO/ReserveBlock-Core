using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Config
{
    public class ForkConfiguration
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
        public bool LogMemory { get; set; }

        public static async Task RunForkedConfiguration(string? forkedId = "")
        {
            if(Globals.IsFork)
            {
                //default forked chain ref.
                BlockchainData.ChainRef = string.IsNullOrEmpty(forkedId) ? "f1_GahcUwfWNTF9H1kym8vEjVkwUbkoQK" : 
                    BlockchainData.ChainRef = $"f1_{forkedId}";
            }
        }
        public static ForkConfiguration ReadConfigFile()
        {
            //Add custom forking logic for read config here.
            return new ForkConfiguration();
        }
        public static void ProcessConfig(ForkConfiguration config)
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
            Globals.SelfSTUNServer = config.SelfSTUNServer;
            Globals.LogMemory = config.LogMemory;

            if (config.TestNet == true)
            {
                Globals.ADJPort = 0000; //replace with forked values.
                Globals.IsTestNet = true;
                Globals.GenesisAddress = "xAfPR4w2cBsvmB7Ju5mToBLtJYuv1AZSyo";
                Globals.Port = 0000; //replace with forked values.
                Globals.APIPort = 0000; //replace with forked values.
                Globals.AddressPrefix = 0x00; //replace with forked values.
                Globals.V1ValHeight = 0; //replace with forked values.
                Globals.TXHeightRule1 = 0; //replace with forked values.
                Globals.TXHeightRule2 = 0; //replace with forked values.
                Globals.DSTClientPort = 00000; //replace with forked values.
                Globals.SelfSTUNPort = 00000;//replace with forked values.
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
            if (config.ChainCheckPoint == true)
            {
                //establish chain checkpoint parameters here.
                Globals.ChainCheckPointInterval = config.ChainCheckPointInterval;
                Globals.ChainCheckPointRetain = config.ChainCheckPointRetain;
                Globals.ChainCheckpointLocation = config.ChainCheckpointLocation;
            }

            if (!string.IsNullOrWhiteSpace(config.ValidatorAddress))
            {
                Globals.ConfigValidator = config.ValidatorAddress;
                Globals.ConfigValidatorName = config.ValidatorName;
            }

            if (!string.IsNullOrEmpty(config.MotherAddress))
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

            if (!fileExist)
            {
                if (Globals.IsTestNet == false)
                {
                    File.AppendAllText(path + "config.txt", "Port=3338"); //replace with forked values.
                    File.AppendAllText(path + "config.txt", Environment.NewLine + "APIPort=7292");
                    File.AppendAllText(path + "config.txt", Environment.NewLine + "TestNet=false");
                    File.AppendAllText(path + "config.txt", Environment.NewLine + "NFTTimeout=15");
                    File.AppendAllText(path + "config.txt", Environment.NewLine + "AutoDownloadNFTAsset=true");
                }
                else
                {
                    File.AppendAllText(path + "config.txt", "Port=13338"); //replace with forked values.
                    File.AppendAllText(path + "config.txt", Environment.NewLine + "APIPort=17292");
                    File.AppendAllText(path + "config.txt", Environment.NewLine + "TestNet=true");
                    File.AppendAllText(path + "config.txt", Environment.NewLine + "NFTTimeout=15");
                    File.AppendAllText(path + "config.txt", Environment.NewLine + "AutoDownloadNFTAsset=true");
                }

            }
        }
    }
}
