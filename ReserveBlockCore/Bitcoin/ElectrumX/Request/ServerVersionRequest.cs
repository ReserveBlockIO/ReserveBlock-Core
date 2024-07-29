namespace ReserveBlockCore.Bitcoin.ElectrumX.Request
{
    internal class ServerVersionRequest : RequestBase<string>
    {
        internal ServerVersionRequest()
        {
            Method = "server.version";
            Parameters = new string[] { };
        }
    }
}
