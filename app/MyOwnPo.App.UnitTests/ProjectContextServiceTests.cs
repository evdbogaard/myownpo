using System.Text.Json;

using Moq;

using MyOwnPo.Models;
using MyOwnPo.Services;
using MyOwnPo.Services.Interfaces;

using Xunit;

namespace MyOwnPo.UnitTests;

public class ProjectContextServiceTests
{
	private static ProjectContextService CreateService(Mock<IContextFileStore>? fileStore = null)
	{
		return new ProjectContextService((fileStore ?? new Mock<IContextFileStore>(MockBehavior.Loose)).Object);
	}

	[Fact]
	public void SetContext_ValidContext_StoresContext()
	{
		var sut = CreateService();
		var context = new ProjectContext { Vision = "Build the best tool" };

		sut.SetContext(context);

		Assert.Same(context, sut.GetContext());
	}

	[Fact]
	public void SetContext_CalledTwice_ReplacesContext()
	{
		var sut = CreateService();
		var first = new ProjectContext { Vision = "First" };
		var second = new ProjectContext { Vision = "Second" };

		sut.SetContext(first);
		sut.SetContext(second);

		Assert.Same(second, sut.GetContext());
	}

	[Fact]
	public void GetContext_NoContextSet_ReturnsNull()
	{
		var sut = CreateService();

		Assert.Null(sut.GetContext());
	}

	[Fact]
	public void HasContext_AfterSet_ReturnsTrue()
	{
		var sut = CreateService();
		sut.SetContext(new ProjectContext { Vision = "Vision" });

		Assert.True(sut.HasContext);
	}

	[Fact]
	public void HasContext_BeforeSet_ReturnsFalse()
	{
		var sut = CreateService();

		Assert.False(sut.HasContext);
	}

	[Fact]
	public void UpdateContext_ExistingContext_AppliesUpdate()
	{
		var sut = CreateService();
		sut.SetContext(new ProjectContext { Vision = "Old vision", SprintFocus = "Performance" });

		sut.UpdateContext(context => context.SprintFocus = "Onboarding");

		var result = sut.GetContext();
		Assert.NotNull(result);
		Assert.Equal("Old vision", result.Vision);
		Assert.Equal("Onboarding", result.SprintFocus);
	}

	[Fact]
	public void UpdateContext_NoExistingContext_CreatesNew()
	{
		var sut = CreateService();

		sut.UpdateContext(context => context.Vision = "New vision");

		var result = sut.GetContext();
		Assert.NotNull(result);
		Assert.Equal("New vision", result.Vision);
	}

	[Fact]
	public void ClearContext_AfterSet_RemovesContext()
	{
		var sut = CreateService();
		sut.SetContext(new ProjectContext { Vision = "Vision" });

		sut.ClearContext();

		Assert.Null(sut.GetContext());
		Assert.False(sut.HasContext);
	}

	[Fact]
	public void ClearContext_WithFile_DeletesFile()
	{
		var fileStore = new Mock<IContextFileStore>(MockBehavior.Strict);
		fileStore.Setup(mock => mock.Save(It.IsAny<ProjectContext>()));
		fileStore.Setup(mock => mock.Delete());
		var sut = CreateService(fileStore);
		sut.SetContext(new ProjectContext { Vision = "Vision" });

		sut.ClearContext();

		fileStore.Verify(mock => mock.Delete(), Times.Once);
	}

	[Fact]
	public void ClearContext_NoFile_DoesNotThrow()
	{
		var fileStore = new Mock<IContextFileStore>(MockBehavior.Strict);
		fileStore.Setup(mock => mock.Delete());
		var sut = CreateService(fileStore);

		var exception = Record.Exception(() => sut.ClearContext());

		Assert.Null(exception);
		fileStore.Verify(mock => mock.Delete(), Times.Once);
	}

	[Fact]
	public void SetThenClearThenRestart_LoadFromFile_ReturnsNoFile()
	{
		var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempDir);

