namespace ReserveBlockCore.Utilities
{
    public class MD5Utility
    {
        public static string MD5ListCreator(List<string> assets, string scUID)
        {
            var output = "";

            var checksumList = "";

            foreach(var asset in assets)
            {
                var path = NFTAssetFileUtility.NFTAssetPath(asset, scUID);

                var checksum = path.ToMD5();

                if(checksumList == "")
                {
                    var checksumAsset = asset + "::" + checksum;
                    checksumList = checksumAsset;
                }
                else
                {
                    var checksumAsset = asset + "::" + checksum;
                    checksumList = checksumList + "<>" + checksumAsset;
                }
            }

            output = checksumList;

            return output;
        }
        public static async Task<List<string>> GetAssetList(string md5List)
        {
            List<string> assetList = new List<string>();
            var md5Split = md5List.Split(new string[] { "<>" }, StringSplitOptions.None);

            foreach(var md5 in md5Split)
            {
                var md5AssetSplit = md5.Split(new string[] { "::" }, StringSplitOptions.None);
                assetList.Add(md5AssetSplit[0]);
            }

            return assetList;
        }
    }
}
