using Newtonsoft.Json;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.DST;
using ReserveBlockCore.Utilities;
using System.Net.Sockets;
using System.Text;

namespace ReserveBlockCore.DST
{
    public class DecShopMessageService
    {
        public static Message? ProcessMessage(Message message)
        {
            if (message.ComType == MessageComType.Request)
            {
                var result = RequestMessage(message);
                return message;
            }
            
            if (message.ComType == MessageComType.Response)
            {
                ResponseMessage(message);
                return null;
            }

            return null;
        }

        private static void ResponseMessage(Message message)
        {
            try
            {
                Globals.ClientMessageDict.TryGetValue(message.ResponseMessageId, out var msg);
                if (msg != null)
                {
                    var requestOptArray = msg.Message.Data.Split(',');
                    var requestOpt = requestOptArray[0];
                    if (requestOpt != null)
                    {
                        var option = requestOpt;
                        if (option == "Info")
                        {
                            var decShopInfo = JsonConvert.DeserializeObject<DecShop>(message.Data);
                            if (Globals.DecShopData == null)
                            {
                                Globals.DecShopData = new DecShopData
                                {
                                    DecShop = decShopInfo
                                };
                            }
                            else
                            {
                                Globals.DecShopData.DecShop = decShopInfo;
                            }

                            msg.HasReceivedResponse = true;
                            msg.MessageResponseReceivedTimestamp = TimeUtil.GetTime();
                            Globals.ClientMessageDict[message.ResponseMessageId] = msg;
                        }
                    }
                }

            }
            catch
            {
                Globals.ClientMessageDict.TryGetValue(message.ResponseMessageId, out var msg);
                if (msg != null)
                {
                    msg.HasReceivedResponse = true;
                    msg.MessageResponseReceivedTimestamp = TimeUtil.GetTime();
                    msg.DidMessageRequestFail = true;
                }
            }
        }

        private static Message RequestMessage(Message message)
        {
            try
            {
                var requestOptArray = message.Data.Split(',');
                var requestOpt = requestOptArray[0];
                if (requestOpt != null)
                {
                    var option = requestOpt;
                    if (option == "Info")
                    {
                        var decShop = DecShop.GetMyDecShopInfo();
                        if (decShop != null)
                        {
                            var respMessage = new Message
                            {
                                ResponseMessage = true,
                                ResponseMessageId = message.Id,
                                Type = message.Type,
                                ComType = MessageComType.Response,
                                Data = JsonConvert.SerializeObject(decShop)
                            };

                            return respMessage;
                        }
                        else
                        {
                            var respMessage = new Message
                            {
                                ResponseMessage = true,
                                ResponseMessageId = message.Id,
                                Type = message.Type,
                                ComType = MessageComType.Response,
                                Data = "BAD"
                            };

                            return respMessage;
                        }
                    }
                }
            }
            catch
            {
                var respMessage = new Message
                {
                    ResponseMessage = true,
                    ResponseMessageId = message.Id,
                    Type = message.Type,
                    ComType = MessageComType.Response,
                    Data = "BAD"
                };

                return respMessage;
            }

            var badRequest = new Message
            {
                ResponseMessage = true,
                ResponseMessageId = message.Id,
                Type = message.Type,
                ComType = MessageComType.Response,
                Data = "BAD"
            };

            return badRequest;
        }
    }
}
