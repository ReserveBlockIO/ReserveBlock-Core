using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;


namespace ReserveBlockCore.P2P
{
    public class P2PBeaconServer : Hub
    {
        #region Connect/Disconnect methods
        public override async Task OnConnectedAsync()
        {
            bool connected = false;
            bool pendingSends = false;
            bool pendingReceives = false;

            var peerIP = GetIP(Context);
            if (Globals.BeaconPeerList.TryGetValue(peerIP, out var context) && context.ConnectionId != Context.ConnectionId)
                context.Abort();

            Globals.BeaconPeerList[peerIP] = Context;

            var httpContext = Context.GetHttpContext();
            if (httpContext != null)
            {
                var beaconRef = httpContext.Request.Headers["beaconRef"].ToString();
                var walletVersion = httpContext.Request.Headers["walver"].ToString();
                var uplReq = httpContext.Request.Headers["uplReq"].ToString();
                var dwnlReq = httpContext.Request.Headers["dwnlReq"].ToString();

                var walletVersionVerify = WalletVersionUtility.Verify(walletVersion);

                var beaconPool = Globals.BeaconPool.ToList();

                if (!string.IsNullOrWhiteSpace(beaconRef) && walletVersionVerify)
                { 
                    var beaconData = BeaconData.GetBeaconData();
                    var beacon = BeaconData.GetBeacon();
                    
                    if(uplReq == "n")
                    {
                        if (beaconData != null)
                        {
                            var beaconSendData = beaconData.Where(x => x.Reference == beaconRef).ToList();
                            if (beaconSendData.Count() > 0)
                            {
                                var removeList = beaconSendData.Where(x => x.AssetExpireDate <= TimeUtil.GetTime());
                                //remove record and remove any data sent
                                if (beacon != null)
                                {
                                    beacon.DeleteManySafe(x => removeList.Contains(x));
                                }
                                beaconData = BeaconData.GetBeaconData();
                                if(beaconData != null)
                                {
                                    beaconSendData = beaconData.Where(x => x.Reference == beaconRef).ToList();
                                    if (beaconSendData.Count() > 0)
                                    {
                                        pendingSends = true;
                                    }
                                }    
                            }
                        }
                    }
                    else
                    {
                        pendingSends = true;
                    }

                    if(dwnlReq == "n")
                    {
                        if (beaconData != null)
                        {
                            var beaconRecData = beaconData.Where(x => x.NextOwnerReference == beaconRef).ToList();
                            if (beaconRecData.Count() > 0)
                            {
                                var removeList = beaconRecData.Where(x => x.AssetExpireDate <= TimeUtil.GetTime());
                                //remove record and remove any data sent
                                if (beacon != null)
                                {
                                    beacon.DeleteManySafe(x => removeList.Contains(x));
                                }
                                beaconData = BeaconData.GetBeaconData();
                                if (beaconData != null)
                                {
                                    beaconRecData = beaconData.Where(x => x.NextOwnerReference == beaconRef).ToList();
                                    if (beaconRecData.Count() > 0)
                                    {
                                        pendingReceives = true;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        pendingReceives = true;
                    }

                    if(pendingSends == true || pendingReceives == true)
                    {
                        var conExist = beaconPool.Where(x => x.Reference == beaconRef || x.IpAddress == peerIP).FirstOrDefault();
                        if (conExist != null)
                        {
                            var beaconCon = Globals.BeaconPool.Where(x => x.Reference == beaconRef || x.IpAddress == peerIP).FirstOrDefault();
                            if (beaconCon != null)
                            {
                                beaconCon.WalletVersion = walletVersion;
                                beaconCon.Reference = beaconRef;
                                beaconCon.ConnectDate = DateTime.Now;
                                beaconCon.ConnectionId = Context.ConnectionId;
                                beaconCon.IpAddress = peerIP;
                            }
                        }
                        else
                        {
                            BeaconPool beaconConnection = new BeaconPool
                            {
                                WalletVersion = walletVersion,
                                Reference = beaconRef,
                                ConnectDate = DateTime.Now,
                                ConnectionId = Context.ConnectionId,
                                IpAddress = peerIP
                            };

                            Globals.BeaconPool.Add(beaconConnection);
                        }

                        connected = true;
                    }

                }
            }

            if(connected)
            {
                await SendBeaconMessageSingle("status", "Connected");
            }
            else
            {
                await SendBeaconMessageSingle("disconnect", "No downloads at this time.");
                Context.Abort();
            }



            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? ex)
        {
            var peerIP = GetIP(Context);
            _ = Globals.BeaconPeerList.TryRemove(peerIP, out var test);
            try 
            { 
                Globals.BeaconPool.RemoveAll(x => x.IpAddress == peerIP && x.ConnectionId == Context.ConnectionId); 
            }
            catch { };
        }

        private async Task SendBeaconMessageSingle(string message, string data)
        {
            await Clients.Caller.SendAsync("GetBeaconData", message, data);
        }

        private async Task SendBeaconMessageAll(string message, string data)
        {
            await Clients.All.SendAsync("GetBeaconData", message, data);
        }

        #endregion

        #region Send Beacon Locator Info
        public async Task<string> SendBeaconInfo()
        {
            return await SignalRQueue(Context, 128, async () => {
                var result = "";

                var beaconInfo = BeaconInfo.GetBeaconInfo();

                if (beaconInfo == null)
                    return "NA";

                result = beaconInfo.BeaconLocator;

                return result;
            });
        }

        #endregion

        #region  Beacon Receive Download Request - The receiver of the NFT Asset
        public async Task<bool> ReceiveDownloadRequest(BeaconData.BeaconDownloadData bdd)
        {
            return await SignalRQueue(Context, 1024, async () =>
            {
                bool result = false;
                var peerIP = GetIP(Context);

                try
                {
                    if (bdd != null)
                    {
                        var scState = SmartContractStateTrei.GetSmartContractState(bdd.SmartContractUID);
                        if (scState == null)
                        {
                            return result; //fail
                        }

                        var sigCheck = SignatureService.VerifySignature(scState.OwnerAddress, bdd.SmartContractUID, bdd.Signature);
                        if (sigCheck == false)
                        {
                            return result; //fail
                        }

                        var beaconDatas = BeaconData.GetBeacon();
                        var beaconData = BeaconData.GetBeaconData();
                        foreach (var fileName in bdd.Assets)
                        {
                            if (beaconData != null)
                            {
                                var bdCheck = beaconData.Where(x => x.SmartContractUID == bdd.SmartContractUID && x.AssetName == fileName && x.NextAssetOwnerAddress == scState.OwnerAddress).FirstOrDefault();
                                if (bdCheck != null)
                                {
                                    if (beaconDatas != null)
                                    {
                                        bdCheck.DownloadIPAddress = peerIP;
                                        beaconDatas.UpdateSafe(bdCheck);
                                    }
                                    else
                                    {
                                        return result;//fail
                                    }
                                }
                                else
                                {
                                    return result; //fail
                                }
                            }
                            else
                            {
                                return result; //fail
                            }

                            result = true; //success
                            //need to then call out to origin to process download
                        }

                    }
                }
                catch (Exception ex)
                {
                    result = false; //just in case setting this to false
                    ErrorLogUtility.LogError($"Error Creating BeaconData. Error Msg: {ex.Message}", "P2PServer.ReceiveUploadRequest()");
                }

                return result;
            });
        }

        #endregion

        #region Beacon Receive Upload Request - The sender of the NFT Asset
        public async Task<bool> ReceiveUploadRequest(BeaconData.BeaconSendData bsd)
        {
            return await SignalRQueue(Context, 1024, async () =>
            {
                bool result = false;
                var peerIP = GetIP(Context);
                try
                {
                    if (bsd != null)
                    {
                        var scState = SmartContractStateTrei.GetSmartContractState(bsd.SmartContractUID);
                        if (scState == null)
                        {
                            return result;
                        }

                        var sigCheck = SignatureService.VerifySignature(scState.OwnerAddress, bsd.SmartContractUID, bsd.Signature);
                        if (sigCheck == false)
                        {
                            return result;
                        }

                        var beaconData = BeaconData.GetBeaconData();
                        foreach (var fileName in bsd.Assets)
                        {
                            if (beaconData == null)
                            {
                                var bd = new BeaconData
                                {
                                    CurrentAssetOwnerAddress = bsd.CurrentOwnerAddress,
                                    AssetExpireDate = TimeUtil.GetTime(),
                                    AssetReceiveDate = TimeUtil.GetTimeForBeaconRelease(),
                                    AssetName = fileName,
                                    IPAdress = peerIP,
                                    NextAssetOwnerAddress = bsd.NextAssetOwnerAddress,
                                    SmartContractUID = bsd.SmartContractUID
                                };

                                BeaconData.SaveBeaconData(bd);
                            }
                            else
                            {
                                var bdCheck = beaconData.Where(x => x.SmartContractUID == bsd.SmartContractUID && x.AssetName == fileName).FirstOrDefault();
                                if (bdCheck == null)
                                {
                                    var bd = new BeaconData
                                    {
                                        AssetExpireDate = 0,
                                        AssetReceiveDate = 0,
                                        AssetName = fileName,
                                        IPAdress = peerIP,
                                        NextAssetOwnerAddress = bsd.NextAssetOwnerAddress,
                                        SmartContractUID = bsd.SmartContractUID
                                    };

                                    BeaconData.SaveBeaconData(bd);
                                }
                            }
                        }

                        result = true;

                    }
                }
                catch (Exception ex)
                {
                    ErrorLogUtility.LogError($"Error Receive Upload Request. Error Msg: {ex.Message}", "P2PServer.ReceiveUploadRequest()");
                }

                return result;
            });
        }

        #endregion

        #region SignalR DOS Protection

        public static async Task<T> SignalRQueue<T>(HubCallerContext context, int sizeCost, Func<Task<T>> func)
        {
            var now = TimeUtil.GetMillisecondTime();
            var ipAddress = GetIP(context);
            if (Globals.MessageLocks.TryGetValue(ipAddress, out var Lock))
            {
                var prev = Interlocked.Exchange(ref Lock.LastRequestTime, now);
                if (Lock.ConnectionCount > 20)
                    Peers.BanPeer(ipAddress);

                if (Lock.BufferCost + sizeCost > 5000000)
                {
                    throw new HubException("Too much buffer usage.  Message was dropped.");
                }
                if (now - prev < 1000)
                    Interlocked.Increment(ref Lock.DelayLevel);
                else
                {
                    Interlocked.CompareExchange(ref Lock.DelayLevel, 1, 0);
                    Interlocked.Decrement(ref Lock.DelayLevel);
                }

                return await SignalRQueue(Lock, sizeCost, func);
            }
            else
            {
                var newLock = new MessageLock { BufferCost = sizeCost, LastRequestTime = now, DelayLevel = 0, ConnectionCount = 0 };
                if (Globals.MessageLocks.TryAdd(ipAddress, newLock))
                    return await SignalRQueue(newLock, sizeCost, func);
                else
                {
                    Lock = Globals.MessageLocks[ipAddress];
                    var prev = Interlocked.Exchange(ref Lock.LastRequestTime, now);
                    if (now - prev < 1000)
                        Interlocked.Increment(ref Lock.DelayLevel);
                    else
                    {
                        Interlocked.CompareExchange(ref Lock.DelayLevel, 1, 0);
                        Interlocked.Decrement(ref Lock.DelayLevel);
                    }

                    return await SignalRQueue(Lock, sizeCost, func);
                }
            }
        }

        private static async Task<T> SignalRQueue<T>(MessageLock Lock, int sizeCost, Func<Task<T>> func)
        {
            Interlocked.Increment(ref Lock.ConnectionCount);
            Interlocked.Add(ref Lock.BufferCost, sizeCost);
            await Lock.Semaphore.WaitAsync();
            try
            {
                var task = func();
                if (Lock.DelayLevel == 0)
                    return await task;

                var delayTask = Task.Delay(500 * (1 << (Lock.DelayLevel - 1)));
                await Task.WhenAll(delayTask, task);
                return await task;
            }
            finally
            {
                if (Lock.Semaphore.CurrentCount == 0) // finally can be executed more than once
                {
                    Interlocked.Decrement(ref Lock.ConnectionCount);
                    Interlocked.Add(ref Lock.BufferCost, -sizeCost);
                    Lock.Semaphore.Release();
                }
            }
        }

        public static async Task SignalRQueue(HubCallerContext context, int sizeCost, Func<Task> func)
        {
            var commandWrap = async () =>
            {
                await func();
                return 1;
            };
            await SignalRQueue(context, sizeCost, commandWrap);
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
