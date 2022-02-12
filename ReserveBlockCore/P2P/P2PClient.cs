using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReserveBlockCore.P2P
{
    public class P2PClient
    {
        public static void GetBlock() //base example
        {
            var connection = new HubConnectionBuilder().WithUrl("https://localhost:3338/blockchain").Build();

            connection.StartAsync().Wait();
            connection.InvokeCoreAsync("SendMessage", args: new[] { "NodeIP", "hello this is my message" });
            connection.On("ReceivedMessage", (string node, string message) => {
                Console.WriteLine(node + " - Message: " + message);
            });
        }
    }
}
