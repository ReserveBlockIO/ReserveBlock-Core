using NBitcoin;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System.Security.Cryptography;
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

        #region Get Bitcoin Address List
        public static List<BitcoinAccount>? GetBitcoinAccounts()
        {
            var bitcoin = GetBitcoin();
            if (bitcoin == null)
            {
                ErrorLogUtility.LogError("GetBitcoin() returned a null value.", "BitcoinAccount.GetBitcoinAccounts()");
            }
            else
            {
                var btcRecs = bitcoin.FindAll().ToList();
                if (btcRecs.Any())
                {
                    return btcRecs;
                }
                else
                {
                    return null;
                }
            }

            return null;

        }
        #endregion

        #region Get Bitcoin Address
        public static BitcoinAccount? GetBitcoinAccount(string address)
        {
            var bitcoin = GetBitcoin();
            if (bitcoin == null)
            {
                ErrorLogUtility.LogError("GetBitcoin() returned a null value.", "BitcoinAccount.GetBitcoinAccount()");
            }
            else
            {
                var btcRec = bitcoin.Query().Where(x => x.Address == address).FirstOrDefault();
                if (btcRec != null)
                {
                    return btcRec;
                }
                else
                {
                    return null;
                }
            }

            return null;

        }
        #endregion

        #region Save Bitcoin Address
        public static bool SaveBitcoinAddress(BitcoinAccount btcAddr)
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
                    return false;
                }
                else
                {
                    bitcoin.InsertSafe(btcAddr);
                    return true;
                }
            }

            return false;

        }
        #endregion

        #region Create Bitcoin Address
        public static BitcoinAccount CreateAddress(bool save = true)
        {
            Key privateKey = new Key();

            PubKey publicKey = privateKey.PubKey;

            // Create a Bitcoin address from the public key
            NBitcoin.BitcoinAddress bitcoinAddress = publicKey.GetAddress(Globals.ScriptPubKeyType, Globals.BTCNetwork);
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

            if(save)
                SaveBitcoinAddress(btcAddress);

            return btcAddress;
        }
        #endregion

        #region Create Bitcoin Address For Arbiter
        public static string CreatePublicKeyForArbiter(string signingPrivateKey, string scUID)
        {
            byte[] hash = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(signingPrivateKey + scUID));
            // Generate a random private key
            Key privateKey = new Key(hash);

            // Derive the corresponding public key
            PubKey publicKey = privateKey.PubKey;
            return publicKey.ToString();
        }
        #endregion

        #region Import Private Key Hex
        public static void ImportPrivateKey(string privateKey, ScriptPubKeyType scriptPubKeyType)
        {
            byte[] privateKeyBytes = privateKey.HexToByteArray();
            Key recreatedKey = new Key(privateKeyBytes);

            PubKey publicKey = recreatedKey.PubKey;

            // Create a Bitcoin address from the public key
            NBitcoin.BitcoinAddress bitcoinAddress = publicKey.GetAddress(scriptPubKeyType, Globals.BTCNetwork);
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
        public static void ImportPrivateKeyWIF(string privateKey, ScriptPubKeyType scriptPubKeyType)
        {
            BitcoinSecret bitcoinSecret = new BitcoinSecret(privateKey, Globals.BTCNetwork);
            // Get the private key
            Key recreatedKey = bitcoinSecret.PrivateKey;

            PubKey publicKey = recreatedKey.PubKey;

            // Create a Bitcoin address from the public key
            NBitcoin.BitcoinAddress bitcoinAddress = publicKey.GetAddress(scriptPubKeyType, Globals.BTCNetwork);
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

        #region Add ADNR Record
        public static async Task AddAdnrToAccount(string address, string name)
        {
            var accounts = GetBitcoin();
            if(accounts != null)
            {
                var account = accounts.FindOne(x => x.Address == address);

                if (account != null)
                {
                    account.ADNR = name.ToLower();
                    accounts.UpdateSafe(account);
                }
            }
        }

        #endregion

        #region Transfer ADNR
        public static async Task TransferAdnrToAccount(string toAddress)
        {
            var adnrs = BitcoinAdnr.GetBitcoinAdnr();
            if (adnrs != null)
            {
                var adnr = adnrs.FindOne(x => x.BTCAddress == toAddress); //state trei has alrea
                if (adnr != null)
                {
                    var accounts = GetBitcoin();
                    var account = accounts.FindOne(x => x.Address == toAddress);

                    if (account != null)
                    {
                        account.ADNR = adnr.Name;
                        accounts.UpdateSafe(account);
                    }
                }
            }
        }

        #endregion

        #region Remove ADNR Record
        public static async Task RemoveAdnrFromAccount(string address)
        {
            var accounts = GetBitcoin();
            if (accounts != null)
            {
                var account = accounts.FindOne(x => x.Address == address);

                if (account != null)
                {
                    account.ADNR = null;
                    accounts.UpdateSafe(account);
                }
            }
        }

        #endregion
    }
}
