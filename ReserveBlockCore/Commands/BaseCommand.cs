using ReserveBlockCore.Services;
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
                case "/menu":
                    StartupService.MainMenu();
                    break;
                case "1":
                    //Insert Method
                    break;
                case "2":
                    //Insert Method
                    break;
                case "3":
                    //Insert Method
                    break;
                case "4":
                    //Insert Method
                    break;
                case "5":
                    //Insert Method
                    break;
                case "6":
                    //Insert Method
                    break;
                case "7":
                    //Insert Method
                    break;
                case "8":
                    //Insert Method
                    break;
                case "9":
                    //Insert Method
                    break;
                case "10":
                    //Insert Method
                    break;

                default:
                    commandResult = "Not a recognized command. Please Try Again...";
                    break;
            }

            return commandResult;

        }


    }
}
