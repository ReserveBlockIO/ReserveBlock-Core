using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace ReserveBlockCore.P2P
{
    public class P2PServer : Hub
    {
        public async Task SendMessage(string node, string message)
        {
            await Clients.All.SendAsync("ReceivedMessage", node, message);
        }
    }
}
