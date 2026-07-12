using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.Tests.Unit;

public sealed class MemoryMafIntegrationCheckpointTests
{
    [Fact]
    public void Maf_memory_entry_points_use_shared_policy_resolver_and_result_shaper()
    {
        var root = FindRepositoryRoot();
        var toolSource = string.Join(
            Environment.NewLine,
            ReadSource(root, "src/MAF/Memory/CanDoItAll.AgentFramework.Memory/Tools/MemoryAgentRuntimeToolProvider.cs"),
            ReadSource(root, "src/MAF/Memory/CanDoItAll.AgentFramework.Memory/Tools/MemoryAgentQueryTools.cs"),
            ReadSource(root, "src/MAF/Memory/CanDoItAll.AgentFramework.Memory/Tools/MemoryAgentToolPolicyFactory.cs"));
        var workflowSource = string.Join(
            Environment.NewLine,
            ReadSource(root, "src/MAF/Memory/CanDoItAll.AgentFramework.Memory/WorkflowExecutors/MemoryWorkflowRequestFactory.cs"),
            ReadSource(root, "src/MAF/Memory/CanDoItAll.AgentFramework.Memory/WorkflowExecutors/MemoryWorkflowQueryOperation.cs"));
        var contextSource = string.Join(
            Environment.NewLine,
            ReadSource(root, "src/MAF/Memory/CanDoItAll.AgentFramework.Memory/Context/MemoryAgentContextContributor.cs"),
            ReadSource(root, "src/MAF/Memory/CanDoItAll.AgentFramework.Memory/Context/MemoryAgentContextQueryDispatcher.cs"));

        Assert.Contains("MemoryMafProviderPolicyResolver.Resolve", toolSource, StringComparison.Ordinal);
        Assert.Contains("MemoryMafProviderPolicyResolver.Resolve", workflowSource, StringComparison.Ordinal);
        Assert.Contains("MemoryMafProviderPolicyResolver.Resolve", contextSource, StringComparison.Ordinal);
        Assert.Contains("MemoryMafToolResultShaper.ToQueryResult", toolSource, StringComparison.Ordinal);
        Assert.Contains("MemoryMafToolResultShaper.ToQueryResult", workflowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private MemoryProviderPolicyResolution ResolvePolicy", toolSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private MemoryProviderPolicyResolution ResolvePolicy", workflowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private MemoryProviderPolicyResolution ResolvePolicy", contextSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private static MemoryContextQueryToolResult MapQueryResult", toolSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private static MemoryContextQueryToolResult MapQueryResult", workflowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("partial class", toolSource, StringComparison.Ordinal);
        Assert.DoesNotContain("partial class", contextSource, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            root,
            "src/Modules/CanDoItAll.Modules.AgentFramework/WorkflowExecutors/MemoryWorkflowExecutor.cs")));
    }

    [Fact]
    public void Shared_policy_resolver_preserves_zero_provider_no_fallback_semantics()
    {
        var resolution = MemoryMafProviderPolicyResolver.Resolve(new MemoryMafProviderPolicyRequest(
            MemoryCapabilityIds.ContextQuerySync,
            RequestedProviderInstanceId: null,
            PreferredProviderInstanceId: null,
            DefaultProviderInstanceId: null,
            AllowedProviderInstanceIds: [],
            AllowedCapabilityIds: [],
            DeniedCapabilityIds: [],
            ProviderAssignments: [],
            MatchedAssignmentProvider: null,
            ProviderRequired: false,
            CapabilityPolicyDescription: "the checkpoint allowed capability policy",
            ProviderPolicyDescription: "the checkpoint allowed provider policy",
            ProviderRequiredDiagnostic: "Provider is required."));

        Assert.Null(resolution.Rejection);
        Assert.Null(resolution.ProviderForPayload);
        Assert.Null(resolution.SelectionPolicy.ExplicitProviderId);
        Assert.Null(resolution.SelectionPolicy.DefaultProviderId);
        Assert.Equal(MemoryProviderFallbackBehavior.DenyImplicitFallback, resolution.SelectionPolicy.FallbackBehavior);
    }

    [Fact]
    public void Shared_result_shaper_preserves_async_operation_metadata()
    {
        var operationId = MemoryOperationId.New();
        var accepted = new MemoryOperationAccepted(
            operationId,
            "/memory/operations/" + operationId,
            DateTimeOffset.UtcNow.AddMinutes(5),
            TimeSpan.FromSeconds(2),
            CallbackAvailable: false);
        var result = new MemoryOperationHandlerResult<MemoryContextPack>(
            MemoryOperationHandlerStatus.Accepted,
            MemoryProviderSelectionResult.Selected(
                CreateProvider("memory.checkpoint"),
                MemoryProviderSelectionReason.ExplicitProvider,
                MemoryCapabilityIds.ContextQueryAsync),
            OperationRecord: null,
            Output: null,
            accepted,
            FeedbackHandle: null,
            DriverDispatchAttempted: true,
            Diagnostic: "Accepted.");

        var shaped = MemoryMafToolResultShaper.ToQueryResult(result);

        Assert.Equal(MemoryToolResultStatus.Accepted, shaped.Status);
        Assert.True(shaped.Success);
        Assert.Equal("memory.checkpoint", shaped.ProviderInstanceId);
        Assert.Equal(operationId.Value, shaped.OperationId);
        Assert.NotNull(shaped.AsyncOperation);
        Assert.Equal(accepted.StatusPath, shaped.AsyncOperation.StatusPath);
        Assert.True(shaped.DispatchAttempted);
    }

    private static MemoryProviderProfile CreateProvider(string providerId)
    {
        return new MemoryProviderProfile(
            MemoryProviderInstanceId.Parse(providerId),
            providerId,
            MemoryProviderDriverKind.Mock,
            IsEnabled: true,
            MemoryProviderHealthState.Healthy,
            MemoryProviderWorkspaceScope.AllWorkspaces,
            SelectionTags: [],
            MemoryProviderProfilePolicy.Default,
            new MemoryProviderManifest(
                MemoryProviderKind.Parse("mock.memory"),
                MemoryProtocolVersion.Current,
                [new MemoryCapabilityDescriptor(MemoryCapabilityIds.ContextQueryAsync, "v1", Supported: true)],
                MemoryProviderInteractionSupport.SyncQueryOnly,
                UiSurfaces: [],
                MemoryProviderLimits.Default,
                MemoryExtensionData.Empty));
    }

    private static string ReadSource(
        string root,
        string relativePath)
    {
        return File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
