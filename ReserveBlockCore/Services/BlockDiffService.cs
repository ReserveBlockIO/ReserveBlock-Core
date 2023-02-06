namespace ReserveBlockCore.Services
{
    public class BlockDiffService
    {
        public static async Task UpdateQueue(long diff)
        {
            try
            {
                if (Globals.IsChainSynced && !Globals.StopAllTimers)
                {
                    if (Globals.BlockDiffQueue.Count() < 3456)
                    {
                        Globals.BlockDiffQueue.Enqueue((int)diff);
                    }
                    else
                    {
                        Globals.BlockDiffQueue.Enqueue((int)diff);
                        Globals.BlockDiffQueue.TryDequeue(out _);
                    }
                }
            }
            catch { }
        }

        public static double CalculateAverage()
        {
            double avg = 0;
            try
            {
                avg = Globals.BlockDiffQueue.Average();
            }
            catch { }

            return avg;
        }
    }
}
