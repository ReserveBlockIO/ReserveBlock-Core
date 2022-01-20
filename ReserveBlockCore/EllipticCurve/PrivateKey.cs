using System.Numerics;

namespace ReserveBlockCore.EllipticCurve
{
    public class PrivateKey
    {

        public CurveFp curve { get; private set; }
        public BigInteger secret { get; private set; }

        public PrivateKey(string curve = "secp256k1", BigInteger? secret = null)
        {
            this.curve = Curves.getCurveByName(curve);

            if (secret == null)
            {
                secret = Integer.randomBetween(1, this.curve.N - 1);
            }
            this.secret = (BigInteger)secret;
        }

        public PublicKey publicKey()
        {
            Point publicPoint = EcdsaMath.multiply(curve.G, secret, curve.N, curve.A, curve.P);
            return new PublicKey(publicPoint, curve);
        }

        public byte[] toString()
        {
            return BinaryAscii.stringFromNumber(secret, curve.length());
        }

        public byte[] toDer()
        {
            byte[] encodedPublicKey = publicKey().toString(true);

            return Der.encodeSequence(
                new List<byte[]> {
                    Der.encodeInteger(1),
                    Der.encodeOctetString(toString()),
                    Der.encodeConstructed(0, Der.encodeOid(curve.oid)),
                    Der.encodeConstructed(1, encodedPublicKey)
                }
            );
        }

        public string toPem()
        {
            return Der.toPem(toDer(), "EC PRIVATE KEY");
        }

        public static PrivateKey fromPem(string str)
        {
            string[] split = str.Split(new string[] { "-----BEGIN EC PRIVATE KEY-----" }, StringSplitOptions.None);

            if (split.Length != 2)
            {
                throw new ArgumentException("invalid PEM");
            }

            return fromDer(Der.fromPem(split[1]));
        }

        public static PrivateKey fromDer(byte[] der)
        {
            Tuple<byte[], byte[]> removeSequence = Der.removeSequence(der);
            if (removeSequence.Item2.Length > 0)
            {
                throw new ArgumentException("trailing junk after DER private key: " + BinaryAscii.hexFromBinary(removeSequence.Item2));
            }

            Tuple<BigInteger, byte[]> removeInteger = Der.removeInteger(removeSequence.Item1);
            if (removeInteger.Item1 != 1)
            {
                throw new ArgumentException("expected '1' at start of DER private key, got " + removeInteger.Item1.ToString());
            }

            Tuple<byte[], byte[]> removeOctetString = Der.removeOctetString(removeInteger.Item2);
            byte[] privateKeyStr = removeOctetString.Item1;

            Tuple<int, byte[], byte[]> removeConstructed = Der.removeConstructed(removeOctetString.Item2);
            int tag = removeConstructed.Item1;
            byte[] curveOidString = removeConstructed.Item2;
            if (tag != 0)
            {
                throw new ArgumentException("expected tag 0 in DER private key, got " + tag.ToString());
            }

            Tuple<int[], byte[]> removeObject = Der.removeObject(curveOidString);
            int[] oidCurve = removeObject.Item1;
            if (removeObject.Item2.Length > 0)
            {
                throw new ArgumentException(
                    "trailing junk after DER private key curve_oid: " +
                    BinaryAscii.hexFromBinary(removeObject.Item2)
                );
            }

            string stringOid = string.Join(",", oidCurve);

            if (!Curves.curvesByOid.ContainsKey(stringOid))
            {
                int numCurves = Curves.supportedCurves.Length;
                string[] supportedCurves = new string[numCurves];
                for (int i = 0; i < numCurves; i++)
                {
                    supportedCurves[i] = Curves.supportedCurves[i].name;
                }
                throw new ArgumentException(
                    "Unknown curve with oid [" +
                    string.Join(", ", oidCurve) +
                    "]. Only the following are available: " +
                    string.Join(", ", supportedCurves)
                );
            }

            CurveFp curve = Curves.curvesByOid[stringOid];

            if (privateKeyStr.Length < curve.length())
            {
                int length = curve.length() - privateKeyStr.Length;
                string padding = "";
                for (int i = 0; i < length; i++)
                {
                    padding += "00";
                }
                privateKeyStr = Der.combineByteArrays(new List<byte[]> { BinaryAscii.binaryFromHex(padding), privateKeyStr });
            }

            return fromString(privateKeyStr, curve.name);

        }

        public static PrivateKey fromString(byte[] str, string curve = "secp256k1")
        {
            return new PrivateKey(curve, BinaryAscii.numberFromString(str));
        }
    }
}
