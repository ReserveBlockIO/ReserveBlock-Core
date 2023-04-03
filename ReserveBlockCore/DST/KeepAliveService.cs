﻿using ReserveBlockCore.Models.DST;
using ReserveBlockCore.Utilities;
using System.Net.Sockets;
using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace ReserveBlockCore.DST
{
    public class KeepAliveService
    {
        public static async Task KeepAlive(int seconds, IPEndPoint peerEndPoint, UdpClient udpClient, bool isShop = false, bool isStun = false)
        {
            bool stop = false;
            while (true && !stop)
            {
                try
                {
                    if (isShop)
                    {
                        if (Globals.ConnectedShops.TryGetValue(peerEndPoint.ToString(), out var shop))
                        {
                            if (shop != null)
                            {
                                var delay = Task.Delay(new TimeSpan(0, 0, seconds));
                                var payload = new Message { Type = MessageType.STUNKeepAlive, Data = "" };
                                var message = MessageService.GenerateMessage(payload, false);
                                var messageDataBytes = Encoding.UTF8.GetBytes(message);
                                udpClient.Send(messageDataBytes, peerEndPoint);

                                shop.LastSentMessage = TimeUtil.GetTime();

                                Globals.ConnectedShops[peerEndPoint.ToString()] = shop;

                                var currentTime = TimeUtil.GetTime();
                                if (currentTime - shop.LastReceiveMessage > 15)
                                {
                                    stop = true;
                                    Globals.ConnectedShops.TryRemove(peerEndPoint.ToString(), out _);
                                }

                                await delay;
                            }
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
                            if (currentTime - Globals.STUNServer.LastReceiveMessage > 15)
                            {
                                stop = true;
                                Globals.STUNServer = null;
                            }

                            await delay;
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
                                if (currentTime - client.LastReceiveMessage > 15)
                                {
                                    stop = true;
                                    Globals.ConnectedClients.TryRemove(peerEndPoint.ToString(), out _);
                                }

                                await delay;
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