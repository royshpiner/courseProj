using System.Reflection;
using Xunit;

namespace Backend.Tests;

public class ConsoleAppTests
{
    [Fact]
    public void BackendAssemblyHasAnEntryPoint()
    {
        var backendAssembly = Assembly.Load("Backend");

        Assert.NotNull(backendAssembly.EntryPoint);
    }
}
