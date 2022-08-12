using ReserveBlockCore.Data;

namespace ReserveBlockCore.Utilities
{
    public class BlockVersionUtility
    {
        public static int GetBlockVersion(long height)
        {
            int blockVersion = 1;

            if(height > 80000)
            {
                blockVersion = 2;
            }

            return blockVersion;
        }
    }
}
