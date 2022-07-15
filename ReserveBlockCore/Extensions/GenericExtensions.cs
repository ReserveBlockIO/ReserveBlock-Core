using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

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

        public static string ToMD5(this string filePath)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
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

        public static string ToBase64(this string source)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(source);
            var stringToBase64 = Convert.ToBase64String(plainTextBytes);

            return stringToBase64;
        }

        public static string ToStringFromBase64(this string source)
        {
            var base64EncodedString = Convert.FromBase64String(source);
            var stringFromBase64 = Encoding.UTF8.GetString(base64EncodedString);

            return stringFromBase64;
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

        public static TValue TryGet<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
        {
            TValue value;
            dictionary.TryGetValue(key, out value);
            return value;
        }

        public static string ToStringHex(this byte[] bytes)
        {
            var hex = BitConverter
                .ToString(bytes)
                .Replace("-", "")
                .ToLower();

            return hex;
        }

        public static T[] Slice<T>(this T[] source, int start, int end)
        {
            if (end < 0)
                end = source.Length;

            var len = end - start;

            // Return new array.
            var res = new T[len];
            for (var i = 0; i < len; i++) res[i] = source[i + start];
            return res;
        }

        public static T[] Slice<T>(this T[] source, int start)
        {
            return Slice<T>(source, start, -1);
        }

        public static byte[] HexToByteArray(this string hex)
        {
            var bytes = Enumerable.Range(0, hex.Length / 2)
                    .Select(x => Convert.ToByte(hex.Substring(x * 2, 2), 16))
                    .ToArray();

            return bytes;
        }

        public static byte[] PaddedByteArray(byte[] bytes, int length)
        {
            var finalBytes = new byte[length];
            Fill(finalBytes, (byte)0);
            Array.Copy(bytes, 0, finalBytes, 0, bytes.Length);

            return finalBytes;
        }

        public static void Fill<T>(this T[] arr, T value)
        {
            for (var i = 0; i < arr.Length; i++)
                arr[i] = value;
        }

        public enum SizeUnits
        {
            Byte, KB, MB, GB, TB, PB, EB, ZB, YB
        }

        public static decimal ToSize(this Int64 value, SizeUnits unit)
        {
            return decimal.Round((value / (decimal)Math.Pow(1024, (long)unit)), 2);
        }

        public static string ToEncrypt(this string source)
        {
            // Salt and IV is randomly generated each time, but is preprended to encrypted cipher text
            // so that the same Salt and IV values can be used when decrypting.  
            var saltStringBytes = Generate256BitsOfRandomEntropy();
            var ivStringBytes = Generate256BitsOfRandomEntropy();
            var plainTextBytes = Encoding.UTF8.GetBytes(source);

            using (var password = new Rfc2898DeriveBytes(source, saltStringBytes, DerivationIterations))
            {
                var keyBytes = password.GetBytes(Keysize / 8);
                using (var symmetricKey = Aes.Create())
                {
                    symmetricKey.BlockSize = Keysize;
                    symmetricKey.Mode = CipherMode.CBC;
                    symmetricKey.Padding = PaddingMode.Zeros;
                    using (var key = symmetricKey.CreateEncryptor(keyBytes, ivStringBytes))
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            using (var cryptoStream = new CryptoStream(memoryStream, key, CryptoStreamMode.Write))
                            {
                                cryptoStream.Write(plainTextBytes);
                                cryptoStream.FlushFinalBlock();
                                var cryptTextBytes = saltStringBytes;
                                cryptTextBytes = cryptTextBytes.Concat(ivStringBytes).ToArray();
                                cryptTextBytes = cryptTextBytes.Concat(memoryStream.ToArray()).ToArray();
                                memoryStream.Close();
                                cryptoStream.Close();
                                return Convert.ToBase64String(cryptTextBytes);
                            }
                        }
                    }
                }
            }
        }

        public static string ToDecrypt(this string cipherText, string passPhrase)
        {
            try
            {
                // Get the complete stream of bytes that represent:
                // [32 bytes of Salt] + [32 bytes of IV] + [n bytes of CipherText]
                var cipherTextBytesWithSaltAndIv = Convert.FromBase64String(cipherText);
                // Get the saltbytes by extracting the first 32 bytes from the supplied cipherText bytes.
                var saltStringBytes = cipherTextBytesWithSaltAndIv.Take(Keysize / 8).ToArray();
                // Get the IV bytes by extracting the next 32 bytes from the supplied cipherText bytes.
                var ivStringBytes = cipherTextBytesWithSaltAndIv.Skip(Keysize / 8).Take(Keysize / 8).ToArray();
                // Get the actual cipher text bytes by removing the first 64 bytes from the cipherText string.
                var cipherTextBytes = cipherTextBytesWithSaltAndIv.Skip((Keysize / 8) * 2).Take(cipherTextBytesWithSaltAndIv.Length - ((Keysize / 8) * 2)).ToArray();

                using (var password = new Rfc2898DeriveBytes(passPhrase, saltStringBytes, DerivationIterations))
                {
                    var keyBytes = password.GetBytes(Keysize / 8);
                    using (var symmetricKey = Aes.Create())
                    {
                        symmetricKey.BlockSize = Keysize;
                        symmetricKey.Mode = CipherMode.CBC;
                        symmetricKey.Padding = PaddingMode.Zeros;
                        using (var key = symmetricKey.CreateDecryptor(keyBytes, ivStringBytes))
                        {
                            using (var memoryStream = new MemoryStream(cipherTextBytes))
                            {
                                using (var cryptoStream = new CryptoStream(memoryStream, key, CryptoStreamMode.Read))
                                {
                                    var plainTextBytes = new byte[cipherTextBytes.Length];

                                    cryptoStream.Read(plainTextBytes);

                                    memoryStream.Close();
                                    cryptoStream.Close();
                                    return Encoding.UTF8.GetString(plainTextBytes).TrimEnd('\0');
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                return "Fail";
            }
            
        }

        private const int Keysize = 128;

        // This constant determines the number of iterations for the password bytes generation function.
        private const int DerivationIterations = 1000;
        private static byte[] Generate256BitsOfRandomEntropy()
        {
            var randomBytes = new byte[Keysize / 8];
            using (var rngCsp = RandomNumberGenerator.Create())
            {
                rngCsp.GetBytes(randomBytes);
            }
            return randomBytes;
        }
    }
}
