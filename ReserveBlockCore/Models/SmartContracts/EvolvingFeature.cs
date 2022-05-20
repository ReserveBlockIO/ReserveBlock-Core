namespace ReserveBlockCore.Models.SmartContracts
{
    public class EvolvingFeature
    {
        public int EvolutionState { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsDynamic { get; set; }
        public bool IsCurrentState { get; set; }
        public DateTime? EvolveDate { get; set; }
        public long? EvolveBlockHeight { get; set; }
        public SmartContractAsset? SmartContractAsset { get; set; }

        public static List<EvolvingFeature> GetEvolveFeature(List<string> eList)
        {
            List<EvolvingFeature> evolveFeatures = new List<EvolvingFeature>();

            eList.ForEach(x => {
                EvolvingFeature evolveFeature = new EvolvingFeature();

                var evArray = x.Split(new string[] { "|->" }, StringSplitOptions.None);

                evolveFeature.EvolutionState = Convert.ToInt32(evArray[0].ToString());
                evolveFeature.Name = evArray[1].ToString();
                evolveFeature.Description = evArray[2].ToString();
                //evolveFeature.EvolveDate = 
                if (evArray.Count() == 6)
                {
                    var blockHeight = evArray[5].ToString();
                    if (blockHeight != "")
                    {
                        evolveFeature.EvolveBlockHeight = Convert.ToInt64(evArray[5].ToString());
                    }
                    else
                    {
                        evolveFeature.EvolveBlockHeight = null;
                    }
                }
                else
                {
                    evolveFeature.EvolveBlockHeight = null;
                }    
                

                //Custom vars
                var assetName = evArray[3].ToString();
                if(assetName != "")
                {
                    SmartContractAsset scAsset = new SmartContractAsset { 
                        AssetAuthorName = "",
                        AssetId = Guid.NewGuid(),
                        Extension = "",
                        FileSize = 0,
                        Location = "",
                        Name = assetName,
                    };

                    evolveFeature.SmartContractAsset = scAsset;
                }
                else
                {
                    evolveFeature.SmartContractAsset = null;
                }    
                var evolveDateTicks = evArray[4].ToString();
                if(evolveDateTicks != "")
                {
                    DateTime myDate = new DateTime(Convert.ToInt64(evolveDateTicks));
                    evolveFeature.EvolveDate = myDate;
                }
                else
                {
                    evolveFeature.EvolveDate = null;
                }

                evolveFeatures.Add(evolveFeature);

            });

            return evolveFeatures;
        }
    }
}
