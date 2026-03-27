using MyOwnPo.Models;

namespace MyOwnPo.Services;

public interface IProjectContextService
{
    void SetContext(ProjectContext context);
    void UpdateContext(Action<ProjectContext> updater);
    ProjectContext? GetContext();
    void ClearContext();
    bool HasContext { get; }
    ContextLoadResult LoadFromFile();
}