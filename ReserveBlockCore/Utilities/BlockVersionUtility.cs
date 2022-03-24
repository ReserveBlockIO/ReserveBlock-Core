using ReserveBlockCore.Data;

namespace ReserveBlockCore.Utilities
{
    public class BlockVersionUtility
    {
        public static int GetBlockVersion(long height)
        {
            int blockVersion = 1;

            if(height > 16000)
            {
                blockVersion = 2;
            }

            if(height > 16534)
            {
                blockVersion = 3;
            }

            if(height > 17296)
            {
                blockVersion = 4;
            }

            return blockVersion;
        }
    }
}
