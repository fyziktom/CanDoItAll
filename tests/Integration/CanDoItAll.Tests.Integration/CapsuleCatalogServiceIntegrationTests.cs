using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Manager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class CapsuleCatalogServiceIntegrationTests
{
    [Fact]
    public async Task RefreshAsync_reports_missing_and_malformed_capsules()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), "candoitall-capsules", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(workspaceRoot, "GoodService.cs"),
                """
                /* codex-capsule
                kind: service
                name: GoodService
                summary: Valid capsule.
                owns: workflow
                deps: none
                risks: drift
                tests: unit:GoodServiceTests
                */
                public sealed class GoodService
                {
                }
                """);
            await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "MissingCapsule.cs"), "public sealed class MissingCapsule { }");
            await File.WriteAllTextAsync(
                Path.Combine(workspaceRoot, "MalformedPage.razor"),
                """
                @page "/broken"
                /* codex-capsule
                kind: page
                name: BrokenPage
                summary:
                */
                <div>Broken</div>
                """);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Manager:WorkspaceRoot"] = workspaceRoot,
                    ["Manager:CapsuleArtifactsRoot"] = ".artifacts/codex-capsules"
                })
                .Build();

            var service = new CapsuleCatalogService(
                NullLogger<CapsuleCatalogService>.Instance,
                configuration,
                new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory()));
            await service.RefreshAsync();
            var coverage = service.GetCoverage();

            Assert.Equal(3, coverage.TotalFiles);
            Assert.Equal(1, coverage.CoveredFiles);
            Assert.Equal(1, coverage.MissingFiles);
            Assert.Equal(1, coverage.MalformedFiles);
            Assert.True(coverage.HasDrift);
            Assert.NotNull(service.GetSymbol("service-goodservice"));
        }
        finally
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }
    }
}
