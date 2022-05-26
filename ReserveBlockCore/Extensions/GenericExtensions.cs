using System.IO.Compression;

namespace ReserveBlockCore.Extensions
{
    public static class GenericExtensions
    {
        public static IEnumerable<T> ToCircular<T>(this IEnumerable<T> source)
        {
            while (true)
            {
                foreach (var x in source) yield return x;
            }
        }

        public static string ToRawTx(this string source)
        {
            var output = "";

            return output;
        }

        public static string ToBase64(this byte[] buffer)
        {
            var byteToBase64 = Convert.ToBase64String(buffer);

            return byteToBase64;
        }

        public static byte[] FromBase64ToByteArray(this string base64String)
        {
            var byteArrayFromBase64 = Convert.FromBase64String(base64String);

            return byteArrayFromBase64;
        }

        public static byte[] ToCompress(this byte[] bytes)
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

    }
}
