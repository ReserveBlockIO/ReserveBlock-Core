using ReserveBlockCore.Commands;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;

StartupService.StartupDatabase();
StartupService.StartupInitializeChain();
StartupService.StartupMenu();
Thread.Sleep(1000);
StartupService.MainMenu();

while (true)
{
    var command = Console.ReadLine();

    if (command != "" || command != null)
    {
        var commandResult = BaseCommand.ProcessCommand(command);

        if (commandResult == "_EXIT")
        {
            Console.WriteLine("Closing and Exiting Wallet Application.");
            Environment.Exit(0);
        }

        Console.WriteLine(commandResult);
    }
    else
    {
        Console.WriteLine("Please enter a command...");
    }

}