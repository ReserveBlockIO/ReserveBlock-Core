using LiteDB;
using ReserveBlockCore.Data;
using ReserveBlockCore.Extensions;
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

            public static string backupValidator = "RBdwbhyqwJCTnoNe1n7vTXPJqi5HKc6NTH";

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

            public static async Task<string> GetNextBlockValidators(string localValidator)
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
                        var currentValidator = validators.FindAll().Where(x => x.Address == localValidator).FirstOrDefault();
                        if(currentValidator != null)
                        {
                            var lastValidatorsPair = lastBlock.NextValidators;//get last validators to determine if main or secondary did the solve.
                            var nextVals = lastValidatorsPair.Split(':');
                            var mainVal = nextVals[0];
                            var secVal = nextVals[1];

                            int posCount = currentValidator.Address == secVal ? 0 : 0;
                            int posCount2 = currentValidator.Address == secVal ? 1 : 1;

                            var valiList = validators.FindAll().Where(x => x.FailCount <= 30).ToList();
                            var valiCount = valiList.OrderByDescending(x => x.Position).FirstOrDefault().Position;

                            var numMain = currentValidator.Position + posCount >= valiCount ? ((currentValidator.Position + posCount) - valiCount) : currentValidator.Position + posCount;
                            
                            var mainValidator = valiList.ToCircular().Where(x => x.Position > numMain).FirstOrDefault();
                            var secondValidator = valiList.ToCircular().Where(x => x.Position > mainValidator.Position).FirstOrDefault();

                            string mainAddr = mainValidator.Address;
                            string backupAddr = secondValidator.Address;

                            var check = await P2PClient.PingNextValidators(mainValidator, secondValidator);

                            //This will need to be revised to get next validator not just revert to previous block validator. 
                            //This could cause a loop.
                            if(check.Item1 == false)
                            {
                                mainAddr = backupAddr;
                                mainValidator.FailCount += 1;
                                validators.Update(mainValidator);
                            }

                            if(check.Item2 == false)
                            {
                                backupAddr = lastBlock.Validator;
                                secondValidator.FailCount += 1;
                                validators.Update(secondValidator);
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

                            string queryAddress = mainVal == localValidator ? mainVal : secondaryVal;

                            var newValidator = validators.FindAll().Where(x => x.Address == queryAddress).FirstOrDefault();
                            if(newValidator != null)
                            {
                                int posCount = 0;
                                int posCount2 = 1;
                                int posCount3 = 2;

                                var valiList = validators.FindAll().Where(x => x.FailCount <= 30).ToList();
                                var valiCount = valiList.OrderByDescending(x => x.Position).FirstOrDefault().Position;

                                //I think issue is with the List and the fact the highest number is 2 could  be caught in a circular loop
                                var numMain = newValidator.Position + posCount >= valiCount ? ((newValidator.Position + posCount) - valiCount) : newValidator.Position + posCount;
                                
                                var mainValidator = valiList.ToCircular().Where(x => x.Position > numMain).FirstOrDefault();
                                var secondValidator = valiList.ToCircular().Where(x => x.Position > mainValidator.Position).FirstOrDefault();

                                Validators thirdValidator = new Validators();
                                var addressThird = "";
                                if (valiList.Count() > 2)
                                {
                                    thirdValidator = valiList.ToCircular().Where(x => x.Position > secondValidator.Position).FirstOrDefault();
                                    addressThird = thirdValidator.Address != "" ? thirdValidator.Address : localValidator;
                                }
                                else
                                {
                                    addressThird = localValidator;
                                }
                                

                                string mainAddr = mainValidator.Address;
                                string backupAddr = secondValidator.Address;

                                var check = await P2PClient.PingNextValidators(mainValidator, secondValidator);

                                if (check.Item1 == false)
                                {
                                    mainAddr = backupAddr;
                                    mainValidator.FailCount += 1;
                                    validators.Update(mainValidator);
                                }

                                if (check.Item2 == false)
                                {
                                    backupAddr = lastBlock.Validator != backupValidator ? lastBlock.Validator : addressThird;
                                    secondValidator.FailCount += 1;
                                    validators.Update(secondValidator);
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
