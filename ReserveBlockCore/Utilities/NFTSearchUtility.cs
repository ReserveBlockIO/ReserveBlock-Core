using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;

namespace ReserveBlockCore.Utilities
{
    public class NFTSearchUtility
    {
        public static async Task<List<SmartContractMain>> Search(string searchParam, bool isMinted = false)
        {
            List<SmartContractMain> scMainList = new List<SmartContractMain>();
            var scs = SmartContractMain.SmartContractData.GetSCs()
                   .Find(x => x.Features == null || !x.Features.Any(x => x.FeatureName != FeatureName.Tokenization))
                   .ToList();

            if (isMinted)
                scs = scs.Where(x => x.IsMinter == true).Where(x => x.Features != null && x.Features.Any(y => y.FeatureName == FeatureName.Evolving)).ToList();

            var specificNFT = scs.Where(x => x.SmartContractUID.ToLower().Contains(searchParam.ToLower())).FirstOrDefault();
            if(specificNFT != null)
            {
                scMainList.Add(specificNFT);
                return scMainList;
            }

            var containsNFTName = scs.Where(x => x.Name.ToLower().Contains(searchParam.ToLower()));
            if(containsNFTName.Any())
            {
                scMainList.AddRange(containsNFTName);
            }

            var containsNFTDesc = scs.Where(x => x.Description.ToLower().Contains(searchParam.ToLower()));
            if(containsNFTDesc.Any())
            {
                scMainList.AddRange(containsNFTDesc);
            }

            if(scMainList.Count > 0)
            {
                return scMainList;
            }
            else
            {
                scMainList = await SearchFurther(searchParam, scs);
            }

            return scMainList;
        }

        private static async Task<List<SmartContractMain>> SearchFurther(string searchParam, List<SmartContractMain> scs)
        {
            List<SmartContractMain> scMainList = new List<SmartContractMain>();

            var searchParamSplit = searchParam.Split(" ").ToList();

            if(searchParamSplit != null)
            {
                foreach (var search in searchParamSplit)
                {
                    var searchName = scs.Where(x => x.Name.ToLower().Contains(search.ToLower()));    
                    if(searchName.Any())
                    {
                        scMainList.AddRange(searchName);
                    }

                    var searchDesc = scs.Where(x => x.Description.ToLower().Contains(search.ToLower()));

                    if(searchDesc.Any())
                    {
                        scMainList.AddRange(searchDesc);
                    }    
                }
            }

            return scMainList;
        }
    }
}
