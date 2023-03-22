using ReserveBlockCore.DST;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Models.DST
{
    public class Message
    {
        public string Id { get; set; }
        public MessageType Type { get; set; }
        public string Data { get; set; } //may want to make this an object
        public string? Address { get; set; } = null;
        public string IPAddress { get; set; }
        public long SentTimestamp { get; set; }
        public int Port { get; set; }
        public long? ReceivedTimestamp { get; set; } = null;
        public string Hash { get; set; }
       
        public void Build()
        {
            Id = RandomStringUtility.GetRandomString(8);
            SentTimestamp = TimeUtil.GetTime();
            IPAddress = Type == MessageType.KeepAlive ? "NA" : P2PClient.MostLikelyIP();
            Port = Globals.DSTClientPort;

            if(Type == MessageType.KeepAlive)
            {
                Data = "0";
                Hash = "0";
            }
            else
            {
                Hash = (Data + SentTimestamp.ToString() + IPAddress).ToHash();
            }
        }

        public void Rebuild()
        {
            if (Type == MessageType.KeepAlive)
            {
                Data = "0";
                Hash = "0";
            }
            else
            {
                Hash = (Data + SentTimestamp.ToString() + IPAddress).ToHash();
            }
        }

        public class ChatMessage
        {
            public string Message { get; set; }
            public string ToAddress { get; set; }
            public string URL { get; set; } 
        }
        public class ActionItemMessage
        {

        }
        public class DecShopRequestMessage
        {
            public string ToAddress { get; set; }
            public string URL { get; set; }
        }
        public class BidMessage
        {

        }
        public class PurchaseMessage
        {

        }
        public class ClaimsMessage
        {
            public string OwnerAddress { get; set; }
            public string URL { get; set; }
            public string Signature { get; set; }
            public string SignatureMessage { get; set; }
            public long Timestamp { get; set; }

            public void Build()
            { 
                //perform build and signature
                Timestamp = TimeUtil.GetTime();
            }
        }

        public class NotificationMessage
        {
            public string Message { get; set; }
            public string Title { get; set; }
            public string ToAddress { get; set; }
        }

        public class Ack
        {
            public string Id { get; set; }
            public bool Response { get; set; }
        }
    }

    public enum MessageComType
    {
        Sender,
        Receiver
    }

    public enum MessageType
    {
        KeepAlive,
        Chat,
        ActionItem,
        DecShopRequest,
        Bid,
        Purchase,
        Notification,
        Typing,
        Claims,
        Rejected,
        Ack,
        ShopConnect,
        STUNConnect,
        STUNKeepAlive
    }

}
