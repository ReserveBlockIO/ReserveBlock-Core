namespace ReserveBlockCore.Models
{
    public class MessageLock
    {
        public int BufferCost;

        public int ConnectionCount;

        public long LastRequestTime;

        public int DelayLevel;

        public readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);
    }
}
