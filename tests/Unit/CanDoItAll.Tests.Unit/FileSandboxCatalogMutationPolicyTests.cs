using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class FileSandboxCatalogMutationPolicyTests {
    public enum CatalogWritePath { SaveCatalog, UpdateCatalog, SaveWorkspace, UpdateWorkspace }

    [Theory]
    [InlineData(CatalogWritePath.SaveCatalog)]
    [InlineData(CatalogWritePath.UpdateCatalog)]
    [InlineData(CatalogWritePath.SaveWorkspace)]
    [InlineData(CatalogWritePath.UpdateWorkspace)]
    public async Task Rejected_catalog_mutation_cannot_bypass_policy_through_any_public_catalog_writer(CatalogWritePath path) {
        await using var environment = CanDoItAllTestEnvironment.Create("catalog-policy-writers");
        var plain = new FileSandboxWorkspaceStore(environment.RootPath);
        var original = await plain.LoadAsync();
        var rejectedId = Guid.NewGuid();
        var rejected = original.Agents[0] with { Id = rejectedId, Name = "Rejected policy change", IsTemplate = false, TemplateKey = "" };
        var changed = original with { Agents = original.Agents.Append(rejected).ToArray() };
        var policy = new RejectAgentPolicy(rejectedId);
        var guarded = new FileSandboxWorkspaceStore(environment.RootPath, catalogMutationPolicy: policy);
        var catalogRevision = (await plain.LoadCatalogAsync()).CatalogDataRevision;
        await Assert.ThrowsAsync<CatalogRejectedException>(async () => {
            switch (path) {
                case CatalogWritePath.SaveCatalog:
                    await guarded.SaveCatalogAsync(changed.ToCatalog());
                    break;
                case CatalogWritePath.UpdateCatalog:
                    await guarded.UpdateCatalogAsync(_ => changed.ToCatalog());
                    break;
                case CatalogWritePath.SaveWorkspace:
                    await guarded.SaveAsync(changed);
                    break;
                case CatalogWritePath.UpdateWorkspace:
                    await guarded.UpdateWorkspaceAsync(_ => changed);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(path));
            }
        });
        var saved = await new FileSandboxWorkspaceStore(environment.RootPath).LoadCatalogAsync();
        Assert.DoesNotContain(saved.Agents, agent => agent.Id == rejectedId);
        Assert.Equal(catalogRevision, saved.CatalogDataRevision);
        Assert.Equal(1, policy.Rejections);
    }

    private sealed class CatalogRejectedException : Exception { }

    private sealed class RejectAgentPolicy(Guid deniedId) : IAgentCatalogMutationPolicy {
        public int Rejections { get; private set; }
        public Task<SandboxWorkspaceCatalog> ApplyAsync(SandboxWorkspaceCatalog before, SandboxWorkspaceCatalog after,
            CancellationToken cancellationToken = default) {
            if (after.Agents.Any(agent => agent.Id == deniedId)) {
                Assert.DoesNotContain(before.Agents, agent => agent.Id == deniedId);
                Rejections++;
                throw new CatalogRejectedException();
            }
            return Task.FromResult(after);
        }
    }
}
