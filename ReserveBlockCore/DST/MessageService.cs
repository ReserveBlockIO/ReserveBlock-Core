using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.DST;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Utilities;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;

namespace ReserveBlockCore.DST
{
    public class MessageService
    {
        public static async Task ProcessMessage(Message message, IPEndPoint endPoint, UdpClient udpClient)
        {
            switch(message.Type)
            {
                case MessageType.STUN:
                    STUNClientConnect(message, endPoint, udpClient);
                    break;
                case MessageType.PunchClient:
                    PunchClient(message, endPoint, udpClient);
                    break;
                case MessageType.KeepAlive:
                    KeepAlive(message, endPoint, udpClient);
                    break;
                case MessageType.STUNKeepAlive:
                    STUNKeepAlive(message, endPoint, udpClient);
                    break;
                case MessageType.ShopConnect:
                    ShopConnect(message, endPoint, udpClient);
                    break;
                case MessageType.ShopKeepAlive:
                    ShopKeepAlive(message, endPoint, udpClient);
                    break;
                case MessageType.STUNConnect:
                    STUNConnect(message, endPoint, udpClient);
                    break;
                case MessageType.DecShop :
                    DecShopMessage(message, endPoint, udpClient);
                    break;
                case MessageType.Chat:
                    ChatMessage(message, endPoint, udpClient);
                    break;
                case MessageType.ChatRec:
                    ChatMessageReceived(message);
                    break;

                default:
                    break;
                    
            }
        }
        public static void STUNClientConnect(Message message, IPEndPoint endPoint, UdpClient udpClient)
        {
            if (!string.IsNullOrEmpty(message.Data))
            {
                //tell SHOP TO RESPOND if connected to this server.
                //respond with some kind of fail or success
                var ipEndPoint = message.Data.Split(':');
                var ip = ipEndPoint[0];
                var port = ipEndPoint[1];

                if(Globals.ConnectedShops.TryGetValue(message.Data, out var shop))
                {
                    if(shop.IsConnected)
                    {
                        //create ack packet to send
                        var successMessage = Encoding.UTF8.GetBytes("ack");

                        var dataString = $"{endPoint.Address}:{endPoint.Port}";

                        //create message for to send to shop to initiate the punch
                        var remoteMessage = new Message { 
                            Type = MessageType.PunchClient,
                            Data = dataString
                        };
                        var remoteDataPayload = GenerateMessage(remoteMessage, false);
                        var remoteClientPunchmessage = Encoding.UTF8.GetBytes(remoteDataPayload);

                        //parse the clients sent shop IP:Port into an IPEndpoint to send UDP packet
                        var remoteEndPoint = IPEndPoint.Parse(message.Data);

                        //send to shop to inform them to initiate the punch
                        udpClient.Send(remoteClientPunchmessage, remoteEndPoint);

                        //let origin endpoint know they can finish the udp punch through
                        udpClient.Send(successMessage, endPoint);
                    }
                    
                }
                else
                {
                    //shop not connected to this STUN server. find another.
                    var notFoundMessage = Encoding.UTF8.GetBytes("nack");
                    udpClient.Send(notFoundMessage, endPoint);
                }

                var failMessage = Encoding.UTF8.GetBytes("fail");
                udpClient.Send(failMessage, endPoint);
            }
        }

        public static void PunchClient(Message message, IPEndPoint endPoint, UdpClient udpClient)
        {
            var punchMessage = Encoding.UTF8.GetBytes("ack");
            var remoteEndPoint = IPEndPoint.Parse(message.Data);
            udpClient.Send(punchMessage, remoteEndPoint);
        }

        public static void STUNConnect(Message message, IPEndPoint endPoint, UdpClient udpClient)
        {
            if (message.Data == "helo")
            {
                Globals.ConnectedShops.TryGetValue(message.IPAddress, out var shop);
                if (shop != null)
                {
                    shop.LastReceiveMessage = TimeUtil.GetTime();
                    shop.IsConnected = false;
                    Globals.ConnectedShops[endPoint.ToString()] = shop;
                }
                else
                {
                    shop = new DSTConnection
                    {
                        LastReceiveMessage = TimeUtil.GetTime(),
                        ConnectDate = TimeUtil.GetTime(),
                        IPAddress = endPoint.ToString(),
                        InitialMessage = message,
                    };
                }

                Globals.ConnectedShops[endPoint.ToString()] = shop;
                var successMessage = Encoding.UTF8.GetBytes("echo");
                udpClient.Send(successMessage, endPoint);
            }
        }
        public static void ShopConnect(Message message, IPEndPoint endPoint, UdpClient udpClient)
        {
            if(message.Data == "helo")
            {
                Globals.ConnectedClients.TryGetValue(message.IPAddress, out var client);
                if(client != null)
                {
                    client.LastReceiveMessage = TimeUtil.GetTime();
                    client.IsConnected = false;
                    Globals.ConnectedClients[endPoint.ToString()] = client;
                }
                else
                {
                    client = new DSTConnection
                    {
                        LastReceiveMessage = TimeUtil.GetTime(),
                        ConnectDate = TimeUtil.GetTime(),
                        IPAddress = endPoint.ToString(),
                        InitialMessage = message,
                    };
                }

                Globals.ConnectedClients[endPoint.ToString()] = client;
                var successMessage = Encoding.UTF8.GetBytes("echo");
                udpClient.Send(successMessage, endPoint);
            }
        }

