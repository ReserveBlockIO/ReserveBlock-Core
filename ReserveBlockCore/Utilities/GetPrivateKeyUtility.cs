using ReserveBlockCore.Models;
using ReserveBlockCore.Services;
using System.Text;

namespace ReserveBlockCore.Utilities
{
    public class GetPrivateKeyUtility
    {
        public static async Task<string> GetPrivateKey(string privkey, string address)
        {
            if (Globals.IsWalletEncrypted == true)
            {
                //decrypt private key for send
                if (Globals.EncryptPassword.Length == 0)
                {
                    return "You must decrypt wallet first before you can send a transaction!";
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
                                return "Could not find the provided address in the keystore.";
                            }
                        }
                        else
                        {
                            return "Keystore is null.";
                        }
                    }
                    catch (Exception ex)
                    {
                        return $"Unknown Error decrypting keys. Error: {ex.Message}";
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
