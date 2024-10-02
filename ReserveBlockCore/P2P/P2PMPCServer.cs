using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.P2P
{
    public class P2PMPCServer : P2PServer
    {
        #region On Connect/Disconnect
        public override async Task OnConnectedAsync()
        {
            string peerIP = null;
            try
            {
                peerIP = GetIP(Context);

                if (Globals.BannedIPs.ContainsKey(peerIP))
                {
                    Context.Abort();
                    return;
                }
                var httpContext = Context.GetHttpContext();

                if (httpContext == null)
                {
                    _ = EndOnConnect(peerIP, "httpContext is null", "httpContext is null");
                    return;
                }


            }
            catch (Exception ex)
            {
                Context?.Abort();
                ErrorLogUtility.LogError($"Unhandled exception has happend. Error : {ex.ToString()}", "P2PValidatorServer.OnConnectedAsync()");
            }

        }
        public override async Task OnDisconnectedAsync(Exception? ex)
        {
            //var peerIP = GetIP(Context);
            //var netVal = Globals.NetworkValidators.Where(x => x.Value.IPAddress == peerIP).FirstOrDefault();

            //Globals.P2PValDict.TryRemove(peerIP, out _);
            Context?.Abort();

            await base.OnDisconnectedAsync(ex);
        }

        #endregion

        #region End on Connect

        private async Task EndOnConnect(string ipAddress, string adjMessage, string logMessage)
        {
            if (Globals.OptionalLogging == true)
            {
                LogUtility.Log(logMessage, "Validator Connection");
                LogUtility.Log($"IP: {ipAddress} ", "Validator Connection");
            }

            Context?.Abort();
        }

        #endregion

        #region Get IP
        private static string GetIP(HubCallerContext context)
        {
            try
            {
                var peerIP = "NA";
                var feature = context.Features.Get<IHttpConnectionFeature>();
                if (feature != null)
                {
                    if (feature.RemoteIpAddress != null)
                    {
                        peerIP = feature.RemoteIpAddress.MapToIPv4().ToString();
                    }
                }

                return peerIP;
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError($"Unknown Error: {ex.ToString()}", "ConsensusServer.GetIP()");
            }

            return "0.0.0.0";
        }

        #endregion
    }
}
