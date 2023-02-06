using ReserveBlockCore.Extensions;
using ReserveBlockCore.Data;
using ReserveBlockCore.BIP39;
using ReserveBlockCore.BIP32;

namespace ReserveBlockCore.Models
{
    public class HDWallet
    {
        public int Id { get; set; }
        public string WalletSeed { get; set; }
        public int Nonce { get; set; }
        public string Path { get; set; }

        public class HDWalletData
        {
            public static LiteDB.ILiteCollection<HDWallet> GetHDWalletData()
            {
                var hdwallet = DbContext.DB_HD_Wallet.GetCollection<HDWallet>(DbContext.RSRV_HD_WALLET);
                return hdwallet;
            }

            public static (bool,string) CreateHDWallet(int amount, BIP39Wordlist wordList, string password = "")
            {
                var hd = GetHDWalletData();
                var hdwExist = GetHDWallet();
                if(hdwExist != null)
                {
                    return (false, "HD wallet exist");
                }

                int strength = 256;

                if(amount == 12)
                {
                    strength = 128;
                }

                Mnemonic mnemonic = new Mnemonic();
                var myMnemonic = mnemonic.GenerateMnemonic(strength, wordList);
                var myMnemonicSeed = mnemonic.MnemonicToSeedHex(myMnemonic, password);

                HDWallet hdw = new HDWallet { 
                    Nonce = 0,
                    Path = "m/0'/0'",
                    WalletSeed = myMnemonicSeed
                };

                hd.InsertSafe(hdw);

                return (true,myMnemonic);
            }

            public static string RestoreHDWallet(string mnemonicStr, string password = "")
            {
                var hd = GetHDWalletData();
                var hdwExist = GetHDWallet();
                if (hdwExist != null)
                {
                    return "HD Wallet Already Exist";
                }

                Mnemonic mnemonic = new Mnemonic();
                var validateMnemonic = mnemonic.ValidateMnemonic(mnemonicStr, BIP39Wordlist.English);
                if(validateMnemonic == false)
                {
                    return "Invalid Mnemonic Entered... Please Try again.";
                }

                var myMnemonicSeed = mnemonic.MnemonicToSeedHex(mnemonicStr, password);

                HDWallet hdw = new HDWallet
                {
                    Nonce = 0,
                    Path = "m/0'/0'",
                    WalletSeed = myMnemonicSeed
                };

                hd.InsertSafe(hdw);

                Globals.HDWallet = true;

                return "Mnemonic Restored...";
            }

            public static HDWallet? GetHDWallet()
            {
                var hd = GetHDWalletData();
                if (hd != null)
                {
                    var hdw = hd.FindAll().FirstOrDefault();
                    if (hdw != null)
                    {
                        return hdw;
                    }
                }
                return null;
            }

            public static Account? GenerateAddress()
            {
                var hd = GetHDWalletData();
                var hdw = GetHDWallet();

                if(hdw != null)
                {
                    var seed = hdw.WalletSeed;
                    var path = hdw.Path;
                    var nonce = hdw.Nonce;
                    var expectedPath = $"{path}/{nonce}'";
                    BIP32.BIP32 bip32 = new BIP32.BIP32();
                    var derivePath = bip32.DerivePath(expectedPath, seed);
                    var key = derivePath.Key.ToStringHex();
                    var account = AccountData.RestoreHDAccount(key);

                    IncrementNonce(hdw);

                    return account;
                }
                else
                {
                    //no hd wallet
                    return null;
                }
            }

            public static void IncrementNonce(HDWallet hdw)
            {
                var hd = GetHDWalletData();
                hdw.Nonce += 1;
                hd.UpdateSafe(hdw);
            }
        }

    }
}
