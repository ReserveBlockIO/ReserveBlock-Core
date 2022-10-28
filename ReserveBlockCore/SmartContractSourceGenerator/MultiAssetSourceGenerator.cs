using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.Utilities;
using System.Text;

namespace ReserveBlockCore.SmartContractSourceGenerator
{
    public class MultiAssetSourceGenerator
    {
        public static async Task<(StringBuilder, StringBuilder)> Build(List<MultiAssetFeature> multiAsset, StringBuilder strBuild, string scUID, bool isReading = false)
        {
            var appendChar = "\"|->\"";
            StringBuilder strMultiAssetBld = new StringBuilder();
            bool failedToSaveAsset = false;

            int counter = 1;
            var multiAssetCount = multiAsset.Count().ToString();
            strBuild.AppendLine("let MultiAssetCount = \"" + multiAssetCount + "\"");
            multiAsset.ForEach(x => {
                var funcLetter = FunctionNameUtility.GetFunctionLetter(counter);
                strMultiAssetBld.AppendLine("function MultiAsset" + funcLetter + "() : string");
                strMultiAssetBld.AppendLine("{");
                //strMultiAssetBld.AppendLine(("var extension = \"" + x.Extension + "\""));
                strMultiAssetBld.AppendLine(("  var fileSize = \"" + x.FileSize.ToString() + "\""));
                //strMultiAssetBld.AppendLine(("var location = \"" + x.Location + "\""));
                strMultiAssetBld.AppendLine(("  var fileName = \"" + x.FileName + "\""));
                strMultiAssetBld.AppendLine(("  var assetAuthorName = \"" + x.AssetAuthorName + "\""));
                strMultiAssetBld.AppendLine("  return (fileName + " + appendChar + " + fileSize + " + appendChar + " + assetAuthorName)");
                strMultiAssetBld.AppendLine("}");

                if(isReading == false)
                {
                    if (!x.Location.Contains("Asset Folder"))
                    {
                        var result = NFTAssetFileUtility.MoveAsset(x.Location, x.FileName, scUID);
                        if (result == false)
                        {
                            //did not copy files
                            failedToSaveAsset = true;
                        }
                    }
                }

                counter += 1;
            });

            if(failedToSaveAsset == true)
            {
                strBuild.Clear();
                strBuild.Append("Failed");
                return (strBuild, strMultiAssetBld);
            }

            return (strBuild, strMultiAssetBld);
        }
    }
}
