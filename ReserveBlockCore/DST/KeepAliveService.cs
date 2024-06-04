using ReserveBlockCore.Models.DST;
using ReserveBlockCore.Utilities;
using System.Net.Sockets;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using ReserveBlockCore.Models;
using System;

namespace ReserveBlockCore.DST
{
    public class KeepAliveService
    {
        public static async Task KeepAlive(int seconds, IPEndPoint peerEndPoint, UdpClient udpClient, string connectionId, bool isShop = false, bool isStun = false, string shopURL = "NA")
        {
            var savedConnectionId = connectionId;
            bool stop = false;
            while (true && !stop)
            {
                try
                {
                    if(shopURL != "NA") //buyers connected to sellers use this. multi connect
                    {
                        if(DSTMultiClient.ShopConnections.TryGetValue(shopURL, out var shopConnection))
                        {
                            var delay = Task.Delay(new TimeSpan(0, 0, seconds));
                            var payload = new Message { Type = MessageType.KeepAlive, Data = "" };
                            var message = MessageService.GenerateMessage(payload, false);
                            var messageDataBytes = Encoding.UTF8.GetBytes(message);
                            udpClient.Send(messageDataBytes, peerEndPoint);

                            shopConnection.LastSentMessage = TimeUtil.GetTime();

                            DSTMultiClient.ShopConnections[shopURL] = shopConnection;

                            var currentTime = TimeUtil.GetTime();
                            if (currentTime - shopConnection.LastReceiveMessage > 60 || savedConnectionId != shopConnection.ConnectionId)
                            {
                                stop = true;
                                shopConnection.KeepAliveStarted = false;
                                DSTMultiClient.ShopConnections[shopURL] = shopConnection;
                                if (savedConnectionId == shopConnection.ConnectionId && shopConnection.AttemptReconnect)
                                {
                                    await DSTMultiClient.DisconnectFromShop(shopURL);
                                    _ = DSTMultiClient.ConnectToShop(shopURL, shopConnection.RBXAddress);
                                }
                                if (!shopConnection.AttemptReconnect)
                                    DSTMultiClient.ShopConnections.TryRemove(shopURL, out _);
                            }

                            await delay;
                        }
                        else
                        {
                            stop = true;
                        }
                    }
                    else if (isShop)
                    {
                        if (Globals.ConnectedShops.TryGetValue(peerEndPoint.ToString(), out var shop))
                        {
                            var delay = Task.Delay(new TimeSpan(0, 0, seconds));
                            var payload = new Message { Type = MessageType.STUNKeepAlive, Data = "" };
                            var message = MessageService.GenerateMessage(payload, false);
                            var messageDataBytes = Encoding.UTF8.GetBytes(message);
                            udpClient.Send(messageDataBytes, peerEndPoint);

                            shop.LastSentMessage = TimeUtil.GetTime();

                            Globals.ConnectedShops[peerEndPoint.ToString()] = shop;

                            var currentTime = TimeUtil.GetTime();
                            if (currentTime - shop.LastReceiveMessage > 60 || shop.ConnectionId != savedConnectionId)
                            {
                                stop = true;
                                if (!shop.AttemptReconnect)
                                    Globals.ConnectedShops.TryRemove(peerEndPoint.ToString(), out _);
                                //Globals.ConnectedShops.TryRemove(peerEndPoint.ToString(), out _);
                            }

                            

                            await delay;
                        }
                        else
                        {
                            stop = true;
                        }
                    }
                    else if (isStun)
                    {
                        if (Globals.STUNServer != null)
                        {
                            var delay = Task.Delay(new TimeSpan(0, 0, seconds));

                            var payload = new Message { Type = MessageType.ShopKeepAlive, Data = "" };
                            var message = MessageService.GenerateMessage(payload, false);
                            var messageDataBytes = Encoding.UTF8.GetBytes(message);
                            udpClient.Send(messageDataBytes, peerEndPoint);

                            Globals.STUNServer.LastSentMessage = TimeUtil.GetTime();

                            var currentTime = TimeUtil.GetTime();
                            if (currentTime - Globals.STUNServer.LastReceiveMessage > 50 || savedConnectionId != Globals.STUNServer.ConnectionId)
                            {
                                stop = true;
                                if(savedConnectionId == Globals.STUNServer.ConnectionId && Globals.STUNServer.AttemptReconnect) 
                                {
                                    await DSTClient.DisconnectFromSTUNServer(true); //disconnect from STUN Server
                                    _ = DSTClient.Run(); //attempt to reconnect to a STUN server.
                                }
                            }

                            await Task.Delay(new TimeSpan(0, 0, seconds));
                        }
                        else
                        {
                            stop = true;
                        }
                    }
                    else
                    {
                        if (Globals.ConnectedClients.TryGetValue(peerEndPoint.ToString(), out var client))
                        {
                            if (client != null)
                            {
                                var delay = Task.Delay(new TimeSpan(0, 0, seconds));
                                var payload = new Message { Type = MessageType.KeepAlive, Data = "" };
                                var message = MessageService.GenerateMessage(payload, false);
                                var messageDataBytes = Encoding.UTF8.GetBytes(message);
                                udpClient.Send(messageDataBytes, peerEndPoint);

                                client.LastSentMessage = TimeUtil.GetTime();

                                Globals.ConnectedClients[peerEndPoint.ToString()] = client;

                                var currentTime = TimeUtil.GetTime();
                                if (currentTime - client.LastReceiveMessage > 50 || savedConnectionId != client.ConnectionId)
                                {
                                    stop = true;
                                    client.KeepAliveStarted = false;
                                    Globals.ConnectedClients[peerEndPoint.ToString()] = client;
                                    if (savedConnectionId == client.ConnectionId && client.AttemptReconnect)
                                    {
                                        var shopServer = client.IPAddress; //this also has port
                                        var shopEndPoint = IPEndPoint.Parse(shopServer);

                                        SCLogUtility.Log($"Disconnected from shop: {shopServer}", "KeepAliveService.KeepAlive()");
                                        await DSTClient.DisconnectFromShop(true);
                                        _ = DSTClient.ConnectToShop(shopEndPoint, shopServer, "NA", client != null ? client.ShopURL : "NA");
                                    }

                                    if (!client.AttemptReconnect)
                                        Globals.ConnectedShops.TryRemove(peerEndPoint.ToString(), out _);
                                }

                                await Task.Delay(new TimeSpan(0, 0, seconds));
                            }
                        }
                        else
                        {
                            stop = true;
                        }
                    }
                }
                catch { stop = true; }
            }
        }

    }
}
