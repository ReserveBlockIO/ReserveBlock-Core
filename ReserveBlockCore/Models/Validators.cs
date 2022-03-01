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
                        var lastValidator = validators.FindAll().Where(x => x.Address == lastBlock.Validator).FirstOrDefault();
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
                            }

                            if(check.Item2 == false)
                            {
                                backupAddr = lastBlock.Validator;
                            }

                            output = mainAddr + ":" + backupAddr;
                        }
                    }
                }
                return output;
            }

        }
    }

}
