using MyOwnPo.Models;

namespace MyOwnPo.Services;

public class ProjectContextService : IProjectContextService
{
    private ProjectContext? _context;

    public bool HasContext => _context is not null;

    public void SetContext(ProjectContext context)
    {
        _context = context;
    }

    public void UpdateContext(Action<ProjectContext> updater)
    {
        _context ??= new ProjectContext();
        updater(_context);
    }

    public ProjectContext? GetContext() => _context;

    public void ClearContext()
    {
        _context = null;
    }
}