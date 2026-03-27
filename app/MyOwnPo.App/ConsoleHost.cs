using MyOwnPo.Models;
using MyOwnPo.Services;

namespace MyOwnPo;

public class ConsoleHost(IBacklogService backlogService, IPrioritizationService prioritizationService, TextReader input, TextWriter output)
{
    private readonly IBacklogService _backlogService = backlogService;
    private readonly IPrioritizationService _prioritizationService = prioritizationService;
    private readonly TextReader _input = input;
    private readonly TextWriter _output = output;

    public ConsoleHost(IBacklogService backlogService, IPrioritizationService prioritizationService)
        : this(backlogService, prioritizationService, Console.In, Console.Out)
    {
    }

    public async Task Run(CancellationToken cancellationToken = default)
    {
        WriteHelp();

        while (!cancellationToken.IsCancellationRequested)
        {
            _output.Write("> ");
            var line = _input.ReadLine();
            if (line is null)
                continue;

            var command = line.Trim();
            if (command.Length == 0)
                continue;

            if (string.Equals(command, "exit", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "quit", StringComparison.OrdinalIgnoreCase))
            {
                _output.WriteLine("Bye.");
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
                        await HandleChat(command);
                        break;
                }
            }
            catch (BacklogCapExceededException ex)
            {
                _output.WriteLine($"Backlog too large: {ex.StoryCount} stories found. Narrow scope (for example by area path) and try again.");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Operation failed: {ex.Message}");
            }
        }
    }

    private async Task HandleConnect()
    {
        var stories = await _backlogService.Connect();
        _output.WriteLine($"Connected. Stories found: {stories.Count}.");

        if (stories.Count == 0)
        {
            _output.WriteLine("No user stories were found in the configured backlog.");
            return;
        }

        _output.WriteLine("Titles:");
        foreach (var story in stories.OrderBy(story => story.Title, StringComparer.OrdinalIgnoreCase))
        {
            _output.WriteLine($"- [{story.Id}] {story.Title}");
        }

        WriteMissingFieldWarnings(stories);
    }

    private async Task HandleRefresh()
    {
        var diff = await _backlogService.Refresh();
        if (!diff.HasChanges)
        {
            _output.WriteLine("Refresh complete. No changes detected.");
            return;
        }

        _output.WriteLine("Refresh complete.");
        WriteDiff("Added", diff.Added);
        WriteDiff("Removed", diff.Removed);
        WriteDiff("Changed", diff.Changed);
        WriteMissingFieldWarnings(_backlogService.GetStories());
    }

    private async Task HandleChat(string userMessage)
    {
        if (_backlogService.GetStories().Count == 0)
        {
            _output.WriteLine("No backlog loaded. Use 'connect' to load stories first.");
            return;
        }

        var response = await _prioritizationService.Chat(userMessage);
        _output.WriteLine(response);
    }

    private void WriteMissingFieldWarnings(IEnumerable<UserStory> stories)
    {
        var incompleteStories = stories
            .Where(story => story.MissingFields.Count > 0)
            .ToList();
        if (incompleteStories.Count == 0)
            return;

        _output.WriteLine("Stories with missing fields:");
        foreach (var story in incompleteStories)
        {
            _output.WriteLine($"- [{story.Id}] {story.Title}: {string.Join(", ", story.MissingFields)}");
        }
    }

    private void WriteDiff(string label, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            _output.WriteLine($"{label}: none");
            return;
        }

        _output.WriteLine($"{label} ({values.Count}):");
        foreach (var value in values)
        {
            _output.WriteLine($"- {value}");
        }
    }

    private void WriteHelp()
    {
        _output.WriteLine("Commands:");
        _output.WriteLine("- connect : connect and ingest user stories");
        _output.WriteLine("- refresh : refresh backlog and show diff");
        _output.WriteLine("- help    : show command help");
        _output.WriteLine("- exit    : quit the app");
        _output.WriteLine();
        _output.WriteLine("Or type a question to chat with the AI Product Owner.");
    }
}