using ReserveBlockCore.Models;
using ReserveBlockCore.Services;
using Spectre.Console;

namespace ReserveBlockCore.Voting
{
    public class Voting
    {
        public static async Task StartVoteProgram()
        {
            await VoteMenu();

            bool exit = false;
            while(!exit)
            {
                var command = Console.ReadLine();
                if(!string.IsNullOrEmpty(command))
                {
                    var result = ProcessCommand(command);
                    if (result == CommandResult.VoteMenu)
                        await VoteMenu();
                    if (result == CommandResult.MainMenu)
                        exit = true;

                }
            }
            Console.WriteLine("Return you to main menu in 5 seconds...");
            Thread.Sleep(5000);
            StartupService.MainMenu();
        }

        private static CommandResult ProcessCommand(string command)
        {
            CommandResult result = CommandResult.Nothing;

            switch (command.ToLower())
            {
                case "/vote":
                    result = CommandResult.VoteMenu;
                    break;
                case "/menu":
                    result = CommandResult.MainMenu;
                    break;
                case "1":
                    
                    break;
                case "2":
                    GetActiveTopics();
                    break;
                case "3":
                    GetInactiveTopics();
                    break;
                case "4":
                    SearchTopic();
                    break;
                case "5":
                    
                    break;
                case "6":

                    break;
                case "7":
                    result = CommandResult.MainMenu;
                    break;
            }

            return result;
        }

        private enum CommandResult
        {
            MainMenu,
            VoteMenu,
            Nothing
        }

        private static void SearchTopic()
        {
            Console.WriteLine("Please enter a search term (Topic UID, Name, or Owner Address)...");
            var search = Console.ReadLine();
            if(!string.IsNullOrEmpty (search))
            {
                var topics = TopicTrei.GetTopics();
                if(topics != null)
                {
                    search = search.ToLower();
                    var results = topics.Query()
                        .Where(x => x.TopicOwnerAddress.ToLower().Contains(search) ||
                            x.TopicUID.ToLower().Contains(search) ||
                            x.TopicName.ToLower().Contains(search))
                        .ToList();

                    if(results.Count > 0)
                    {
                        var table = new Table();

                        table.Title("[yellow]RBX Search Topic Results[/]").Centered();
                        table.AddColumn(new TableColumn(new Panel("Topic ID")));
                        table.AddColumn(new TableColumn(new Panel("Topic Owner")));
                        table.AddColumn(new TableColumn(new Panel("Name")));
                        table.AddColumn(new TableColumn(new Panel("Description")));
                        table.AddColumn(new TableColumn(new Panel("Start Date")));
                        table.AddColumn(new TableColumn(new Panel("End Date")));
                        table.AddColumn(new TableColumn(new Panel("Topic Cat.")));
                        table.AddColumn(new TableColumn(new Panel("Votes Yes")));
                        table.AddColumn(new TableColumn(new Panel("Votes No")));
                        table.AddColumn(new TableColumn(new Panel("Passing Vote? (51%>)")));

                        foreach (var topic in results)
                        {
                            table.AddRow(
                                topic.TopicUID,
                                (topic.TopicOwnerAddress.Substring(0, 8) + "..."),
                                topic.TopicName,
                                topic.TopicDescription,
                                topic.TopicCreateDate.ToString(),
                                topic.VotingEndDate.ToString(),
                                topic.VoteTopicCategory.ToString(),
                                (topic.PercentVotesYes.ToString() + "%"),
                                (topic.PercentVotesNo.ToString() + "%"),
                                (topic.PercentInFavor.ToString() + "%")
                                );

                        }

                        table.Border(TableBorder.Rounded);

                        AnsiConsole.Write(table);
                    }
                    else
                    {
                        Console.WriteLine("No results found");
                    }
                }
                else
                {
                    Console.WriteLine("No topics found to search. Most likely due to no topics existing on network.");
                }
            }
            else
            {
                Console.WriteLine("Search cannot be empty");
            }
        }

