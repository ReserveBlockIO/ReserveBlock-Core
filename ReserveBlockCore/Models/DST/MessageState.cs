namespace ReserveBlockCore.Models.DST
{
    public class MessageState
    {
        public string MessageId { get; set; }
        public Message Message { get; set; }
        public bool ResponseRequested { get; set; }
        public long MessageResponseReceivedTimestamp { get; set; }
        public bool HasReceivedResponse { get; set; }
        public long MessageSentTimestamp { get; set; }
        public bool HasBeenResent { get; set; }
        public bool DidMessageRequestFail { get; set; }

    }
}
