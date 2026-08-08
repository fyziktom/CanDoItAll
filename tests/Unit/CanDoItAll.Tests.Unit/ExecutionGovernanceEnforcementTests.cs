using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit;

/// <summary>
/// The admitted execution governance snapshot is the single runtime permission
/// input: composition excludes tools outside it, and the invocation policy
/// independently denies calls that exceed it — even when an approval path or
/// auto-approval would otherwise allow the tool.
/// </summary>
public sealed class ExecutionGovernanceEnforcementTests
{
    [Fact]
    public async Task Read_only_authority_denies_mutation_tool_even_with_auto_approval()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateInteractiveContext(
            ToolContractCatalog.WorkspaceWriteFile,
            ToolInvocationClassification.Mutation,
            CreateGovernance(readAllowed: true, mutationAllowed: false),
            autoApprovalAllowed: true,
            approvalWrapperAvailable: true);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("read-only", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Read_only_authority_still_allows_read_tools()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateInteractiveContext(
            ToolContractCatalog.WorkspaceReadFile,
            ToolInvocationClassification.Read,
            CreateGovernance(readAllowed: true, mutationAllowed: false),
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task Authority_without_workspace_read_denies_workspace_read_tools()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateInteractiveContext(
            ToolContractCatalog.WorkspaceReadFile,
            ToolInvocationClassification.Read,
            CreateGovernance(readAllowed: false, mutationAllowed: false),
            autoApprovalAllowed: true,
            approvalWrapperAvailable: true);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("read access", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Composition_filter_excludes_mutation_tools_for_read_only_authority()
    {
        var capabilityState = new RuntimeCapabilityState();
        capabilityState.Tools.Add(AIFunctionFactory.Create(
            () => "written",
            ToolContractCatalog.WorkspaceWriteFile,
            "Writes a file."));
        capabilityState.Tools.Add(AIFunctionFactory.Create(
            () => "content",
            ToolContractCatalog.WorkspaceReadFile,
            "Reads a file."));
        var progressMessages = new List<string>();

        await MafRuntimeAgentFactory.FilterToolsOutsideExecutionGovernanceAsync(
            capabilityState,
            CreateGovernance(readAllowed: true, mutationAllowed: false),
            (_, _, message) =>
            {
                progressMessages.Add(message);
                return Task.CompletedTask;
            });

        var remainingTool = Assert.Single(capabilityState.Tools);
        Assert.Equal(ToolContractCatalog.WorkspaceReadFile, remainingTool.Name);
        Assert.Contains(progressMessages, message =>
            message.Contains(ToolContractCatalog.WorkspaceWriteFile, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Composition_filter_keeps_all_tools_when_no_snapshot_is_present()
    {
        var capabilityState = new RuntimeCapabilityState();
        capabilityState.Tools.Add(AIFunctionFactory.Create(
            () => "written",
            ToolContractCatalog.WorkspaceWriteFile,
            "Writes a file."));

        await MafRuntimeAgentFactory.FilterToolsOutsideExecutionGovernanceAsync(
            capabilityState,
            governance: null,
            (_, _, _) => Task.CompletedTask);

        Assert.Single(capabilityState.Tools);
    }

    [Fact]
    public void Authority_projection_round_trips_into_the_governance_snapshot()
    {
        var authority = CreateAuthorityRecord(
            readAllowed: true,
            mutationAllowed: false,
            allowedOperations: ["ReadProjectStructure", "RunValidation"],
            allowedExternalTargetAliases: ["external-target/c/products/demo"]);
        var turnReference = CreateTurnReference();

        var metadataJson = AgentTurnContextMetadata.Apply("{}", turnReference, authority);
        var snapshot = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(metadataJson);

        Assert.NotNull(snapshot);
        Assert.Equal(authority.AuthorityId, snapshot!.AuthorityId);
        Assert.Equal(authority.AgentId, snapshot.AgentId);
        Assert.Equal(authority.DatabaseProfileId, snapshot.DatabaseProfileId);
        Assert.Equal(authority.WorkspaceScope, snapshot.WorkspaceScope);
        Assert.True(snapshot.ReadAllowed);
        Assert.False(snapshot.MutationAllowed);
        Assert.Equal(authority.PolicyFingerprint, snapshot.PolicyFingerprint);
        Assert.Contains("ReadProjectStructure", snapshot.AllowedOperations);
        Assert.Contains("external-target/c/products/demo", snapshot.WritableExternalTargetAliases);
    }

    [Fact]
    public void Tampered_or_missing_authority_projection_yields_no_snapshot()
    {
        Assert.Null(AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(null));
        Assert.Null(AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot("{}"));
        Assert.Null(AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot("not-json"));
        Assert.Null(AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(
            """{"agentExecutionAuthority":{"authorityId":"tampered","workspaceScopeKind":"Project"}}"""));
    }

    [Fact]
    public void Persisted_malformed_authority_projection_fails_closed_during_runtime_restore()
    {
        var run = CreateRun(
            """{"agentExecutionAuthority":{"authorityId":"tampered","workspaceScopeKind":"Project"}}""");
        var identity = new AgentExecutionActivityWorkspaceIdentity(
            Guid.NewGuid(),
            WorkspaceScopeDescriptor.Sandbox,
            new DatabaseProfileGeneration(1));

        Assert.IsType<AgentExecutionAuthorityMismatchException>(
            InvokeGovernanceRestore(run, null, identity));
    }

    [Fact]
    public void Authority_projection_reader_distinguishes_absent_valid_and_malformed_metadata()
    {
        var authority = CreateAuthorityRecord(readAllowed: true, mutationAllowed: false);
        var validMetadata = AgentTurnContextMetadata.Apply("{}", CreateTurnReference(), authority);

        var absent = AgentTurnContextMetadata.ReadExecutionGovernanceSnapshot("{}");
        var valid = AgentTurnContextMetadata.ReadExecutionGovernanceSnapshot(validMetadata);
        var malformed = AgentTurnContextMetadata.ReadExecutionGovernanceSnapshot(
            """{"agentExecutionAuthority":{"authorityId":"tampered"}}""");

        Assert.Equal(AgentExecutionGovernanceReadState.Absent, absent.State);
        Assert.Null(absent.Snapshot);
        Assert.Equal(AgentExecutionGovernanceReadState.Valid, valid.State);
        Assert.NotNull(valid.Snapshot);
        Assert.Equal(AgentExecutionGovernanceReadState.Malformed, malformed.State);
        Assert.Null(malformed.Snapshot);
    }

    [Fact]
    public void Context_admission_evidence_requires_a_valid_authority_projection()
    {
        var identity = new AgentExecutionActivityWorkspaceIdentity(
            Guid.NewGuid(),
            WorkspaceScopeDescriptor.Sandbox,
            new DatabaseProfileGeneration(1));
        var transientMetadata = ExecutionInvocationMetadata.ApplyTransientContextRequirement(
            "{}",
            AgentChatContextDigest.Compute(new AgentRuntimeTransientContext("Trusted selected record.")));
        var metadataSamples = new[]
        {
            """{"agentTurnContextReference":{}}""",
            transientMetadata
        };

        foreach (var metadata in metadataSamples)
        {
            var exception = InvokeGovernanceRestore(CreateRun(metadata), null, identity);
            Assert.IsType<AgentExecutionAuthorityMismatchException>(exception);
        }
    }

    [Fact]
    public void Governance_restore_rejects_agent_profile_generation_and_scope_mismatches()
    {
        var agentId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var generation = new DatabaseProfileGeneration(7);
        var scope = WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D"));
        var authority = CreateAuthorityRecord(
            readAllowed: true,
            mutationAllowed: false,
            agentId: agentId,
            databaseProfileId: profileId,
            databaseProfileGeneration: generation,
            workspaceScope: scope);
        var metadata = AgentTurnContextMetadata.Apply("{}", CreateTurnReference(), authority);
        var run = CreateRun(metadata, agentId);
        var matchingIdentity = new AgentExecutionActivityWorkspaceIdentity(profileId, scope, generation);

        Assert.IsType<AgentExecutionAuthorityMismatchException>(
            InvokeGovernanceRestore(run with { AgentId = Guid.NewGuid() }, scope, matchingIdentity));
        Assert.IsType<AgentExecutionAuthorityMismatchException>(InvokeGovernanceRestore(
            run,
            scope,
            new AgentExecutionActivityWorkspaceIdentity(Guid.NewGuid(), scope, generation)));
        Assert.IsType<AgentExecutionAuthorityMismatchException>(InvokeGovernanceRestore(
            run,
            scope,
            new AgentExecutionActivityWorkspaceIdentity(
                profileId,
                scope,
                new DatabaseProfileGeneration(8))));
        Assert.IsType<AgentExecutionAuthorityMismatchException>(InvokeGovernanceRestore(
            run,
            WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D")),
            matchingIdentity));
        Assert.Null(InvokeGovernanceRestore(run, scope, matchingIdentity));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    public void Detached_or_legacy_run_without_context_evidence_remains_compatible(string? metadataJson)
    {
        var identity = new AgentExecutionActivityWorkspaceIdentity(
            Guid.NewGuid(),
            WorkspaceScopeDescriptor.Sandbox,
            new DatabaseProfileGeneration(1));

        Assert.Null(InvokeGovernanceRestore(CreateRun(metadataJson), null, identity));
    }

    [Fact]
    public void Initial_and_continuation_execution_share_the_single_governance_restoration_gate()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "MAF",
            "Common",
            "CanDoItAll.AgentFramework.Core",
            "Execution",
            "AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs"));

        Assert.Equal(3, CountOccurrences(source, "CreateRuntimeExecutionOptionsCore("));
        Assert.Equal(1, CountOccurrences(source, "ReadExecutionGovernanceSnapshot("));
        Assert.DoesNotContain("TryReadExecutionGovernanceSnapshot(run.MetadataJson)", source, StringComparison.Ordinal);
    }

    private static ToolInvocationPolicyContext CreateInteractiveContext(
        string toolName,
        ToolInvocationClassification classification,
        AgentExecutionGovernanceSnapshot governance,
        bool autoApprovalAllowed,
        bool approvalWrapperAvailable)
    {
        var redactedArguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["path"] = "artifacts/report.md"
        };
        return new ToolInvocationPolicyContext(
            AgentId: governance.AgentId,
            AgentName: "Governance enforcement agent",
            ToolName: toolName,
            RedactedArguments: redactedArguments,
            Classification: classification,
            IsKnownTool: true,
            AutoApprovalAllowed: autoApprovalAllowed,
            ApprovalWrapperAvailable: approvalWrapperAvailable,
            ExecutionRunId: Guid.NewGuid().ToString("D"),
            SourceKind: "project-structure",
            ProcessRunId: string.Empty,
            ProcessStepId: string.Empty,
            ApprovalWrapperEffectiveForProvider: approvalWrapperAvailable)
        {
            ExecutionGovernance = governance,
            PathArguments = ToolInvocationPathArgumentResolver.Resolve(
                toolName,
                redactedArguments.Select(argument =>
                    new KeyValuePair<string, object?>(argument.Key, argument.Value)))
        };
    }

    private static AgentExecutionGovernanceSnapshot CreateGovernance(
        bool readAllowed,
        bool mutationAllowed)
        => AgentExecutionGovernanceSnapshot.FromAuthority(CreateAuthorityRecord(readAllowed, mutationAllowed));

    private static AgentExecutionAuthorityRecord CreateAuthorityRecord(
        bool readAllowed,
        bool mutationAllowed,
        IReadOnlyList<string>? allowedOperations = null,
        IReadOnlyList<string>? allowedExternalTargetAliases = null,
        Guid? agentId = null,
        Guid? databaseProfileId = null,
        DatabaseProfileGeneration? databaseProfileGeneration = null,
        WorkspaceScopeDescriptor? workspaceScope = null)
        => new(
            AgentExecutionAuthorityId.Create(),
            agentId ?? Guid.NewGuid(),
            databaseProfileId ?? Guid.NewGuid(),
            databaseProfileGeneration ?? new DatabaseProfileGeneration(1),
            workspaceScope ?? WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D")),
            readAllowed,
            mutationAllowed,
            "v2-canonical",
            "policy-fingerprint-value",
            DateTimeOffset.UtcNow,
            allowedOperations: allowedOperations,
            allowedExternalTargetAliases: allowedExternalTargetAliases);

    private static AgentTurnContextReference CreateTurnReference()
        => new(
            AgentTurnContextId.Create(),
            AgentContextEpochId.Create(),
            new AgentChatContextSourceKind("project-structure"),
            new AgentChatContextSourceId(Guid.NewGuid().ToString("D")),
            surface: "project-structure",
            view: "hierarchy",
            observationVersion: 3,
            modelContextDigest: "digest-value",
            capturedAtUtc: DateTimeOffset.UtcNow);

    private static Exception? InvokeGovernanceRestore(
        ExecutionRunRecord run,
        WorkspaceScopeDescriptor? contextWorkspaceScope,
        AgentExecutionActivityWorkspaceIdentity identity)
    {
        var executionServiceType = typeof(AgentFrameworkWorkspaceService).Assembly.GetType(
            "CanDoItAll.AgentFramework.Core.AgentFrameworkWorkspaceExecutionService");
        Assert.NotNull(executionServiceType);
        var restore = executionServiceType.GetMethod(
            "ResolveValidatedExecutionGovernance",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(restore);

        try
        {
            restore.Invoke(null, [run, contextWorkspaceScope, identity]);
            return null;
        }
        catch (TargetInvocationException exception)
        {
            return exception.InnerException;
        }
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
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

        throw new InvalidOperationException("Unable to locate repository root.");
    }

    private static ExecutionRunRecord CreateRun(string? metadataJson, Guid? agentId = null)
        => new(
            Id: Guid.NewGuid(),
            AgentId: agentId ?? Guid.NewGuid(),
            ChatSessionId: Guid.NewGuid(),
            Title: "Governance restore test run",
            SourceKind: "project-structure",
            SourceId: Guid.NewGuid().ToString("D"),
            CorrelationId: string.Empty,
            CausationId: string.Empty,
            RequestedBy: "test",
            RequestedByKind: "interactive",
            MetadataJson: metadataJson ?? string.Empty,
            InputSummary: string.Empty,
            ResultSummary: string.Empty,
            ProviderName: "OpenAI",
            Model: "unit-test-model",
            State: ExecutionState.Running,
            Outcome: null,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            StartedAtUtc: DateTimeOffset.UtcNow,
            CompletedAtUtc: null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: []);
}
