using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class FileSandboxWorkspaceCatalogNormalizerTests {
    [Fact]
    public void Reuse_RequiresTheSameCurrentSourceAndTemplateInputs() {
        var calls = 0;
        var inputsCurrent = true;
        var normalizer = Create(source => {
            calls++;
            return new(source, source, () => inputsCurrent);
        });
        var catalog = Assigned();
        Assert.True(normalizer.TryNormalizeUnchanged(catalog, out var first));
        Assert.True(normalizer.TryNormalizeUnchanged(catalog, out var reused));
        Assert.Same(first, reused);
        Assert.Equal(1, calls);
        Assert.True(normalizer.TryNormalizeUnchanged(catalog with { }, out _));
        Assert.Equal(2, calls);
        inputsCurrent = false;
        Assert.True(normalizer.TryNormalizeUnchanged(catalog, out _));
        Assert.True(normalizer.TryNormalizeUnchanged(catalog, out _));
        Assert.Equal(4, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangedOrUnassignedCatalog_RequiresPublicationEveryTime(bool assigned) {
        var calls = 0;
        var normalizer = Create(source => {
            calls++;
            return new(source, source with { Version = "normalized" }, () => true);
        });
        var catalog = assigned ? Assigned() : SandboxWorkspaceCatalog.Empty;
        Assert.False(normalizer.TryNormalizeUnchanged(catalog, out _));
        Assert.False(normalizer.TryNormalizeUnchanged(catalog, out _));
        Assert.Equal(2, calls);
    }

    [Fact]
    public void UnretainedOrEvictedSource_IsNeverMemoized() {
        var calls = 0;
        var retained = true;
        var normalizer = new FileSandboxWorkspaceCatalogNormalizer(Changed, _ => retained, source => {
            calls++;
            return new(source, source, () => true);
        });
        var catalog = Assigned();
        Assert.True(normalizer.TryNormalizeUnchanged(catalog, out _));
        retained = false;
        Assert.True(normalizer.TryNormalizeUnchanged(catalog, out _));
        Assert.True(normalizer.TryNormalizeUnchanged(catalog, out _));
        Assert.Equal(3, calls);
    }

    [Fact]
    public void FailedRefresh_NeverReturnsOldValueAndDoesNotPoisonRepairedInput() {
        var fail = false;
        var normalizer = Create(source => fail
            ? throw new InvalidOperationException("Invalid current template")
            : new(source, source, () => !fail));
        var catalog = Assigned();
        Assert.True(normalizer.TryNormalizeUnchanged(catalog, out var first));
        fail = true;
        Assert.Throws<InvalidOperationException>(() => normalizer.TryNormalizeUnchanged(catalog, out _));
        fail = false;
        Assert.True(normalizer.TryNormalizeUnchanged(catalog, out var repaired));
        Assert.NotSame(first, repaired);
    }

    [Fact]
    public void RetainedResult_FreezesCallerCollections() {
        List<AgentMemoryRecord> memory = [new(Guid.NewGuid(), Guid.NewGuid(), MemoryKind.Context,
            "Original", "Content", "test", 1, "{}", DateTimeOffset.UnixEpoch)];
        var catalog = Assigned() with { Memory = memory };
        var normalizer = Create(source => new(source, source, () => true));
        Assert.True(normalizer.TryNormalizeUnchanged(catalog, out var result));
        memory.Clear();
        Assert.Equal("Original", Assert.Single(result.Memory).Title);
        Assert.Throws<NotSupportedException>(() => ((IList<AgentMemoryRecord>)result.Memory).Clear());
    }

    [Fact]
    public void RequiresSave_IgnoresOnlyDataRevision() {
        var normalizer = Create(source => new(source, source, () => true));
        Assert.False(normalizer.RequiresSave(Assigned(), Assigned() with { CatalogDataRevision = new(21) }));
        Assert.True(normalizer.RequiresSave(Assigned(), Assigned() with { Version = "changed" }));
    }

    [Fact]
    public async Task ConcurrentReads_ReturnOnlyTheirOwnSource() {
        var normalizer = Create(source => new(source, source, () => true));
        var values = Enumerable.Range(0, 16).Select(index => Assigned() with { Version = index.ToString() }).ToArray();
        await Task.WhenAll(values.Select(source => Task.Run(() => {
            Assert.True(normalizer.TryNormalizeUnchanged(source, out var result));
            Assert.Equal(source.Version, result.Version);
        })));
    }

    private static FileSandboxWorkspaceCatalogNormalizer Create(Func<SandboxWorkspaceCatalog, CatalogReadSnapshot> normalize)
        => new(Changed, _ => true, normalize);

    private static bool Changed(SandboxWorkspaceCatalog left, SandboxWorkspaceCatalog right)
        => JsonSerializer.Serialize(left) != JsonSerializer.Serialize(right);

    private static SandboxWorkspaceCatalog Assigned()
        => SandboxWorkspaceCatalog.Empty with { CatalogDataRevision = CatalogDataRevision.Initial };
}
