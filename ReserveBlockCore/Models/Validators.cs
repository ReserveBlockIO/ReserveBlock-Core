using LiteDB;
using ReserveBlockCore.Data;
using ReserveBlockCore.P2P;

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
        public string NodeIP { get; set; } // this will be used to call out to next node after validator is complete. If node is online it will be chosen next. 
        public class Validator
        {
            public static List<Validators> ValidatorList { get; set; }

            public static void Add(Validators validator)
            {
                var validators = GetAll();

                // insert into database
                validators.Insert(validator);
            }

            public static ILiteCollection<Validators> GetAll()
            {
                var validators = DbContext.DB_Peers.GetCollection<Validators>(DbContext.RSRV_VALIDATORS);
                return validators;
            }

            public static List<Account> GetLocalValidator()
            {
                var validators = AccountData.GetPossibleValidatorAccounts();
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
                //Look at previous block and see if I am selected next validator.
                var lastBlock = BlockchainData.GetLastBlock();
                if(lastBlock == null)
                {
                    return "NaN"; //last block should not be null.
                }
                if(lastBlock.Height == 0) //this is just for genesis block as it has no next validators.
                {
                    var localVals = GetLocalValidator().First();
                    return localVals.Address;
                }
                var nextValidators = lastBlock.NextValidators;

                return nextValidators;
            }

            public static async Task<string> GetNextBlockValidators()
            {
                var output = "";
                var lastBlock = BlockchainData.GetLastBlock();
                var lastVal = lastBlock.Validator;
                if(lastBlock.Height == 0)
                {
                    var accounts = DbContext.DB_Wallet.GetCollection<Account>(DbContext.RSRV_ACCOUNTS);
                    var account = accounts.FindAll().Where(x => x.IsValidating == true).FirstOrDefault();

                    if(account != null)
                    {
                        output = account.Address + ":" + account.Address;
                    }
                }
                else
                {
                    var validators = Validators.Validator.GetAll();
                    var validatorCount = validators.Count();
                    if (validatorCount == 1)
                    {
                        var nextValidator = validators.FindAll().FirstOrDefault();
                        if(nextValidator != null)
                        {
                            output = nextValidator.Address + ":" + nextValidator.Address;
                        }
                    }
                    else if(validatorCount == 2)
                    {
                        var nextValidator = validators.FindAll().Where(x => x.Address != lastBlock.Validator).FirstOrDefault();
                        if( nextValidator != null)
                        {
                            output = nextValidator.Address + ":" + lastBlock.Validator;
                        }
                    }
                    else
                    {
                        var lastValidator = validators.FindAll().Where(x => x.Address == lastBlock.Validator).FirstOrDefault();//ISSUE IS HERE!
                        if(lastValidator != null)
                        {
                            var nextNum = lastValidator.Position + 1 > validatorCount ? 1 : lastValidator.Position + 1;
                            var secondNextNum = nextNum + 1 > validatorCount ? 1 : nextNum + 1;


                            var nextVal = validators.FindAll().Where(x => x.Position == nextNum).FirstOrDefault();
                            var secondaryVal = validators.FindAll().Where(x => x.Position == secondNextNum).FirstOrDefault();

                            string mainAddr = nextVal.Address;
                            string backupAddr = secondaryVal.Address;

                            var check = await P2PClient.PingNextValidators(nextVal.NodeIP, secondaryVal.NodeIP);

                            //This will need to be revised to get next validator not just revert to previous block validator. 
                            //This could cause a loop.
                            if(check.Item1 == false)
                            {
                                mainAddr = backupAddr;
                                nextVal.FailCount += 1;
                                validators.Update(nextVal);
                            }

                            if(check.Item2 == false)
                            {
                                backupAddr = lastBlock.Validator;
                                secondaryVal.FailCount += 1;
                                validators.Update(secondaryVal);
                            }

                            output = mainAddr + ":" + backupAddr;
                        }
                        else
                        {
                            //means the genesis validator must be working
                            var lastValidatorsPair = lastBlock.NextValidators;
                            var nextVals = lastValidatorsPair.Split(':');
                            var mainVal = nextVals[0];
                            var secondaryVal = nextVals[1];

                            var newValidator = validators.FindAll().Where(x => x.Address == secondaryVal).FirstOrDefault();
                            if(newValidator != null)
                            {
                                var nextNum = newValidator.Position + 1 > validatorCount ? 1 : newValidator.Position + 1;
                                var secondNextNum = nextNum + 1 > validatorCount ? 1 : nextNum + 1;
                                var thirdNextNum = secondNextNum + 1 > validatorCount ? 1 : secondNextNum + 1;

                                var nextVali = validators.FindAll().Where(x => x.Position == nextNum).FirstOrDefault();
                                var secondaryVali = validators.FindAll().Where(x => x.Position == secondNextNum).FirstOrDefault();
                                var thirdVali = validators.FindAll().Where(x => x.Position == thirdNextNum).FirstOrDefault();

                                string mainAddr = nextVali.Address;
                                string backupAddr = secondaryVali.Address;

                                var check = await P2PClient.PingNextValidators(nextVali.NodeIP, secondaryVali.NodeIP);

                                if (check.Item1 == false)
                                {
                                    mainAddr = backupAddr;
                                    nextVali.FailCount += 1;
                                    validators.Update(nextVali);
                                }

                                if (check.Item2 == false)
                                {
                                    backupAddr = lastBlock.Validator != "RBdwbhyqwJCTnoNe1n7vTXPJqi5HKc6NTH" ? lastBlock.Validator : thirdVali.Address;
                                    secondaryVali.FailCount += 1;
                                    validators.Update(secondaryVali);
                                }

                                output = mainAddr + ":" + backupAddr;
                            }

                        }
                    }
                }
                return output;
            }

        }
    }

}
