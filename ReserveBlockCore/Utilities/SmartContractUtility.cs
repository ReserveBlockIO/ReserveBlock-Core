using System.IO.Compression;

namespace ReserveBlockCore.Utilities
{
    public static class SmartContractUtility
    {
		public static byte[] Compress(byte[] bytes)
		{
			using (var memoryStream = new MemoryStream())
			{
				using (var gzipStream = new GZipStream(memoryStream, CompressionLevel.Optimal))
				{
					gzipStream.Write(bytes, 0, bytes.Length);
				}
				return memoryStream.ToArray();
			}
		}

		public static byte[] Decompress(byte[] bytes)
		{
			using (var memoryStream = new MemoryStream(bytes))
			{

				using (var outputStream = new MemoryStream())
				{
					using (var decompressStream = new GZipStream(memoryStream, CompressionMode.Decompress))
					{
						decompressStream.CopyTo(outputStream);
					}
					return outputStream.ToArray();
				}
			}
		}

		public static IEnumerable<string> Split(string str, int chunkSize)
		{
			return Enumerable.Range(0, str.Length / chunkSize)
				.Select(i => str.Substring(i * chunkSize, chunkSize));
		}

		public static string Unsplit(IEnumerable<string> split)
        {
			var output = "";

			var textUnsplit = "";
			split.ToList().ForEach(x => {
				textUnsplit += x;
			});

			output = textUnsplit;

			return output;
        }
	}
}
