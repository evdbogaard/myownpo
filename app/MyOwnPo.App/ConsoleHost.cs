using MyOwnPo.Models;
using MyOwnPo.Services;

namespace MyOwnPo;

public class ConsoleHost(IBacklogService backlogService, IPrioritizationService prioritizationService, IProjectContextService projectContextService, TextReader input, TextWriter output)
{
    private readonly IBacklogService _backlogService = backlogService;
    private readonly IPrioritizationService _prioritizationService = prioritizationService;
    private readonly IProjectContextService _projectContextService = projectContextService;
    private readonly TextReader _input = input;
    private readonly TextWriter _output = output;

    public ConsoleHost(IBacklogService backlogService, IPrioritizationService prioritizationService, IProjectContextService projectContextService)
        : this(backlogService, prioritizationService, projectContextService, Console.In, Console.Out)
    {
    }

    public async Task Run(CancellationToken cancellationToken = default)
    {
        var loadResult = _projectContextService.LoadFromFile();
        switch (loadResult)
        {
            case ContextLoadResult.Loaded:
                var context = _projectContextService.GetContext()!;
                _output.WriteLine("Project context loaded from file:");
                WriteContextField("Vision", context.Vision);
                WriteContextField("Business goals", context.BusinessGoals);
                WriteContextField("Target users", context.TargetUsers);
                WriteContextField("Sprint focus", context.SprintFocus);
                WriteContextField("Constraints", context.Constraints);
                break;
            case ContextLoadResult.Malformed:
                _output.WriteLine("Warning: Could not read project context file. Starting without context.");
                break;
        }

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
                if (command.StartsWith("context ", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(command, "context", StringComparison.OrdinalIgnoreCase))
                {
                    HandleContext(command);
                }
                else
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

    private void HandleContext(string command)
    {
        var subCommand = command.Length > "context".Length
            ? command["context ".Length..].Trim()
            : string.Empty;

        if (string.Equals(subCommand, "set", StringComparison.OrdinalIgnoreCase))
            HandleContextSet();
        else if (string.Equals(subCommand, "show", StringComparison.OrdinalIgnoreCase))
            HandleContextShow();
        else if (string.Equals(subCommand, "clear", StringComparison.OrdinalIgnoreCase))
            HandleContextClear();
        else
            _output.WriteLine("Unknown context command. Use: context set, context show, context clear.");
    }

    private void HandleContextSet()
    {
        _output.WriteLine("Provide project context (press Enter to skip a field):");

        _output.Write("  Vision: ");
        var vision = _input.ReadLine();

        _output.Write("  Business goals: ");
        var goals = _input.ReadLine();

        _output.Write("  Target users: ");
        var users = _input.ReadLine();

        _output.Write("  Sprint focus: ");
        var sprint = _input.ReadLine();

        _output.Write("  Constraints: ");
        var constraints = _input.ReadLine();

        var context = new ProjectContext
        {
            Vision = NullIfEmpty(vision),
            BusinessGoals = NullIfEmpty(goals),
            TargetUsers = NullIfEmpty(users),
            SprintFocus = NullIfEmpty(sprint),
            Constraints = NullIfEmpty(constraints)
        };

        _projectContextService.SetContext(context);
        _output.WriteLine(context.IsEmpty ? "Context cleared (all fields were empty)." : "Project context updated.");
    }

    private void HandleContextShow()
    {
        var context = _projectContextService.GetContext();
        if (context is null || context.IsEmpty)
        {
            _output.WriteLine("No project context set. Use 'context set' to provide context.");
            return;
        }

        _output.WriteLine("Current project context:");
        WriteContextField("Vision", context.Vision);
        WriteContextField("Business goals", context.BusinessGoals);
        WriteContextField("Target users", context.TargetUsers);
        WriteContextField("Sprint focus", context.SprintFocus);
        WriteContextField("Constraints", context.Constraints);
    }

    private void HandleContextClear()
    {
        _projectContextService.ClearContext();
        _output.WriteLine("Project context cleared.");
    }

    private void WriteContextField(string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            _output.WriteLine($"  {label}: {value}");
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

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
        _output.WriteLine("- connect       : connect and ingest user stories");
        _output.WriteLine("- refresh       : refresh backlog and show diff");
        _output.WriteLine("- context set   : set project context (vision, goals, etc.)");
        _output.WriteLine("- context show  : show current project context");
        _output.WriteLine("- context clear : remove project context");
        _output.WriteLine("- help          : show command help");
        _output.WriteLine("- exit          : quit the app");
        _output.WriteLine();
        _output.WriteLine("Or type a question to chat with the AI Product Owner.");
    }
}