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
    }
}
