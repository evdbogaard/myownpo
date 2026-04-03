using System.ComponentModel;
using System.Text.Json;

using MyOwnPo.Models;
using MyOwnPo.Services.Interfaces;

namespace MyOwnPo.App.Tools;

public class BacklogTools(IBacklogService backlogService)
{
    private readonly IBacklogService _backlogService = backlogService;
    private bool _hasAttemptedBacklogBootstrap = false;

    [Description("Retrieves backlog stories from memory. If empty, tries one automatic load attempt for this session.")]
    public async Task<string> GetBacklogStories()
    {
        var stories = await EnsureBacklogStories();

        return JsonSerializer.Serialize(new
        {
            Count = stories.Count,
            Stories = stories.Select(story => new
            {
                story.Id,
                story.Title,
                story.Description,
                story.AcceptanceCriteria,
                story.Priority,
                story.Labels,
                story.Status,
                story.MissingFields
            }),
            Message = stories.Count == 0
                ? "No backlog stories available. Run connect to load stories manually."
                : null
        });
    }

    private async Task<IReadOnlyList<UserStory>> EnsureBacklogStories()
    {
        var stories = _backlogService.GetStories();
        if (stories.Count > 0)
            return stories;

        if (_hasAttemptedBacklogBootstrap)
            return stories;

        _hasAttemptedBacklogBootstrap = true;
        try
        {
            return await _backlogService.Connect();
        }
        catch
        {
            return _backlogService.GetStories();
        }
    }
}