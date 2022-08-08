using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Extensions;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Services
{
    public class ValidatorService
    {
        public static async void DoValidate()
        {
            Console.Clear();
            var accountList = AccountData.GetPossibleValidatorAccounts();
            var accountNumberList = new Dictionary<string, Account>();

            if (accountList.Count() > 0)
            {
                int count = 1;
                accountList.ToList().ForEach(x => {
                    accountNumberList.Add(count.ToString(), x);
                    Console.WriteLine("********************************************************************");
                    Console.WriteLine("Please choose an address below to be a validator by typing its # and pressing enter.");

                    Console.WriteLine("\n #" + count.ToString());
                    Console.WriteLine("\nAddress :\n{0}", x.Address);
                    Console.WriteLine("\nAccount Balance:\n{0}", x.Balance);
                    count++;
                });

                var walletChoice = Console.ReadLine();
                while(walletChoice == "")
                {
                    Console.WriteLine("You must choose a wallet please. Type a number from above and press enter please."); 
                    walletChoice = Console.ReadLine();
                }
                var account = accountNumberList[walletChoice];
                Console.WriteLine("********************************************************************");
                Console.WriteLine("The chosen validator address is:");
                string validatorAddress = account.Address;
                Console.WriteLine(validatorAddress);
                Console.WriteLine("Are you sure you want to activate this address as a validator? (Type 'y' for yes and 'n' for no.)");
                var confirmChoice = Console.ReadLine();

                if(confirmChoice == null)
                {
                    Console.WriteLine("You must only type 'y' or 'n'. Please choose the correct option. (Type 'y' for yes and 'n' for no.)");
                    Console.WriteLine("Returning you to main menu...");
                    Thread.Sleep(5000);
                    StartupService.MainMenu();
                }
                else if(confirmChoice.ToLower() == "n")
                {
                    Console.WriteLine("Returning you to main menu in 3 seconds...");
                    Thread.Sleep(3000);
                    StartupService.MainMenu();
                }
                else
                {
                    Console.Clear();
                    Console.WriteLine("Please type a unique name for your node to be known by. If you do not want a name leave this blank and one will be assigned. (Ex. NodeSwarm_1, TexasNodes, Node1337, AaronsNode, etc.");
                    var nodeName = Console.ReadLine();
                    
                    if(nodeName != null || nodeName != "")
                    {
                        var nodeNameCheck = UniqueNameCheck(nodeName);

                        while (nodeNameCheck == false)
                        {
                            Console.WriteLine("Please choose another name as we show that as taken. (Ex. NodeSwarm_1, TexasNodes, Node1337, AaronsNode, etc.");
                            nodeName = Console.ReadLine();
                            nodeNameCheck = UniqueNameCheck(nodeName);
                        }

                        var result = await StartValidating(account,nodeName);
                        Console.WriteLine(result);
                        Console.WriteLine("Returning you to main menu in 10 seconds...");
                        Thread.Sleep(10000);
                        StartupService.MainMenu();
                    }

                }

            }
            else
            {
                Console.WriteLine("********************************************************************");
                Console.WriteLine("No wallets found with a balance.");
                Console.WriteLine("Returning you to main menu...");
                Thread.Sleep(5000);
                StartupService.MainMenu();
            }
        }
        public static async Task<string> StartValidating(Account account, string uName = "")
        {
            string output = "";
            Validators validator = new Validators();

            if(Program.StopAllTimers == true || Program.BlocksDownloading == true)
            {
                output = "Wallet is still starting. Please wait";
                return output;
            }
            

            if(account == null) { throw new ArgumentNullException(nameof(account)); }
            else 
            {
                var sTreiAcct = StateData.GetSpecificAccountStateTrei(account.Address);
                
                if(sTreiAcct == null)
                {
                    output = "Account not found in the State Trei. Please send funds to desired account and wait for at least 1 confirm.";
                    return output;
                }
                if(sTreiAcct != null && sTreiAcct.Balance < 1000.0M)
                {
                    output = "Account Found, but does not meet the minimum of 1000 RBX. Please send funds to get account balance to 1000 RBX.";
                    return output;
                }
                if(uName != "" && UniqueNameCheck(uName) == false)
                {
                    output = "Unique name has already been taken. Please choose another.";
                    return output;
                }
                if(sTreiAcct != null && sTreiAcct.Balance >= 1000.0M)
                {
                    //validate account with signature check
                    var signature = SignatureService.CreateSignature(account.Address, AccountData.GetPrivateKey(account), account.PublicKey);

                    var verifySig = SignatureService.VerifySignature(account.Address, account.Address, signature);

                    if(verifySig == false)
                    {
                        output = "Signature check has failed. Please provide correct private key for public address: " + account.Address;
                        return output;
                    }

                    //need to request validator list from someone. 

                    var accounts = AccountData.GetAccounts();
                    var IsThereValidator = accounts.FindOne(x => x.IsValidating == true);
                    if(IsThereValidator != null)
                    {
                        output = "This wallet already has a validator active on it. You can only have 1 validator active per wallet: " + IsThereValidator.Address;
                        return output;
                    }

                    var validatorTable = Validators.Validator.GetAll();

                    var validatorCount = validatorTable.FindAll().Count();
                    if (validatorCount > 0)
                    {
                        output = "Account is already a validator";
                        return output;
                    }
                    else
                    {

                        //add total num of validators to block
                        validator.NodeIP = "SELF"; //this is as new as other users will fill this in once connected
                        validator.Amount = account.Balance;
                        validator.Address = account.Address;
                        validator.EligibleBlockStart = -1;
                        validator.UniqueName = uName == "" ? Guid.NewGuid().ToString() : uName;
                        validator.IsActive = true;
                        validator.Signature = signature;
                        validator.FailCount = 0;
                        validator.Position = validatorTable.FindAll().Count() + 1;
                        validator.NodeReferenceId = BlockchainData.ChainRef;
                        validator.WalletVersion = Program.CLIVersion;
                        validator.LastChecked = DateTime.UtcNow;

                        validatorTable.InsertSafe(validator);

                        account.IsValidating = true;
                        var accountTable = AccountData.GetAccounts();
                        accountTable.UpdateSafe(account);

                        Program.ValidatorAddress = validator.Address;

                        output = "Account found and activated as a validator! Thank you for service to the network!";

                        await StartupService.ConnectoToAdjudicator();
                    }
                }
            }

            return output;
        }

        public static void DoMasterNodeStop()
        {
            Console.Clear();
            //var validatortList = Validators.Validator.GetLocalValidator();
            //var accountNumberList = new Dictionary<string, Account>();

            //if (validatortList.Count() == 0)
            //{
            //    Console.WriteLine("********************************************************************");
            //    Console.WriteLine("No active validator accounts found.");
            //    Console.WriteLine("Please note that if there was ever a time your account went below 1000 RBX you would have been automatically removed from the validator network.");
            //    Console.WriteLine("Returning you to main menu...");
            //    Thread.Sleep(5000);
            //    StartupService.MainMenu();
            //}
            //else
            //{
            //    int count = 1;
            //    validatortList.ToList().ForEach(x => {
            //        accountNumberList.Add(count.ToString(), x);
            //        Console.WriteLine("********************************************************************");
            //        Console.WriteLine("Please choose an address below to stop being a validator by typing its # and pressing enter.");

            //        Console.WriteLine("\n #" + count.ToString());
            //        Console.WriteLine("\nAddress :\n{0}", x.Address);
            //        count++;
            //    });

            //    var walletChoice = Console.ReadLine();
            //    var validator = accountNumberList[walletChoice];
            //    Console.WriteLine("********************************************************************");
            //    Console.WriteLine("The chosen validator address is:");
            //    string validatorAddress = validator.Address;
            //    Console.WriteLine(validatorAddress);
            //    Console.WriteLine("Are you sure you want to deactivate this address as a validator? (Type 'y' for yes and 'n' for no.)");
            //    var confirmChoice = Console.ReadLine();

            //    if (confirmChoice == null)
            //    {
            //        Console.WriteLine("You must only type 'y' or 'n'. Please choose the correct option. (Type 'y' for yes and 'n' for no.)");
            //        Console.WriteLine("Returning you to main menu...");
            //        Thread.Sleep(5000);
            //        StartupService.MainMenu();
            //    }
            //    else if (confirmChoice.ToLower() == "n")
            //    {
            //        Console.WriteLine("Returning you to main menu...");
            //        Thread.Sleep(3000);
            //        StartupService.MainMenu();
            //    }
            //    else
            //    {
            //        Console.Clear();
            //        //StopValidating(validator);

            //        Console.WriteLine("The chosen addresses is no longer a validator...");
            //        Console.WriteLine("Returning you to main menu in 5 seconds...");
            //        Thread.Sleep(5000);
            //        StartupService.MainMenu();
            //    }
            //}
            
        }

        public static bool ValidateTheValidator(Validators validator)
        {
            bool result = false;
            var sTreiAcct = StateData.GetSpecificAccountStateTrei(validator.Address);

            if (sTreiAcct == null)
            {
                //output = "Account not found in the State Trei. Please send funds to desired account and wait for at least 1 confirm.";
                return result;
            }
            if (sTreiAcct != null && sTreiAcct.Balance < 1000.0M)
            {
                //output = "Account Found, but does not meet the minimum of 1000 RBX. Please send funds to get account balance to 1000 RBX.";
                return result;
            }
            if (validator.UniqueName != "" && UniqueNameCheck(validator.UniqueName) == false)
            {
                //output = "Unique name has already been taken. Please choose another.";
                return result;
            }
            if (sTreiAcct != null && sTreiAcct.Balance >= 1000.0M)
            {
                result = true; //success
            }

            return result;
        }

        public static async void StopValidating(Validators validator)
        {           
            //Validators.Validator.GetAll().DeleteSafe(validator.Id);

            var accounts = AccountData.GetAccounts();
            var myAccount = accounts.FindOne(x => x.Address == validator.Address);

            myAccount.IsValidating = false;
            accounts.UpdateSafe(myAccount);

            var validators = Validators.Validator.GetAll();
            validators.Delete(validator.Id);

            await P2PClient.DisconnectAdjudicator();

            ValidatorLogUtility.Log("Funds have dropped below 1000 RBX. Removing from pool.", "ValidatorService.StopValidating()");

        }

        public static async void ClearDuplicates()
        {
            try
            {
                var validators = Validators.Validator.GetAll();
                var validatorList = validators.FindAll().ToList();

                if(validatorList.Count() > 0)
                {
                    List<Validators> dups = validatorList.GroupBy(x => new {
                        x.Address,
                        x.NodeIP
                    })
                    .Where(x => x.Count() > 1)
                    .Select(x => x.First())
                    .ToList();

                    if (dups.Count() > 0)
                    {
                        dups.ForEach(x =>
                        {
                            var dupList = validatorList.Where(y => y.Address == x.Address && y.NodeIP == x.NodeIP).ToList();
                            if (dupList.Exists(z => z.IsActive == true))
                            {
                                var dupsDel = dupList.Where(z => z.IsActive == false).ToList();
                                validators.DeleteManySafe(z => z.Address == x.Address && z.IsActive == false);
                            }
                            else
                            {
                                var countRem = dupList.Count() - 1;
                                var dupsDel = dupList.Take(countRem);
                                dupsDel.ToList().ForEach(d =>
                                {
                                    validators.DeleteManySafe(p => p.Id == d.Id);
                                });
                            }
                        });
                    }
                }
                
            }
            catch (Exception ex)
            {

            }
        }

        public static bool UniqueNameCheck(string uName)
        {
            bool output = false;
            var validatorTable = Validators.Validator.GetAll();
            var uNameCount = validatorTable.FindAll().Where(x => x.UniqueName.ToLower() == uName.ToLower()).Count();

            if (uNameCount == 0)
                output = true;

            return output;

        }

    }
}
