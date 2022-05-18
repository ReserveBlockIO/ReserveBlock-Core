namespace ReserveBlockCore.Models.SmartContracts
{
    public class MultiAssetFeature
    {
        public Guid AssetId { get; set; }
        public string AssetAuthorName { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
        public string Extension { get; set; }
        public long FileSize { get; set; }
    }
}
