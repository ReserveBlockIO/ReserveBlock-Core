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

                if (location != "NA")
                {
                    var assetBytes = NFTAssetFileUtility.GetNFTAssetByteArray(location);
                    if(assetBytes != null)
                    {
                        var packets = NFTAssetFileUtility.SplitIntoPackets(assetBytes);
                        if(packets != null)
                        {
                            if (ackNum < packets.Length)
                            {
                                var packet = packets[ackNum];
                                if(packet != null)
                                    await udpClient.SendAsync(packet, packet.Length, endPoint);
                            }
                            else
                            {
                                NFTLogUtility.Log($"Packet Ack Num too large. Asset: {asset} | AckNum: {ackNum} | Contract UID : {scUID} | IP: {endPoint.ToString()}", "AssetSendService.SendAsset()");
                            }
                        }
                    }
                }
            }
            catch(Exception ex) 
            {
                NFTLogUtility.Log($"Unknown Error: {ex.ToString()} - Asset: {asset} - ACK Num: {ackNum} - SCUID: {scUID} - IP: {endPoint.ToString()}", "AssetSendService.SendAsset()");
            }
        }
    }
}
