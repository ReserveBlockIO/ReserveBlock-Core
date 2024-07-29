namespace ReserveBlockCore.Bitcoin.ElectrumX
{
    internal class IdCounter
    {
        private static IdCounter _instance;
        private static int _counter;
        private static readonly object SyncRoot = new object();
        static IdCounter()
        {
            _counter = 0;
        }
        internal static int UseId()
        {
            lock (SyncRoot)
            {
                if (_instance == null)
                    _instance = new IdCounter();
                _counter++;
                return _counter;
            }
        }
    }
}
