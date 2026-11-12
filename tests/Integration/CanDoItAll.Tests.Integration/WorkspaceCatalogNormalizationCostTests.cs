using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "HostPlatform")]
public sealed class WorkspaceCatalogNormalizationCostTests(ITestOutputHelper output) {
    public enum CatalogPhase { BuildSeeds, NormalizeCatalog, CompareSerializedCatalog }

    [Theory]
    [InlineData(CatalogPhase.BuildSeeds)]
    [InlineData(CatalogPhase.NormalizeCatalog)]
    [InlineData(CatalogPhase.CompareSerializedCatalog)]
    public void Warm_catalog_components_record_synchronous_allocation_and_time(CatalogPhase phase) {
        var catalog = SandboxWorkspaceSeedFactory.NormalizeCatalog(SandboxWorkspaceSeedFactory.Create().ToCatalog());
        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(catalog);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        Assert.Equal(JsonSerializer.Serialize(catalog, options), JsonSerializer.Serialize(normalized, options));
        var expectedAgents = catalog.Agents.Count;
        Assert.True(expectedAgents > 0);
        for (var warmup = 0; warmup < 2; warmup++) {
            Assert.Equal(expectedAgents, Measure());
        }
        for (var sample = 0; sample < 5; sample++) {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            var agents = Measure();
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(expectedAgents, agents);
            output.WriteLine(JsonSerializer.Serialize(new {
                Workload = "workspace-catalog-component", Phase = phase.ToString(), Sample = sample,
                ElapsedMilliseconds = elapsed, ThreadAllocatedBytes = allocated, Agents = agents,
                CatalogUtf8Bytes = JsonSerializer.SerializeToUtf8Bytes(catalog, options).Length,
                Limits = "Synchronous default-template component diagnostic; normalization includes seed creation; not an HTTP capacity gate."
            }));
        }

        int Measure() => phase switch {
            CatalogPhase.BuildSeeds => SandboxWorkspaceSeedFactory.Create().Agents.Count,
            CatalogPhase.NormalizeCatalog => SandboxWorkspaceSeedFactory.NormalizeCatalog(catalog).Agents.Count,
            CatalogPhase.CompareSerializedCatalog => JsonSerializer.Serialize(catalog with {
                CatalogDataRevision = CatalogDataRevision.Unassigned
            }, options) == JsonSerializer.Serialize(normalized with {
                CatalogDataRevision = CatalogDataRevision.Unassigned
            }, options) ? expectedAgents : throw new InvalidDataException("An unchanged normalized catalog must retain its serialized payload."),
            _ => throw new ArgumentOutOfRangeException(nameof(phase))
        };
    }
}
