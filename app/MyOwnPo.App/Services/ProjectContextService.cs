using System.Text.Json;

using MyOwnPo.Models;
using MyOwnPo.Services.Interfaces;

namespace MyOwnPo.Services;

public class ProjectContextService(IContextFileStore contextFileStore) : IProjectContextService
{
	private readonly IContextFileStore _contextFileStore = contextFileStore;
	private ProjectContext? _context;

	public bool HasContext => _context is not null;

	public void SetContext(ProjectContext context)
	{
		_context = context;
		_contextFileStore.Save(context);
	}

	public void UpdateContext(Action<ProjectContext> updater)
	{
		_context ??= new ProjectContext();
		updater(_context);
		_contextFileStore.Save(_context);
	}

	public ProjectContext? GetContext() => _context;

	public void ClearContext()
	{
		_context = null;
		_contextFileStore.Delete();
	}

	public ContextLoadResult LoadFromFile()
	{
		try
		{
			var context = _contextFileStore.Load();
			if (context is null || context.IsEmpty)
				return ContextLoadResult.NoFile;

			_context = context;
			return ContextLoadResult.Loaded;
		}
		catch (JsonException)
		{
			return ContextLoadResult.Malformed;
		}
	}
}