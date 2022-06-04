namespace ReserveBlockCore.Models.SmartContracts
{
    public class SelfDestructingFeature
    {
        public long SelfDestructDate { get; set; }
        public int SelfDestructBlockHeight { get; set; }
        public bool IsDestructed { get; set; }
    }
}
