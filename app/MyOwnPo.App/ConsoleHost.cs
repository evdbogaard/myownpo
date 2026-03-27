using MyOwnPo.Models;
using MyOwnPo.Services;

namespace MyOwnPo;

public class ConsoleHost(IBacklogService backlogService)
{
    private readonly IBacklogService _backlogService = backlogService;

    public async Task Run(CancellationToken cancellationToken = default)
    {
        WriteHelp();

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write("> ");
            var input = Console.ReadLine();
            if (input is null)
                continue;

            var command = input.Trim();
            if (command.Length == 0)
                continue;

            if (string.Equals(command, "exit", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "quit", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Bye.");
                return;
            }

            try
            {
                switch (command.ToLowerInvariant())
                {
                    case "help":
                        WriteHelp();
                        break;
                    case "connect":
                        await HandleConnect();
                        break;
                    case "refresh":
                        await HandleRefresh();
                        break;
                    default:
                        Console.WriteLine($"Unknown command: '{command}'.");
                        WriteHelp();
                        break;
                }
            }
            catch (BacklogCapExceededException ex)
            {
                Console.WriteLine($"Backlog too large: {ex.StoryCount} stories found. Narrow scope (for example by area path) and try again.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Operation failed: {ex.Message}");
            }
        }
    }

    private async Task HandleConnect()
    {
        var stories = await _backlogService.Connect();
        Console.WriteLine($"Connected. Stories found: {stories.Count}.");

        if (stories.Count == 0)
        {
            Console.WriteLine("No user stories were found in the configured backlog.");
            return;
        }

        Console.WriteLine("Titles:");
        foreach (var story in stories.OrderBy(story => story.Title, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"- [{story.Id}] {story.Title}");
        }

        WriteMissingFieldWarnings(stories);
    }

    private async Task HandleRefresh()
    {
        var diff = await _backlogService.Refresh();
        if (!diff.HasChanges)
        {
            Console.WriteLine("Refresh complete. No changes detected.");
            return;
        }

        Console.WriteLine("Refresh complete.");
        WriteDiff("Added", diff.Added);
        WriteDiff("Removed", diff.Removed);
        WriteDiff("Changed", diff.Changed);
        WriteMissingFieldWarnings(_backlogService.GetStories());
    }

    private static void WriteMissingFieldWarnings(IEnumerable<UserStory> stories)
    {
        var incompleteStories = stories
            .Where(story => story.MissingFields.Count > 0)
            .ToList();
        if (incompleteStories.Count == 0)
            return;

        Console.WriteLine("Stories with missing fields:");
        foreach (var story in incompleteStories)
        {
            Console.WriteLine($"- [{story.Id}] {story.Title}: {string.Join(", ", story.MissingFields)}");
        }
    }

    private static void WriteDiff(string label, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            Console.WriteLine($"{label}: none");
            return;
        }

        Console.WriteLine($"{label} ({values.Count}):");
        foreach (var value in values)
        {
            Console.WriteLine($"- {value}");
        }
    }

    private static void WriteHelp()
    {
        Console.WriteLine("Commands:");
        Console.WriteLine("- connect : connect and ingest user stories");
        Console.WriteLine("- refresh : refresh backlog and show diff");
        Console.WriteLine("- help    : show command help");
        Console.WriteLine("- exit    : quit the app");
    }
}