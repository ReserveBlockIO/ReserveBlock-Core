using LiteDB;
using ReserveBlockCore.Data;
using ReserveBlockCore.Extensions;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Models
{
    public class Validators
    {
        public long Id { get; set; }
        public string Address { get; set; }
        public string UniqueName { get; set; }
        public long Position { get; set; }
        public decimal Amount { get; set; } //Must be 1000 or more.
        public long EligibleBlockStart { get; set; }
        public string Signature { get; set; }
        public bool IsActive { get; set; }
        public int FailCount { get; set; }
        public string NodeIP { get; set; } 
        public string NodeReferenceId { get; set; } 
        public string WalletVersion { get; set; }
        public DateTime LastChecked { get; set; }
        public class Validator
        {
            public static List<Validators> ValidatorList { get; set; }

            public static string backupValidator = Program.GenesisAddress;

            public static int FailCountLimit = 10;

            public static void Add(Validators validator)
            {
                var validators = GetAll();

                // insert into database
                validators.Insert(validator);
            }

            public static ILiteCollection<Validators> GetAll()
            {
                try
                {
                    var validators = DbContext.DB_Peers.GetCollection<Validators>(DbContext.RSRV_VALIDATORS);
                    return validators;
                }
                catch (Exception ex)
                {
                    ErrorLogUtility.LogError(ex.Message, "Validators.GetAll()");
                    return null;
                }
                
            }

            public static List<Account> GetLocalValidator()
            {
                var validators = AccountData.GetLocalValidator();
                var query = validators.ToList();

                return query;
            }

            internal static void Initialize()
            {
                ValidatorList = new List<Validators>();
                var staker = GetAll();
                if (staker.Count() < 1)
                {
                    
                }
                else
                {
                    ValidatorList.AddRange(GetAll().FindAll());
                }


            }

            //This will be a more stochastic ordered list. For now just grabbing a random person.
            public static string GetBlockValidator()
            {
                try
                {
                    var lastBlock = BlockchainData.GetLastBlock();
                    if (lastBlock == null)
                    {
                        return "NaN"; //last block should not be null.
                    }


                    var nextValidators = lastBlock.NextValidators;

                    return nextValidators;
                }
                //Look at previous block and see if I am selected next validator.
                catch (Exception ex)
                {
                    ErrorLogUtility.LogError(ex.Message, "Validators.GetBlockValidator()");
                    return "NaN";
                }
            }

            public static async Task<string> GetNextBlockValidators(string localValidator)
            {
                var output = "";
                var lastBlock = BlockchainData.GetLastBlock();
                var lastVal = lastBlock.Validator;
                bool validatorFound = false;
                if(lastBlock.Height == 0)
                {
                    var accounts = AccountData.GetAccounts();
                    var account = accounts.FindOne(x => x.IsValidating == true);

                    if(account != null)
                    {
                        output = account.Address + ":" + account.Address;
                    }
                }
                else
                {
                    var validators = Validators.Validator.GetAll();
                    if(validators != null)
                    {
                        var validatorCount = validators.Count();
                        if (validatorCount == 1)
                        {
                            var nextValidator = validators.FindAll().FirstOrDefault();
                            if (nextValidator != null)
                            {
                                output = nextValidator.Address + ":" + nextValidator.Address;
                            }
                        }
                        else if (validatorCount == 2)
                        {
                            var nextValidator = validators.FindAll().Where(x => x.Address != localValidator).FirstOrDefault();
                            if (nextValidator != null)
                            {
                                output = nextValidator.Address + ":" + lastBlock.Validator;
                            }
                        }
                        else
                        {
                            var currentValidator = validators.FindAll().Where(x => x.Address == localValidator).FirstOrDefault();
                            if (currentValidator != null)
                            {
                                bool mainFound = false;
                                string mainV = "";
                                bool secondaryFound = false;
                                string secV = "";

                                while (validatorFound == false)
                                {
                                    var validList = validators.FindAll().Where(x => x.IsActive == true).ToList();
                                    if (validList.Count() > 0)
                                    {
                                        if (validList.Count() == 1)
                                        {
                                            var nxtValidator = validList.FirstOrDefault();
                                            if (nxtValidator != null)
                                            {
                                                output = nxtValidator.Address + ":" + nxtValidator.Address;
                                                validatorFound = true;
                                            }
                                        }
                                        else if (validList.Count() == 2)
                                        {
                                            var nextValidator = validList.Where(x => x.Address != localValidator).FirstOrDefault();
                                            if (nextValidator != null)
                                            {
                                                output = nextValidator.Address + ":" + lastBlock.Validator;
                                                validatorFound = true;
                                            }
                                        }
                                        else
                                        {
                                            Random rnd = new Random();
                                            var rndValidators = validList.Where(x => x.IsActive == true).OrderBy(x => rnd.Next()).Take(2).ToList();
                                            var mainValidator = rndValidators[0];
                                            var secondValidator = rndValidators[1];
                                            var pingResults = await P2PClient.PingNextValidators(mainValidator, secondValidator);
                                            if (pingResults.Item1 == false)
                                            {
                                                mainValidator.FailCount += 1;
                                                mainValidator.IsActive = false;
                                                mainValidator.LastChecked = DateTime.UtcNow;
                                                validators.Update(mainValidator);
                                                Program.InactiveValidators.Add(mainValidator);
                                            }
                                            else
                                            {
                                                if (mainFound == false)
                                                {
                                                    mainFound = true;
                                                    mainV = mainValidator.Address;
                                                }

                                            }

                                            if (pingResults.Item2 == false)
                                            {
                                                secondValidator.FailCount += 1;
                                                secondValidator.IsActive = false;
                                                secondValidator.LastChecked = DateTime.UtcNow;
                                                validators.Update(secondValidator);
                                                Program.InactiveValidators.Add(secondValidator);
                                            }
                                            else
                                            {
                                                if (secondaryFound == false)
                                                {
                                                    secondaryFound = true;
                                                    secV = secondValidator.Address;
                                                }

                                            }

                                            if (mainFound && secondaryFound)
                                            {
                                                validatorFound = true;
                                                output = mainV + ":" + secV;
                                            }

                                        }
                                    }
                                    else
                                    {
                                        //no valid - blocks will stop
                                    }
                                }

                            }
                            else
                            {
                                //means the genesis validator must be working
                                bool mainFound = false;
                                string mainV = "";
                                bool secondaryFound = false;
                                string secV = "";

                                while (validatorFound == false)
                                {
                                    var validList = validators.FindAll().Where(x => x.IsActive == true).ToList();
                                    if (validList.Count() > 0)
                                    {
                                        if (validList.Count() == 1)
                                        {
                                            var nxtValidator = validList.FirstOrDefault();
                                            if (nxtValidator != null)
                                            {
                                                output = nxtValidator.Address + ":" + nxtValidator.Address;
                                                validatorFound = true;
                                            }
                                        }
                                        else if (validList.Count() == 2)
                                        {
                                            var nextValidator = validList.Where(x => x.Address != localValidator).FirstOrDefault();
                                            if (nextValidator != null)
                                            {
                                                output = nextValidator.Address + ":" + lastBlock.Validator;
                                                validatorFound = true;
                                            }
                                        }
                                        else
                                        {
                                            Random rnd = new Random();
                                            var rndValidators = validList.Where(x => x.IsActive == true).OrderBy(x => rnd.Next()).Take(2).ToList();
                                            var mainValidator = rndValidators[0];
                                            var secondValidator = rndValidators[1];
                                            var pingResults = await P2PClient.PingNextValidators(mainValidator, secondValidator);
                                            if (pingResults.Item1 == false)
                                            {
                                                mainValidator.FailCount += 1;
                                                mainValidator.IsActive = false;
                                                mainValidator.LastChecked = DateTime.UtcNow;
                                                validators.Update(mainValidator);
                                                Program.InactiveValidators.Add(mainValidator);
                                            }
                                            else
                                            {
                                                if (mainFound == false)
                                                {
                                                    mainFound = true;
                                                    mainV = mainValidator.Address;
                                                }

                                            }

                                            if (pingResults.Item2 == false)
                                            {
                                                secondValidator.FailCount += 1;
                                                secondValidator.IsActive = false;
                                                secondValidator.LastChecked = DateTime.UtcNow;
                                                validators.Update(secondValidator);
                                                Program.InactiveValidators.Add(secondValidator);
                                            }
                                            else
                                            {
                                                if (secondaryFound == false)
                                                {
                                                    secondaryFound = true;
                                                    secV = secondValidator.Address;
                                                }

                                            }

                                            if (mainFound && secondaryFound)
                                            {
                                                validatorFound = true;
                                                output = mainV + ":" + secV;
                                            }

                                        }
                                    }
                                    else
                                    {
                                        //no valid - blocks will stop
                                    }
                                }

                            }
                        }
                    }
                    
                }
                return output;
            }

            

        }
    }

}
