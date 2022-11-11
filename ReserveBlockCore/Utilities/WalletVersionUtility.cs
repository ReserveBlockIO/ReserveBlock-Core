namespace ReserveBlockCore.Utilities
{
    public class WalletVersionUtility
    {
        public static bool Verify(string walver)
        {
            bool output = false;
            if(walver == null || walver == "")
            {
                return output;
            }
            else
            {
                try
                {
                    var walletVerSplit = walver.Split('.');
                    var major = Convert.ToInt32(walletVerSplit[0]);
                    var minor = Convert.ToInt32(walletVerSplit[1]);

                    if (major < Globals.MajorVer)
                    {
                        return output;
                    }
                    if (!Globals.IsTestNet)
                    {
                        if (minor < 1)
                        {
                            return output;
                        }
                    }

                    output = true;
                }
                catch (Exception ex)
                {
                    //wallet version either mismatched or malformed
                }
            }

            return output;
        }
    }
}
