using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.Utilities;
using System.Text;

namespace ReserveBlockCore.SmartContractSourceGenerator
{
    public class MultiAssetSourceGenerator
    {
        public static async Task<(StringBuilder, StringBuilder)> Build(List<MultiAssetFeature> multiAsset, StringBuilder strBuild)
        {
            var appendChar = "\"|->\"";
            StringBuilder strMultiAssetBld = new StringBuilder();

            int counter = 1;
            var multiAssetCount = multiAsset.Count().ToString();
            strBuild.AppendLine("let MultiAssetCount = \"" + multiAssetCount + "\"");
            multiAsset.ForEach(x => {
                var funcLetter = FunctionNameUtility.GetFunctionLetter(counter);
                strMultiAssetBld.AppendLine("function MultiAsset" + funcLetter + "() : string");
                strMultiAssetBld.AppendLine("{");
                strMultiAssetBld.AppendLine(("var extension = \"" + x.Extension + "\""));
                strMultiAssetBld.AppendLine(("var fileSize = \"" + x.FileSize.ToString() + "\""));
                strMultiAssetBld.AppendLine(("var location = \"" + x.Location + "\""));
                strMultiAssetBld.AppendLine(("var fileName = \"" + x.FileName + "\""));
                strMultiAssetBld.AppendLine(("var assetAuthorName = \"" + x.AssetAuthorName + "\""));
                strMultiAssetBld.AppendLine("return (fileName + " + appendChar + " + location + " + appendChar + " + fileSize + " + appendChar + " + extension + " + appendChar + " + assetAuthorName)");
                strMultiAssetBld.AppendLine("}");

                counter += 1;
            });

            return (strBuild, strMultiAssetBld);
        }
    }
}
