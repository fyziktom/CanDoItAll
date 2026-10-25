using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "HostPlatform")]
[Trait("Category", "FileSystemPortability")]
public sealed class WorkspaceCatalogNormalizationIntegrationTests {
    [Fact]
    public async Task WarmCatalog_ObservesSameSizeSameTimestampPermissionChangeAndKeepsScopesSeparate() {
        await using var environment = CanDoItAllTestEnvironment.Create("catalog-normalization");
        var firstRoot = environment.CreateInMemoryProfile("first").WorkspaceRootPath;
        var secondRoot = environment.CreateInMemoryProfile("second").WorkspaceRootPath;
        var first = new FileSandboxWorkspaceStore(firstRoot);
        var second = new FileSandboxWorkspaceStore(secondRoot);
        var initial = await first.LoadCatalogAsync();
        var originalSecret = Guid.NewGuid();
        var replacementSecret = Guid.NewGuid();
        var agent = initial.Agents[0] with {
            Id = Guid.NewGuid(), IsTemplate = false, TemplateKey = string.Empty, ConfigurationJson = "{}",
            Permissions = initial.Agents[0].Permissions with {
                AllowedSecrets = [new(originalSecret, "Catalog permission", AgentSecretPurposes.GeneralAgentRequest)]
            }
        };
        await first.UpdateCatalogAsync(catalog => catalog with { Agents = [.. catalog.Agents, agent] });
        var before = await first.LoadCatalogAsync();
        Assert.Same(before, await first.LoadCatalogAsync());
        var oldAgent = Assert.Single(before.Agents, candidate => candidate.Id == agent.Id);
        var path = new FileSandboxWorkspaceStorageLayout(firstRoot, null).CatalogPath;
        var originalBytes = await File.ReadAllBytesAsync(path);
        var timestamp = File.GetLastWriteTimeUtc(path);
        var text = await File.ReadAllTextAsync(path);
        Assert.Contains(originalSecret.ToString("D"), text);
        await File.WriteAllTextAsync(path, text.Replace(originalSecret.ToString("D"), replacementSecret.ToString("D"), StringComparison.Ordinal));
        File.SetLastWriteTimeUtc(path, timestamp);
        Assert.Equal(originalBytes.Length, new FileInfo(path).Length);

        var changed = await first.LoadCatalogAsync();

        var changedAgent = Assert.Single(changed.Agents, candidate => candidate.Id == agent.Id);
        Assert.Equal(replacementSecret, Assert.Single(changedAgent.Permissions.AllowedSecrets).SecretId);
        Assert.NotEqual(AgentConfigurationVersion.Create(oldAgent), AgentConfigurationVersion.Create(changedAgent));
        Assert.DoesNotContain((await second.LoadCatalogAsync()).Agents, candidate => candidate.Id == agent.Id);
        Assert.Throws<NotSupportedException>(() => ((IList<AgentAllowedSecretReference>)changedAgent.Permissions.AllowedSecrets).Clear());
    }

    [Fact]
    public async Task CatalogJson_DeletionAndCorruptionNeverReturnRetainedSnapshot() {
        await using var environment = CanDoItAllTestEnvironment.Create("catalog-normalization");
        var path = Path.Combine(environment.RootPath, "catalog.json");
        var json = new FileSandboxWorkspaceJsonStore();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(SandboxWorkspaceCatalog.Empty);
        await File.WriteAllBytesAsync(path, bytes);
        var first = await json.ReadJsonAsync<SandboxWorkspaceCatalog>(path, default);
        Assert.NotNull(first);
        File.Delete(path);
        Assert.Null(await json.ReadJsonAsync<SandboxWorkspaceCatalog>(path, default));
        await File.WriteAllTextAsync(path, "{invalid}");
        Assert.NotNull(await Record.ExceptionAsync(() => json.ReadJsonAsync<SandboxWorkspaceCatalog>(path, default)));
        await File.WriteAllBytesAsync(path, bytes);
        var repaired = await json.ReadJsonAsync<SandboxWorkspaceCatalog>(path, default);
        Assert.NotNull(repaired);
        Assert.NotSame(first, repaired);
    }

    [Fact]
    public async Task WarmLockedCatalog_StillInvokesCurrentPolicyAndPropagatesDenial() {
        await using var environment = CanDoItAllTestEnvironment.Create("catalog-normalization");
        var policy = new CurrentPolicy();
        var store = new FileSandboxWorkspaceStore(environment.CreateInMemoryProfile("first").WorkspaceRootPath,
            catalogMutationPolicy: policy);
        await store.LoadCatalogAsync();
        await store.LoadCatalogAsync();
        var calls = policy.Calls;
        policy.Denied = true;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => store.LoadCatalogAsync());
        Assert.Equal(calls + 1, policy.Calls);
        policy.Denied = false;
        Assert.NotEmpty((await store.LoadCatalogAsync()).Agents);
        Assert.Equal(calls + 2, policy.Calls);
    }

    [Fact]
    public async Task CapturedSeedInputs_IncludeBothPacksAndInlineAssetsAndDetectRepair() {
        await using var environment = CanDoItAllTestEnvironment.Create("catalog-normalization");
        var roots = new WorkspaceSeedRoots(Path.Combine(environment.RootPath, "capabilities"), Path.Combine(environment.RootPath, "agents"));
        CopyPack(CapabilityTemplatePackLoader.FindPackRoot(), roots.Capabilities);
        CopyPack(AgentTemplatePackLoader.FindPackRoot(), roots.Agents);
        var reader = new WorkspaceSeedInputReader(() => roots);
        List<string> reads = [];
        var captured = SandboxWorkspaceSeedBuilder.Build(roots.Capabilities, roots.Agents, path => {
            reads.Add(path);
            return reader.ReadText(path);
        });
        var ordinary = SandboxWorkspaceSeedBuilder.Build(roots.Capabilities, roots.Agents);
        Assert.Equal(JsonSerializer.Serialize(ordinary), JsonSerializer.Serialize(captured));
        Assert.True(reader.IsCurrent());
        Assert.Contains(reads, path => path.StartsWith(roots.Agents, StringComparison.Ordinal) && Path.GetFileName(path) == "settings.json");
        var asset = reads.First(path => path.StartsWith(roots.Capabilities, StringComparison.Ordinal) && Path.GetExtension(path) == ".md");
        var original = await File.ReadAllBytesAsync(asset);
        var changed = original.ToArray();
        changed[^1] ^= 1;
        var timestamp = File.GetLastWriteTimeUtc(asset);
        await File.WriteAllBytesAsync(asset, changed);
        File.SetLastWriteTimeUtc(asset, timestamp);
        Assert.False(reader.IsCurrent());
        await File.WriteAllBytesAsync(asset, original);
        Assert.True(reader.IsCurrent());
    }

    private static void CopyPack(string source, string destination) {
        foreach (var path in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)) {
            var target = Path.Combine(destination, Path.GetRelativePath(source, path));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(path, target);
        }
    }

    private sealed class CurrentPolicy : IAgentCatalogMutationPolicy {
        internal int Calls { get; private set; }
        internal bool Denied { get; set; }

        public Task<SandboxWorkspaceCatalog> ApplyAsync(SandboxWorkspaceCatalog before, SandboxWorkspaceCatalog after,
            CancellationToken cancellationToken = default) {
            Calls++;
            return Denied ? throw new UnauthorizedAccessException("Current policy denies access") : Task.FromResult(after);
        }
    }
}
