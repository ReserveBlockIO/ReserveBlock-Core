using LiteDB;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace ReserveBlockCore.Models
{
    public class ReserveAccount
    {

        #region Variables

        [BsonId]
        public long Id { get; set; }
        /// <summary>
        /// This is where a private key is stored. Do not use this to get the private key. Instead use GetKey.
        /// </summary>
        public string PrivateKey { get; set; }
        public string PublicKey { set; get; }
        public string Address { get; set; }
        public string RecoveryAddress { get; set; }
        public string? RecoveryPrivateKey { get; set; } = null;
        public string? RecoveryEncryptedDecryptKey { get; set; } = null;
        public string EncryptedDecryptKey { get; set; }
        public decimal AvailableBalance { get; set; } //funds reserved or locked
        public decimal LockedBalance { get; set; } //funds currently pending use
        public bool IsNetworkProtected { get; set; } // this is set once 1 RBX has been sent.
        public string GetKey { get { return GetPrivateKey(PrivateKey, Address, EncryptedDecryptKey); } }
        public decimal TotalBalance { get { return AvailableBalance + LockedBalance; } }

        public class ReserveAccountInfo
        {
            public string PrivateKey { get; set; }
            public string Address { set; get; }
            public string RecoveryAddress { get; set; }
            public string RecoveryPrivateKey { get; set; }
            public string RestoreCode { get { return (PrivateKey + "//" + RecoveryPrivateKey).ToBase64(); } }
        }

        public class ReserveAccountCreatePayload
        {
            public string Password { get; set; }
            public bool StoreRecoveryAccount { get; set; }
        }

        public class ReserveAccountRestorePayload
        {
            public string RestoreCode { get; set; }
            public string Password { get; set; }
            public bool StoreRecoveryAccount { get; set; }
            public bool RescanForTx { get; set; }
        }

        #endregion

        #region Get Reserve Accounts Db
        public static LiteDB.ILiteCollection<ReserveAccount>? GetReserveAccountsDb()
        {
            try
            {
                var raDB = DbContext.DB_Wallet.GetCollection<ReserveAccount>(DbContext.RSRV_RESERVE_ACCOUNTS);
                return raDB;
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.ToString(), "ReserveAccount.GetReserveAccountsDb()");
                return null;
            }

        }

        #endregion

        #region Save Reserve Account

        public static void SaveReserveAccount(ReserveAccount reserveAccount)
        {
            var db = GetReserveAccountsDb();
            if(db != null)
            {
                var accountExist = db.Query().Where(x => x.Address == reserveAccount.Address).FirstOrDefault();
                if(accountExist == null)
                {
                    db.InsertSafe(reserveAccount);
                }
            }
        }

        #endregion

        #region Get Reserve Accounts List

        public static List<ReserveAccount>? GetReserveAccounts()
        {
            var db = GetReserveAccountsDb();
            if (db != null)
            {
                var accountList = db.Query().Where(x => true).ToList();
                if (accountList.Count() > 0)
                {
                    return accountList;
                }
            }

            return null;
        }

        #endregion

        #region Update Balances

        public static void UpdateLocalBalance(string address, decimal amount)
        {
            var db = GetReserveAccountsDb();
            var accountList = GetReserveAccounts();
            if(accountList?.Count() > 0)
            {
                var localAccount = accountList.Where(x => x.Address == address).FirstOrDefault();
                if(localAccount != null)
                {
                    localAccount.AvailableBalance -= amount;
                    localAccount.LockedBalance += amount;
                }
                db.UpdateSafe(localAccount);
            }
            
        }

        #endregion

        #region Get Private key

        private string GetPrivateKey(string privkey, string address, string decryptKey)
        {
            //decrypt private key for send
            if (Globals.DecryptPassword.Length == 0)
            {
                return "0";
            }
            else
            {
                try
                {

                    return privkey;
                }
                catch (Exception ex)
                {
                    return "0";
                }
            }
        }

        #endregion

        #region Create New Reserve Account

        public static ReserveAccountInfo CreateNewReserveAccount(string encryptionPassword, bool storeRecoveryKey = false)
        {
            ReserveAccount rAccount = new ReserveAccount();
            Account account = new Account();
            ReserveAccountInfo rAccountInfo = new ReserveAccountInfo();
            var accountMade = false;

            var newPasswordArray = Encoding.ASCII.GetBytes(encryptionPassword);
            var passwordKey = new byte[32 - newPasswordArray.Length].Concat(newPasswordArray).ToArray();

            while (accountMade == false)
            {
                try
                {
                    var key = new byte[32];
                    RandomNumberGenerator.Create().GetBytes(key);
                    var encryptionString = Convert.ToBase64String(key);

                    rAccount = new ReserveAccount();
                    rAccountInfo = new ReserveAccountInfo();

                    PrivateKey privateKey = new PrivateKey();
                    var privKeySecretHex = privateKey.secret.ToString("x");
                    var pubKey = privateKey.publicKey();

                    rAccount.PrivateKey = privKeySecretHex;
                    rAccount.PublicKey = "04" + ByteToHex(pubKey.toString());
                    rAccount.AvailableBalance = 0.0M;
                    rAccount.LockedBalance = 0.0M;
                    rAccount.EncryptedDecryptKey = "";
                    rAccount.Address = GetHumanAddress(rAccount.PublicKey);

                    rAccountInfo.Address = rAccount.Address;
                    rAccountInfo.PrivateKey = privKeySecretHex;

                    //Encrypting private key with random 32 byte
                    byte[] encrypted = EncryptKey(rAccount.PrivateKey, key);

                    //Encrypting random 32 byte with clients supplied password. This key will be stored and is encrypted
                    byte[] keyEncrypted = EncryptKey(encryptionString, passwordKey);

                    rAccount.PrivateKey = Convert.ToBase64String(encrypted);
                    rAccount.EncryptedDecryptKey = Convert.ToBase64String(keyEncrypted);

                    account = AccountData.CreateNewAccount(true);
                    rAccount.RecoveryAddress = account.Address;

                    rAccountInfo.RecoveryAddress = account.Address;
                    rAccountInfo.RecoveryPrivateKey = account.GetKey;

                    if (storeRecoveryKey)
                    {
                        var key2 = new byte[32];
                        RandomNumberGenerator.Create().GetBytes(key2);
                        var encryptionString2 = Convert.ToBase64String(key2);

                        //Encrypting private key with random 32 byte
                        byte[] encrypted2 = EncryptKey(account.PrivateKey, key2);

                        //Encrypting random 32 byte with clients supplied password. This key will be stored and is encrypted
                        byte[] keyEncrypted2 = EncryptKey(encryptionString2, passwordKey);

                        rAccount.RecoveryPrivateKey = Convert.ToBase64String(encrypted2);
                        rAccount.RecoveryEncryptedDecryptKey = Convert.ToBase64String(keyEncrypted2);
                    }

                    //var password = "Hey";
                    //var newPasswordArrays = Encoding.ASCII.GetBytes(password);
                    //var passwordKeys = new byte[32 - newPasswordArray.Length].Concat(newPasswordArray).ToArray();

                    //var keys = Convert.FromBase64String(rAccount.EncryptedDecryptKey);
                    //var encryptedPrivKey = Convert.FromBase64String(rAccount.PrivateKey);

                    //var keyDecrypted = WalletEncryptionService.DecryptKey(keys, passwordKeys);
                    //var privKeyDecrypted = WalletEncryptionService.DecryptKey(encryptedPrivKey, Convert.FromBase64String(keyDecrypted));

                    var sigScript = SignatureService.CreateSignature("test", privateKey, rAccount.PublicKey);
                    var verify = SignatureService.VerifySignature(rAccount.Address, "test", sigScript);

                    if (verify == true && rAccount.Address.StartsWith("xRBX"))
                    {
                        accountMade = true;
                        //save account here!
                        SaveReserveAccount(rAccount);
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogUtility.LogError($"Unknown Error: {ex.ToString()}", "ReserveAccount.CreateNewReserveAccount()");
                }
            }

            return rAccountInfo;
        }

        #endregion

        #region RestoreReserveAccount(string restoreCodeBase, string password, bool storeRecoveryKey = false, bool rescanForTx = false)

        public static async Task<ReserveAccountInfo> RestoreReserveAccount(string restoreCodeBase, string password, bool storeRecoveryKey = false, bool rescanForTx = false)
        {
            ReserveAccount rAccount = new ReserveAccount();
            Account account = new Account();
            ReserveAccountInfo rAccountInfo = new ReserveAccountInfo();

            try
            {
                var restoreCode = restoreCodeBase.ToStringFromBase64().Split("//");
                var privKey = restoreCode[0];
                var recoveryKey = restoreCode[1];

                //Reserve Account - xRBX...
                var privateKeyMod = privKey.Replace(" ", ""); //remove any accidental spaces
                BigInteger b1 = BigInteger.Parse(privateKeyMod, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
                PrivateKey privateKey = new PrivateKey("secp256k1", b1);
                var privKeySecretHex = privateKey.secret.ToString("x");
                var pubKey = privateKey.publicKey();

                //Regular Account - R...
                var recoveryKeyMod = recoveryKey.Replace(" ", "");//remove any accidental spaces
                BigInteger b2 = BigInteger.Parse(recoveryKeyMod, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
                PrivateKey recPrivateKey = new PrivateKey("secp256k1", b2);
                var recPrivateKeySecretHex = recPrivateKey.secret.ToString("x");
                var recPubKey = recPrivateKey.publicKey();

                account.PrivateKey = recPrivateKeySecretHex;
                account.PublicKey = "04" + ByteToHex(recPubKey.toString());
                account.Address = AccountData.GetHumanAddress(account.PublicKey);

                var scStateTrei = SmartContractStateTrei.GetSCST();
                var scs = scStateTrei.Query().Where(x => x.OwnerAddress == rAccount.Address).ToEnumerable();

                rAccount = new ReserveAccount();
                rAccountInfo = new ReserveAccountInfo();

                rAccount.PrivateKey = privKeySecretHex;
                rAccount.PublicKey = "04" + ByteToHex(pubKey.toString());
                rAccount.AvailableBalance = 0.0M;
                rAccount.LockedBalance = 0.0M;
                rAccount.EncryptedDecryptKey = "";
                rAccount.Address = GetHumanAddress(rAccount.PublicKey);

                rAccountInfo.Address = rAccount.Address;
                rAccountInfo.PrivateKey = privKeySecretHex;

                var newPasswordArray = Encoding.ASCII.GetBytes(password);
                var passwordKey = new byte[32 - newPasswordArray.Length].Concat(newPasswordArray).ToArray();

                var key = new byte[32];
                RandomNumberGenerator.Create().GetBytes(key);
                var encryptionString = Convert.ToBase64String(key);

                //Encrypting private key with random 32 byte
                byte[] encrypted = EncryptKey(rAccount.PrivateKey, key);

                //Encrypting random 32 byte with clients supplied password. This key will be stored and is encrypted
                byte[] keyEncrypted = EncryptKey(encryptionString, passwordKey);

                rAccount.PrivateKey = Convert.ToBase64String(encrypted);
                rAccount.EncryptedDecryptKey = Convert.ToBase64String(keyEncrypted);

                rAccount.RecoveryAddress = account.Address;

                rAccountInfo.RecoveryAddress = account.Address;
                rAccountInfo.RecoveryPrivateKey = account.GetKey;

                if (storeRecoveryKey)
                {
                    var key2 = new byte[32];
                    RandomNumberGenerator.Create().GetBytes(key2);
                    var encryptionString2 = Convert.ToBase64String(key2);

                    //Encrypting private key with random 32 byte
                    byte[] encrypted2 = EncryptKey(account.PrivateKey, key2);

                    //Encrypting random 32 byte with clients supplied password. This key will be stored and is encrypted
                    byte[] keyEncrypted2 = EncryptKey(encryptionString2, passwordKey);

                    rAccount.RecoveryPrivateKey = Convert.ToBase64String(encrypted2);
                    rAccount.RecoveryEncryptedDecryptKey = Convert.ToBase64String(keyEncrypted2);
                }

                var sigScript = SignatureService.CreateSignature("test", privateKey, rAccount.PublicKey);
                var verify = SignatureService.VerifySignature(rAccount.Address, "test", sigScript);

                if (verify == true && rAccount.Address.StartsWith("xRBX"))
                {
                    var accountStateTrei = StateData.GetSpecificAccountStateTrei(rAccount.Address);
                    if (accountStateTrei != null)
                    {
                        rAccount.AvailableBalance = accountStateTrei.Balance;
                        rAccount.LockedBalance = accountStateTrei.LockedBalance;
                    }

                    SaveReserveAccount(rAccount);
                }

                if (scs.Count() > 0)
                {
                    foreach (var sc in scs)
                    {
                        try
                        {
                            var scMain = SmartContractMain.GenerateSmartContractInMemory(sc.ContractData);
                            if (sc.MinterManaged == true)
                            {
                                if (sc.MinterAddress == rAccount.Address)
                                {
                                    scMain.IsMinter = true;
                                }
                            }

                            SmartContractMain.SmartContractData.SaveSmartContract(scMain, null);
                        }
                        catch (Exception ex)
                        {
                            ErrorLogUtility.LogError($"Failed to import Smart contract during account restore. SCUID: {sc.SmartContractUID}", "ReserveAccount.RestoreReserveAccount()");

                        }
                    }
                }

                var accountCheck = AccountData.GetSingleAccount(account.Address);
                if (accountCheck == null)
                {
                    //AddToAccount(account); //only add if not already in accounts
                    if (rescanForTx == true)
                    {
                        //fire and forget
                        _ = Task.Run(() => BlockchainRescanUtility.RescanForTransactions(account.Address, rAccount.Address));
                    }
                    if (Globals.IsWalletEncrypted == true)
                    {
                        await WalletEncryptionService.EncryptWallet(account, true);
                    }
                }
            }
            catch (Exception ex)
            {
                //restore failed				
                Console.WriteLine("Account restore failed. Not a valid private key");
            }

            return rAccountInfo;
        }

        #endregion

        # region RestoreReserveAccount(string privKey, string recoveryKey, string password, bool storeRecoveryKey = false, bool rescanForTx = false)
        public static async Task<ReserveAccountInfo> RestoreReserveAccount(string privKey, string recoveryKey, string password, bool storeRecoveryKey = false, bool rescanForTx = false)
        {
            ReserveAccount rAccount = new ReserveAccount();
            Account account = new Account();
            ReserveAccountInfo rAccountInfo = new ReserveAccountInfo();

            try
            {
                //Reserve Account - xRBX...
                var privateKeyMod = privKey.Replace(" ", ""); //remove any accidental spaces
                BigInteger b1 = BigInteger.Parse(privateKeyMod, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
                PrivateKey privateKey = new PrivateKey("secp256k1", b1);
                var privKeySecretHex = privateKey.secret.ToString("x");
                var pubKey = privateKey.publicKey();

                //Regular Account - R...
                var recoveryKeyMod = recoveryKey.Replace(" ", "");//remove any accidental spaces
                BigInteger b2 = BigInteger.Parse(recoveryKeyMod, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
                PrivateKey recPrivateKey = new PrivateKey("secp256k1", b2);
                var recPrivateKeySecretHex = recPrivateKey.secret.ToString("x");
                var recPubKey = recPrivateKey.publicKey();

                account.PrivateKey = recPrivateKeySecretHex;
                account.PublicKey = "04" + ByteToHex(recPubKey.toString());
                account.Address = AccountData.GetHumanAddress(account.PublicKey);

                var scStateTrei = SmartContractStateTrei.GetSCST();
                var scs = scStateTrei.Query().Where(x => x.OwnerAddress == rAccount.Address).ToEnumerable();

                rAccount = new ReserveAccount();
                rAccountInfo = new ReserveAccountInfo();

                rAccount.PrivateKey = privKeySecretHex;
                rAccount.PublicKey = "04" + ByteToHex(pubKey.toString());
                rAccount.AvailableBalance = 0.0M;
                rAccount.LockedBalance = 0.0M;
                rAccount.EncryptedDecryptKey = "";
                rAccount.Address = GetHumanAddress(rAccount.PublicKey);

                rAccountInfo.Address = rAccount.Address;
                rAccountInfo.PrivateKey = privKeySecretHex;

                var newPasswordArray = Encoding.ASCII.GetBytes(password);
                var passwordKey = new byte[32 - newPasswordArray.Length].Concat(newPasswordArray).ToArray();

                var key = new byte[32];
                RandomNumberGenerator.Create().GetBytes(key);
                var encryptionString = Convert.ToBase64String(key);

                //Encrypting private key with random 32 byte
                byte[] encrypted = EncryptKey(rAccount.PrivateKey, key);

                //Encrypting random 32 byte with clients supplied password. This key will be stored and is encrypted
                byte[] keyEncrypted = EncryptKey(encryptionString, passwordKey);

                rAccount.PrivateKey = Convert.ToBase64String(encrypted);
                rAccount.EncryptedDecryptKey = Convert.ToBase64String(keyEncrypted);

                rAccount.RecoveryAddress = account.Address;

                rAccountInfo.RecoveryAddress = account.Address;
                rAccountInfo.RecoveryPrivateKey = account.GetKey;

                if (storeRecoveryKey)
                {
                    var key2 = new byte[32];
                    RandomNumberGenerator.Create().GetBytes(key2);
                    var encryptionString2 = Convert.ToBase64String(key2);

                    //Encrypting private key with random 32 byte
                    byte[] encrypted2 = EncryptKey(account.PrivateKey, key2);

                    //Encrypting random 32 byte with clients supplied password. This key will be stored and is encrypted
                    byte[] keyEncrypted2 = EncryptKey(encryptionString2, passwordKey);

                    rAccount.RecoveryPrivateKey = Convert.ToBase64String(encrypted2);
                    rAccount.RecoveryEncryptedDecryptKey = Convert.ToBase64String(keyEncrypted2);
                }

                var sigScript = SignatureService.CreateSignature("test", privateKey, rAccount.PublicKey);
                var verify = SignatureService.VerifySignature(rAccount.Address, "test", sigScript);

                if (verify == true && rAccount.Address.StartsWith("xRBX"))
                {
                    var accountStateTrei = StateData.GetSpecificAccountStateTrei(rAccount.Address);
                    if(accountStateTrei != null)
                    {
                        rAccount.AvailableBalance = accountStateTrei.Balance;
                        rAccount.LockedBalance = accountStateTrei.LockedBalance;
                    }

                    SaveReserveAccount(rAccount);
                }

                if (scs.Count() > 0)
                {
                    foreach (var sc in scs)
                    {
                        try
                        {
                            var scMain = SmartContractMain.GenerateSmartContractInMemory(sc.ContractData);
                            if (sc.MinterManaged == true)
                            {
                                if (sc.MinterAddress == rAccount.Address)
                                {
                                    scMain.IsMinter = true;
                                }
                            }

                            SmartContractMain.SmartContractData.SaveSmartContract(scMain, null);
                        }
                        catch (Exception ex)
                        {
                            ErrorLogUtility.LogError($"Failed to import Smart contract during account restore. SCUID: {sc.SmartContractUID}", "ReserveAccount.RestoreReserveAccount()");

                        }
                    }
                }

                var accountCheck = AccountData.GetSingleAccount(account.Address);
                if (accountCheck == null)
                {
                    //AddToAccount(account); //only add if not already in accounts
                    if (rescanForTx == true)
                    {
                        //fire and forget
                        _ = Task.Run(() => BlockchainRescanUtility.RescanForTransactions(account.Address, rAccount.Address));
                    }
                    if (Globals.IsWalletEncrypted == true)
                    {
                        await WalletEncryptionService.EncryptWallet(account, true);
                    }
                }
            }
            catch (Exception ex)
            {
                //restore failed				
                Console.WriteLine("Account restore failed. Not a valid private key");
            }

            return rAccountInfo;
        }
        #endregion

        #region Reserve Account Create Methods

        public static string GetHumanAddress(string pubKeyHash)
        {
            byte[] PubKey = HexToByte(pubKeyHash);
            byte[] PubKeySha = Sha256(PubKey);
            byte[] PubKeyShaRIPE = RipeMD160(PubKeySha);
            byte[] PreHashWNetwork = AppendReserveBlockNetwork(PubKeyShaRIPE);//This will create Address starting with 'xRBX'
            byte[] PublicHash = Sha256(PreHashWNetwork);
            byte[] PublicHashHash = Sha256(PublicHash);
            byte[] Address = ConcatAddress(PreHashWNetwork, PublicHashHash);
            return Base58Encode(Address); //Returns human readable address starting with an 'xRBX'
        }

        private static string ByteToHex(byte[] pubkey)
        {
            return Convert.ToHexString(pubkey).ToLower();
        }
        private static byte[] HexToByte(string HexString)
        {
            if (HexString.Length % 2 != 0)
                throw new Exception("Invalid HEX");
            byte[] retArray = new byte[HexString.Length / 2];
            for (int i = 0; i < retArray.Length; ++i)
            {
                retArray[i] = byte.Parse(HexString.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            return retArray;
        }
        private static byte[] Sha256(byte[] array)
        {
            SHA256Managed hashstring = new SHA256Managed();
            return hashstring.ComputeHash(array);
        }

        private static byte[] RipeMD160(byte[] array)
        {
            RIPEMD160Managed hashstring = new RIPEMD160Managed();
            return hashstring.ComputeHash(array);
        }

        private static byte[] AppendReserveBlockNetwork(byte[] RipeHash)
        {
            //xRBX
            byte[] prefixBytes = new byte[] { 0x89, 0xB9, 0x21 };

            byte[] result = new byte[prefixBytes.Length + RipeHash.Length];
            Buffer.BlockCopy(prefixBytes, 0, result, 0, prefixBytes.Length);
            Buffer.BlockCopy(RipeHash, 0, result, prefixBytes.Length, RipeHash.Length);

            byte[] newData = new byte[result.Length - 2];
            Array.Copy(result, 0, newData, 0, newData.Length);

            byte[] extended = new byte[RipeHash.Length + 1];

            Array.Copy(RipeHash, 0, extended, 2, RipeHash.Length - 1);
            return newData;
        }
        private static byte[] ConcatAddress(byte[] RipeHash, byte[] Checksum)
        {
            byte[] ret = new byte[RipeHash.Length + 4];
            Array.Copy(RipeHash, ret, RipeHash.Length);
            Array.Copy(Checksum, 0, ret, RipeHash.Length, 4);
            return ret;
        }

        private static string Base58Encode(byte[] array)
        {
            const string ALPHABET = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
            string retString = string.Empty;
            BigInteger encodeSize = ALPHABET.Length;
            BigInteger arrayToInt = 0;

            for (int i = 0; i < array.Length; ++i)
            {
                arrayToInt = arrayToInt * 256 + array[i];
            }

            while (arrayToInt > 0)
            {
                int rem = (int)(arrayToInt % encodeSize);
                arrayToInt /= encodeSize;
                retString = ALPHABET[rem] + retString;
            }

            for (int i = 0; i < array.Length && array[i] == 0; ++i)
                retString = ALPHABET[0] + retString;
            return retString;
        }
        private static byte[] EncryptKey(string plainText, byte[] Key)
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

        public static string DecryptKey(byte[] cipherTextCombined, byte[] Key)
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

        #endregion

    }
}
