using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;

namespace ReserveBlockCore.Utilities
{
    public class BlockVersionUtility
    {
        public static int GetBlockVersion(long height)
        {
            int blockVersion = 1;

            if(height > Globals.BlockLock)
            {
                blockVersion = 2;
            }

            return blockVersion;
        }

        public static async Task<bool> Version2Rules(Block block)
        {
            //1.
            //Verify Adj Signature
            bool result = false;
            var leadAdj = Globals.LeadAdjudicator;
            var leadAdjAddr = leadAdj.Address;

            if(block.AdjudicatorSignature != null)
            {
                var sigResult = SignatureService.VerifySignature(leadAdjAddr, block.Hash, block.AdjudicatorSignature);
                result = sigResult;
            }

            return result;
            //////////////////////
        }
    }
}
