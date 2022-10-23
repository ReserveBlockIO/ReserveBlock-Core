using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReserveBlockCore.Models;
using ReserveBlockCore.EllipticCurve;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using ReserveBlockCore.Utilities;
using ReserveBlockCore.Extensions;
using Spectre.Console;
using ReserveBlockCore.Services;
using ReserveBlockCore.Models.SmartContracts;

namespace ReserveBlockCore.Data
{
    internal static class AccountData
    {
		public static Account CreateNewAccount(bool skipSave = false)
        {
			Account account = new Account();
			var accountMade = false;
			while(accountMade == false)
            {
				try
				{
					PrivateKey privateKey = new PrivateKey();
					var privKeySecretHex = privateKey.secret.ToString("x");
					var pubKey = privateKey.publicKey();

					account.PrivateKey = privKeySecretHex;
					account.PublicKey = "04" + ByteToHex(pubKey.toString());
					account.Balance = 0.00M;
					account.Address = GetHumanAddress(account.PublicKey);

					var sig = Ecdsa.sign("test", privateKey);
					var verify = Ecdsa.verify("test", sig, privateKey.publicKey());

                    if (verify == true)
                    {
						if (!skipSave)
							AddToAccount(account);
						accountMade = true;
					}
				}
				catch { }
			}
			

			return account;
		}
		public static async Task<Account> RestoreAccount(string privKey, bool rescanForTx = false)
        {
			Account account = new Account();
            try
            {
				var privateKeyMod = privKey.Replace(" ", ""); //remove any accidental spaces
				BigInteger b1 = BigInteger.Parse(privateKeyMod, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
				PrivateKey privateKey = new PrivateKey("secp256k1", b1);
				var privKeySecretHex = privateKey.secret.ToString("x");
				var pubKey = privateKey.publicKey();

				account.PrivateKey = privKeySecretHex;
				account.PublicKey = "04" + ByteToHex(pubKey.toString());
				account.Address = GetHumanAddress(account.PublicKey);
				//Update balance from state trei
				var accountState = StateData.GetSpecificAccountStateTrei(account.Address);
				var adnrState = Adnr.GetAdnr(account.Address);
				var scStateTrei = SmartContractStateTrei.GetSCST();
				var scs = scStateTrei.Find(x => x.OwnerAddress == account.Address || (x.MinterAddress == account.Address && x.MinterManaged == true)).ToList();

				account.ADNR = adnrState != null ? adnrState : null;
				account.Balance = accountState != null ? accountState.Balance : 0M;

				var validators = Validators.Validator.GetAll();
				var validator = validators.FindOne(x => x.Address == account.Address);
				var accounts = AccountData.GetAccounts();
				var accountsValidating = accounts.FindOne(x => x.IsValidating == true);
				if(accountsValidating == null)
                {
					if (validator != null)
					{

					}
				}

				if(scs.Count() > 0)
				{
					foreach (var sc in scs)
					{
						try
						{
                            var transferTask = Task.Run(() => { SmartContractMain.SmartContractData.CreateSmartContract(sc.ContractData); });
                            bool isCompletedSuccessfully = transferTask.Wait(TimeSpan.FromMilliseconds(Globals.NFTTimeout * 1000));
                            if (!isCompletedSuccessfully)
                            {
                                NFTLogUtility.Log("Failed to decompile smart contract in time.", "AccountData.RestoreAccount()");
                            }
                        }
						catch(Exception ex)
						{
							ErrorLogUtility.LogError($"Failed to import Smart contract during account restore. SCUID: {sc.SmartContractUID}", "AccountData.RestoreAccount()");

                        }
                    }
                }

				var accountCheck = AccountData.GetSingleAccount(account.Address);
				if(accountCheck == null)
                {
					AddToAccount(account); //only add if not already in accounts
					if(rescanForTx == true)
                    {
						//rescan for all tx's sent out and all tx's received.
                    }
					if(Globals.IsWalletEncrypted == true)
					{
						await WalletEncryptionService.EncryptWallet(account, true);
					}
				}
			}
			catch (Exception ex)
            {
				//restore failed
				DbContext.Rollback();
				Console.WriteLine("Account restore failed. Not a valid private key");
            }
			
			//Now need to scan to check for transactions  - feature coming soon.

			return account;
		}

		public static Account RestoreHDAccount(string privKey)
		{
			Account account = new Account();
			try
			{
				var privateKeyMod = privKey.Replace(" ", ""); //remove any accidental spaces
				var privateKeyZeroPad = "00" + privateKeyMod;
				BigInteger b1 = BigInteger.Parse(privateKeyZeroPad, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
				PrivateKey privateKey = new PrivateKey("secp256k1", b1);
				var privKeySecretHex = privateKey.secret.ToString("x");
				var pubKey = privateKey.publicKey();

				account.PrivateKey = privateKeyZeroPad;
				account.PublicKey = "04" + ByteToHex(pubKey.toString());
				account.Address = GetHumanAddress(account.PublicKey);
				//Update balance from state trei
				var accountState = StateData.GetSpecificAccountStateTrei(account.Address);
				account.Balance = accountState != null ? accountState.Balance : 0M;

				var validators = Validators.Validator.GetAll();
				var validator = validators.FindOne(x => x.Address == account.Address);
				var accounts = AccountData.GetAccounts();
				var accountsValidating = accounts.FindOne(x => x.IsValidating == true);
				if (accountsValidating == null)
				{
					if (validator != null)
					{

					}
				}

				var accountCheck = AccountData.GetSingleAccount(account.Address);
				if (accountCheck == null)
				{
					AddToAccount(account); //only add if not already in accounts
				}
			}
			catch (Exception ex)
			{
				//restore failed
				DbContext.Rollback();
				Console.WriteLine("Account restore failed. Not a valid private key");
			}

			//Now need to scan to check for transactions  - feature coming soon.

			return account;
		}

		public static PrivateKey GetPrivateKey(Account account)
        {
            var accPrivateKey = GetPrivateKeyUtility.GetPrivateKey(account.PrivateKey, account.Address);

            BigInteger b1 = BigInteger.Parse(accPrivateKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

			return privateKey;
		}
		public static void PrintWalletAccounts()
        {
			Console.Clear();
			var accounts = GetAccounts();

			var accountList = accounts.FindAll().ToList();

			if (accountList.Count() > 0)
            {
				Console.Clear();
				Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop);

				AnsiConsole.Write(
				new FigletText("RBX Accounts")
				.Centered()
				.Color(Color.Green));

				var table = new Table();

				table.Title("[yellow]RBX Wallet Accounts[/]").Centered();
				table.AddColumn(new TableColumn(new Panel("Address")));
				table.AddColumn(new TableColumn(new Panel("Balance"))).Centered();

				accountList.ForEach(x => {
					table.AddRow($"[blue]{x.Address}[/]", $"[green]{x.Balance}[/]");
				});

				table.Border(TableBorder.Rounded);

				AnsiConsole.Write(table);

                Console.WriteLine("Please type /menu to return to mainscreen.");
            }
			else
            {
				Console.Clear();
				Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop);
				Console.WriteLine("ReserveBlock Main Menu");
                Console.WriteLine("|======================================|");
                Console.WriteLine("| 1. Genesis Block (Check)             |");
                Console.WriteLine("| 2. Create Account                    |");
                Console.WriteLine("| 2hd. Create HD Wallet                |");
                Console.WriteLine("| 3. Restore Account                   |");
                Console.WriteLine("| 3hd. Restore HD Wallet               |");
                Console.WriteLine("| 4. Send Coins                        |");
                Console.WriteLine("| 5. Get Latest Block                  |");
                Console.WriteLine("| 6. Transaction History               |");
                Console.WriteLine("| 7. Wallet Address(es) Info           |");
                Console.WriteLine("| 8. Startup Masternode                |");
                Console.WriteLine("| 9. Search Block                      |");
                Console.WriteLine("| 10. Enable API (Turn On and Off)     |");
                Console.WriteLine("| 11. Stop Masternode                  |");
                Console.WriteLine("| 12. Import Smart Contract (disabled) |");
                Console.WriteLine("| 13. Exit                             |");
                Console.WriteLine("|======================================|");
                Console.WriteLine("|type /help for menu options           |");
                Console.WriteLine("|type /menu to come back to main area  |");
                Console.WriteLine("|======================================|");

                Console.WriteLine("********************************************************************");
				Console.WriteLine("You do not have any accounts yet. Please choose option 2 to create a new account.");
				

			}

		}
		public static void WalletInfo(Account account)
		{
			Console.Clear();
			Console.WriteLine("\n\n\nYour Wallet");
			Console.WriteLine("======================");
			Console.WriteLine("\nAddress :\n{0}", account.Address);
			Console.WriteLine("\nPublic Key (Uncompressed):\n{0}", account.PublicKey);
			Console.WriteLine("\nPrivate Key:\n{0}", account.PrivateKey);
			Console.WriteLine("\n - - - - - - - - - - - - - - - - - - - - - - ");
			Console.WriteLine("*** Be sure to save private key!                   ***");
			Console.WriteLine("*** Use your private key to restore account!       ***");
		}
		public static async void AddToAccount(Account account)
		{
			var accountList = GetAccounts();
			var accountCheck = accountList.FindOne(x => x.PrivateKey == account.PrivateKey);

			//This is checking in the event the user is restoring an account, and not creating a brand new one.
			if(accountCheck == null)
            {
				accountList.InsertSafe(account);
     //           if (Globals.IsWalletEncrypted == true)
     //           {
					//var result = await WalletEncryptionService.EncryptWallet(account, true);
     //           }
            }
            else
            {
				//do nothing as account is already in table. They are attempting to restore a key that already exist.
            }
		}
		public static void UpdateLocalBalance(string address, decimal amount)
        {
			var accountList = GetAccounts();
			var localAccount = accountList.FindOne(x => x.Address == address);
			localAccount.Balance -= amount;

			accountList.UpdateSafe(localAccount);
		}
		public static void UpdateLocalBalanceSub(string address, decimal amount)
		{
			var accountList = GetAccounts();
			var localAccount = accountList.FindOne(x => x.Address == address);
			localAccount.Balance += amount;

			accountList.UpdateSafe(localAccount);
			accountList.UpdateSafe(localAccount);
		}

		public static void UpdateLocalBalanceAdd(string address, decimal amount)
		{
			var accountList = GetAccounts();
			var localAccount = accountList.FindOne(x => x.Address == address);
			localAccount.Balance += amount;

			accountList.UpdateSafe(localAccount);
		}
		public static LiteDB.ILiteCollection<Account> GetAccounts()
		{
            try
            {
				var accounts = DbContext.DB_Wallet.GetCollection<Account>(DbContext.RSRV_ACCOUNTS);				
				return accounts;
			}
			catch(Exception ex)
            {
				DbContext.Rollback();
				ErrorLogUtility.LogError(ex.ToString(), "AccountData.GetAccounts()");
				return null;
			}			
		}

		public static IEnumerable<Account> GetAccountsWithBalance()
		{
			var accounts = DbContext.DB_Wallet.GetCollection<Account>(DbContext.RSRV_ACCOUNTS);
			var accountsWithBal = accounts.Find(x => x.Balance > 0);

			return accountsWithBal;
		}
		public static IEnumerable<Account> GetAccountsWithBalanceForAdnr()
		{
			var accounts = DbContext.DB_Wallet.GetCollection<Account>(DbContext.RSRV_ACCOUNTS);
			var accountsWithBal = accounts.Find(x => x.Balance >= 1.00M);

			return accountsWithBal;
		}
		public static IEnumerable<Account> GetAccountsWithAdnr()
		{
			var accounts = DbContext.DB_Wallet.GetCollection<Account>(DbContext.RSRV_ACCOUNTS);
			var accountsWithAdnr = accounts.Find(x => x.ADNR != null);

			return accountsWithAdnr;
		}
		public static IEnumerable<Account> GetPossibleValidatorAccounts()
		{
			var accounts = DbContext.DB_Wallet.GetCollection<Account>(DbContext.RSRV_ACCOUNTS);
			var accountsWithBal = accounts.Find(x => x.Balance >= 1000);

			return accountsWithBal;
		}

		public static Account GetLocalValidator()
		{
			var accounts = DbContext.DB_Wallet.GetCollection<Account>(DbContext.RSRV_ACCOUNTS);
			var accountsWithBal = accounts.FindOne(x => x.IsValidating == true);

			return accountsWithBal;
		}

		public static Account? GetSingleAccount(string humanAddress)
        {
			var account = new Account();
			var accounts = DbContext.DB_Wallet.GetCollection<Account>(DbContext.RSRV_ACCOUNTS);
			account = accounts.FindOne(x => x.Address == humanAddress);

			if(account == null)
            {
				return null;//This means a null account was found. This should never happen, but just in case the DB is erased or some other memory issue.
            }
			return account;
		}

		public static string GetHumanAddress(string pubKeyHash)
        {
			byte[] PubKey = HexToByte(pubKeyHash);
			byte[] PubKeySha = Sha256(PubKey);
			byte[] PubKeyShaRIPE = RipeMD160(PubKeySha);
			byte[] PreHashWNetwork = AppendReserveBlockNetwork(PubKeyShaRIPE, Globals.AddressPrefix);//This will create Address starting with 'R'
			byte[] PublicHash = Sha256(PreHashWNetwork);
			byte[] PublicHashHash = Sha256(PublicHash);
			byte[] Address = ConcatAddress(PreHashWNetwork, PublicHashHash);
			return Base58Encode(Address); //Returns human readable address starting with an 'R'
        }

        public static string ByteToHex(byte[] pubkey)
        {
            return Convert.ToHexString(pubkey).ToLower();
        }
		public static byte[] HexToByte(string HexString)
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
		public static byte[] Sha256(byte[] array)
		{
			SHA256Managed hashstring = new SHA256Managed();
			return hashstring.ComputeHash(array);
		}

		public static byte[] RipeMD160(byte[] array)
		{
			RIPEMD160Managed hashstring = new RIPEMD160Managed();
			return hashstring.ComputeHash(array);
		}

		public static byte[] AppendReserveBlockNetwork(byte[] RipeHash, byte Network)
		{
			byte[] extended = new byte[RipeHash.Length + 1];
			extended[0] = (byte)Network;
			Array.Copy(RipeHash, 0, extended, 1, RipeHash.Length);
			return extended;
		}
		public static byte[] ConcatAddress(byte[] RipeHash, byte[] Checksum)
		{
			byte[] ret = new byte[RipeHash.Length + 4];
			Array.Copy(RipeHash, ret, RipeHash.Length);
			Array.Copy(Checksum, 0, ret, RipeHash.Length, 4);
			return ret;
		}
		
		public static string Base58Encode(byte[] array)
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
	}
}
