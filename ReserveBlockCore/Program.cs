using ReserveBlockCore.Commands;


Console.WriteLine("Starting up Reserve Block Wallet");

Thread.Sleep(1000);

Console.WriteLine("Wallet Started. Awaiting Command...");

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