        public static void KeepAlive(Message message, IPEndPoint endPoint, UdpClient udpClient)
        {
            if (message.Type == MessageType.KeepAlive)
            {
                message.ReceivedTimestamp = TimeUtil.GetTime();

                if (Globals.ConnectedClients.TryGetValue(endPoint.ToString(), out var client))
                {
                    if (client != null)
                    {
                        client.LastReceiveMessage = TimeUtil.GetTime();
                        if (!client.IsConnected)
                        {
                            client.IsConnected = true;
                            _ = KeepAliveService.KeepAlive(10, endPoint, udpClient);
                        }
                            
                        Globals.ConnectedClients[endPoint.ToString()] = client;
                    }
                }
            }
        }
        public static void ShopKeepAlive(Message message, IPEndPoint endPoint, UdpClient udpClient)
        {
            if (message.Type == MessageType.ShopKeepAlive)
            {
                message.ReceivedTimestamp = TimeUtil.GetTime();

                if (Globals.ConnectedShops.TryGetValue(endPoint.ToString(), out var shop))
                {
                    if (shop != null)
                    {
                        shop.LastReceiveMessage = TimeUtil.GetTime();
                        if (!shop.IsConnected)
                        {
                            shop.IsConnected = true;
                            _ = KeepAliveService.KeepAlive(10, endPoint, udpClient, true);
                        }

                        Globals.ConnectedClients[endPoint.ToString()] = shop;
                    }
                }
            }
        }

        public static void STUNKeepAlive(Message message, IPEndPoint endPoint, UdpClient udpClient)
        {
            if (message.Type == MessageType.STUNKeepAlive)
            {
                message.ReceivedTimestamp = TimeUtil.GetTime();

                if (Globals.STUNServer != null)
                {
                    Globals.STUNServer.LastReceiveMessage = TimeUtil.GetTime();
                    if (!Globals.STUNServer.IsConnected)
                    {
                        Globals.STUNServer.IsConnected = true;
                        _ = KeepAliveService.KeepAlive(10, endPoint, udpClient, false, true);
                    }
                }
            }
        }

        public static void DecShopMessage(Message message, IPEndPoint endPoint, UdpClient udpClient)
        {
            var respMessage = DecShopMessageService.ProcessMessage(message);

            if(respMessage != null)
            {
                var messagePayload = GenerateMessage(respMessage, false);

                var successMessage = Encoding.UTF8.GetBytes(messagePayload);
                udpClient.Send(successMessage, endPoint);
            }
        }

        public static void ChatMessage(Message message, IPEndPoint endPoint, UdpClient udpClient)
        {
            if(message.Type == MessageType.Chat)
            {
                message.ReceivedTimestamp = TimeUtil.GetTime();

                var chatMessage = JsonConvert.DeserializeObject<Chat.ChatMessage>(message.Data);
                if(chatMessage != null)
                {
                    if (chatMessage.IsMessageTrusted)
                    {
                        chatMessage.MessageReceived = true;
                        if (!chatMessage.IsShopSentMessage)
                        {
                            //Adds user to shop message list
                            if (Globals.ShopChatUsers.TryGetValue(chatMessage.FromAddress, out var chatUser))
                            {
                                Globals.ShopChatUsers[chatMessage.FromAddress] = chatUser;
                            }
                            else
                            {
                                Globals.ShopChatUsers.TryAdd(chatMessage.FromAddress, endPoint);
                            }

                            //Adds chat message to global chat list.
                            if (Globals.ChatMessageDict.TryGetValue(chatMessage.FromAddress, out var chatMessageList))
                            {
                                chatMessageList.Add(chatMessage);
                                Globals.ChatMessageDict[chatMessage.FromAddress] = chatMessageList;
                            }
                            else
                            {
                                List<Chat.ChatMessage> chatMessages = new List<Chat.ChatMessage>{ chatMessage };
                                Globals.ChatMessageDict[chatMessage.FromAddress] = chatMessages;
                            }

                            var messageBytes = Chat.CreateChatReceivedMessage(chatMessage);
                            if (messageBytes != null)
                                udpClient.Send(messageBytes, endPoint);
                        }
                        else
                        {
                            if (Globals.ChatMessageDict.TryGetValue(chatMessage.ShopURL, out var chatMessageList))
                            {
                                if (Chat.ValidateChatMessage(chatMessage))
                                {
                                    chatMessageList.Add(chatMessage);
                                    Globals.ChatMessageDict[chatMessage.ShopURL] = chatMessageList;
                                }
                            }
                            else
                            {
                                List<Chat.ChatMessage> chatMessages = new List<Chat.ChatMessage> { chatMessage };
                                Globals.ChatMessageDict[chatMessage.FromAddress] = chatMessages;
                            }

                            var messageBytes = Chat.CreateChatReceivedMessage(chatMessage);
                            if (messageBytes != null)
                                udpClient.Send(messageBytes, endPoint);
                        }
                    }
                }

            }
        }
        public static void ChatMessageReceived(Message message)
        {
            if (message.Type == MessageType.ChatRec)
            {
                message.ReceivedTimestamp = TimeUtil.GetTime();
                var dataSplit = message.Data.Split(',');
                var key = dataSplit[0];
                var messageId = dataSplit[1];
                if(Globals.ChatMessageDict.TryGetValue(key, out var chatMessageList))
                {
                    var chatMessage = chatMessageList.Where(x => x.Id == messageId).FirstOrDefault();
                    if(chatMessage != null)
                    {
                        chatMessage.MessageReceived = true;
                        Globals.ChatMessageDict[key] = chatMessageList;
                    }
                    
                }
            }
        }

        public static string GenerateMessage(Message message, bool responseRequested)
        {
            var output = "";
            message.Build();

            Globals.ClientMessageDict.TryAdd(message.Id, new MessageState { Message = message, MessageId = message.Id, MessageSentTimestamp = TimeUtil.GetTime() });
            output = JsonConvert.SerializeObject(message);

            return output;
        }
    }
}
