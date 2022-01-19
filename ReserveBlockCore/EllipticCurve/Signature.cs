using System.Numerics;
using System;
using System.Collections.Generic;

namespace ReserveBlockCore.EllipticCurve
{
    public class Signature
    {

        public BigInteger r { get; }
        public BigInteger s { get; }

        public Signature(BigInteger r, BigInteger s)
        {
            this.r = r;
            this.s = s;
        }

        public byte[] toDer()
        {
            List<byte[]> sequence = new List<byte[]> { Der.encodeInteger(r), Der.encodeInteger(s) };
            return Der.encodeSequence(sequence);
        }

        public string toBase64()
        {
            return Base64.encode(toDer());
        }

        public static Signature fromDer(byte[] bytes)
        {
            Tuple<byte[], byte[]> removeSequence = Der.removeSequence(bytes);
            byte[] rs = removeSequence.Item1;
            byte[] removeSequenceTrail = removeSequence.Item2;

            if (removeSequenceTrail.Length > 0)
            {
                throw new ArgumentException("trailing junk after DER signature: " + BinaryAscii.hexFromBinary(removeSequenceTrail));
            }

            Tuple<BigInteger, byte[]> removeInteger = Der.removeInteger(rs);
            BigInteger r = removeInteger.Item1;
            byte[] rest = removeInteger.Item2;

            removeInteger = Der.removeInteger(rest);
            BigInteger s = removeInteger.Item1;
            byte[] removeIntegerTrail = removeInteger.Item2;

            if (removeIntegerTrail.Length > 0)
            {
                throw new ArgumentException("trailing junk after DER numbers: " + BinaryAscii.hexFromBinary(removeIntegerTrail));
            }

            return new Signature(r, s);

        }

        public static Signature fromBase64(string str)
        {
            return fromDer(Base64.decode(str));
        }

    }
}
