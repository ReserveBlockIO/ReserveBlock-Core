using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.P2P
{
    public class P2PValidator : P2PServer
    {
        #region On Connected 
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
                    EndOnConnect(peerIP, "httpContext is null", "httpContext is null");
                    return;
                }

                var address = httpContext.Request.Headers["address"].ToString();
                var time = httpContext.Request.Headers["time"].ToString();
                var uName = httpContext.Request.Headers["uName"].ToString();
                var publicKey = httpContext.Request.Headers["publicKey"].ToString();
                var blockStart = httpContext.Request.Headers["blockStart"].ToString();
                var signature = httpContext.Request.Headers["signature"].ToString();
                var walletVersion = httpContext.Request.Headers["walver"].ToString();

                if (!Globals.NetworkValidators.ContainsKey(address))
                {
                    EndOnConnect(peerIP, address + " attempted to connect as adjudicator", address + " attempted to connect as adjudicator");
                    return;
                }

                var SignedMessage = address;
                var Now = TimeUtil.GetTime();
                SignedMessage = address + ":" + time;
                if (TimeUtil.GetTime() - long.Parse(time) > 300)
                {
                    EndOnConnect(peerIP, "Signature Bad time.", "Signature Bad time.");
                    return;
                }

                var walletVersionVerify = WalletVersionUtility.Verify(walletVersion);

                if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(signature))
                {
                    EndOnConnect(peerIP,
                        "Connection Attempted, but missing field(s). Address, and Public Key required. You are being disconnected.",
                        "Connected, but missing field(s). Address, and Public Key required: " + address);
                    return;
                }
                var stateAddress = StateData.GetSpecificAccountStateTrei(address);
                if (stateAddress == null)
                {
                    EndOnConnect(peerIP,
                        "Connection Attempted, But failed to find the address in trie. You are being disconnected.",
                        "Connection Attempted, but missing field Address: " + address + " IP: " + peerIP);
                    return;
                }

                if (stateAddress.Balance < ValidatorService.ValidatorRequiredAmount())
                {
                    EndOnConnect(peerIP,
                        $"Connected, but you do not have the minimum balance of {ValidatorService.ValidatorRequiredAmount()} RBX. You are being disconnected.",
                        $"Connected, but you do not have the minimum balance of {ValidatorService.ValidatorRequiredAmount()} RBX: " + address);
                    return;
                }

                var verifySig = SignatureService.VerifySignature(address, SignedMessage, signature);
                if (!verifySig)
                {
                    _ = EndOnConnect(peerIP,
                        "Connected, but your address signature failed to verify. You are being disconnected.",
                        "Connected, but your address signature failed to verify with ADJ: " + address);
                    return;
                }

                //TO-DO: Add to Globals.NetworkValidators
                var netVal = new NetworkValidator { 
                    Address = address,
                    BlockStart = Globals.LastBlock.Height + 144,
                    IPAddress = peerIP,
                    LastBlockProof = 0,
                    PublicKey = publicKey,
                    Signature = signature,
                    UniqueName = uName
                };

                Globals.NetworkValidators.TryAdd(address, netVal);

            }
            catch (Exception ex)
            {
                Context?.Abort();
                ErrorLogUtility.LogError($"Unhandled exception has happend. Error : {ex.ToString()}", "ConsensusServer.OnConnectedAsync()");
            }

        }
        public override async Task OnDisconnectedAsync(Exception? ex)
        {
            var peerIP = GetIP(Context);
            //var netVal = Globals.NetworkValidators.Where(x => x.Value.IPAddress == peerIP).FirstOrDefault();

            Globals.P2PPeerDict.TryRemove(peerIP, out _);
            Context?.Abort();

            await base.OnDisconnectedAsync(ex);
        }

        #endregion

        #region Receive Block - Receives Block and then Broadcast out.
        public async Task<bool> ReceiveBlock(Block nextBlock)
        {
            try
            {
                return await SignalRQueue(Context, (int)nextBlock.Size, async () =>
                {

                    if (nextBlock.ChainRefId == BlockchainData.ChainRef)
                    {
                        var IP = GetIP(Context);
                        var nextHeight = Globals.LastBlock.Height + 1;
                        var currentHeight = nextBlock.Height;

                        if (currentHeight >= nextHeight && BlockDownloadService.BlockDict.TryAdd(currentHeight, (nextBlock, IP)))
                        {
                            await BlockValidatorService.ValidateBlocks();

                            if (nextHeight == currentHeight)
                            {
                                string data = "";
                                data = JsonConvert.SerializeObject(nextBlock);
                                await Clients.All.SendAsync("GetMessage", "blk", data);
                            }

                            if (nextHeight < currentHeight)
                                await BlockDownloadService.GetAllBlocks();

                            return true;
                        }
                    }

                    return false;
                });
            }
            catch { }

            return false;
        }

        #endregion

        #region Send Block - returns specific block
        //Send Block to client from p2p server
        public async Task<Block?> SendBlock(long currentBlock)
        {
            try
            {
                var peerIP = GetIP(Context);

                var message = "";
                var nextBlockHeight = currentBlock + 1;
                var nextBlock = BlockchainData.GetBlockByHeight(nextBlockHeight);

                if (nextBlock != null)
                {
                    return nextBlock;
                }
                else
                {
                    return null;
                }
            }
            catch { }

            return null;

        }

        #endregion

        #region Send to Mempool
        public async Task<string> SendTxToMempool(Transaction txReceived)
        {
            //This needs to have new modified network reach
            //No longer uses ADJs
            return "";
        }

        #endregion

        #region Get Validator Status
        public async Task<bool> GetValidatorStatus()
        {
            return await SignalRQueue(Context, bool.FalseString.Length, async () => !string.IsNullOrEmpty(Globals.ValidatorAddress));
        }

        #endregion

        #region Get Wallet Version
        public async Task<string> GetWalletVersion()
        {
            return await SignalRQueue(Context, Globals.CLIVersion.Length, async () => Globals.CLIVersion);
        }

        #endregion

        #region End on Connect

        private void EndOnConnect(string ipAddress, string adjMessage, string logMessage)
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
