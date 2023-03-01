using Microsoft.AspNetCore.HttpOverrides;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using Spectre.Console;
using System.IO;

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
            await Task.Delay(5000);
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
                    CreateTopic();
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
                    VoteOnTopic();
                    break;
                case "6":
                    GetTopicDetails();
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

        private static async void GetTopicDetails()
        {
            Console.WriteLine("Please enter the topic ID");
            var topicUID = Console.ReadLine();
            if (!string.IsNullOrEmpty(topicUID))
            {
                var topic = TopicTrei.GetSpecificTopic(topicUID);
                if (topic != null)
                {
                    Console.Clear();
                    Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop);

                    var table = new Table();

                    table.Title("[yellow]Topic Info[/]").Centered();
                    table.AddColumn(new TableColumn(new Panel("Title")));
                    table.AddColumn(new TableColumn(new Panel("Description"))).Centered();

                    table.AddRow("[blue]Topic UID[/]", $"[green]{topic.TopicUID}[/]");
                    table.AddRow("[blue]Name[/]", $"[green]{topic.TopicName}[/]");
                    table.AddRow("[blue]Description[/]", $"[green]{topic.TopicDescription}[/]");
                    table.AddRow("[blue]Topic Creator[/]", $"[green]{topic.TopicOwnerAddress}[/]");
                    table.AddRow("[blue]Block Height[/]", $"[green]{topic.BlockHeight}[/]");
                    table.AddRow("[blue]Create Date[/]", $"[green]{topic.TopicCreateDate.ToLocalTime()}[/]");
                    table.AddRow("[blue]End Date[/]", $"[green]{topic.VotingEndDate.ToLocalTime()}[/]");
                    table.AddRow("[blue]Category[/]", $"[green]{topic.VoteTopicCategory}[/]");

                    table.AddRow("[blue]Total Votes[/]", $"[green]{topic.TotalVotes}[/]");
                    table.AddRow("[blue]Votes Yes[/]", $"[green]{topic.VoteYes}[/]");
                    table.AddRow("[blue]Votes No[/]", $"[green]{topic.VoteNo}[/]");

                    table.AddRow("[blue]Percent Votes Yes[/]", $"[green]{topic.PercentVotesYes}%[/]");
                    table.AddRow("[blue]Percent Votes No[/]", $"[green]{topic.PercentVotesNo}%[/]");
                    table.AddRow("[blue]Percent In Favor[/]", $"[green]{topic.PercentInFavor}%[/]");
                    table.AddRow("[blue]Percent Against[/]", $"[green]{topic.PercentAgainst}%[/]");


                    table.Border(TableBorder.Rounded);

                    AnsiConsole.Write(table);

                    Console.WriteLine("|========================================|");
                    Console.WriteLine("|type /vote to come back to the vote area|");
                    Console.WriteLine("|type /menu to come back to main area    |");
                    Console.WriteLine("|========================================|");
                }
                else
                {
                    await VoteMenu();
                    Console.WriteLine("Could not find topic. Returned you to vote menu.");
                }
            }
        }
        private static async void VoteOnTopic()
        {
            Console.WriteLine("Please enter the topic ID");
            var topicUID = Console.ReadLine();

            if (string.IsNullOrEmpty(Globals.ValidatorAddress))
            {
                Console.WriteLine("You must be a validator to vote on a topic.");
            }
            else if (!string.IsNullOrEmpty(topicUID))
            {
                var topic = TopicTrei.GetSpecificTopic(topicUID);
                if (topic != null)
                {
                    var currentTime = DateTime.UtcNow;
                    if (currentTime < topic.VotingEndDate)
                    {
                        Console.WriteLine($"Topic UID: {topic.TopicUID}");
                        Console.WriteLine($"Topic Name: {topic.TopicName}");
                        Console.WriteLine($"Please choose vote. ('y' for yes and 'n' for no.");
                        var voteChoice = Console.ReadLine();

                        if (!string.IsNullOrEmpty(voteChoice))
                        {
                            if (voteChoice == "y")
                            {
                                Vote.VoteCreate voteC = new Vote.VoteCreate
                                {
                                    TopicUID = topicUID,
                                    VoteType = VoteType.Yes
                                };

                                Vote vote = new Vote();

                                vote.Build(voteC);

                                var result = await Vote.CreateVoteTx(vote);

                                if (result.Item1 != null)
                                {
                                    Console.WriteLine(result.Item1.Hash);
                                }
                                else
                                {
                                    Console.WriteLine(result.Item2);
                                }
                            }

                            if (voteChoice == "n")
                            {
                                Vote.VoteCreate voteC = new Vote.VoteCreate
                                {
                                    TopicUID = topicUID,
                                    VoteType = VoteType.No
                                };

                                Vote vote = new Vote();

                                vote.Build(voteC);

                                var result = await Vote.CreateVoteTx(vote);

                                if (result.Item1 != null)
                                {
                                    Console.WriteLine(result.Item1.Hash);
                                }
                                else
                                {
                                    Console.WriteLine(result.Item2);
                                }
                            }


                        }
                        else
                        {
                            await VoteMenu();
                            Console.WriteLine("You must choose yes or no. Returned you to main menu.");
                        }
                    }
                    else
                    {
                        await VoteMenu();
                        Console.WriteLine("Voting for this topic has ended. Returned you to vote menu.");
                    }
                }
                else
                {
                    await VoteMenu();
                    Console.WriteLine("Could not find topic. Returned you to vote menu.");
                }
            }
            else
            {
                await VoteMenu();
                Console.WriteLine("You must enter a topic.");
            }
        }
        private static async void CreateTopic()
        {
            try
            {
                bool fail = false;
                if (string.IsNullOrEmpty(Globals.ValidatorAddress))
                {
                    fail = true;
                    Console.WriteLine("You must be a validator to vote on a topic.");
                }
                else
                {
                    Console.WriteLine("Enter name for Topic:");
                    var topicName = Console.ReadLine();

                    Console.WriteLine("Enter description for Topic:");
                    var topicDescription = Console.ReadLine();

                    Console.WriteLine("Please Choose a topic category:");
                    Console.WriteLine("0. General");
                    Console.WriteLine("1. Coding Changes");
                    Console.WriteLine("2. Add Developer(s)");
                    Console.WriteLine("3. Remove Developer(s)");
                    Console.WriteLine("4. Network Change");
                    Console.WriteLine("5. Adjudicator Vote In");
                    Console.WriteLine("6. Adjudicator Vote Out");
                    Console.WriteLine("7. Validating Change");
                    Console.WriteLine("8. Block Modify");
                    Console.WriteLine("9. Transaction Modify");
                    Console.WriteLine("10. Balance Correction");
                    Console.WriteLine("11. Hack or Exploitation Correction");

                    Console.WriteLine("12. Other");

                    var topicCat = Console.ReadLine();

                    if(!string.IsNullOrEmpty(topicCat))
                    {
                        if(topicCat == "5")
                        {
                            await VoteMenu();
                            Console.WriteLine("Adj Vote In must be started through GUI or API.");
                            return;
                        }
                    }

                    Console.WriteLine("Please choose voting days for Topic:");
                    Console.WriteLine("1. Thirty Days (30)");
                    Console.WriteLine("2. Sixty Days (60)");
                    Console.WriteLine("3. Ninety Days (90)");
                    Console.WriteLine("4. One-Hundred and Eighty Days (180)");
                    var topicEndDays = Console.ReadLine();

                    if (!string.IsNullOrEmpty(topicName) && !string.IsNullOrEmpty(topicDescription) && !string.IsNullOrEmpty(topicCat) && !string.IsNullOrEmpty(topicEndDays))
                    {
                        var topicCreate = new TopicTrei.TopicCreate();

                        if (topicCat == "12")
                        {
                            topicCreate.VoteTopicCategory = VoteTopicCategories.Other;
                        }
                        else
                        {
                            int topicCatNum;
                            var topicCatNumTry = int.TryParse(topicCat, out topicCatNum);

                            if (topicCatNumTry)
                            {
                                if (topicCatNum >= 0 && topicCatNum <= 11)
                                    topicCreate.VoteTopicCategory = (VoteTopicCategories)topicCatNum;
                            }
                            else
                            {
                                Console.WriteLine("Error. Incorrect number chosen for topic category.");
                                fail = true;
                            }
                        }

                        int topicDaysNum;
                        var topicDaysNumTry = int.TryParse(topicEndDays, out topicDaysNum);

                        if (topicDaysNumTry)
                        {
                            if (topicDaysNum == 1 || topicDaysNum == 2 || topicDaysNum == 3 || topicDaysNum == 4)
                            {
                                if (topicDaysNum == 1)
                                {
                                    topicCreate.VotingEndDays = VotingDays.Thirty;
                                }
                                if (topicDaysNum == 2)
                                {
                                    topicCreate.VotingEndDays = VotingDays.Sixty;
                                }
                                if (topicDaysNum == 3)
                                {
                                    topicCreate.VotingEndDays = VotingDays.Ninety;
                                }
                                if (topicDaysNum == 4)
                                {
                                    topicCreate.VotingEndDays = VotingDays.OneHundredEighty;
                                }
                            }
                            else
                            {
                                Console.WriteLine("Error. Incorrect number chosen for topic days.");
                                fail = true;
                            }

                        }

                        topicCreate.TopicName = topicName;
                        topicCreate.TopicDescription = topicDescription;

                        if (!fail)
                        {
                            var topic = new TopicTrei
                            {
                                TopicName = topicCreate.TopicName,
                                TopicDescription = topicCreate.TopicDescription,
                            };

                            topic.Build(topicCreate.VotingEndDays, topicCreate.VoteTopicCategory);

                            var result = await TopicTrei.CreateTopicTx(topic);

                            if (result.Item1 == null)
                            {
                                Console.WriteLine($"Topic Create Failed. Reason: {result.Item2}");
                            }
                            else
                            {
                                Console.WriteLine($"Success (TX ID): {result.Item1.Hash}");
                            }
                        }
                        else
                        {
                            await VoteMenu();
                            Console.WriteLine("Returned you to vote menu.");
                        }
                    }
                    else
                    {
                        await VoteMenu();
                        Console.WriteLine("Error. You cannot leave any of these fields blank.");
                    }
                }    
            }
            catch { }
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
                        .ToEnumerable();

                    if(results.Count() > 0)
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

                        Console.WriteLine("|========================================|");
                        Console.WriteLine("|type /vote to come back to the vote area|");
                        Console.WriteLine("|type /menu to come back to main area    |");
                        Console.WriteLine("|========================================|");
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

                Console.WriteLine("|========================================|");
                Console.WriteLine("|type /vote to come back to the vote area|");
                Console.WriteLine("|type /menu to come back to main area    |");
                Console.WriteLine("|========================================|");
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

                Console.WriteLine("|========================================|");
                Console.WriteLine("|type /vote to come back to the vote area|");
                Console.WriteLine("|type /menu to come back to main area    |");
                Console.WriteLine("|========================================|");
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
                .LeftJustified()
                .Color(Color.Blue));
            }
            else
            {
                AnsiConsole.Write(
                new FigletText("RBX Voting - TestNet")
                .LeftJustified()
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

            Console.WriteLine("|========================================|");
            Console.WriteLine("| 1. Create Vote Topic                   |");
            Console.WriteLine("| 2. Show Active Topics                  |");
            Console.WriteLine("| 3. Show Inactive Topics                |");
            Console.WriteLine("| 4. Search For Topic                    |");
            Console.WriteLine("| 5. Vote On Topic                       |");
            Console.WriteLine("| 6. Topic Details                       |");
            Console.WriteLine("| 7. Exit Voting Program                 |");
            Console.WriteLine("|========================================|");
            Console.WriteLine("|type /vote to come back to the vote area|");
            Console.WriteLine("|type /menu to come back to main area    |");
            Console.WriteLine("|========================================|");
        }
    }
}
