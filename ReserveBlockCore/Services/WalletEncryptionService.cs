using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using System.Security.Cryptography;
using System.Text;

namespace ReserveBlockCore.Services
{
    public class WalletEncryptionService
    {
        public static async Task<Keystore?> EncryptWallet(Account account, bool updateAccount = false)
        {
			var accounts = AccountData.GetAccounts();
			if(Globals.EncryptPassword.Length == 0)
            {
				return null;	
            }

			//Pulling password from secure string and converting to a byte.
			var password = Globals.EncryptPassword.ToUnsecureString();
			var newPasswordArray = Encoding.ASCII.GetBytes(password);
			var passwordKey = new byte[32 - newPasswordArray.Length].Concat(newPasswordArray).ToArray();

			//Generating a random key to encrypt private key with
			var key = new byte[32]; 
			RandomNumberGenerator.Create().GetBytes(key);
			var encryptionString = Convert.ToBase64String(key);

			//Encrypting private key with random 32 byte
			byte[] encrypted = EncryptKey(account.PrivateKey, key);

			//Encrypting random 32 byte with clients supplied password. This key will be stored and is encrypted
			byte[] keyEncrypted = EncryptKey(encryptionString, passwordKey);

			Keystore keystore = new Keystore
			{
				Address = account.Address,
				PrivateKey = Convert.ToBase64String(encrypted), 
				PublicKey = account.PublicKey,
				Key = Convert.ToBase64String(keyEncrypted),
			};

			if(updateAccount == true)
            {
				account.PrivateKey = Convert.ToBase64String(encrypted);
				accounts.UpdateSafe(account);
			}

			return keystore;
		}

        public static void DecryptWallet(string passphrase)
        {
            
        }

        public static void LockWallet()
        {
            //Removes key in memory.
        }

		static byte[] EncryptKey(string plainText, byte[] Key)
		{
			byte[] encrypted;
			byte[] IV;

			using (Aes aesAlg = Aes.Create())
			{
				aesAlg.Key = Key;

				aesAlg.GenerateIV();
				IV = aesAlg.IV;

				aesAlg.Mode = CipherMode.CBC;

				var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

				// Create the streams used for encryption. 
				using (var msEncrypt = new MemoryStream())
				{
					using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
					{
						using (var swEncrypt = new StreamWriter(csEncrypt))
						{
							//Write all data to the stream.
							swEncrypt.Write(plainText);
						}
						encrypted = msEncrypt.ToArray();
					}
				}
			}

			var combinedIvCt = new byte[IV.Length + encrypted.Length];
			Array.Copy(IV, 0, combinedIvCt, 0, IV.Length);
			Array.Copy(encrypted, 0, combinedIvCt, IV.Length, encrypted.Length);

			// Return the encrypted bytes from the memory stream. 
			return combinedIvCt;

		}

		static string DecryptKey(byte[] cipherTextCombined, byte[] Key)
		{

			// Declare the string used to hold 
			// the decrypted text. 
			string plaintext = null;

			// Create an Aes object 
			// with the specified key and IV. 
			using (Aes aesAlg = Aes.Create())
			{
				aesAlg.Key = Key;

				byte[] IV = new byte[aesAlg.BlockSize / 8];
				byte[] cipherText = new byte[cipherTextCombined.Length - IV.Length];

				Array.Copy(cipherTextCombined, IV, IV.Length);
				Array.Copy(cipherTextCombined, IV.Length, cipherText, 0, cipherText.Length);

				aesAlg.IV = IV;

				aesAlg.Mode = CipherMode.CBC;

				// Create a decrytor to perform the stream transform.
				ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

				// Create the streams used for decryption. 
				using (var msDecrypt = new MemoryStream(cipherText))
				{
					using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
					{
						using (var srDecrypt = new StreamReader(csDecrypt))
						{

							// Read the decrypted bytes from the decrypting stream
							// and place them in a string.
							plaintext = srDecrypt.ReadToEnd();
						}
					}
				}

			}

			return plaintext;

		}
	}
}
