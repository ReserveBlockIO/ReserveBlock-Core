using ReserveBlockCore.Data;
using ReserveBlockCore.Models;

namespace ReserveBlockCore.Services
{
    public class ValidatorService
    {
        public static string StartValidating(Account account, string uName = "")
        {
            string output = "";
            Validators validator = new Validators();
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
                    var validatorTable = Validators.Validator.GetAll();

                    var validatorCount = validatorTable.FindAll().Where(x => x.Address == account.Address).Count();
                    if (validatorCount > 0)
                    {
                        output = "Account is already a validator";
                    }
                    else
                    {
                        validator.NodeIP = "new"; //this is as new as other users will fill this in once connected
                        validator.Amount = account.Balance;
                        validator.Address = account.Address;
                        validator.LastBlockSolvedTime = 0;
                        validator.SolvedBlocks = 0;
                        validator.UniqueName = uName == "" ? Guid.NewGuid().ToString() : uName; 
                        validator.IsActive = true;
                        validator.Signature = signature;

                        validatorTable.Insert(validator);
                    }

                    //Publish out to other validators
                    //SomePublishOutMethod(validator);

                    output = "Account found and activated as a validator! Thank you for service to the network!";
                }
            }

            return output;
        }

        public static string StopValidating(Account account)
        {

            string output = "";
            Validators validator = new Validators();
            if (account == null) { throw new ArgumentNullException(nameof(account)); }
            else
            {

            }
            return output;
        }

        private static bool UniqueNameCheck(string uName)
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
