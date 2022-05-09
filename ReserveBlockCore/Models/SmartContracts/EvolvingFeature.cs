namespace ReserveBlockCore.Models.SmartContracts
{
    public class EvolvingFeature
    {
        public int EvolutionState { get; set; }
        public EvolveParamaterType EvolveParamaterType { get; set; }
        public object EvolveParamater { get; set; }
        public string AssetId { get; set; }
    }

    public enum EvolveParamaterType
    {
        Date,
        String,
        Number
    }
}
