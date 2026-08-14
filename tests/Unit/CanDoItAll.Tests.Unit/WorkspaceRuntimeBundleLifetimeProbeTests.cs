using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Tests.Support;
using CanDoItAll.Tools.Documents;

namespace CanDoItAll.Tests.Unit;

/// <summary>
/// Lifetime probes for the run-owned workspace service bundle: each bundle owns
/// its scope-bound services (including its process host) and disposes them
/// exactly once. The per-workspace single-host ownership requirement is tracked
/// through source assertions in the bundle proof until the workspace factory
/// passes one owned host through every consumer.
/// </summary>
public sealed class WorkspaceRuntimeBundleLifetimeProbeTests
{
    [Fact]
    public async Task Bundle_disposal_is_idempotent_and_owns_scope_bound_services()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("workspace-bundle-lifetime");
        try
        {
            var factory = new WorkspaceRuntimeServicesFactory(
                lifecycleFactExtractors: [],
                new ManagedCodeMarkItDownDocumentMarkdownConverter(),
                TestWorkspaceServices.PhysicalPathPolicyFactory,
                new ExternalTargetPathRegistryFactory());
            var bundle = factory.Create(new WorkspaceExecutionScope(
                workspaceRoot,
                WorkspaceScopeDescriptor.Sandbox));

            Assert.NotNull(bundle.FileService);
            Assert.NotNull(bundle.CommandExecutionService);
            Assert.NotNull(bundle.ArtifactToolService);
            Assert.NotNull(bundle.ImageOperationService);

            await bundle.DisposeAsync();
            // A second disposal must be a safe no-op: the workspace owner
            // disposes the bundle exactly once, and defensive double-dispose
            // must not throw.
            await bundle.DisposeAsync();
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public void Each_created_bundle_is_a_distinct_scope_bound_instance()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("workspace-bundle-identity");
        try
        {
            var factory = new WorkspaceRuntimeServicesFactory(
                lifecycleFactExtractors: [],
                new ManagedCodeMarkItDownDocumentMarkdownConverter(),
                TestWorkspaceServices.PhysicalPathPolicyFactory,
                new ExternalTargetPathRegistryFactory());
            var scope = new WorkspaceExecutionScope(
                workspaceRoot,
                WorkspaceScopeDescriptor.Sandbox);

            var first = factory.Create(scope);
            var second = factory.Create(scope);

            // Two runs never share mutable scope-bound services; each run owns
            // a complete bundle it can dispose without affecting other runs.
            Assert.NotSame(first, second);
            Assert.NotSame(first.CommandExecutionService, second.CommandExecutionService);
            Assert.NotSame(first.FileService, second.FileService);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }
}
