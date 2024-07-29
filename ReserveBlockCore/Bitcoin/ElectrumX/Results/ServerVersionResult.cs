namespace ReserveBlockCore.Bitcoin.ElectrumX.Results
{
    public class ServerVersionResult
    {
        /// <summary>
        /// A string identifying the connecting server software.
        /// </summary>
        public string ClientName { get; set; }
        /// <summary>
        /// The protocol version that will be used for future communication.
        /// </summary>
        public Version ProtocolVersion { get; set; }
    }
}
