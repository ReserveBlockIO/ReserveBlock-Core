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
                    var checksumAsset = asset + "," + checksum;
                    checksumList = checksumAsset;
                }
                else
                {
                    var checksumAsset = asset + ":" + checksum;
                    checksumList = checksumList + "," + checksumAsset;
                }
            }

            output = checksumList;

            return output;
        }
    }
}
