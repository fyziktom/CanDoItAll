using CanDoItAll.Manager;

namespace CanDoItAll.Tests.Unit;

public sealed class LaunchProfileSettingsResolverTests
{
    [Fact]
    public async Task ResolveRuntimeProbeUrls_reads_selected_launch_profile()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "candoitall-launch-profile-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(rootPath, "Properties"));

        try
        {
            var projectPath = Path.Combine(rootPath, "SampleWeb.csproj");
            await File.WriteAllTextAsync(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>");
            await File.WriteAllTextAsync(
                Path.Combine(rootPath, "Properties", "launchSettings.json"),
                """
                {
                  "profiles": {
                    "http": {
                      "applicationUrl": "http://localhost:5032"
                    },
                    "https": {
                      "applicationUrl": "https://localhost:7271;http://localhost:5032"
                    }
                  }
                }
                """);

            var readinessUrls = LaunchProfileSettingsResolver.ResolveRuntimeProbeUrls(projectPath, "https");

            Assert.Equal(
                ["https://localhost:7271/_dev/runtime", "http://localhost:5032/_dev/runtime"],
                readinessUrls);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }
}
