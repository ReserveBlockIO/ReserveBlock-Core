namespace ReserveBlockCore.Models.SmartContracts
{
    public class SmartContractAsset
    {
        public Guid AssetId { get; set; }
        public string AssetAuthorName { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
        public string Extension { get; set; }
        public long FileSize { get; set; }

        public static SmartContractAsset GetSmartContractAsset(string assetAuthor, string name, string location, string extension, long fileSize)
        {
            SmartContractAsset asset = new SmartContractAsset();
            asset.AssetId = Guid.NewGuid();
            asset.Name = name;
            asset.AssetAuthorName = assetAuthor;
            asset.Extension = extension;
            asset.FileSize = fileSize;
            asset.Location = location;

            return asset;
        }
    }
}
