using NBitcoin;

namespace ReserveBlockCore.Bitcoin.ElectrumX
{
    public class ClientSettings
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public bool UseSsl { get; set; }
        public ulong Count { get; set; }
        public ulong FailCount { get; set; }
    }
}
