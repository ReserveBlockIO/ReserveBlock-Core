namespace ReserveBlockCore.Models.SmartContracts
{
    public class SoulboundFeature
    {
        public string SoulboundOwner { get; set; }
        public DateTime SoulboundDate { get; set; }
        public bool BindOnMint { get; set; }
    }
}
