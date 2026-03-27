using MyOwnPo.Models;

namespace MyOwnPo.Services;

public interface IContextFileStore
{
    ProjectContext? Load();
    void Save(ProjectContext context);
    void Delete();
}
