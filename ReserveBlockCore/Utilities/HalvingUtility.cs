using ReserveBlockCore.Data;

namespace ReserveBlockCore.Utilities
{
    public static class HalvingUtility
    {
        //Returns next 10 halvings
        public static void GetHalvingSchedule()
        {
            decimal blockReward = 32.00M;
            int currentBlockHeight = 4730400 * 10;
            int blockHalvingInterval = 4730400;
            int halving = currentBlockHeight / blockHalvingInterval;
            decimal CoinsMined = 67500000.00M; //coins in existence at block 0

            var n = 1;
            var count = halving;
            while (n <= halving)
            {
                CoinsMined += (blockReward * blockHalvingInterval);
                var coinMineTotal = CoinsMined.ToString("N0");
                var percentMined = (CoinsMined / 372000000.00M) * 100;
                blockReward /= 2;
                Console.WriteLine("================================================================");
                Console.WriteLine("Halving Number " + n.ToString() + ") New Block Reward: " + blockReward + " at year " + (3 * n).ToString());
                Console.WriteLine("Coins mined at beginning of halving: " + coinMineTotal);
                Console.WriteLine("Total percent of coins mined: " + percentMined.ToString("#.##") + "%");
                n++;
            }

        }

        public static decimal GetBlockReward()
        {
            decimal blockReward = 32.00M;
            int currentBlockHeight = Program.BlockHeight != -1 ? (int)Program.BlockHeight : 1;
            int blockHalvingInterval = 4730400; // Roughly every 3 year halving

            //An int will always result in a rounded down whole number. 4730399 / 4730400   = 0 | 9,460,799 / 4730400  = 1
            int halving = currentBlockHeight / blockHalvingInterval;

            var n = 1;
            while (n <= halving)
            {
                blockReward /= 2;
                n++;
            }

            return blockReward;
        }

    }
}
