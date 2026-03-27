using MyOwnPo.Models;
using MyOwnPo.Services;

using Xunit;

namespace MyOwnPo.UnitTests;

public class ProjectContextServiceTests
{
    [Fact]
    public void SetContext_ValidContext_StoresContext()
    {
        var sut = new ProjectContextService();
        var context = new ProjectContext { Vision = "Build the best tool" };

        sut.SetContext(context);

        Assert.Same(context, sut.GetContext());
    }

    [Fact]
    public void SetContext_CalledTwice_ReplacesContext()
    {
        var sut = new ProjectContextService();
        var first = new ProjectContext { Vision = "First" };
        var second = new ProjectContext { Vision = "Second" };

        sut.SetContext(first);
        sut.SetContext(second);

        Assert.Same(second, sut.GetContext());
    }

    [Fact]
    public void GetContext_NoContextSet_ReturnsNull()
    {
        var sut = new ProjectContextService();

        Assert.Null(sut.GetContext());
    }

    [Fact]
    public void HasContext_AfterSet_ReturnsTrue()
    {
        var sut = new ProjectContextService();
        sut.SetContext(new ProjectContext { Vision = "Vision" });

        Assert.True(sut.HasContext);
    }

    [Fact]
    public void HasContext_BeforeSet_ReturnsFalse()
    {
        var sut = new ProjectContextService();

        Assert.False(sut.HasContext);
    }

    [Fact]
    public void UpdateContext_ExistingContext_AppliesUpdate()
    {
        var sut = new ProjectContextService();
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
        var sut = new ProjectContextService();

        sut.UpdateContext(context => context.Vision = "New vision");

        var result = sut.GetContext();
        Assert.NotNull(result);
        Assert.Equal("New vision", result.Vision);
    }

    [Fact]
    public void ClearContext_AfterSet_RemovesContext()
    {
        var sut = new ProjectContextService();
        sut.SetContext(new ProjectContext { Vision = "Vision" });

        sut.ClearContext();

        Assert.Null(sut.GetContext());
        Assert.False(sut.HasContext);
    }
}