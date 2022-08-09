namespace ReserveBlockCore.Services
{
    public class WalletEncryptionService
    {
        public static void EncryptWallet(string passphrase)
        {
            //encrypt all current keys
            //Produce a keypool for newly generated keys
        }

        public static void DecryptWallet(string passphrase, int minutes)
        {
            //Decrypts wallet with key in memory for X amount of minutes
        }

        public static void LockWallet()
        {
            //Removes key in memory.
        }
    }
}
