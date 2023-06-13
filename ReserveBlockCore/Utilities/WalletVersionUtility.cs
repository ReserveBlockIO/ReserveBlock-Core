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

                    //if (major < Globals.MajorVer && Globals.LastBlock.Height >= Globals.BlockLock)
                    if (major < Globals.MajorVer)
                    {
                        return output;
                    }

                    //removing this as minor releases should never be breaking. If they are then we need to increase the majorver and blocklock
                    //if (!Globals.IsTestNet)
                    //{
                    //    if (minor < 0)
                    //    {
                    //        return output;
                    //    }
                    //}

                    output = true;
                }
                catch (Exception ex)
                {
                    //wallet version either mismatched or malformed
                }
            }

            return output;
        }

        public static int GetBuildVersion()
        {
            DateTime originDate = new DateTime(2022, 1, 1);
            DateTime currentDate = DateTime.Now;

            var dateDiff = (int)Math.Round((currentDate - originDate).TotalDays);

            return dateDiff;
        }
    }
}
