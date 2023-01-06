using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using ReserveBlockCore.Models;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.P2P
{
    public class P2PMotherServer : Hub
    {
        #region Connect/Disconnect methods
        public override async Task OnConnectedAsync()
        {
            bool connected = false;

            var peerIP = GetIP(Context);
            if (Globals.MothersKidsContext.TryGetValue(peerIP, out var context) && context.ConnectionId != Context.ConnectionId)
                context.Abort();

            var httpContext = Context.GetHttpContext();

            if (httpContext != null)
            {
                var password = httpContext.Request.Headers["password"].ToString();
                var walletVersion = httpContext.Request.Headers["walver"].ToString();

                var walletVersionVerify = WalletVersionUtility.Verify(walletVersion);

                if (!string.IsNullOrWhiteSpace(password) && walletVersionVerify)
                {
                    var mother = Mother.GetMother();
                    if(mother == null)
                    {
                        //Mother is not present. Cannot continue
                        Context.Abort();
                    }
                    else
                    {
                        var passAttempt = mother.Password.ToDecrypt(password);
                        if(passAttempt != "Fail" && passAttempt == password)
                        {
                            Globals.MothersKidsContext[peerIP] = Context;
                            connected = true;
                        }
                        else
                        {
                            //password attempt failed
                            Context.Abort();
                        }
                    }
                }
            }

            if (connected)
            {
                await SendMotherMessageSingle("status", "Connected");
            }
            else
            {
                await SendMotherMessageSingle("disconnect", "Failed to authenticate");
                Context.Abort();
            }


            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? ex)
        {
            var peerIP = GetIP(Context);
            Globals.MothersKidsContext.TryRemove(peerIP, out _);
        }
        private async Task SendMessageClient(string clientId, string method, string message)
        {
            await Clients.Client(clientId).SendAsync("GetMotherData", method, message);
        }

        private async Task SendMotherMessageSingle(string message, string data)
        {
            await Clients.Caller.SendAsync("GetMotherData", message, data);
        }

        private async Task SendMotherMessageAll(string message, string data)
        {
            await Clients.All.SendAsync("GetMotherData", message, data);
        }

        #endregion

        #region Mother Get Data
        public async Task<bool> SendMotherData(string data)
        {
            var peerIP = GetIP(Context);

            bool result = false;
            var payload = JsonConvert.DeserializeObject<Mother.DataPayload>(data);
            if (payload != null)
            {
                Globals.MothersKids.TryGetValue(payload.Address, out var kid);
                if (kid != null)
                {
                    kid.Address = payload.Address;
                    kid.IPAddress = peerIP;
                    kid.Balance = payload.Balance;
                    kid.IsValidating = payload.IsValidating;
                    kid.BlockHeight = payload.BlockHeight;
                    kid.LastTaskSent = payload.LastTaskSent;
                    kid.LastTaskBlockSent = payload.LastTaskBlockSent;
                    kid.ValidatorName = payload.ValidatorName;
                    kid.PeerCount = payload.PeerCount;
                    kid.LastDataSentTime = DateTime.Now;

                    Globals.MothersKids[peerIP] = kid;
                    result = true;
                }
                else
                {
                    Mother.Kids nKid = new Mother.Kids { 
                        ConnectTime = DateTime.Now,
                        LastDataSentTime = DateTime.Now,
                        Address = payload.Address,
                        IPAddress = peerIP,
                        Balance = payload.Balance,
                        IsValidating = payload.IsValidating,
                        BlockHeight = payload.BlockHeight,
                        LastTaskSent = payload.LastTaskSent,
                        LastTaskBlockSent = payload.LastTaskBlockSent,
                        ValidatorName = payload.ValidatorName,
                        PeerCount = payload.PeerCount,
                    };

                    Globals.MothersKids[peerIP] = nKid;
                    result = true;
                }
            }
            return result;
        }

        #endregion

        #region Get IP
        private static string GetIP(HubCallerContext context)
        {
            var feature = context.Features.Get<IHttpConnectionFeature>();
            var peerIP = feature.RemoteIpAddress.MapToIPv4().ToString();

            return peerIP;
        }

        #endregion
    }
}