        private static void GetActiveTopics()
        {
            var topics = TopicTrei.GetActiveTopics();
            if(topics != null)
            {
                Console.Clear();
                Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop);

                AnsiConsole.Write(
                new FigletText("RBX Topics")
                .Centered()
                .Color(Color.Green));

                var table = new Table();

                table.Title("[yellow]RBX Active Topics[/]").Centered();
                table.AddColumn(new TableColumn(new Panel("Topic ID")));
                table.AddColumn(new TableColumn(new Panel("Topic Owner")));
                table.AddColumn(new TableColumn(new Panel("Name")));
                table.AddColumn(new TableColumn(new Panel("Description")));
                table.AddColumn(new TableColumn(new Panel("Start Date")));
                table.AddColumn(new TableColumn(new Panel("End Date")));
                table.AddColumn(new TableColumn(new Panel("Topic Cat.")));
                table.AddColumn(new TableColumn(new Panel("Votes Yes")));
                table.AddColumn(new TableColumn(new Panel("Votes No")));
                table.AddColumn(new TableColumn(new Panel("Passing Vote? (51%>)")));

                foreach(var topic in topics)
                {
                    table.AddRow(
                        topic.TopicUID,
                        (topic.TopicOwnerAddress.Substring(0, 8) + "..."),
                        topic.TopicName,
                        topic.TopicDescription,
                        topic.TopicCreateDate.ToString(),
                        topic.VotingEndDate.ToString(),
                        topic.VoteTopicCategory.ToString(),
                        (topic.PercentVotesYes.ToString() + "%"),
                        (topic.PercentVotesNo.ToString() + "%"),
                        (topic.PercentInFavor.ToString() + "%")
                        );

                }

                table.Border(TableBorder.Rounded);

                AnsiConsole.Write(table);
            }
        }

        private static void GetInactiveTopics()
        {
            var topics = TopicTrei.GetInactiveTopics();
            if (topics != null)
            {
                Console.Clear();
                Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop);

                AnsiConsole.Write(
                new FigletText("RBX Topics")
                .Centered()
                .Color(Color.Green));

                var table = new Table();

                table.Title("[yellow]RBX Inactive Topics[/]").Centered();
                table.AddColumn(new TableColumn(new Panel("Topic ID")));
                table.AddColumn(new TableColumn(new Panel("Topic Owner")));
                table.AddColumn(new TableColumn(new Panel("Name")));
                table.AddColumn(new TableColumn(new Panel("Description")));
                table.AddColumn(new TableColumn(new Panel("Start Date")));
                table.AddColumn(new TableColumn(new Panel("End Date")));
                table.AddColumn(new TableColumn(new Panel("Topic Cat.")));
                table.AddColumn(new TableColumn(new Panel("Votes Yes")));
                table.AddColumn(new TableColumn(new Panel("Votes No")));
                table.AddColumn(new TableColumn(new Panel("Passing Vote? (51%>)")));

                foreach (var topic in topics)
                {
                    table.AddRow(
                        topic.TopicUID,
                        (topic.TopicOwnerAddress.Substring(0, 8) + "..."),
                        topic.TopicName,
                        topic.TopicDescription,
                        topic.TopicCreateDate.ToString(),
                        topic.VotingEndDate.ToString(),
                        topic.VoteTopicCategory.ToString(),
                        (topic.PercentVotesYes.ToString() + "%"),
                        (topic.PercentVotesNo.ToString() + "%"),
                        (topic.PercentInFavor.ToString() + "%")
                        );
                }

                table.Border(TableBorder.Rounded);

                AnsiConsole.Write(table);
            }
        }

        private static async Task VoteMenu()
        {
            Console.Clear();
            Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop);

            if (Globals.IsTestNet != true)
            {
                AnsiConsole.Write(
                new FigletText("RBX Voting")
                .LeftAligned()
                .Color(Color.Blue));
            }
            else
            {
                AnsiConsole.Write(
                new FigletText("RBX Voting - TestNet")
                .LeftAligned()
                .Color(Color.Green));
            }

            if (Globals.IsTestNet != true)
            {
                Console.WriteLine("ReserveBlock Voting");
            }
            else
            {
                Console.WriteLine("ReserveBlock Voting **TestNet**");
            }

            Console.WriteLine("|======================================|");
            Console.WriteLine("| 1. Create Vote Topic                 |");
            Console.WriteLine("| 2. Show Active Topics                |");
            Console.WriteLine("| 3. Show Inactive Topics              |");
            Console.WriteLine("| 4. Search For Topic                  |");
            Console.WriteLine("| 5. Vote On Topic                     |");
            Console.WriteLine("| 7. Topic Details                     |");
            Console.WriteLine("| 8. Exit Voting Program               |");
            Console.WriteLine("|======================================|");
            Console.WriteLine("|type /vote to come back to main area  |");
            Console.WriteLine("|type /menu to come back to main area  |");
            Console.WriteLine("|======================================|");
        }
    }
}