		try
		{
			var filePath = Path.Combine(tempDir, "context.json");
			var fileStore = new JsonContextFileStore(filePath);

			var firstSession = new ProjectContextService(fileStore);
			firstSession.SetContext(new ProjectContext { Vision = "Persisted context" });
			firstSession.ClearContext();

			var secondSession = new ProjectContextService(fileStore);
			var result = secondSession.LoadFromFile();

			Assert.Equal(ContextLoadResult.NoFile, result);
			Assert.False(secondSession.HasContext);
			Assert.Null(secondSession.GetContext());
		}
		finally
		{
			if (Directory.Exists(tempDir))
				Directory.Delete(tempDir, recursive: true);
		}
	}

	[Fact]
	public void LoadFromFile_FileExists_LoadsContext()
	{
		var fileStore = new Mock<IContextFileStore>(MockBehavior.Strict);
		fileStore.Setup(mock => mock.Load())
			.Returns(new ProjectContext { Vision = "Loaded vision", BusinessGoals = "Growth" });
		var sut = CreateService(fileStore);

		var result = sut.LoadFromFile();

		Assert.Equal(ContextLoadResult.Loaded, result);
		Assert.True(sut.HasContext);
		Assert.Equal("Loaded vision", sut.GetContext()!.Vision);
		Assert.Equal("Growth", sut.GetContext()!.BusinessGoals);
	}

	[Fact]
	public void LoadFromFile_NoFile_RemainsEmpty()
	{
		var fileStore = new Mock<IContextFileStore>(MockBehavior.Strict);
		fileStore.Setup(mock => mock.Load())
			.Returns((ProjectContext?)null);
		var sut = CreateService(fileStore);

		var result = sut.LoadFromFile();

		Assert.Equal(ContextLoadResult.NoFile, result);
		Assert.False(sut.HasContext);
	}

	[Fact]
	public void LoadFromFile_MalformedFile_RemainsEmpty()
	{
		var fileStore = new Mock<IContextFileStore>(MockBehavior.Strict);
		fileStore.Setup(mock => mock.Load())
			.Throws(new JsonException("Invalid JSON"));
		var sut = CreateService(fileStore);

		var result = sut.LoadFromFile();

		Assert.Equal(ContextLoadResult.Malformed, result);
		Assert.False(sut.HasContext);
	}

	[Fact]
	public void SetContext_ValidContext_PersistsToFile()
	{
		var fileStore = new Mock<IContextFileStore>(MockBehavior.Strict);
		var context = new ProjectContext { Vision = "Persist me" };
		fileStore.Setup(mock => mock.Save(context));
		var sut = CreateService(fileStore);

		sut.SetContext(context);

		fileStore.Verify(mock => mock.Save(context), Times.Once);
	}

	[Fact]
	public void UpdateContext_ExistingContext_PersistsToFile()
	{
		var fileStore = new Mock<IContextFileStore>(MockBehavior.Loose);
		var sut = CreateService(fileStore);
		sut.SetContext(new ProjectContext { Vision = "Original" });
		fileStore.Invocations.Clear();

		sut.UpdateContext(ctx => ctx.SprintFocus = "New focus");

		fileStore.Verify(mock => mock.Save(It.Is<ProjectContext>(c => c.SprintFocus == "New focus")), Times.Once);
	}

	[Fact]
	public void SetContext_SaveThrows_RetainsInMemoryContext()
	{
		var fileStore = new Mock<IContextFileStore>(MockBehavior.Strict);
		var context = new ProjectContext { Vision = "Survive failure" };
		fileStore.Setup(mock => mock.Save(context)).Throws(new IOException("disk full"));
		var sut = CreateService(fileStore);

		Assert.Throws<IOException>(() => sut.SetContext(context));

		Assert.True(sut.HasContext);
		Assert.Same(context, sut.GetContext());
	}
}