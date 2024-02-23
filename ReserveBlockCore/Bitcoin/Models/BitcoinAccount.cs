using NBitcoin;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System.Text;

namespace ReserveBlockCore.Bitcoin.Models
{
    public class BitcoinAccount
    {
        #region Variables

        public long Id { get; set; }
        public string PrivateKey { get; set; }
        public string WifKey { get; set; }
        public string PublicKey { set; get; }
        public string Address { get; set; }
        public string? ADNR { get; set; }
        public decimal Balance { get; set; }
        public bool IsValidating { get; set; }

        #endregion

        #region GetBitcoin DB
        public static LiteDB.ILiteCollection<BitcoinAccount>? GetBitcoin()
        {
            try
            {
                var bitcoin = DbContext.DB_Bitcoin.GetCollection<BitcoinAccount>(DbContext.RSRV_BITCOIN);
                return bitcoin;
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.ToString(), "BitcoinAccount.GetBitcoin()");
                return null;
            }

        }

        #endregion

        #region Save Bitcoin Address
        public static string SaveBitcoinAddress(BitcoinAccount btcAddr)
        {
            var bitcoin = GetBitcoin();
            if (bitcoin == null)
            {
                ErrorLogUtility.LogError("GetBitcoin() returned a null value.", "BitcoinAddress.GetBitcoin()");
            }
            else
            {
                var btcRec = bitcoin.FindOne(x => x.Address == btcAddr.Address);
                if (btcRec != null)
                {
                    return "Address Already Exist";
                }
                else
                {
                    bitcoin.InsertSafe(btcAddr);
                }
            }

            return "Error Saving ADNR";

        }
        #endregion

        #region Create Bitcoin Address
        public static BitcoinAccount CreateAddress(bool useTaproot = false)
        {
            Key privateKey = new Key();
            PubKey publicKey = privateKey.PubKey;

            // Create a Bitcoin address from the public key
            NBitcoin.BitcoinAddress bitcoinAddress = publicKey.GetAddress(!useTaproot ? ScriptPubKeyType.SegwitP2SH : ScriptPubKeyType.TaprootBIP86, Globals.BTCNetwork);
            string privateKeyHex = privateKey.ToHex();

            string wif = privateKey.GetWif(Globals.BTCNetwork).ToString();

            BitcoinAccount btcAddress = new BitcoinAccount {
                Address = bitcoinAddress.ToString(),
                Balance = 0M,
                IsValidating = false,
                PrivateKey = privateKeyHex,
                PublicKey = publicKey.ToString(),
                WifKey = wif, 
            };

            SaveBitcoinAddress(btcAddress);

            return btcAddress;
        }
        #endregion

        #region Import Private Key Hex
        public static void ImportPrivateKey(string privateKey, bool useTaproot = false)
        {
            byte[] privateKeyBytes = privateKey.HexToByteArray();
            Key recreatedKey = new Key(privateKeyBytes);

            PubKey publicKey = recreatedKey.PubKey;

            // Create a Bitcoin address from the public key
            NBitcoin.BitcoinAddress bitcoinAddress = publicKey.GetAddress(!useTaproot ? ScriptPubKeyType.SegwitP2SH : ScriptPubKeyType.TaprootBIP86, Globals.BTCNetwork);
            string privateKeyHex = recreatedKey.ToHex();

            string wif = recreatedKey.GetWif(Globals.BTCNetwork).ToString();

            BitcoinAccount btcAddress = new BitcoinAccount
            {
                Address = bitcoinAddress.ToString(),
                Balance = 0M, //perform balance check here
                IsValidating = false,
                PrivateKey = privateKeyHex,
                PublicKey = publicKey.ToString(),
                WifKey = wif,
            };

            SaveBitcoinAddress(btcAddress);
        }

        #endregion

        #region Import Private Key WIF
        public static void ImportPrivateKeyWIF(string privateKey, bool useTaproot = false)
        {
            BitcoinSecret bitcoinSecret = new BitcoinSecret(privateKey, Globals.BTCNetwork);
            // Get the private key
            Key recreatedKey = bitcoinSecret.PrivateKey;

            PubKey publicKey = recreatedKey.PubKey;

            // Create a Bitcoin address from the public key
            NBitcoin.BitcoinAddress bitcoinAddress = publicKey.GetAddress(!useTaproot ? ScriptPubKeyType.SegwitP2SH : ScriptPubKeyType.TaprootBIP86, Globals.BTCNetwork);
            string privateKeyHex = recreatedKey.ToHex();

            string wif = recreatedKey.GetWif(Globals.BTCNetwork).ToString();

            BitcoinAccount btcAddress = new BitcoinAccount
            {
                Address = bitcoinAddress.ToString(),
                Balance = 0M, //perform balance check here
                IsValidating = false,
                PrivateKey = privateKeyHex,
                PublicKey = publicKey.ToString(),
                WifKey = wif,
            };

            SaveBitcoinAddress(btcAddress);
        }

        #endregion

        #region Print Account Info
        public static void PrintAccountInfo(BitcoinAccount account)
        {
            Console.Clear();
            Console.WriteLine("\n\n\nYour Wallet");
            Console.WriteLine("======================");
            Console.WriteLine("\nAddress :\n{0}", account.Address);
            Console.WriteLine("\nPublic Key (Uncompressed):\n{0}", account.PublicKey);
            Console.WriteLine("\nPrivate Key:\n{0}", account.PrivateKey);
            Console.WriteLine("\nWif Key:\n{0}", account.WifKey);
            Console.WriteLine("\n - - - - - - - - - - - - - - - - - - - - - - ");
            Console.WriteLine("*** Be sure to save private key!                   ***");
            Console.WriteLine("*** Use your private key to restore account!       ***");
        }

        #endregion
    }
}
