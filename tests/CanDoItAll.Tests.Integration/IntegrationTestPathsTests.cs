namespace CanDoItAll.Tests.Integration;

public sealed class IntegrationTestPathsTests
{
    [Theory]
    [InlineData("CanDoItAll.Mcp.Processes", "CanDoItAll.Mcp.Processes.dll")]
    [InlineData("CanDoItAll.Mcp.ProjectStructure", "CanDoItAll.Mcp.ProjectStructure.dll")]
    public void ResolveProjectOutputAssembly_uses_current_build_configuration(string projectDirectoryName, string assemblyFileName)
    {
        var assemblyPath = IntegrationTestPaths.ResolveProjectOutputAssembly(projectDirectoryName, assemblyFileName);

        Assert.True(File.Exists(assemblyPath));
        Assert.Contains(
            Path.Combine("bin", IntegrationTestPaths.BuildConfiguration, "net10.0"),
            assemblyPath,
            StringComparison.OrdinalIgnoreCase);
    }
}
