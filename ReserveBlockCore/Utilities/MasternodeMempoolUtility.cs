using ReserveBlockCore.Models;

namespace ReserveBlockCore.Utilities
{
    public class MasternodeMempoolUtility
    {
        public static async Task UpdateMasternodeMempool()
        {
            var validators = Validators.Validator.GetAll().FindAll().ToList();
            var masternodeMempool = Program.MasternodePool.ToList();
            if(validators.Count() > 0)
            {
                var activeVals = validators.Where(x => x.IsActive == true).ToList();
                activeVals.ForEach(x => { 
                    if(!masternodeMempool.Exists(y => y.Address == x.Address))
                    {
                        Program.MasternodePool.Add(x);
                    }
                });
            }
            
        }
    }
}
