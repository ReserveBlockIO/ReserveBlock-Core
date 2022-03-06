namespace ReserveBlockCore.Utilities
{
    public class BadBlocksUtility
    {
        public static Dictionary<long, string> GetBadBlocks()
        {
            Dictionary<long, string> badBlock = new Dictionary<long, string>();

            badBlock.Add(339, "e64bc942a82c175a83b16ea2cd5905775c75c2ea0bdd87277f094095f806da27");

            return badBlock;

        }
    }
}
