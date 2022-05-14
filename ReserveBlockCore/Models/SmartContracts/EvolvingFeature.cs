namespace ReserveBlockCore.Models.SmartContracts
{
    public class EvolvingFeature
    {
        public int EvolutionState { get; set; }
        public EvolveParamaterType EvolveParamaterType { get; set; }
        public object EvolveParamater { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsDynamic { get; set; }
        public SmartContractAsset? SmartContractAsset { get; set; }
    }

    public enum EvolveParamaterType
    {
        Date,
        String,
        Number
    }
}
