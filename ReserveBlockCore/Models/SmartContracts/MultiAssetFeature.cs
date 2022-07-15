namespace ReserveBlockCore.Models.SmartContracts
{
    public class MultiAssetFeature
    {
        public Guid AssetId { get; set; }
        public string AssetAuthorName { get; set; }
        public string FileName { get; set; }
        public string Location { get; set; }
        public string Extension { get; set; }
        public long FileSize { get; set; }

        public static List<MultiAssetFeature> GetMultiAssetFeature(List<string> maList)
        {
            List<MultiAssetFeature> multiAssetFeatures = new List<MultiAssetFeature>();

            maList.ForEach(x => {
                MultiAssetFeature multiAssetFeature = new MultiAssetFeature();

                var mafArray = x.Split(new string[] { "|->" }, StringSplitOptions.None);

                multiAssetFeature.FileName = mafArray[0].ToString();
                multiAssetFeature.Location = "Asset Folder";
                multiAssetFeature.FileSize = Convert.ToInt32(mafArray[1].ToString());
                multiAssetFeature.Extension = multiAssetFeature.FileName != "" && multiAssetFeature.FileName != null ? Path.GetExtension(multiAssetFeature.FileName) : ""; 
                multiAssetFeature.AssetAuthorName = mafArray[2].ToString();

                multiAssetFeatures.Add(multiAssetFeature);

            });

            return multiAssetFeatures;
        }
    }
}
