using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReserveBlockCore.Commands
{
    internal class BaseCommand
    {
        internal static string ProcessCommand(string command)
        {
            var commandResult = string.Empty;

            switch (command)
            {
                case "/help":
                    commandResult = "Help Command List Goes Here...";
                    break;
                case "/exit":
                    commandResult = "_EXIT";
                    break;
                default:
                    commandResult = "Not a recognized command. Please Try Again...";
                    break;
            }

            return commandResult;

        }
    }
}
