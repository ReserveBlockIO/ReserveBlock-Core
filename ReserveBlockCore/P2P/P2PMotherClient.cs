using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using ReserveBlockCore.Nodes;
using Newtonsoft.Json;
using ReserveBlockCore.Models;
using ReserveBlockCore.Data;
using System.ComponentModel.DataAnnotations;

namespace ReserveBlockCore.P2P
{
    public class P2PMotherClient
    {
        private static HubConnection? hubMotherConnection;
        public static bool IsMotherConnected => hubMotherConnection?.State == HubConnectionState.Connected;

        #region Connect Mother
        public static async Task ConnectMother(string url)
        {
            try
            {
                var password = Globals.MotherPassword != null ? Globals.MotherPassword.ToUnsecureString() : null;
                if(password != null)
                {
                    hubMotherConnection = new HubConnectionBuilder()
                    .WithUrl(url, options => {
                        options.Headers.Add("password", password);
                        options.Headers.Add("walver", Globals.CLIVersion);
                    })
                    .WithAutomaticReconnect()
                    .Build();

                    LogUtility.Log("Connecting to Beacon", "ConnectMother()");

                    var ipAddress = GetPathUtility.IPFromURL(url);
                    hubMotherConnection.Reconnecting += (sender) =>
                    {
                        LogUtility.Log("Reconnecting to Mother", "ConnectMother()");
                        ConsoleWriterService.Output("[" + DateTime.Now.ToString() + "] Connection to Mother lost. Attempting to Reconnect.");
                        return Task.CompletedTask;
                    };

                    hubMotherConnection.Reconnected += (sender) =>
                    {
                        LogUtility.Log("Success! Reconnected to Mother", "ConnectMother()");
                        ConsoleWriterService.Output("[" + DateTime.Now.ToString() + "] Connection to Mother has been restored.");
                        return Task.CompletedTask;
                    };

                    hubMotherConnection.Closed += (sender) =>
                    {
                        LogUtility.Log("Closed to MOther", "ConnectMother()");
                        ConsoleWriterService.Output("[" + DateTime.Now.ToString() + "] Connection to Mother has been closed.");
                        return Task.CompletedTask;
                    };

                    hubMotherConnection.On<string, string>("GetMotherData", async (message, data) => {
                        if (message == "status" ||
                        message == "disconnect")
                        {
                            switch (message)
                            {
                                case "status":
                                    ConsoleWriterService.Output(data);
                                    LogUtility.Log("Success! Connected to Mother", "ConnectMother()");
                                    break;
                                case "disconnect":
                                    await DisconnectMother();
                                    break;
                            }
                        }
                    });

                    await hubMotherConnection.StartAsync();
                }

            }
            catch (Exception ex)
            {
                ValidatorLogUtility.Log("Failed! Connecting to Adjudicator: Reason - " + ex.ToString(), "ConnectAdjudicator()");
            }
        }

        #endregion

        #region Disconnect Mother
        public static async Task DisconnectMother()
        {
            try
            {
                if (hubMotherConnection != null)
                {
                    if (IsMotherConnected)
                    {
                        await hubMotherConnection.DisposeAsync();
                        ConsoleWriterService.Output($"Success! Disconnected from Mother on: {DateTime.Now}");
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError("Failed! Did not disconnect from Mother: Reason - " + ex.ToString(), "DisconnectMother()");
            }
        }


        #endregion

        #region Send Mother Data
        public static async Task SendMotherData()
        {
            bool result = false;
            try
            {
                if (IsMotherConnected)
                {
                    if (hubMotherConnection != null)
                    {
                        var accounts = AccountData.GetAccounts();
                        var localValidator = accounts.FindOne(x => x.IsValidating == true);
                        var validators = Validators.Validator.GetAll();
                        var validator = validators.FindOne(x => x.Address == localValidator.Address);
                        var peerCount = P2PServer.GetConnectedPeerCount();
                        var lastTaskBlock =  Globals.AdjNodes.Values.Where(x => x.IsConnected).OrderByDescending(x => x.LastSentBlockHeight).FirstOrDefault();
                        var lastTaskSent = Globals.AdjNodes.Values.Where(x => x.IsConnected).OrderByDescending(x => x.LastTaskSentTime).FirstOrDefault();

                        Mother.DataPayload mPayload = new Mother.DataPayload {
                            Address = Globals.ValidatorAddress,
                            Balance = accounts.FindAll().Sum(x => x.Balance),
                            BlockHeight = Globals.LastBlock.Height,
                            IsValidating = true,
                            ValidatorName = validator.UniqueName,
                            PeerCount = peerCount,
                            LastTaskBlockSent = lastTaskBlock != null ? lastTaskBlock.LastTaskBlockHeight : 0,
                            LastTaskSent = lastTaskSent != null ? lastTaskSent.LastTaskSentTime : null
                        };

                        var jsonPayload  = JsonConvert.SerializeObject(mPayload);

                        var response = await hubMotherConnection.InvokeCoreAsync<bool>("SendMotherData", args: new object?[] { jsonPayload });
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError($"Unknown Error: {ex.ToString()}", "P2PMotherClient.SendMotherData() - catch");
            }

        }

        #endregion
    }
}
