using LiteDB;
using ReserveBlockCore.Data;

namespace ReserveBlockCore.Models
{
    public class Validators
    {
        public long Id { get; set; }
        public string Address { get; set; }
        public string UniqueName { get; set; }
        public decimal Amount { get; set; } //Must be 1000 or more.
        public long SolvedBlocks { get; set; }
        public long LastBlockSolvedTime { get; set; } //timestamp
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
                var validators = DbContext.DB.GetCollection<Validators>(DbContext.RSRV_VALIDATORS);
                return validators;
            }

            internal static void Initialize()
            {
                ValidatorList = new List<Validators>();
                var staker = GetAll();
                if (staker.Count() < 1)
                {
                    // each account must stake at least 1000. We hard code in a few to get blocks moving. 
                    Add(new Validators
                    {
                        Address = "Address_1",
                        UniqueName = "Name1",
                        Amount = 1000
                    });

                    Add(new Validators
                    {
                        Address = "Address_2",
                        UniqueName = "Name2",
                        Amount = 1000
                    });

                    Add(new Validators
                    {
                        Address = "Address_3",
                        UniqueName = "Name3",
                        Amount = 1000
                    });

                    Add(new Validators
                    {
                        Address = "Address_4",
                        UniqueName = "Name4",
                        Amount = 1000
                    });

                    ValidatorList.AddRange(GetAll().FindAll());
                }
                else
                {
                    ValidatorList.AddRange(GetAll().FindAll());
                }


            }

            //This will be a more stochastic ordered list. For now just grabbing a random person.
            public static string GetBlockValidator()
            {
                var numOfValidators = ValidatorList.Count;
                if(numOfValidators == 0)
                {
                    return "NaN";
                }
                var random = new Random();
                int choosed = random.Next(0, numOfValidators);
                var validator = ValidatorList[choosed].Address;
                return validator;
            }

        }
    }

}
