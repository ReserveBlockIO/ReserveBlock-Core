using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Services;
using ReserveBlockCore.Voting;

namespace ReserveBlockCore.Models
{
    public class Account
    {
        private string _privateKey;
        public long Id { get; set; }
        /// <summary>
        /// This is where a private key is stored. Do not use this to get the private key. Instead use GetKey.
        /// </summary>
        public string PrivateKey { get; set; }
        public string PublicKey { set; get; }
        public string Address { get; set; }
        public string? ADNR { get; set; }
        public decimal Balance { get; set; }
        public bool IsValidating { get; set; }

        /// <summary>
        /// This will return your private key. It called the GetPrivateKey(PrivateKey, Address) method.
        /// It will return either the private key, or the private key encrypted/decrypted depending if password is present.
        /// </summary>
        /// <returns>
        /// public string PrivateKey
        /// </returns>
        /// <exception cref="PrivateKey"></exception>
        public string GetKey{ get { return GetPrivateKey(PrivateKey, Address); } }

        public Account Build()
        {
            var account = new Account();
            account = AccountData.CreateNewAccount();
            return account;
        }

        public async static Task<Account> Restore(string privKey)
        {
            Account account = await AccountData.RestoreAccount(privKey);
            return account;
        }
        public static async Task AddAdnrToAccount(string address, string name)
        {
            var accounts = AccountData.GetAccounts();
            var account = accounts.FindOne(x => x.Address == address);

            if(account != null)
            {
                account.ADNR = name.ToLower();
                accounts.UpdateSafe(account);
            }
        }
        public static async Task DeleteAdnrFromAccount(string address)
        {
            var accounts = AccountData.GetAccounts();
            var account = accounts.FindOne(x => x.Address == address);

            if (account != null)
            {
                account.ADNR = null;
                accounts.UpdateSafe(account);
            }
        }
        public static async Task TransferAdnrToAccount(string fromAddress, string toAddress)
        {
            var adnrs = Adnr.GetAdnr();
            if(adnrs != null)
            {
                var adnr = adnrs.FindOne(x => x.Address == toAddress); //state trei has alrea
                if (adnr != null)
                {
                    var accounts = AccountData.GetAccounts();
                    var account = accounts.FindOne(x => x.Address == toAddress);

                    if (account != null)
                    {
                        account.ADNR = adnr.Name;
                        accounts.UpdateSafe(account);
                    }
                }
            }
        }

        private string GetPrivateKey(string privkey, string address)
        {
            if (Globals.IsWalletEncrypted == true)
            {
                //decrypt private key for send
                if (Globals.EncryptPassword.Length == 0)
                {
                    return privkey;
                }
                else
                {
                    try
                    {
                        var keystores = Keystore.GetKeystore();
                        if (keystores != null)
                        {
                            var keystore = keystores.FindOne(x => x.Address == address);
                            if (keystore != null)
                            {
                                var password = Globals.EncryptPassword.ToUnsecureString();
                                var newPasswordArray = Encoding.ASCII.GetBytes(password);
                                var passwordKey = new byte[32 - newPasswordArray.Length].Concat(newPasswordArray).ToArray();

                                var key = Convert.FromBase64String(keystore.Key);
                                var encryptedPrivKey = Convert.FromBase64String(privkey);

                                var keyDecrypted = WalletEncryptionService.DecryptKey(key, passwordKey);
                                var privKeyDecrypted = WalletEncryptionService.DecryptKey(encryptedPrivKey, Convert.FromBase64String(keyDecrypted));

                                //clearing values
                                password = "0";
                                newPasswordArray = new byte[0];
                                passwordKey = new byte[0];

                                key = new byte[0];
                                encryptedPrivKey = new byte[0];

                                keyDecrypted = "0";
                                return privKeyDecrypted;

                            }
                            else
                            {
                                return privkey;
                            }
                        }
                        else
                        {
                            return privkey;
                        }
                    }
                    catch (Exception ex)
                    {
                        return privkey;
                    }
                }
            }
            else
            {
                return privkey;
            }
        }
    }


}
