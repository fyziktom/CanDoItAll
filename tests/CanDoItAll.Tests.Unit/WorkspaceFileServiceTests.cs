using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceFileServiceTests
{
    [Fact]
    public void WriteTextFile_registers_showcase_deliverable_as_execution_artifact()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceFileServiceTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            var service = new WorkspaceFileService(workspaceRoot);

            var result = service.WriteTextFile(
                "showcases/blazor-ssr-calculator/app/SimpleCalculatorApp/Program.cs",
                "var builder = WebApplication.CreateBuilder(args);");

            Assert.True(result.Succeeded);
            Assert.Contains(
                result.Receipt.ArtifactReferences,
                item =>
                    string.Equals(item.Zone, "generated-output", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        item.RelativePath,
                        "showcases/blazor-ssr-calculator/app/SimpleCalculatorApp/Program.cs",
                        StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
            catch
            {
            }
        }
    }
}
