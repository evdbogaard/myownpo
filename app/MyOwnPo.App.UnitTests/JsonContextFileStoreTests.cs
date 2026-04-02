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

	[Fact]
	public void Save_ContextWithNullFields_OmitsNullFieldsFromJson()
	{
		var sut = CreateStore();
		var context = new ProjectContext { Vision = "Only vision set" };

		sut.Save(context);

		var json = File.ReadAllText(TempFile());
		Assert.Contains("Vision", json);
		Assert.DoesNotContain("BusinessGoals", json);
		Assert.DoesNotContain("TargetUsers", json);
		Assert.DoesNotContain("SprintFocus", json);
		Assert.DoesNotContain("Constraints", json);
	}

	[Fact]
	public void Save_OverwritesExistingFile()
	{
		var sut = CreateStore();
		sut.Save(new ProjectContext { Vision = "First" });

		sut.Save(new ProjectContext { Vision = "Second" });

		var loaded = sut.Load();
		Assert.NotNull(loaded);
		Assert.Equal("Second", loaded.Vision);
	}

	[Fact]
	public void Load_ExternallyEditedFile_LoadsCorrectValues()
	{
		var sut = CreateStore();
		File.WriteAllText(TempFile(), """
            {
              "Vision": "Deliver a delightful mobile experience",
              "BusinessGoals": "Increase DAU by 20%",
              "TargetUsers": "Small business owners",
              "SprintFocus": "Onboarding flow redesign",
              "Constraints": "Must ship by Q3"
            }
            """);

		var result = sut.Load();

		Assert.NotNull(result);
		Assert.Equal("Deliver a delightful mobile experience", result.Vision);
		Assert.Equal("Increase DAU by 20%", result.BusinessGoals);
		Assert.Equal("Small business owners", result.TargetUsers);
		Assert.Equal("Onboarding flow redesign", result.SprintFocus);
		Assert.Equal("Must ship by Q3", result.Constraints);
	}

	[Fact]
	public void Load_MixedCasePropertyNames_LoadsCaseInsensitive()
	{
		var sut = CreateStore();
		File.WriteAllText(TempFile(), """
            {
              "vision": "lowercase vision",
              "BUSINESSGOALS": "uppercase goals",
              "targetusers": "all lowercase users",
              "sprintFocus": "camelCase focus",
              "CONSTRAINTS": "UPPER constraints"
            }
            """);

		var result = sut.Load();

		Assert.NotNull(result);
		Assert.Equal("lowercase vision", result.Vision);
		Assert.Equal("uppercase goals", result.BusinessGoals);
		Assert.Equal("all lowercase users", result.TargetUsers);
		Assert.Equal("camelCase focus", result.SprintFocus);
		Assert.Equal("UPPER constraints", result.Constraints);
	}

	[Fact]
	public void Load_ExtraProperties_IgnoresUnknownFields()
	{
		var sut = CreateStore();
		File.WriteAllText(TempFile(), """
            {
              "Vision": "Core vision",
              "CustomField": "this should be ignored",
              "Notes": "also ignored",
              "BusinessGoals": "Growth"
            }
            """);

		var result = sut.Load();

		Assert.NotNull(result);
		Assert.Equal("Core vision", result.Vision);
		Assert.Equal("Growth", result.BusinessGoals);
		Assert.Null(result.TargetUsers);
		Assert.Null(result.SprintFocus);
		Assert.Null(result.Constraints);
	}

	[Fact]
	public void Load_PartialFields_ReturnsPartialContext()
	{
		var sut = CreateStore();
		File.WriteAllText(TempFile(), """
            {
              "Vision": "Partial vision",
              "SprintFocus": "Current sprint only"
            }
            """);

		var result = sut.Load();

		Assert.NotNull(result);
		Assert.Equal("Partial vision", result.Vision);
		Assert.Equal("Current sprint only", result.SprintFocus);
		Assert.Null(result.BusinessGoals);
		Assert.Null(result.TargetUsers);
		Assert.Null(result.Constraints);
	}
}