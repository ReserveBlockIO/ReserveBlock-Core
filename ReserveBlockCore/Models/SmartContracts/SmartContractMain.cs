using LiteDB;
using ReserveBlockCore.Data;

namespace ReserveBlockCore.Models.SmartContracts
{
    public class SmartContractMain
    {
        public string Name { get; set; } //User Defined
        public string Description { get; set; } //User Defined
        public string Address { get; set; } //User Defined
        public SmartContractAsset SmartContractAsset { get; set; } 
        public bool IsPublic { get; set; } //System Set
        public Guid SmartContractUID { get; set; }//System Set
        public string Signature { get; set; }//System Set
        public List<SmartContractFeatures> Features { get; set; }

        public class SmartContractData
        {
            public static ILiteCollection<SmartContractMain> GetSCs()
            {
                var scs = DbContext.DB_Assets.GetCollection<SmartContractMain>(DbContext.RSRV_ASSETS);
                return scs;
            }

            public static SmartContractMain? GetSmartContract(Guid smartContractUID)
            {
                var scs = GetSCs();
                if(scs != null)
                {
                    var sc = scs.FindOne(x => x.SmartContractUID == smartContractUID);
                    if(sc != null)
                    {
                        return sc;
                    }
                }

                return null;
            }

            public static void SaveSmartContract(SmartContractMain scMain)
            {
                var scs = GetSCs();

                scs.Insert(scMain);
            }
        }

    }
}
