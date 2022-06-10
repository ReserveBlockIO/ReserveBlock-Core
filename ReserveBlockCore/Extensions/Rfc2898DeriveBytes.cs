using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace ReserveBlockCore.Extensions
{
    namespace System.Security.Cryptography
    {
        /// <summary>
        /// This is a copy of Rfc28989DeiveBytes from corefx, since .NET Standard 2.0 leaves out the version
        /// where you can specify the HMAC to use I had to copy this version in and use it instead.
        /// </summary>
        public class Rfc2898DeriveBytesExtended : DeriveBytes
        {
            private const int MinimumSaltSize = 8;

            private readonly byte[] _password;
            private byte[] _salt;
            private uint _iterations;
            private HMAC _hmac;
            private int _blockSize;

            private byte[] _buffer;
            private uint _block;
            private int _startIndex;
            private int _endIndex;

            public HashAlgorithmName HashAlgorithm { get; }

            public Rfc2898DeriveBytesExtended(byte[] password, byte[] salt, int iterations)
                : this(password, salt, iterations, HashAlgorithmName.SHA1)
            {
            }

            public Rfc2898DeriveBytesExtended(byte[] password, byte[] salt, int iterations, HashAlgorithmName hashAlgorithm)
            {
                if (salt == null)
                    throw new ArgumentNullException(nameof(salt));
                if (salt.Length < MinimumSaltSize)
                    throw new ArgumentException("Salt is not at least eight bytes.", nameof(salt));
                if (iterations <= 0)
                    throw new ArgumentOutOfRangeException(nameof(iterations), "Positive number required.");
                if (password == null)
                    throw new NullReferenceException();  // This "should" be ArgumentNullException but for compat, we throw NullReferenceException.

                _salt = salt.CloneByteArray();
                _iterations = (uint)iterations;
                _password = password.CloneByteArray();
                HashAlgorithm = hashAlgorithm;
                _hmac = OpenHmac();
                // _blockSize is in bytes, HashSize is in bits.
                _blockSize = _hmac.HashSize >> 3;

                Initialize();
            }

            public Rfc2898DeriveBytesExtended(string password, byte[] salt)
                 : this(password, salt, 1000)
            {
            }

            public Rfc2898DeriveBytesExtended(string password, byte[] salt, int iterations)
                : this(password, salt, iterations, HashAlgorithmName.SHA1)
            {
            }

            public Rfc2898DeriveBytesExtended(string password, byte[] salt, int iterations, HashAlgorithmName hashAlgorithm)
                : this(Encoding.UTF8.GetBytes(password), salt, iterations, hashAlgorithm)
            {
            }

            public Rfc2898DeriveBytesExtended(string password, int saltSize)
                : this(password, saltSize, 1000)
            {
            }

            public Rfc2898DeriveBytesExtended(string password, int saltSize, int iterations)
                : this(password, saltSize, iterations, HashAlgorithmName.SHA1)
            {
            }

            public Rfc2898DeriveBytesExtended(string password, int saltSize, int iterations, HashAlgorithmName hashAlgorithm)
            {
                if (saltSize < 0)
                    throw new ArgumentOutOfRangeException(nameof(saltSize), "Non-negative number required.");
                if (saltSize < MinimumSaltSize)
                    throw new ArgumentException("Salt is not at least eight bytes.", nameof(saltSize));
                if (iterations <= 0)
                    throw new ArgumentOutOfRangeException(nameof(iterations), "Positive number required.");

                _salt = Helpers.GenerateRandom(saltSize);
                _iterations = (uint)iterations;
                _password = Encoding.UTF8.GetBytes(password);
                HashAlgorithm = hashAlgorithm;
                _hmac = OpenHmac();
                // _blockSize is in bytes, HashSize is in bits.
                _blockSize = _hmac.HashSize >> 3;

                Initialize();
            }

            public int IterationCount
            {
                get
                {
                    return (int)_iterations;
                }

                set
                {
                    if (value <= 0)
                        throw new ArgumentOutOfRangeException(nameof(value), "Positive number required.");
                    _iterations = (uint)value;
                    Initialize();
                }
            }

            public byte[] Salt
            {
                get
                {
                    return _salt.CloneByteArray();
                }

                set
                {
                    if (value == null)
                        throw new ArgumentNullException(nameof(value));
                    if (value.Length < MinimumSaltSize)
                        throw new ArgumentException("Salt is not at least eight bytes.");
                    _salt = value.CloneByteArray();
                    Initialize();
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    if (_hmac != null)
                    {
                        _hmac.Dispose();
                        _hmac = null;
                    }

                    if (_buffer != null)
                        Array.Clear(_buffer, 0, _buffer.Length);
                    if (_password != null)
                        Array.Clear(_password, 0, _password.Length);
                    if (_salt != null)
                        Array.Clear(_salt, 0, _salt.Length);
                }
                base.Dispose(disposing);
            }

            public override byte[] GetBytes(int cb)
            {
                Debug.Assert(_blockSize > 0);

                if (cb <= 0)
                    throw new ArgumentOutOfRangeException(nameof(cb), "Positive number required.");
                byte[] password = new byte[cb];

                int offset = 0;
                int size = _endIndex - _startIndex;
                if (size > 0)
                {
                    if (cb >= size)
                    {
                        Buffer.BlockCopy(_buffer, _startIndex, password, 0, size);
                        _startIndex = _endIndex = 0;
                        offset += size;
                    }
                    else
                    {
                        Buffer.BlockCopy(_buffer, _startIndex, password, 0, cb);
                        _startIndex += cb;
                        return password;
                    }
                }

                Debug.Assert(_startIndex == 0 && _endIndex == 0, "Invalid start or end index in the internal buffer.");

                while (offset < cb)
                {
                    byte[] T_block = Func();
                    int remainder = cb - offset;
                    if (remainder > _blockSize)
                    {
                        Buffer.BlockCopy(T_block, 0, password, offset, _blockSize);
                        offset += _blockSize;
                    }
                    else
                    {
                        Buffer.BlockCopy(T_block, 0, password, offset, remainder);
                        offset += remainder;
                        Buffer.BlockCopy(T_block, remainder, _buffer, _startIndex, _blockSize - remainder);
                        _endIndex += (_blockSize - remainder);
                        return password;
                    }
                }
                return password;
            }

            public byte[] CryptDeriveKey(string algname, string alghashname, int keySize, byte[] rgbIV)
            {
                // If this were to be implemented here, CAPI would need to be used (not CNG) because of
                // unfortunate differences between the two. Using CNG would break compatibility. Since this
                // assembly currently doesn't use CAPI it would require non-trivial additions.
                // In addition, if implemented here, only Windows would be supported as it is intended as
                // a thin wrapper over the corresponding native API.
                // Note that this method is implemented in PasswordDeriveBytes (in the Csp assembly) using CAPI.
                throw new PlatformNotSupportedException();
            }

            public override void Reset()
            {
                Initialize();
            }

            private HMAC OpenHmac()
            {
                Debug.Assert(_password != null);

                HashAlgorithmName hashAlgorithm = HashAlgorithm;

                if (string.IsNullOrEmpty(hashAlgorithm.Name))
                    throw new CryptographicException("The hash algorithm name cannot be null or empty.");

                if (hashAlgorithm == HashAlgorithmName.SHA1)
                    return new HMACSHA1(_password);
                if (hashAlgorithm == HashAlgorithmName.SHA256)
                    return new HMACSHA256(_password);
                if (hashAlgorithm == HashAlgorithmName.SHA384)
                    return new HMACSHA384(_password);
                if (hashAlgorithm == HashAlgorithmName.SHA512)
                    return new HMACSHA512(_password);

                throw new CryptographicException($"{hashAlgorithm.Name} is not a known hash algorithm.");
            }

            private void Initialize()
            {
                if (_buffer != null)
                    Array.Clear(_buffer, 0, _buffer.Length);
                _buffer = new byte[_blockSize];
                _block = 1;
                _startIndex = _endIndex = 0;
            }

            // This function is defined as follows:
            // Func (S, i) = HMAC(S || i) ^ HMAC2(S || i) ^ ... ^ HMAC(iterations) (S || i) 
            // where i is the block number.
            private byte[] Func()
            {
                byte[] temp = new byte[_salt.Length + sizeof(uint)];
                Buffer.BlockCopy(_salt, 0, temp, 0, _salt.Length);
                Helpers.WriteInt(_block, temp, _salt.Length);

                temp = _hmac.ComputeHash(temp);

                byte[] ret = temp;
                for (int i = 2; i <= _iterations; i++)
                {
                    temp = _hmac.ComputeHash(temp);

                    for (int j = 0; j < _blockSize; j++)
                    {
                        ret[j] ^= temp[j];
                    }
                }

                // increment the block count.
                _block++;
                return ret;
            }


        }

        internal static class Helpers
        {
            public static byte[] CloneByteArray(this byte[] src)
            {
                if (src == null)
                {
                    return null;
                }

                return (byte[])(src.Clone());
            }

            public static KeySizes[] CloneKeySizesArray(this KeySizes[] src)
            {
                return (KeySizes[])(src.Clone());
            }

            public static bool UsesIv(this CipherMode cipherMode)
            {
                return cipherMode != CipherMode.ECB;
            }

            public static byte[] GetCipherIv(this CipherMode cipherMode, byte[] iv)
            {
                if (cipherMode.UsesIv())
                {
                    if (iv == null)
                    {
                        throw new CryptographicException("The cipher mode specified requires that an initialization vector (IV) be used.");
                    }

                    return iv;
                }

                return null;
            }

            public static bool IsLegalSize(this int size, KeySizes[] legalSizes)
            {
                for (int i = 0; i < legalSizes.Length; i++)
                {
                    KeySizes currentSizes = legalSizes[i];

                    // If a cipher has only one valid key size, MinSize == MaxSize and SkipSize will be 0
                    if (currentSizes.SkipSize == 0)
                    {
                        if (currentSizes.MinSize == size)
                            return true;
                    }
                    else if (size >= currentSizes.MinSize && size <= currentSizes.MaxSize)
                    {
                        // If the number is in range, check to see if it's a legal increment above MinSize
                        int delta = size - currentSizes.MinSize;

                        // While it would be unusual to see KeySizes { 10, 20, 5 } and { 11, 14, 1 }, it could happen.
                        // So don't return false just because this one doesn't match.
                        if (delta % currentSizes.SkipSize == 0)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            public static byte[] GenerateRandom(int count)
            {
                byte[] buffer = new byte[count];
                using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(buffer);
                }

                return buffer;
            }

            // encodes the integer i into a 4-byte array, in big endian.
            public static void WriteInt(uint i, byte[] arr, int offset)
            {
                unchecked
                {
                    Debug.Assert(arr != null);
                    Debug.Assert(arr.Length >= offset + sizeof(uint));

                    arr[offset] = (byte)(i >> 24);
                    arr[offset + 1] = (byte)(i >> 16);
                    arr[offset + 2] = (byte)(i >> 8);
                    arr[offset + 3] = (byte)i;
                }
            }

            public static byte[] FixupKeyParity(this byte[] key)
            {
                byte[] oddParityKey = new byte[key.Length];
                for (int index = 0; index < key.Length; index++)
                {
                    // Get the bits we are interested in
                    oddParityKey[index] = (byte)(key[index] & 0xfe);

                    // Get the parity of the sum of the previous bits
                    byte tmp1 = (byte)((oddParityKey[index] & 0xF) ^ (oddParityKey[index] >> 4));
                    byte tmp2 = (byte)((tmp1 & 0x3) ^ (tmp1 >> 2));
                    byte sumBitsMod2 = (byte)((tmp2 & 0x1) ^ (tmp2 >> 1));

                    // We need to set the last bit in oddParityKey[index] to the negation
                    // of the last bit in sumBitsMod2
                    if (sumBitsMod2 == 0)
                        oddParityKey[index] |= 1;
                }

                return oddParityKey;
            }

            internal static void ConvertIntToByteArray(uint value, byte[] dest)
            {
                Debug.Assert(dest != null);
                Debug.Assert(dest.Length == 4);
                dest[0] = (byte)((value & 0xFF000000) >> 24);
                dest[1] = (byte)((value & 0xFF0000) >> 16);
                dest[2] = (byte)((value & 0xFF00) >> 8);
                dest[3] = (byte)(value & 0xFF);
            }
        }
    }
}
