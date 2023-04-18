using ReserveBlockCore.Utilities;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace ReserveBlockCore.Services
{
    public class AssetSendService
    {
        public static async Task SendAsset(string asset, string scUID, IPEndPoint endPoint, UdpClient udpClient, int ackNum)
        {
            try
            {
                var location = NFTAssetFileUtility.NFTAssetPath(asset, scUID, true);

                if (location != null && location != "NA")
                {
                    var assetBytes = NFTAssetFileUtility.GetNFTAssetByteArray(location);
                    if(assetBytes != null)
                    {
                        var packets = NFTAssetFileUtility.SplitIntoPackets(assetBytes);
                        if(packets != null)
                        {
                            var packet = packets[ackNum];
                            await udpClient.SendAsync(packets[ackNum], packet.Length, endPoint);
                        }
                    }
                }
            }
            catch(Exception ex) 
            {
                NFTLogUtility.Log($"Unknown Error: {ex.ToString()}", "AssetSendService.SendAsset()");
            }
        }
    }
}
