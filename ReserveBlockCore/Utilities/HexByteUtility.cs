using System.Globalization;

namespace ReserveBlockCore.Utilities
{
    public class HexByteUtility
    {
		public static string ByteToHex(byte[] pubkey)
		{
			return Convert.ToHexString(pubkey).ToLower();
		}
		public static byte[] HexToByte(string HexString)
		{
			if (HexString.Length % 2 != 0)
				throw new Exception("Invalid HEX");
			byte[] retArray = new byte[HexString.Length / 2];
			for (int i = 0; i < retArray.Length; ++i)
			{
				retArray[i] = byte.Parse(HexString.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
			}
			return retArray;
		}
	}
}
