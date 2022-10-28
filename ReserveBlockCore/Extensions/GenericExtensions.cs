using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security;
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
        public static int ToInt32(this string obj)
        {
            int value;
            if (obj != null && int.TryParse(obj, out value))
                return value;
            else
                return -1000000;
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
        
        public static SecureString ToSecureString(this string source)
        {
            var secureStr = new SecureString();
            if (source.Length > 0)
            {
                foreach (var c in source.ToCharArray()) secureStr.AppendChar(c);
            }
            return secureStr;
        }

        public static bool SecureStringCompare(this SecureString s1, SecureString s2)
        {
            if (s1 == null)
            {
                return false;
            }
            if (s2 == null)
            {
                return false;
            }

            if (s1.Length != s2.Length)
            {
                return false;
            }

            IntPtr ss_bstr1_ptr = IntPtr.Zero;
            IntPtr ss_bstr2_ptr = IntPtr.Zero;

            try
            {
                ss_bstr1_ptr = Marshal.SecureStringToBSTR(s1);
                ss_bstr2_ptr = Marshal.SecureStringToBSTR(s2);

                String str1 = Marshal.PtrToStringBSTR(ss_bstr1_ptr);
                String str2 = Marshal.PtrToStringBSTR(ss_bstr2_ptr);

                return str1.Equals(str2);
            }
            finally
            {
                if (ss_bstr1_ptr != IntPtr.Zero)
                {
                    Marshal.ZeroFreeBSTR(ss_bstr1_ptr);
                }

                if (ss_bstr2_ptr != IntPtr.Zero)
                {
                    Marshal.ZeroFreeBSTR(ss_bstr2_ptr);
                }
            }
        }

        public static string ToUnsecureString(this SecureString source)
        {
            IntPtr unmanagedString = IntPtr.Zero;
            try
            {
                unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(source);
                return Marshal.PtrToStringUni(unmanagedString);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
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
            byte[] key = GetKey(source);
            byte[] iv = new byte[16];
            byte[] array;

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (CryptoStream cryptoStream = new CryptoStream((Stream)memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter streamWriter = new StreamWriter((Stream)cryptoStream))
                        {
                            streamWriter.Write(source);
                        }

                        array = memoryStream.ToArray();
                    }
                }
            }

            return Convert.ToBase64String(array);
        }
        public static string ToDecrypt(this string cipherText, string passPhrase)
        {
            try
            {
                byte[] key = GetKey(passPhrase);
                byte[] iv = new byte[16];
                byte[] buffer = Convert.FromBase64String(cipherText);

                using (Aes aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;
                    ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                    using (MemoryStream memoryStream = new MemoryStream(buffer))
                    {
                        using (CryptoStream cryptoStream = new CryptoStream((Stream)memoryStream, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader streamReader = new StreamReader((Stream)cryptoStream))
                            {
                                return streamReader.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                return "Fail";
            }
        }
        private static byte[] GetKey(string password)
        {
            var keyBytes = Encoding.UTF8.GetBytes(password);
            using (var md5 = MD5.Create())
            {
                return md5.ComputeHash(keyBytes);
            }
        }
    }
}
