using Trillium.CodeAnalysis;
using Trillium.IO;
using Trillium.ReadConstants;
using Trillium.Symbols;
using Trillium.Syntax;

namespace ReserveBlockCore.Trillium
{
    public static class TrilliumRT
    {
        public static Dictionary<string, string[]>? ReadSmartContract()
        {
            //This is the Runtime that will process smart contract and perform self-executable task.
            var npath = Directory.GetCurrentDirectory() + @"\samples\hello\";

            List<string> pathList = new List<string>();
            pathList.Add(npath);

            var paths = GetFilePaths(pathList);
            var syntaxTrees = new List<SyntaxTree>();
            var hasErrors = false;

            foreach (var path in paths)
            {
                if (!File.Exists(path))
                {
                    Console.Error.WriteLine($"error: file '{path}' doesn't exist");
                    hasErrors = true;
                    continue;
                }
                var syntaxTree = SyntaxTree.Load(path);
                syntaxTrees.Add(syntaxTree);
            }

            var compilation = Compilation.Create(syntaxTrees.ToArray());
            var result = compilation.Evaluate(new Dictionary<VariableSymbol, object>());

            if (!result.Diagnostics.Any())
            {
                if (result.Value != null)
                    Console.WriteLine(result.Value);
            }
            else
            {
                Console.Error.WriteDiagnostics(result.Diagnostics);
                //log errors
            }

            //log errors

            var data = ReadConstants.SCReadData;
            if(data.ContainsKey("NftData"))
            {
                return data;
            }
            else
            {
                return null;
            }
            
        }

        private static IEnumerable<string> GetFilePaths(IEnumerable<string> paths)
        {
            var result = new SortedSet<string>();

            foreach (var path in paths)
            {
                if (Directory.Exists(path))
                {
                    result.UnionWith(Directory.EnumerateFiles(path, "*.trlm", SearchOption.AllDirectories));
                }
                else
                {
                    result.Add(path);
                }
            }

            return result;
        }
    }
}
