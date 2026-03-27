using System.Text.Json;

using MyOwnPo.Models;
using MyOwnPo.Services;

using Xunit;

namespace MyOwnPo.UnitTests;

public class JsonContextFileStoreTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string TempFile(string name = "context.json") => Path.Combine(_tempDir, name);

    private JsonContextFileStore CreateStore(string? fileName = null)
    {
        Directory.CreateDirectory(_tempDir);
        return new JsonContextFileStore(TempFile(fileName ?? "context.json"));
    }

    [Fact]
    public void Load_ValidFile_ReturnsContext()
    {
        var sut = CreateStore();
        File.WriteAllText(TempFile(), """
			{
				"Vision": "Build the best tool",
				"BusinessGoals": "Revenue growth",
				"TargetUsers": "Enterprise teams",
				"SprintFocus": "Performance",
				"Constraints": "Limited budget"
			}
			""");

        var result = sut.Load();

        Assert.NotNull(result);
        Assert.Equal("Build the best tool", result.Vision);
        Assert.Equal("Revenue growth", result.BusinessGoals);
        Assert.Equal("Enterprise teams", result.TargetUsers);
        Assert.Equal("Performance", result.SprintFocus);
        Assert.Equal("Limited budget", result.Constraints);
    }

    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        var sut = new JsonContextFileStore(TempFile("nonexistent.json"));

        var result = sut.Load();

        Assert.Null(result);
    }

    [Fact]
    public void Load_EmptyFile_ReturnsNull()
    {
        var sut = CreateStore();
        File.WriteAllText(TempFile(), "");

        var result = sut.Load();

        Assert.Null(result);
    }

    [Fact]
    public void Load_MalformedJson_ThrowsJsonException()
    {
        var sut = CreateStore();
        File.WriteAllText(TempFile(), "{ broken json }");

        Assert.Throws<JsonException>(() => sut.Load());
    }

    [Fact]
    public void Save_ValidContext_WritesJsonFile()
    {
        var sut = CreateStore();
        var context = new ProjectContext
        {
            Vision = "Best tool",
            BusinessGoals = "Grow revenue",
            TargetUsers = "Developers",
            SprintFocus = "Onboarding",
            Constraints = "No budget"
        };

        sut.Save(context);

        Assert.True(File.Exists(TempFile()));
        var loaded = sut.Load();
        Assert.NotNull(loaded);
        Assert.Equal("Best tool", loaded.Vision);
        Assert.Equal("Grow revenue", loaded.BusinessGoals);
    }

    [Fact]
    public void Save_DirectoryDoesNotExist_CreatesDirectory()
    {
        var nestedPath = Path.Combine(_tempDir, "sub", "dir", "context.json");
        var sut = new JsonContextFileStore(nestedPath);

        sut.Save(new ProjectContext { Vision = "Test" });

        Assert.True(File.Exists(nestedPath));
    }

    [Fact]
    public void Delete_FileExists_RemovesFile()
    {
        var sut = CreateStore();
        File.WriteAllText(TempFile(), "{}");

        sut.Delete();

        Assert.False(File.Exists(TempFile()));
    }

    [Fact]
    public void Delete_FileDoesNotExist_DoesNotThrow()
    {
        var sut = new JsonContextFileStore(TempFile("nonexistent.json"));

        var exception = Record.Exception(() => sut.Delete());

        Assert.Null(exception);
    }
}
