using MyOwnPo.Models;

namespace MyOwnPo.Services.Interfaces;

public interface IContextFileStore
{
	ProjectContext? Load();
	void Save(ProjectContext context);
	void Delete();
}