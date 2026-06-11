using CanDoItAll.Infrastructure.Logging;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core.Artifacts;
using CanDoItAll.Processes.Core.Diagnostics;
using CanDoItAll.Processes.Core.Execution;
using CanDoItAll.Processes.Core.Finalization;
using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Gateway;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;
using CanDoItAll.Processes.Drivers.BusinessAnalysis;
using CanDoItAll.Processes.Drivers.OfficeEvidence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;
using CoreArtifactExpectationSnapshot = CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot;
using CoreArtifactRecordSnapshot = CanDoItAll.Processes.Core.Artifacts.ProcessArtifactRecordSnapshot;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessDomainEvidenceReadOnlyAdapterTests
{
    private const string ArtifactEvidencePayload = """{"projection":[{"source":"file-write"}],"validation":[{"kind":"deliverable"}]}""";
    private const string OfficeEvidencePayload = """{"items":[{"kind":"email","id":"message-1"}]}""";
    private const string OfficeBatchEvidencePayload = """{"items":[{"kind":"email","id":"message-1"},{"kind":"document","id":"document-1"}]}""";
    private const string BusinessAnalysisPayload = """{"items":[{"kind":"deliverable","id":"analysis-1"},{"kind":"evidence","id":"evidence-1"}]}""";

    [Fact]
    public void Process_artifact_evidence_readonly_adapter_SB021_INV_001_maps_supplied_descriptors_to_observation_without_mutation()
    {
        var adapter = new ProcessArtifactEvidenceReadOnlyAdapter();
        var payload = CreateArtifactPayload();

        var observation = adapter.Verify(payload);

        Assert.True(observation.Accepted);
        Assert.Equal(ProcessDriverDenialReason.None, observation.DenialReason);
        Assert.True(observation.NoMutationPerformed);
        Assert.Equal(ProcessArtifactEvidenceSourceLane.ArtifactEvidenceConsistency, observation.SourceLane);
        Assert.Equal(payload.ProcessRunId, observation.ProcessRunId);
        Assert.Equal(payload.StepRunId, observation.StepRunId);
        Assert.Equal(payload.ArtifactId, observation.ArtifactId);
        Assert.Equal(payload.RequestedAt, observation.ObservedAt);
        Assert.Equal(ProcessDriverContractVersion.Current, observation.ContractVersion);
        Assert.Contains(
            observation.Diagnostics,
            diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.NoIssueDetected);
        Assert.Contains(
            observation.EvidenceReferences,
            evidenceReference => evidenceReference.CoreDescriptorFamily == ProcessDriverCoreDescriptorFamily.ArtifactProjectionEvidence);
        Assert.Contains(
            observation.EvidenceReferences,
            evidenceReference => evidenceReference.CoreDescriptorFamily == ProcessDriverCoreDescriptorFamily.ArtifactProjectionValidation);
        AssertReadonlyAuditFacts(
            observation.AuditFacts,
            ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead,
            ProcessDriverDenialReason.None);
    }

    [Fact]
    public void Process_artifact_evidence_readonly_adapter_SB021_INV_002_denies_mutation_and_untrusted_sources_without_verifier_side_effects()
    {
        var adapter = new ProcessArtifactEvidenceReadOnlyAdapter();

        var mutationObservation = adapter.Verify(CreateArtifactPayload(
            requestedOperations: [ProcessDriverOperation.WriteArtifact]));
        var untrustedObservation = adapter.Verify(CreateArtifactPayload(
            projectionEvidenceUri: "https://example.invalid/artifact-projection.json"));

        Assert.False(mutationObservation.Accepted);
        Assert.True(mutationObservation.NoMutationPerformed);
        Assert.Equal(ProcessDriverDenialReason.MutationDenied, mutationObservation.DenialReason);
        Assert.Contains(
            mutationObservation.Diagnostics,
            diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.MutationAttemptDenied);
        Assert.False(untrustedObservation.Accepted);
        Assert.True(untrustedObservation.NoMutationPerformed);
        Assert.Equal(ProcessDriverDenialReason.MissingEvidence, untrustedObservation.DenialReason);
        Assert.Contains(
            untrustedObservation.Diagnostics,
            diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.TranscriptUntrusted);
        Assert.All(
            mutationObservation.AuditFacts.Concat(untrustedObservation.AuditFacts),
            fact => Assert.Equal(ProcessDriverAuditFactKind.OperationDenied, fact.Kind));
    }

    [Fact]
    public void Process_office_and_business_readonly_adapters_SB024_INV_001_map_supplied_items_and_deny_external_or_record_mutation()
    {
        var officeAdapter = new ProcessOfficeEvidenceReadOnlyAdapter();
        var businessAdapter = new ProcessBusinessAnalysisReadOnlyAdapter();

        var officeObservation = officeAdapter.Verify(CreateOfficePayload());
        var businessObservation = businessAdapter.Verify(CreateBusinessPayload());
        var officeGraphAttempt = officeAdapter.Verify(CreateOfficePayload(
            requestedOperations: [ProcessDriverOperation.CallOfficeGraph]));
        var businessMutationAttempt = businessAdapter.Verify(CreateBusinessPayload(
            requestedOperations: [ProcessDriverOperation.MutateBusinessRecord]));

        Assert.True(officeObservation.Accepted);
        Assert.Equal(ProcessOfficeEvidenceSourceLane.OfficeEvidenceRead, officeObservation.SourceLane);
        Assert.Equal(ProcessDriverCapabilityScopeKind.OfficeEvidenceRead, officeObservation.AuditFacts[0].Lane);
        Assert.True(officeObservation.NoMutationPerformed);
        Assert.True(businessObservation.Accepted);
        Assert.Equal(ProcessBusinessAnalysisSourceLane.BusinessAnalysisRead, businessObservation.SourceLane);
        Assert.Equal(ProcessDriverCapabilityScopeKind.BusinessAnalysisRead, businessObservation.AuditFacts[0].Lane);
        Assert.True(businessObservation.NoMutationPerformed);

        Assert.False(officeGraphAttempt.Accepted);
        Assert.Equal(ProcessDriverDenialReason.ExternalCallDenied, officeGraphAttempt.DenialReason);
        Assert.True(officeGraphAttempt.NoMutationPerformed);
        Assert.Contains(
            officeGraphAttempt.Diagnostics,
            diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.MutationAttemptDenied);

        Assert.False(businessMutationAttempt.Accepted);
        Assert.Equal(ProcessDriverDenialReason.MutationDenied, businessMutationAttempt.DenialReason);
        Assert.True(businessMutationAttempt.NoMutationPerformed);
        Assert.Contains(
            businessMutationAttempt.Diagnostics,
            diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.MutationAttemptDenied);
    }

    [Fact]
    public void Process_observation_aggregation_readonly_adapter_SB027_INV_001_combines_existing_responses_without_persistence()
    {
        var adapter = new ProcessDriverObservationAggregationReadOnlyAdapter();
        var payload = new ProcessDriverObservationAggregationReadOnlyPayload(
            ProcessRunId,
            StepRunId,
            "process-consumer:aggregate-readonly",
            [
                CreateVerificationResponse(ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead, accepted: true),
                CreateVerificationResponse(ProcessDriverCapabilityScopeKind.OfficeEvidenceRead, accepted: true),
                CreateVerificationResponse(ProcessDriverCapabilityScopeKind.BusinessAnalysisRead, accepted: false)
            ],
            RequestedAt);

        var observation = adapter.Aggregate(payload);

        Assert.Equal(payload.ProcessRunId, observation.ProcessRunId);
        Assert.Equal(payload.StepRunId, observation.StepRunId);
        Assert.Equal(payload.RequestedAt, observation.ObservedAt);
        Assert.Equal(3, observation.ResponseCount);
        Assert.Equal(2, observation.AcceptedCount);
        Assert.Equal(1, observation.DeniedCount);
        Assert.True(observation.AggregationMutationFree);
        Assert.True(observation.AllResponsesMutationFree);
        Assert.Equal(ProcessDriverContractVersion.Current, observation.ContractVersion);
        Assert.Contains(
            observation.LaneSummaries,
            summary => summary.Lane == ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead);
        Assert.Contains(
            observation.LaneSummaries,
            summary => summary.Lane == ProcessDriverCapabilityScopeKind.OfficeEvidenceRead);
        Assert.Contains(
            observation.LaneSummaries,
            summary => summary.Lane == ProcessDriverCapabilityScopeKind.BusinessAnalysisRead);
    }

    [Fact]
    public void Process_readonly_verification_batch_orchestrator_SB015_INV_001_runs_all_supplied_payload_lanes_without_runtime_host()
    {
        var orchestrator = new ProcessReadOnlyVerificationBatchOrchestrator();
        var payload = new ProcessReadOnlyVerificationBatchPayload(
            ProcessRunId,
            StepRunId,
            " process-consumer:batch-readonly ",
            RequestedAt,
            transcriptPayloads: [CreateTranscriptPayload()],
            runtimeEvidencePayloads: [CreateRuntimePayload()],
            artifactEvidencePayloads: [CreateArtifactPayload()],
            officeEvidencePayloads: [CreateOfficePayload()],
            businessAnalysisPayloads: [CreateBusinessPayload()]);

        var observation = orchestrator.Verify(payload);

        Assert.Equal(ProcessRunId, observation.ProcessRunId);
        Assert.Equal(StepRunId, observation.StepRunId);
        Assert.Equal("process-consumer:batch-readonly", observation.CallerContext);
        Assert.Equal(RequestedAt, observation.ObservedAt);
        Assert.Equal(5, observation.ResponseCount);
        Assert.Single(observation.TranscriptObservations);
        Assert.Single(observation.RuntimeEvidenceObservations);
        Assert.Single(observation.ArtifactEvidenceObservations);
        Assert.Single(observation.OfficeEvidenceObservations);
        Assert.Single(observation.BusinessAnalysisObservations);
        Assert.All(observation.Responses, response =>
        {
            Assert.True(response.Accepted);
            Assert.True(response.NoMutationPerformed);
            Assert.Equal(ProcessDriverContractVersion.Current, response.ContractVersion);
        });

        var aggregate = Assert.IsType<ProcessReadOnlyVerificationAggregateObservation>(
            observation.AggregateObservation);

        Assert.Equal(ProcessRunId, aggregate.ProcessRunId);
        Assert.Equal(StepRunId, aggregate.StepRunId);
        Assert.Equal("process-consumer:batch-readonly", aggregate.CallerContext);
        Assert.Equal(5, aggregate.ResponseCount);
        Assert.Equal(5, aggregate.AcceptedCount);
        Assert.Equal(0, aggregate.DeniedCount);
        Assert.True(aggregate.AggregationMutationFree);
        Assert.True(aggregate.AllResponsesMutationFree);
        Assert.Equal(ProcessDriverContractVersion.Current, aggregate.ContractVersion);
        Assert.Contains(
            aggregate.LaneSummaries,
            summary => summary.Lane == ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification);
        Assert.Contains(
            aggregate.LaneSummaries,
            summary => summary.Lane == ProcessDriverCapabilityScopeKind.RuntimeFactsRead);
        Assert.Contains(
            aggregate.LaneSummaries,
            summary => summary.Lane == ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead);
        Assert.Contains(
            aggregate.LaneSummaries,
            summary => summary.Lane == ProcessDriverCapabilityScopeKind.OfficeEvidenceRead);
        Assert.Contains(
            aggregate.LaneSummaries,
            summary => summary.Lane == ProcessDriverCapabilityScopeKind.BusinessAnalysisRead);
        AssertReadOnlyList(observation.Responses, observation.Responses[0]);
        AssertReadOnlyList(observation.TranscriptObservations, observation.TranscriptObservations[0]);
        AssertReadOnlyList(aggregate.LaneSummaries, aggregate.LaneSummaries[0]);
    }

    [Fact]
    public async Task Process_verification_runtime_host_SB015_INV_002_selects_exact_lane_without_cross_lane_fallback()
    {
        var host = CreateHost(new ProcessVerificationRuntimeHostOptions());
        var payload = new ProcessReadOnlyVerificationBatchPayload(
            ProcessRunId,
            StepRunId,
            "process-consumer:host-readonly",
            RequestedAt,
            artifactEvidencePayloads: [CreateArtifactPayload()],
            officeEvidencePayloads: [CreateOfficePayload()],
            businessAnalysisPayloads: [CreateBusinessPayload()]);
        var request = new ProcessVerificationHostRequest(
            ProcessDriverVerificationGatewayLane.ArtifactEvidenceConsistency,
            payload,
            "process-manager",
            RequestedAt);

        var result = await host.VerifyAsync(request);
        var response = result.Response ?? throw new InvalidOperationException("Expected a successful verification host response.");

        Assert.True(result.IsSuccess);
        Assert.False(result.IsDenied);
        Assert.Equal(ProcessDriverVerificationGatewayLane.ArtifactEvidenceConsistency, response.Lane);
        Assert.Equal(ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead, response.Registration.RequiredScopeKind);
        Assert.Equal(ProcessDriverPermissionMode.VerificationOnly, response.Registration.RequiredPermissionMode);
        Assert.True(response.NoMutationPerformed);
        Assert.False(response.AllowsProcessMutation);
        Assert.False(response.AllowsTransitionMutation);
        Assert.False(response.AllowsFinalizerMutation);
        Assert.Single(response.Observation.ArtifactEvidenceObservations);
        Assert.Empty(response.Observation.OfficeEvidenceObservations);
        Assert.Empty(response.Observation.BusinessAnalysisObservations);
        Assert.Equal(1, response.Observation.ResponseCount);
        Assert.Equal(response.Observation.ResponseCount, response.AuditRecord.ResponseCount);
        Assert.Equal(response.Lane, response.AuditRecord.Lane);
        Assert.True(response.AuditRecord.NoMutationPerformed);
        Assert.Matches("^[A-F0-9]{64}$", response.AuditRecord.ObservationHash);
    }

    [Fact]
    public void Process_verification_lane_selector_SB019_INV_001_returns_exact_selection_result()
    {
        var selector = new ProcessVerificationLaneSelector(new ProcessVerificationLaneRegistry());

        var selected = selector.SelectExact(ProcessDriverVerificationGatewayLane.OfficeEvidenceRead);

        Assert.True(selected.IsSelected);
        Assert.Equal(ProcessVerificationLaneSelectionStatus.Selected, selected.Status);
        Assert.Equal(ProcessDriverVerificationGatewayLane.OfficeEvidenceRead, selected.Lane);
        Assert.NotNull(selected.Registration);
        Assert.Equal(ProcessDriverCapabilityScopeKind.OfficeEvidenceRead, selected.Registration.RequiredScopeKind);

        var unsupported = selector.SelectExact((ProcessDriverVerificationGatewayLane)999);

        Assert.False(unsupported.IsSelected);
        Assert.Equal(ProcessVerificationLaneSelectionStatus.UnsupportedLane, unsupported.Status);
        Assert.Null(unsupported.Registration);

        var missingSelector = new ProcessVerificationLaneSelector(CreateRegistryExcluding(
            ProcessDriverVerificationGatewayLane.OfficeEvidenceRead));
        var missing = missingSelector.SelectExact(ProcessDriverVerificationGatewayLane.OfficeEvidenceRead);

        Assert.False(missing.IsSelected);
        Assert.Equal(ProcessVerificationLaneSelectionStatus.MissingRegistration, missing.Status);
        Assert.Equal(ProcessDriverVerificationGatewayLane.OfficeEvidenceRead, missing.Lane);
        Assert.Null(missing.Registration);
    }

    [Fact]
    public async Task Process_verification_runtime_host_SB020_INV_001_denies_defined_but_unregistered_lane_without_fallback()
    {
        var host = CreateHost(
            new ProcessVerificationRuntimeHostOptions(),
            CreateRegistryExcluding(ProcessDriverVerificationGatewayLane.BusinessAnalysisRead));

        var result = await host.VerifyAsync(CreateBusinessAnalysisHostRequest());

        AssertHostDenial(result, ProcessVerificationHostDenialCode.MissingLaneRegistration);
        var denial = result.Denial ?? throw new InvalidOperationException("Expected a missing registration denial.");
        Assert.Equal(ProcessDriverVerificationGatewayLane.BusinessAnalysisRead, denial.Lane);
        Assert.Contains("No verification lane registration exists for lane BusinessAnalysisRead", denial.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_verification_lane_selector_SB020_INV_002_uses_explicit_registry_without_reflection_discovery_or_fallback()
    {
        var selectorSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessVerificationLaneRegistry.cs");
        var hostSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessVerificationRuntimeHost.cs");
        var combinedSource = string.Concat(selectorSource, Environment.NewLine, hostSource);

        Assert.Contains("ProcessDriverVerificationGatewayLaneRules.AllowedLanes.Select", selectorSource, StringComparison.Ordinal);
        Assert.Contains("SelectExact(", selectorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Reflection", combinedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Assembly.", combinedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Activator.", combinedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTypes(", combinedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Type.GetType", combinedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("dynamic ", combinedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("fallback", combinedSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("discover", combinedSource, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Process_verification_runtime_host_SB018_INV_001_rejects_unsupported_or_empty_lane_without_fallback()
    {
        var host = CreateHost(new ProcessVerificationRuntimeHostOptions());
        var invalidLane = (ProcessDriverVerificationGatewayLane)999;
        var emptyArtifactPayload = new ProcessReadOnlyVerificationBatchPayload(
            ProcessRunId,
            StepRunId,
            "process-consumer:empty-artifact-lane",
            RequestedAt,
            officeEvidencePayloads: [CreateOfficePayload()]);

        var invalidResult = await host.VerifyAsync(new ProcessVerificationHostRequest(
                invalidLane,
                emptyArtifactPayload,
                "process-manager",
                RequestedAt));
        var emptyResult = await host.VerifyAsync(new ProcessVerificationHostRequest(
                ProcessDriverVerificationGatewayLane.ArtifactEvidenceConsistency,
                emptyArtifactPayload,
                "process-manager",
                RequestedAt));

        Assert.True(invalidResult.IsDenied);
        var invalidDenial = invalidResult.Denial ?? throw new InvalidOperationException("Expected an unsupported lane denial.");
        Assert.Equal(ProcessVerificationHostDenialCode.UnsupportedLane, invalidDenial.Code);
        Assert.Contains("Unsupported verification lane", invalidDenial.Message, StringComparison.Ordinal);
        Assert.True(invalidDenial.NoMutationPerformed);
        Assert.False(invalidDenial.AllowsProcessMutation);
        Assert.False(invalidDenial.AllowsTransitionMutation);
        Assert.False(invalidDenial.AllowsFinalizerMutation);
        Assert.Equal(1, invalidDenial.AuditRecord.DeniedCount);

        Assert.True(emptyResult.IsDenied);
        var emptyDenial = emptyResult.Denial ?? throw new InvalidOperationException("Expected a missing lane payload denial.");
        Assert.Equal(ProcessVerificationHostDenialCode.MissingLanePayload, emptyDenial.Code);
        Assert.Contains("No payloads were supplied for lane ArtifactEvidenceConsistency", emptyDenial.Message, StringComparison.Ordinal);
        Assert.True(emptyDenial.NoMutationPerformed);
        Assert.False(emptyDenial.AllowsProcessMutation);
        Assert.False(emptyDenial.AllowsTransitionMutation);
        Assert.False(emptyDenial.AllowsFinalizerMutation);
        Assert.Equal(1, emptyDenial.AuditRecord.DeniedCount);
    }

    [Fact]
    public async Task Process_verification_runtime_host_SB013_INV_001_honors_cancellation_before_verification()
    {
        var host = CreateHost(new ProcessVerificationRuntimeHostOptions());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var request = new ProcessVerificationHostRequest(
            ProcessDriverVerificationGatewayLane.BusinessAnalysisRead,
            new ProcessReadOnlyVerificationBatchPayload(
                ProcessRunId,
                StepRunId,
                "process-manager:cancellation",
                RequestedAt,
                businessAnalysisPayloads: [CreateBusinessPayload()]),
            "process-manager",
            RequestedAt);

        await Assert.ThrowsAsync<OperationCanceledException>(() => host.VerifyAsync(request, cancellation.Token));
    }

    [Fact]
    public void Process_verification_runtime_host_options_SB016_INV_001_validate_configured_limits()
    {
        var services = new ServiceCollection();
        services.AddProcessVerificationRuntimeHost();
        services.AddInMemoryProcessVerificationAuditStoreForTests();
        services.Configure<ProcessVerificationRuntimeHostOptions>(options =>
        {
            options.MaxPayloadItemsPerLane = 0;
        });

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<ProcessVerificationRuntimeHostOptions>>().Value);

        Assert.Contains(
            exception.Failures,
            failure => failure.Contains(nameof(ProcessVerificationRuntimeHostOptions.MaxPayloadItemsPerLane), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Process_verification_runtime_host_options_SB017_INV_001_deny_disabled_host_disabled_lane_and_payload_limit_breaches()
    {
        var request = CreateBusinessAnalysisHostRequest();

        var disabledHostResult = await CreateHost(new ProcessVerificationRuntimeHostOptions
        {
            Enabled = false
        }).VerifyAsync(request);

        AssertHostDenial(disabledHostResult, ProcessVerificationHostDenialCode.HostDisabled);

        var disabledLaneOptions = new ProcessVerificationRuntimeHostOptions();
        disabledLaneOptions.Lanes.BusinessAnalysisRead = false;
        var disabledLaneResult = await CreateHost(disabledLaneOptions).VerifyAsync(request);

        AssertHostDenial(disabledLaneResult, ProcessVerificationHostDenialCode.LaneDisabled);

        var itemLimitResult = await CreateHost(new ProcessVerificationRuntimeHostOptions
        {
            MaxPayloadItemsPerLane = 1
        }).VerifyAsync(new ProcessVerificationHostRequest(
            ProcessDriverVerificationGatewayLane.OfficeEvidenceRead,
            new ProcessReadOnlyVerificationBatchPayload(
                ProcessRunId,
                StepRunId,
                "process-manager:payload-count-limit",
                RequestedAt,
                officeEvidencePayloads: [CreateOfficePayload(), CreateOfficePayload()]),
            "process-manager",
            RequestedAt));

        AssertHostDenial(itemLimitResult, ProcessVerificationHostDenialCode.PayloadLimitExceeded);

        var contentLimitResult = await CreateHost(new ProcessVerificationRuntimeHostOptions
        {
            MaxSuppliedEvidenceContentBytes = 10
        }).VerifyAsync(request);

        AssertHostDenial(contentLimitResult, ProcessVerificationHostDenialCode.SuppliedEvidenceContentLimitExceeded);
    }

    [Fact]
    public async Task Process_verification_runtime_host_status_SB012_INV_001_reports_readiness_and_emergency_disable_without_execution_permission() {
        AppDbContextModelRegistry.ConfigureAssemblies([typeof(ProcessesModuleAssemblyMarker).Assembly]);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISecretRedactor, SecretRedactor>();
        services.AddPooledDbContextFactory<AppDbContext>(options =>
            options.UseInMemoryDatabase($"process-verification-status-{Guid.NewGuid():N}"));
        services.AddProcessVerificationRuntimeHost();
        services.AddEfCoreProcessVerificationAuditStore();

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await using var scope = provider.CreateAsyncScope();
        var statusService = scope.ServiceProvider.GetRequiredService<IProcessVerificationRuntimeHostStatusService>();

        var status = await statusService.GetStatusAsync(new ProcessVerificationRuntimeHostStatusRequest(
            "corr-status-ready",
            "operator-status-test",
            RequestedAt));

        Assert.Equal("corr-status-ready", status.CorrelationId);
        Assert.Equal("operator-status-test", status.RequestedBy);
        Assert.Equal(RequestedAt, status.RequestedAt);
        Assert.True(status.Enabled);
        Assert.False(status.EmergencyDisabled);
        Assert.Equal(ProcessVerificationRuntimeHostReadiness.Ready, status.Readiness);
        Assert.Equal(ProcessVerificationAuditStoreKind.DurableEfCore, status.AuditStoreKind);
        Assert.True(status.UsesDurableAuditStore);
        Assert.True(status.SupportsAuditRetentionQuery);
        Assert.Equal(ProcessRuntimeHostContractSurface.OperatorStatus, status.Contract.Surface);
        Assert.Equal(ProcessRuntimeHostContractVersion.Current, status.Contract.Version);
        Assert.Equal(ProcessDriverVerificationGatewayLaneRules.AllowedLanes.Count + 1, status.Capabilities.Count);
        Assert.Contains(status.Capabilities, capability =>
            capability.Key == ProcessVerificationHostCapabilityCatalog.DryRunExecutionGateKey &&
            capability.Kind == ProcessVerificationHostCapabilityKind.DryRunExecutionGate &&
            capability.ContractSurface == ProcessRuntimeHostContractSurface.DryRunExecution &&
            capability.OperationCategory == ProcessRuntimeHostOperationCategory.DryRunPlanning &&
            capability.IsStaticReadOnlyDescriptor &&
            !capability.ExecutionAllowed);
        Assert.All(status.Capabilities, capability =>
        {
            Assert.False(capability.ReflectionDiscoveryAllowed);
            Assert.False(capability.SelfRegistrationAllowed);
            Assert.All(capability.DeniedOperations, operation =>
                Assert.True(ProcessDriverOperationRules.IsSideEffectOperation(operation)));
        });
        Assert.True(status.NoMutationPerformed);
        Assert.False(status.AllowsProcessMutation);
        Assert.False(status.AllowsTransitionMutation);
        Assert.False(status.AllowsFinalizerMutation);
        Assert.Contains(status.Lanes, lane =>
            lane.Lane == ProcessDriverVerificationGatewayLane.BusinessAnalysisRead &&
            lane.Registered &&
            lane.Enabled &&
            lane.RequiredPermissionMode == ProcessDriverPermissionMode.VerificationOnly);

        var facade = scope.ServiceProvider.GetRequiredService<IProcessManagerReadOnlyVerificationFacade>();
        var facadeStatus = await facade.GetRuntimeHostStatusAsync(new ProcessVerificationRuntimeHostStatusRequest(
            "corr-manager-status",
            "process-manager",
            RequestedAt.AddSeconds(1)));

        Assert.Equal("corr-manager-status", facadeStatus.CorrelationId);
        Assert.Equal("process-manager", facadeStatus.RequestedBy);
        Assert.Equal(RequestedAt.AddSeconds(1), facadeStatus.RequestedAt);
        Assert.Equal(ProcessVerificationRuntimeHostReadiness.Ready, facadeStatus.Readiness);

        var disabledServices = new ServiceCollection();
        disabledServices.AddProcessVerificationRuntimeHost();
        disabledServices.AddInMemoryProcessVerificationAuditStoreForTests();
        disabledServices.Configure<ProcessVerificationRuntimeHostOptions>(options => options.Enabled = false);

        using var disabledProvider = disabledServices.BuildServiceProvider();
        using var disabledScope = disabledProvider.CreateScope();
        var disabledStatus = await disabledScope.ServiceProvider
            .GetRequiredService<IProcessVerificationRuntimeHostStatusService>()
            .GetStatusAsync();

        Assert.False(disabledStatus.Enabled);
        Assert.True(disabledStatus.EmergencyDisabled);
        Assert.Equal(ProcessVerificationRuntimeHostReadiness.EmergencyDisabled, disabledStatus.Readiness);
        Assert.Equal(ProcessVerificationAuditStoreKind.TestInMemory, disabledStatus.AuditStoreKind);
        Assert.False(disabledStatus.UsesDurableAuditStore);
        Assert.True(disabledStatus.SupportsAuditRetentionQuery);
        Assert.Equal(ProcessRuntimeHostContractSurface.OperatorStatus, disabledStatus.Contract.Surface);
        Assert.True(disabledStatus.NoMutationPerformed);
        Assert.False(disabledStatus.AllowsProcessMutation);
        Assert.False(disabledStatus.AllowsTransitionMutation);
        Assert.False(disabledStatus.AllowsFinalizerMutation);
    }

    [Fact]
    public async Task Process_verification_runtime_host_SB046_INV_001_classifies_denials_with_reason_codes_audit_and_no_mutation_flags()
    {
        var request = CreateBusinessAnalysisHostRequest();
        var denialScenarios = new Dictionary<ProcessVerificationHostDenialCode, ProcessVerificationHostFailureCategory>
        {
            [ProcessVerificationHostDenialCode.HostDisabled] = ProcessVerificationHostFailureCategory.OperationalPolicy,
            [ProcessVerificationHostDenialCode.LaneDisabled] = ProcessVerificationHostFailureCategory.OperationalPolicy,
            [ProcessVerificationHostDenialCode.UnsupportedLane] = ProcessVerificationHostFailureCategory.RequestValidation,
            [ProcessVerificationHostDenialCode.MissingLaneRegistration] = ProcessVerificationHostFailureCategory.LaneConfiguration,
            [ProcessVerificationHostDenialCode.MissingLanePayload] = ProcessVerificationHostFailureCategory.RequestValidation,
            [ProcessVerificationHostDenialCode.PayloadLimitExceeded] = ProcessVerificationHostFailureCategory.ResourceLimit,
            [ProcessVerificationHostDenialCode.SuppliedEvidenceContentLimitExceeded] = ProcessVerificationHostFailureCategory.ResourceLimit
        };

        foreach (var scenario in denialScenarios)
        {
            var denial = await CreateDenialForCodeAsync(scenario.Key, request);

            Assert.Equal(scenario.Value, denial.Category);
            Assert.Equal(scenario.Key, denial.Code);
            Assert.Equal(ProcessRuntimeHostContractSurface.VerificationHost, denial.Contract.Surface);
            Assert.Equal(ProcessRuntimeHostContractVersion.Current, denial.Contract.Version);
            Assert.False(string.IsNullOrWhiteSpace(denial.Message));
            Assert.True(denial.NoMutationPerformed);
            Assert.False(denial.AllowsProcessMutation);
            Assert.False(denial.AllowsTransitionMutation);
            Assert.False(denial.AllowsFinalizerMutation);
            Assert.Equal(request.Payload.ProcessRunId, denial.ProcessRunId);
            Assert.Equal(request.Payload.StepRunId, denial.StepRunId);
            Assert.Equal(request.RequestedBy, denial.RequestedBy);
            Assert.Equal(0, denial.AuditRecord.ResponseCount);
            Assert.Equal(0, denial.AuditRecord.AcceptedCount);
            Assert.Equal(1, denial.AuditRecord.DeniedCount);
            Assert.Matches("^[A-F0-9]{64}$", denial.AuditRecord.ObservationHash);
        }

        Assert.Equal(
            ProcessVerificationHostFailureCategory.VerificationOutcome,
            ProcessVerificationHostDenialClassifier.Classify(ProcessVerificationHostDenialCode.NoResponsesProduced));
    }

    [Fact]
    public void Process_manager_readonly_verification_command_SB024_INV_001_returns_diagnostics_and_audit_without_mutation()
    {
        var auditStore = new InMemoryProcessVerificationAuditStore();
        var command = new ProcessManagerReadOnlyVerificationCommandService(
            CreateHost(new ProcessVerificationRuntimeHostOptions(), auditStore: auditStore),
            auditStore);
        var payload = new ProcessReadOnlyVerificationBatchPayload(
            ProcessRunId,
            StepRunId,
            "process-manager:diagnostics",
            RequestedAt,
            businessAnalysisPayloads: [CreateBusinessPayload()]);
        var request = new ProcessManagerReadOnlyVerificationCommandRequest(
            ProcessDriverVerificationGatewayLane.BusinessAnalysisRead,
            payload,
            ProcessManagerReadOnlyVerificationProjectionMode.Diagnostics,
            "process-manager",
            RequestedAt);

        var result = command.Run(request);

        Assert.Equal(ProcessDriverVerificationGatewayLane.BusinessAnalysisRead, result.Lane);
        Assert.True(result.NoMutationPerformed);
        Assert.False(result.AllowsProcessMutation);
        Assert.False(result.AllowsTransitionMutation);
        Assert.False(result.AllowsFinalizerMutation);
        Assert.Equal(ProcessManagerReadOnlyVerificationProjectionMode.Diagnostics, result.Projection.Mode);
        Assert.True(result.Projection.IsAttached);
        Assert.NotEmpty(result.Projection.Diagnostics);
        Assert.Equal(result.AuditRecord.Id, result.Projection.AuditRecordId);
        Assert.Equal(result.Observation.ResponseCount, result.AuditRecord.ResponseCount);
    }

    [Fact]
    public async Task Process_manager_readonly_verification_facade_SB025_INV_001_returns_structured_success_and_audit_query_without_mutation()
    {
        var services = new ServiceCollection();
        services.AddProcessVerificationRuntimeHost();
        services.AddInMemoryProcessVerificationAuditStoreForTests();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var facade = scope.ServiceProvider.GetRequiredService<IProcessManagerReadOnlyVerificationFacade>();
        var payload = new ProcessReadOnlyVerificationBatchPayload(
            ProcessRunId,
            StepRunId,
            "process-manager:facade-diagnostics",
            RequestedAt,
            businessAnalysisPayloads: [CreateBusinessPayload()]);

        var result = await facade.VerifyAsync(new ProcessManagerReadOnlyVerificationCommandRequest(
            ProcessDriverVerificationGatewayLane.BusinessAnalysisRead,
            payload,
            ProcessManagerReadOnlyVerificationProjectionMode.Diagnostics,
            " process-manager ",
            RequestedAt));

        Assert.True(result.IsSuccess);
        Assert.False(result.IsDenied);
        Assert.True(result.NoMutationPerformed);
        Assert.False(result.AllowsProcessMutation);
        Assert.False(result.AllowsTransitionMutation);
        Assert.False(result.AllowsFinalizerMutation);
        var response = result.Response ?? throw new InvalidOperationException("Expected a manager read-only verification response.");
        Assert.Equal(ProcessDriverVerificationGatewayLane.BusinessAnalysisRead, response.Lane);
        Assert.Equal(ProcessManagerReadOnlyVerificationProjectionMode.Diagnostics, response.Projection.Mode);
        Assert.Equal(ProcessManagerReadOnlyVerificationProjectionSource.SuppliedEvidenceOnly, response.Projection.Source);
        Assert.Equal("process-manager", response.Projection.RequestedBy);
        Assert.Equal(response.AuditRecord.Id, response.Projection.AuditRecordId);
        Assert.True(response.NoMutationPerformed);
        Assert.False(response.AllowsProcessMutation);
        Assert.False(response.AllowsTransitionMutation);
        Assert.False(response.AllowsFinalizerMutation);

        var auditReadback = await facade.ListAuditAsync(new ProcessManagerReadOnlyVerificationAuditQueryRequest(
            "manager-auditor",
            ProcessRunId,
            StepRunId,
            ProcessDriverVerificationGatewayLane.BusinessAnalysisRead,
            limit: 20));

        Assert.Equal("manager-auditor", auditReadback.RequestedBy);
        Assert.True(auditReadback.NoMutationPerformed);
        Assert.False(auditReadback.AllowsProcessMutation);
        Assert.False(auditReadback.AllowsTransitionMutation);
        Assert.False(auditReadback.AllowsFinalizerMutation);
        Assert.Contains(auditReadback.Records, record => record.Id == response.AuditRecord.Id);
    }

    [Fact]
    public async Task Process_verification_audit_retention_query_SB003_INV_001_lists_old_records_without_deleting_or_mutating()
    {
        var auditStore = new InMemoryProcessVerificationAuditStore();
        var oldRecord = new ProcessVerificationAuditRecord(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            RequestedAt.AddDays(-40),
            ProcessRunId,
            StepRunId,
            "process-manager",
            ProcessDriverVerificationGatewayLane.BusinessAnalysisRead,
            ResponseCount: 1,
            AcceptedCount: 1,
            DeniedCount: 0,
            NoMutationPerformed: true,
            AllowsProcessMutation: false,
            AllowsTransitionMutation: false,
            AllowsFinalizerMutation: false,
            ProcessDriverEvidencePolicy.ComputeSha256("old-record"));
        var currentRecord = oldRecord with
        {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            RecordedAt = RequestedAt,
            ObservationHash = ProcessDriverEvidencePolicy.ComputeSha256("current-record")
        };

        await auditStore.AppendAsync(oldRecord);
        await auditStore.AppendAsync(currentRecord);

        var retentionCandidates = await auditStore.ListRetentionCandidatesAsync(
            new ProcessVerificationAuditRetentionQuery(RequestedAt.AddDays(-7), limit: 10));
        var allRecords = await auditStore.ListAsync();

        var candidate = Assert.Single(retentionCandidates);
        Assert.Equal(oldRecord.Id, candidate.Id);
        Assert.Equal(2, allRecords.Count);
        Assert.Contains(allRecords, record => record.Id == oldRecord.Id);
        Assert.Contains(allRecords, record => record.Id == currentRecord.Id);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProcessVerificationAuditRetentionQuery(RequestedAt, limit: 0));
    }

    [Fact]
    public async Task Process_manager_readonly_verification_facade_SB026_INV_001_enforces_requester_projection_query_and_denial_guards()
    {
        var payload = new ProcessReadOnlyVerificationBatchPayload(
            ProcessRunId,
            StepRunId,
            "process-manager:facade-guards",
            RequestedAt,
            businessAnalysisPayloads: [CreateBusinessPayload()]);

        Assert.Throws<ArgumentException>(() => new ProcessManagerReadOnlyVerificationCommandRequest(
            ProcessDriverVerificationGatewayLane.BusinessAnalysisRead,
            payload,
            ProcessManagerReadOnlyVerificationProjectionMode.Diagnostics,
            " ",
            RequestedAt));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProcessManagerReadOnlyVerificationCommandRequest(
            ProcessDriverVerificationGatewayLane.BusinessAnalysisRead,
            payload,
            (ProcessManagerReadOnlyVerificationProjectionMode)999,
            "process-manager",
            RequestedAt));
        Assert.Throws<ArgumentException>(() => new ProcessManagerReadOnlyVerificationAuditQueryRequest(" "));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProcessManagerReadOnlyVerificationAuditQueryRequest(
            "process-manager",
            limit: 0));
        Assert.Throws<ArgumentException>(() => new ProcessManagerReadOnlyVerificationAuditQueryRequest(
            "process-manager",
            recordedAtOrAfter: RequestedAt,
            recordedBefore: RequestedAt));

        var facade = new ProcessManagerReadOnlyVerificationCommandService(
            CreateHost(new ProcessVerificationRuntimeHostOptions
            {
                Enabled = false
            }),
            new InMemoryProcessVerificationAuditStore());
        var request = new ProcessManagerReadOnlyVerificationCommandRequest(
            ProcessDriverVerificationGatewayLane.BusinessAnalysisRead,
            payload,
            ProcessManagerReadOnlyVerificationProjectionMode.Diagnostics,
            "process-manager",
            RequestedAt);

        var result = await facade.VerifyAsync(request);

        Assert.True(result.IsDenied);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Response);
        var denial = result.Denial ?? throw new InvalidOperationException("Expected a manager read-only verification denial.");
        Assert.Equal(ProcessVerificationHostDenialCode.HostDisabled, denial.Code);
        Assert.True(result.NoMutationPerformed);
        Assert.False(result.AllowsProcessMutation);
        Assert.False(result.AllowsTransitionMutation);
        Assert.False(result.AllowsFinalizerMutation);
        Assert.Throws<InvalidOperationException>(() => facade.Run(request));
    }

    [Fact]
    public async Task Process_manager_verification_readback_SB028_INV_001_exposes_diagnostics_dto_and_audit_records()
    {
        var services = new ServiceCollection();
        services.AddProcessVerificationRuntimeHost();
        services.AddInMemoryProcessVerificationAuditStoreForTests();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var facade = scope.ServiceProvider.GetRequiredService<IProcessManagerReadOnlyVerificationFacade>();
        var payload = new ProcessReadOnlyVerificationBatchPayload(
            ProcessRunId,
            StepRunId,
            "process-manager:readback-diagnostics",
            RequestedAt,
            businessAnalysisPayloads: [CreateBusinessPayload()]);
        var verificationRequest = new ProcessManagerReadOnlyVerificationCommandRequest(
            ProcessDriverVerificationGatewayLane.BusinessAnalysisRead,
            payload,
            ProcessManagerReadOnlyVerificationProjectionMode.Diagnostics,
            "process-manager",
            RequestedAt);

        var readback = await facade.VerifyForReadbackAsync(new ProcessManagerReadOnlyVerificationReadbackRequest(
            verificationRequest,
            auditRecordLimit: 10));

        Assert.Equal(ProcessManagerReadOnlyVerificationFacadeStatus.Succeeded, readback.Status);
        Assert.Equal(
            ProcessVerificationHostCapabilityCatalog.VerificationLaneKey(ProcessDriverVerificationGatewayLane.BusinessAnalysisRead),
            readback.CapabilityKey);
        Assert.Equal(ProcessDriverVerificationGatewayLane.BusinessAnalysisRead, readback.Lane);
        Assert.Equal(ProcessRunId, readback.ProcessRunId);
        Assert.Equal(StepRunId, readback.StepRunId);
        Assert.Equal("process-manager:readback-diagnostics", readback.CallerContext);
        Assert.Equal(ProcessManagerReadOnlyVerificationProjectionMode.Diagnostics, readback.ProjectionMode);
        Assert.Equal(ProcessManagerReadOnlyVerificationProjectionSource.SuppliedEvidenceOnly, readback.ProjectionSource);
        Assert.True(readback.ProjectionAttached);
        Assert.True(readback.AuditRecordId.HasValue);
        Assert.True(readback.ResponseCount > 0);
        Assert.Equal(readback.Diagnostics.Count, readback.DiagnosticCount);
        Assert.True(readback.EvidenceReferenceCount > 0);
        Assert.NotEmpty(readback.Diagnostics);
        Assert.Contains(readback.AuditRecords, record => record.Id == readback.AuditRecordId);
        Assert.All(readback.AuditRecords, record => Assert.Matches("^[A-F0-9]{64}$", record.ObservationHash));
        Assert.Equal(
            readback.AuditRecords.Single(record => record.Id == readback.AuditRecordId).ObservationHash,
            readback.AuditRecordObservationHash);
        Assert.Equal(ProcessRuntimeHostContractSurface.ManagerReadback, readback.Contract.Surface);
        Assert.True(readback.NoMutationPerformed);
        Assert.False(readback.AllowsProcessMutation);
        Assert.False(readback.AllowsTransitionMutation);
        Assert.False(readback.AllowsFinalizerMutation);
        Assert.Null(readback.DenialCategory);
        Assert.Null(readback.DenialCode);
        Assert.Equal(string.Empty, readback.DenialMessage);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProcessManagerReadOnlyVerificationReadbackRequest(
            verificationRequest,
            auditRecordLimit: 0));
    }

    [Fact]
    public async Task Process_manager_verification_readback_SB047_INV_001_projects_denial_category_reason_code_audit_and_no_mutation_flags()
    {
        var options = new ProcessVerificationRuntimeHostOptions
        {
            Enabled = false
        };
        var auditStore = new InMemoryProcessVerificationAuditStore();
        var host = CreateHost(options, auditStore: auditStore);
        var facade = new ProcessManagerReadOnlyVerificationCommandService(host, auditStore);
        var payload = new ProcessReadOnlyVerificationBatchPayload(
            ProcessRunId,
            StepRunId,
            "process-manager:denial-readback",
            RequestedAt,
            businessAnalysisPayloads: [CreateBusinessPayload()]);
        var verificationRequest = new ProcessManagerReadOnlyVerificationCommandRequest(
            ProcessDriverVerificationGatewayLane.BusinessAnalysisRead,
            payload,
            ProcessManagerReadOnlyVerificationProjectionMode.Diagnostics,
            "process-manager",
            RequestedAt);

        var readback = await facade.VerifyForReadbackAsync(new ProcessManagerReadOnlyVerificationReadbackRequest(
            verificationRequest,
            auditRecordLimit: 10));

        Assert.Equal(ProcessManagerReadOnlyVerificationFacadeStatus.Denied, readback.Status);
        Assert.Equal(
            ProcessVerificationHostCapabilityCatalog.VerificationLaneKey(ProcessDriverVerificationGatewayLane.BusinessAnalysisRead),
            readback.CapabilityKey);
        Assert.Equal(ProcessDriverVerificationGatewayLane.BusinessAnalysisRead, readback.Lane);
        Assert.Equal(ProcessRunId, readback.ProcessRunId);
        Assert.Equal(StepRunId, readback.StepRunId);
        Assert.Equal("process-manager:denial-readback", readback.CallerContext);
        Assert.Equal(ProcessManagerReadOnlyVerificationProjectionMode.Diagnostics, readback.ProjectionMode);
        Assert.False(readback.ProjectionAttached);
        Assert.Null(readback.ProjectionSource);
        Assert.Empty(readback.Diagnostics);
        Assert.Equal(0, readback.DiagnosticCount);
        Assert.Equal(0, readback.ResponseCount);
        Assert.Equal(0, readback.EvidenceReferenceCount);
        Assert.Equal(ProcessVerificationHostFailureCategory.OperationalPolicy, readback.DenialCategory);
        Assert.Equal(ProcessVerificationHostDenialCode.HostDisabled, readback.DenialCode);
        Assert.Contains("disabled by options", readback.DenialMessage, StringComparison.Ordinal);
        Assert.True(readback.AuditRecordId.HasValue);
        var auditRecord = Assert.Single(readback.AuditRecords);
        Assert.Equal(readback.AuditRecordId, auditRecord.Id);
        Assert.Equal(0, auditRecord.ResponseCount);
        Assert.Equal(0, auditRecord.AcceptedCount);
        Assert.Equal(1, auditRecord.DeniedCount);
        Assert.True(auditRecord.NoMutationPerformed);
        Assert.False(auditRecord.AllowsProcessMutation);
        Assert.False(auditRecord.AllowsTransitionMutation);
        Assert.False(auditRecord.AllowsFinalizerMutation);
        Assert.Matches("^[A-F0-9]{64}$", auditRecord.ObservationHash);
        Assert.Equal(auditRecord.ObservationHash, readback.AuditRecordObservationHash);
        Assert.Equal(ProcessRuntimeHostContractSurface.ManagerReadback, readback.Contract.Surface);
        Assert.True(readback.NoMutationPerformed);
        Assert.False(readback.AllowsProcessMutation);
        Assert.False(readback.AllowsTransitionMutation);
        Assert.False(readback.AllowsFinalizerMutation);
    }

    [Fact]
    public async Task Process_manager_verification_readback_api_smoke_SB029_INV_001_serializes_diagnostics_projection_without_mutation_permissions()
    {
        var services = new ServiceCollection();
        services.AddProcessVerificationRuntimeHost();
        services.AddInMemoryProcessVerificationAuditStoreForTests();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var facade = scope.ServiceProvider.GetRequiredService<IProcessManagerReadOnlyVerificationFacade>();
        var payload = new ProcessReadOnlyVerificationBatchPayload(
            ProcessRunId,
            StepRunId,
            "process-manager:api-smoke-diagnostics",
            RequestedAt,
            businessAnalysisPayloads: [CreateBusinessPayload()]);

        var readback = await facade.VerifyForReadbackAsync(new ProcessManagerReadOnlyVerificationReadbackRequest(
            new ProcessManagerReadOnlyVerificationCommandRequest(
                ProcessDriverVerificationGatewayLane.BusinessAnalysisRead,
                payload,
                ProcessManagerReadOnlyVerificationProjectionMode.Diagnostics,
                "process-manager",
                RequestedAt)));
        var json = System.Text.Json.JsonSerializer.Serialize(
            readback,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("diagnostics", out var diagnostics));
        Assert.True(root.TryGetProperty("auditRecords", out var auditRecords));
        Assert.Equal(
            ProcessVerificationHostCapabilityCatalog.VerificationLaneKey(ProcessDriverVerificationGatewayLane.BusinessAnalysisRead),
            root.GetProperty("capabilityKey").GetString());
        Assert.True(root.GetProperty("evidenceReferenceCount").GetInt32() > 0);
        Assert.Matches("^[A-F0-9]{64}$", root.GetProperty("auditRecordObservationHash").GetString());
        Assert.Equal((int)ProcessRuntimeHostContractSurface.ManagerReadback, root.GetProperty("contract").GetProperty("surface").GetInt32());
        Assert.True(diagnostics.GetArrayLength() > 0);
        Assert.True(auditRecords.GetArrayLength() > 0);
        Assert.True(root.GetProperty("noMutationPerformed").GetBoolean());
        Assert.False(root.GetProperty("allowsProcessMutation").GetBoolean());
        Assert.False(root.GetProperty("allowsTransitionMutation").GetBoolean());
        Assert.False(root.GetProperty("allowsFinalizerMutation").GetBoolean());
    }

    [Fact]
    public async Task Process_runtime_host_readback_SB06_INV_001_uses_real_process_run_and_step_ids_without_mutation() {
        await using var application = await ProcessTemplateAutomationTestSupport.CreateProcessMockEnabledApplicationAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();

        var projectId = await ProcessTemplateAutomationTestSupport.CreateProjectAsync(
            projectsService,
            "Runtime-host readback automation validation project",
            "Verification");
        var runResult = await ProcessTemplateAutomationTestSupport.ExecuteTemplateWithProcessMockAgentsAsync(
            scope.ServiceProvider,
            "business-plan-development",
            projectId,
            "Runtime-host readback automation validation",
            "Runtime-host readback automation validation launch",
            "Validate runtime-host manager readback against a real automation-dispatched process run and step.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["business-strategist"] = ProcessMockAgentRoleKeys.BusinessStrategist,
                ["financial-strategist"] = ProcessMockAgentRoleKeys.FinancialStrategist,
                ["marketing-specialist"] = ProcessMockAgentRoleKeys.MarketingSpecialist
            },
            timeout: TimeSpan.FromSeconds(90));
        var completedStep = Assert.Single(
            runResult.StepRuns,
            step => string.Equals(step.Title, "Review integrated business plan", StringComparison.Ordinal));
        Assert.Equal(ProcessStepRunStatus.Completed, completedStep.Status);

        var requestedAt = DateTimeOffset.UtcNow;
        var payload = new ProcessReadOnlyVerificationBatchPayload(
            runResult.RunId,
            completedStep.Id,
            "process-manager:sb06-real-run-readback",
            requestedAt,
            businessAnalysisPayloads: [CreateBusinessPayload()]);
        var facade = scope.ServiceProvider.GetRequiredService<IProcessManagerReadOnlyVerificationFacade>();
        var readback = await facade.VerifyForReadbackAsync(new ProcessManagerReadOnlyVerificationReadbackRequest(
            new ProcessManagerReadOnlyVerificationCommandRequest(
                ProcessDriverVerificationGatewayLane.BusinessAnalysisRead,
                payload,
                ProcessManagerReadOnlyVerificationProjectionMode.Diagnostics,
                "process-manager",
                requestedAt),
            auditRecordLimit: 10));

        Assert.Equal(ProcessManagerReadOnlyVerificationFacadeStatus.Succeeded, readback.Status);
        Assert.Equal(runResult.RunId, readback.ProcessRunId);
        Assert.Equal(completedStep.Id, readback.StepRunId);
        Assert.Equal(
            ProcessVerificationHostCapabilityCatalog.VerificationLaneKey(ProcessDriverVerificationGatewayLane.BusinessAnalysisRead),
            readback.CapabilityKey);
        Assert.True(readback.AuditRecordId.HasValue);
        Assert.True(readback.EvidenceReferenceCount > 0);
        Assert.Matches("^[A-F0-9]{64}$", readback.AuditRecordObservationHash);
        Assert.Null(readback.DenialCategory);
        Assert.Null(readback.DenialCode);
        Assert.Equal(string.Empty, readback.DenialMessage);
        Assert.True(readback.NoMutationPerformed);
        Assert.False(readback.AllowsProcessMutation);
        Assert.False(readback.AllowsTransitionMutation);
        Assert.False(readback.AllowsFinalizerMutation);
        Assert.Equal(ProcessRuntimeHostContractVersion.Current, readback.Contract.Version);
        Assert.Equal(ProcessRuntimeHostContractSurface.ManagerReadback, readback.Contract.Surface);
        Assert.True(readback.Contract.IsReadOnlySafe);
        Assert.Empty(readback.Contract.ValidateReadOnlySafety());

        var status = await scope.ServiceProvider.GetRequiredService<IProcessVerificationRuntimeHostStatusService>()
            .GetStatusAsync(new ProcessVerificationRuntimeHostStatusRequest(
                "sb06-real-run-status",
                "process-manager",
                requestedAt));

        Assert.Equal("sb06-real-run-status", status.CorrelationId);
        Assert.Equal(ProcessVerificationRuntimeHostReadiness.Ready, status.Readiness);
        Assert.True(status.NoMutationPerformed);
        Assert.False(status.AllowsProcessMutation);
        Assert.False(status.AllowsTransitionMutation);
        Assert.False(status.AllowsFinalizerMutation);
        Assert.Equal(ProcessRuntimeHostContractVersion.Current, status.Contract.Version);
        Assert.Equal(ProcessRuntimeHostContractSurface.OperatorStatus, status.Contract.Surface);
        Assert.True(status.Contract.IsReadOnlySafe);
        Assert.Empty(status.Contract.ValidateReadOnlySafety());

        var dryRunHost = new ProcessDryRunExecutionHost(new ProcessExecutionCapableDriverFutureGate());
        var dryRun = await dryRunHost.EvaluateAsync(new ProcessDryRunExecutionRequest(
            Guid.NewGuid(),
            runResult.RunId,
            completedStep.Id,
            "process-operator",
            "SB06 dry-run readback for real process run",
            [ProcessExecutionCapableDriverSurface.CommandExecution],
            [ProcessDriverOperation.ExecuteCommand],
            ProcessExecutionCapableDriverSandboxPolicy.DefaultBlockedDryRun,
            ProcessExecutionCapableDriverApprovalEvidence.None,
            requestedAt));
        var dryRunReadback = ProcessManagerRuntimeHostDryRunReadbackMapper.Project(dryRun);

        Assert.Equal(runResult.RunId, dryRunReadback.ProcessRunId);
        Assert.Equal(completedStep.Id, dryRunReadback.StepRunId);
        Assert.Equal(ProcessDryRunExecutionHostDecision.Denied, dryRunReadback.Decision);
        Assert.Equal(ProcessVerificationHostCapabilityCatalog.DryRunExecutionGateKey, dryRunReadback.CapabilityKey);
        Assert.True(dryRunReadback.DenialCount > 0);
        Assert.True(dryRunReadback.AuthorizationGapCount > 0);
        Assert.False(string.IsNullOrWhiteSpace(dryRunReadback.AuditReferenceId));
        Assert.Matches("^[a-f0-9]{64}$", dryRunReadback.AuditReferenceContentHash);
        Assert.True(dryRunReadback.NoMutationPerformed);
        Assert.False(dryRunReadback.AllowsProcessMutation);
        Assert.False(dryRunReadback.AllowsTransitionMutation);
        Assert.False(dryRunReadback.AllowsFinalizerMutation);
        Assert.Contains(ProcessExecutionCapableDriverSurface.CommandExecution, dryRunReadback.DeniedSurfaces);
        Assert.Contains(ProcessDriverOperation.ExecuteCommand, dryRunReadback.DeniedOperations);
        Assert.Contains(dryRunReadback.Denials, denial => denial.Category == ProcessRuntimeHostDenialCategory.SideEffect);
        Assert.Equal(ProcessRuntimeHostContractVersion.Current, dryRunReadback.Contract.Version);
        Assert.Equal(ProcessRuntimeHostContractSurface.DryRunExecution, dryRunReadback.Contract.Surface);
        Assert.True(dryRunReadback.Contract.IsReadOnlySafe);
        Assert.Empty(dryRunReadback.Contract.ValidateReadOnlySafety());
        Assert.NotNull(dryRunReadback.Contract.RequestIdentity);
        var requestIdentity = dryRunReadback.Contract.RequestIdentity!;
        Assert.Equal(runResult.RunId, requestIdentity.ProcessRunId);
        Assert.Equal(completedStep.Id, requestIdentity.StepRunId);
        Assert.NotNull(dryRunReadback.Contract.SandboxDecision);
        var sandboxDecision = dryRunReadback.Contract.SandboxDecision!;
        Assert.Equal(ProcessRuntimeHostSandboxDecisionKind.Denied, sandboxDecision.Kind);
        Assert.False(sandboxDecision.ExecutionAllowed);
        Assert.True(sandboxDecision.DryRunOnly);
        Assert.Contains(ProcessRuntimeHostEffectSurface.LocalCommand, sandboxDecision.DeniedSurfaces);
    }

    [Fact]
    public async Task Process_manager_diagnostics_operator_smoke_SB055_INV_001_serializes_large_screen_api_readback_with_audit_contract()
    {
        var services = new ServiceCollection();
        services.AddProcessVerificationRuntimeHost();
        services.AddInMemoryProcessVerificationAuditStoreForTests();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var facade = scope.ServiceProvider.GetRequiredService<IProcessManagerReadOnlyVerificationFacade>();
        var payload = new ProcessReadOnlyVerificationBatchPayload(
            ProcessRunId,
            StepRunId,
            "process-manager:large-screen-api-smoke",
            RequestedAt,
            businessAnalysisPayloads: [CreateBusinessPayload()]);
        var readback = await facade.VerifyForReadbackAsync(new ProcessManagerReadOnlyVerificationReadbackRequest(
            new ProcessManagerReadOnlyVerificationCommandRequest(
                ProcessDriverVerificationGatewayLane.BusinessAnalysisRead,
                payload,
                ProcessManagerReadOnlyVerificationProjectionMode.Diagnostics,
                "process-manager",
                RequestedAt),
            auditRecordLimit: 10));
        var json = System.Text.Json.JsonSerializer.Serialize(
            readback,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(ProcessRunId.ToString("D"), root.GetProperty("processRunId").GetString());
        Assert.Equal(StepRunId.ToString("D"), root.GetProperty("stepRunId").GetString());
        Assert.Equal(
            ProcessVerificationHostCapabilityCatalog.VerificationLaneKey(ProcessDriverVerificationGatewayLane.BusinessAnalysisRead),
            root.GetProperty("capabilityKey").GetString());
        Assert.Equal("process-manager:large-screen-api-smoke", root.GetProperty("callerContext").GetString());
        Assert.True(root.GetProperty("projectionAttached").GetBoolean());
        Assert.True(root.GetProperty("responseCount").GetInt32() > 0);
        Assert.True(root.GetProperty("diagnosticCount").GetInt32() > 0);
        Assert.True(root.GetProperty("evidenceReferenceCount").GetInt32() > 0);
        Assert.Equal((int)ProcessRuntimeHostContractSurface.ManagerReadback, root.GetProperty("contract").GetProperty("surface").GetInt32());

        var auditRecordId = root.GetProperty("auditRecordId").GetString();
        var auditRecord = Assert.Single(
            root.GetProperty("auditRecords").EnumerateArray(),
            item => string.Equals(item.GetProperty("id").GetString(), auditRecordId, StringComparison.Ordinal));
        Assert.Equal(auditRecord.GetProperty("observationHash").GetString(), root.GetProperty("auditRecordObservationHash").GetString());
        Assert.True(auditRecord.GetProperty("responseCount").GetInt32() > 0);
        Assert.True(auditRecord.GetProperty("acceptedCount").GetInt32() > 0);
        Assert.Equal(0, auditRecord.GetProperty("deniedCount").GetInt32());
        Assert.True(auditRecord.GetProperty("noMutationPerformed").GetBoolean());
        Assert.False(auditRecord.GetProperty("allowsProcessMutation").GetBoolean());
        Assert.False(auditRecord.GetProperty("allowsTransitionMutation").GetBoolean());
        Assert.False(auditRecord.GetProperty("allowsFinalizerMutation").GetBoolean());
        Assert.Matches("^[A-F0-9]{64}$", auditRecord.GetProperty("observationHash").GetString());
        Assert.True(root.GetProperty("noMutationPerformed").GetBoolean());
        Assert.False(root.GetProperty("allowsProcessMutation").GetBoolean());
        Assert.False(root.GetProperty("allowsTransitionMutation").GetBoolean());
        Assert.False(root.GetProperty("allowsFinalizerMutation").GetBoolean());
    }

    [Fact]
    public async Task Process_run_detail_verification_audit_readback_SB056_INV_001_projects_process_step_audit_and_denial_metadata_without_mutation()
    {
        var auditStore = new InMemoryProcessVerificationAuditStore();
        var host = CreateHost(
            new ProcessVerificationRuntimeHostOptions
            {
                Enabled = false
            },
            auditStore: auditStore);
        var facade = new ProcessManagerReadOnlyVerificationCommandService(host, auditStore);
        var payload = new ProcessReadOnlyVerificationBatchPayload(
            ProcessRunId,
            StepRunId,
            "process-run-detail:verification-audit-readback",
            RequestedAt,
            businessAnalysisPayloads: [CreateBusinessPayload()]);
        var readback = await facade.VerifyForReadbackAsync(new ProcessManagerReadOnlyVerificationReadbackRequest(
            new ProcessManagerReadOnlyVerificationCommandRequest(
                ProcessDriverVerificationGatewayLane.BusinessAnalysisRead,
                payload,
                ProcessManagerReadOnlyVerificationProjectionMode.Diagnostics,
                "process-run-detail",
                RequestedAt),
            auditRecordLimit: 10));
        var json = System.Text.Json.JsonSerializer.Serialize(
            readback,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(ProcessRunId.ToString("D"), root.GetProperty("processRunId").GetString());
        Assert.Equal(StepRunId.ToString("D"), root.GetProperty("stepRunId").GetString());
        Assert.Equal(
            ProcessVerificationHostCapabilityCatalog.VerificationLaneKey(ProcessDriverVerificationGatewayLane.BusinessAnalysisRead),
            root.GetProperty("capabilityKey").GetString());
        Assert.Equal("process-run-detail:verification-audit-readback", root.GetProperty("callerContext").GetString());
        Assert.False(root.GetProperty("projectionAttached").GetBoolean());
        Assert.Equal(0, root.GetProperty("responseCount").GetInt32());
        Assert.Equal(0, root.GetProperty("diagnosticCount").GetInt32());
        Assert.Equal(0, root.GetProperty("evidenceReferenceCount").GetInt32());
        Assert.Equal((int)ProcessRuntimeHostContractSurface.ManagerReadback, root.GetProperty("contract").GetProperty("surface").GetInt32());
        Assert.Equal((int)ProcessVerificationHostFailureCategory.OperationalPolicy, root.GetProperty("denialCategory").GetInt32());
        Assert.Equal((int)ProcessVerificationHostDenialCode.HostDisabled, root.GetProperty("denialCode").GetInt32());
        Assert.Contains("disabled by options", root.GetProperty("denialMessage").GetString(), StringComparison.Ordinal);

        var auditRecord = Assert.Single(root.GetProperty("auditRecords").EnumerateArray());
        Assert.Equal(root.GetProperty("auditRecordId").GetString(), auditRecord.GetProperty("id").GetString());
        Assert.Equal(auditRecord.GetProperty("observationHash").GetString(), root.GetProperty("auditRecordObservationHash").GetString());
        Assert.Equal(0, auditRecord.GetProperty("responseCount").GetInt32());
        Assert.Equal(0, auditRecord.GetProperty("acceptedCount").GetInt32());
        Assert.Equal(1, auditRecord.GetProperty("deniedCount").GetInt32());
        Assert.True(auditRecord.GetProperty("noMutationPerformed").GetBoolean());
        Assert.False(auditRecord.GetProperty("allowsProcessMutation").GetBoolean());
        Assert.False(auditRecord.GetProperty("allowsTransitionMutation").GetBoolean());
        Assert.False(auditRecord.GetProperty("allowsFinalizerMutation").GetBoolean());
        Assert.Matches("^[A-F0-9]{64}$", auditRecord.GetProperty("observationHash").GetString());
        Assert.True(root.GetProperty("noMutationPerformed").GetBoolean());
        Assert.False(root.GetProperty("allowsProcessMutation").GetBoolean());
        Assert.False(root.GetProperty("allowsTransitionMutation").GetBoolean());
        Assert.False(root.GetProperty("allowsFinalizerMutation").GetBoolean());
    }

    [Fact]
    public void Process_readonly_verification_job_SB031_INV_001_models_scheduler_and_workflow_jobs_as_manager_readback_requests_without_mutation()
    {
        var payload = new ProcessReadOnlyVerificationBatchPayload(
            ProcessRunId,
            StepRunId,
            "process-manager:scheduled-verification",
            RequestedAt,
            businessAnalysisPayloads: [CreateBusinessPayload()]);
        var schedulerJob = new ProcessReadOnlyVerificationJob(
            Guid.NewGuid(),
            ProcessReadOnlyVerificationJobSourceKind.Scheduler,
            "scheduler-plan:daily-review",
            "scheduler-correlation:daily-review",
            ProcessDriverVerificationGatewayLane.BusinessAnalysisRead,
            payload,
            ProcessManagerReadOnlyVerificationProjectionMode.Diagnostics,
            "scheduler-verifier",
            RequestedAt,
            auditRecordLimit: 10);
        var workflowJob = new ProcessReadOnlyVerificationJob(
            Guid.NewGuid(),
            ProcessReadOnlyVerificationJobSourceKind.Workflow,
            "workflow:qa-readback",
            "workflow-correlation:qa-readback",
            ProcessDriverVerificationGatewayLane.BusinessAnalysisRead,
            payload,
            ProcessManagerReadOnlyVerificationProjectionMode.Diagnostics,
            "workflow-verifier",
            RequestedAt);

        Assert.Equal(ProcessReadOnlyVerificationJobSourceKind.Scheduler, schedulerJob.SourceKind);
        Assert.Equal("scheduler-plan:daily-review", schedulerJob.SourceReference);
        Assert.Equal("scheduler-correlation:daily-review", schedulerJob.CorrelationId);
        Assert.True(schedulerJob.NoMutationPerformed);
        Assert.False(schedulerJob.AllowsProcessMutation);
        Assert.False(schedulerJob.AllowsTransitionMutation);
        Assert.False(schedulerJob.AllowsFinalizerMutation);

        var readbackRequest = schedulerJob.ToManagerReadbackRequest();

        Assert.Equal(10, readbackRequest.AuditRecordLimit);
        Assert.Equal(ProcessDriverVerificationGatewayLane.BusinessAnalysisRead, readbackRequest.VerificationRequest.Lane);
        Assert.Equal(ProcessManagerReadOnlyVerificationProjectionMode.Diagnostics, readbackRequest.VerificationRequest.ProjectionMode);
        Assert.Equal("scheduler-verifier", readbackRequest.VerificationRequest.RequestedBy);
        Assert.Equal(payload, readbackRequest.VerificationRequest.Payload);
        Assert.Equal(ProcessReadOnlyVerificationJobSourceKind.Workflow, workflowJob.SourceKind);
        Assert.Equal("workflow:qa-readback", workflowJob.SourceReference);
        Assert.Equal("workflow-correlation:qa-readback", workflowJob.CorrelationId);
        Assert.True(workflowJob.NoMutationPerformed);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProcessReadOnlyVerificationJob(
            Guid.NewGuid(),
            (ProcessReadOnlyVerificationJobSourceKind)999,
            "bad-source",
            "bad-correlation",
            ProcessDriverVerificationGatewayLane.BusinessAnalysisRead,
            payload,
            ProcessManagerReadOnlyVerificationProjectionMode.Diagnostics,
            "scheduler-verifier",
            RequestedAt));
    }

    [Fact]
    public async Task Process_readonly_verification_job_runner_SB07_INV_001_executes_scheduler_and_workflow_lifecycle_status_provenance_readback_without_mutation() {
        var services = new ServiceCollection();
        services.AddProcessVerificationRuntimeHost();
        services.AddInMemoryProcessVerificationAuditStoreForTests();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IProcessReadOnlyVerificationJobRunner>();
        var payload = new ProcessReadOnlyVerificationBatchPayload(
            ProcessRunId,
            StepRunId,
            "scheduler:runtime-host-boundary",
            RequestedAt,
            businessAnalysisPayloads: [CreateBusinessPayload()]);
        var job = new ProcessReadOnlyVerificationJob(
            Guid.NewGuid(),
            ProcessReadOnlyVerificationJobSourceKind.Scheduler,
            "scheduler-plan:daily-review",
            "scheduler-correlation:daily-review",
            ProcessDriverVerificationGatewayLane.BusinessAnalysisRead,
            payload,
            ProcessManagerReadOnlyVerificationProjectionMode.Diagnostics,
            "scheduler-verifier",
            RequestedAt,
            auditRecordLimit: 10);

        var result = await runner.RunAsync(job);

        Assert.Equal(job.Id, result.JobId);
        Assert.Equal(ProcessReadOnlyVerificationJobSourceKind.Scheduler, result.SourceKind);
        Assert.Equal("scheduler-plan:daily-review", result.SourceReference);
        Assert.Equal("scheduler-correlation:daily-review", result.CorrelationId);
        Assert.Equal(ProcessReadOnlyVerificationJobLifecycleStatus.Completed, result.Lifecycle.Status);
        Assert.Equal(job.Id, result.Lifecycle.JobId);
        Assert.Equal(job.SourceKind, result.Lifecycle.SourceKind);
        Assert.Equal(job.SourceReference, result.Lifecycle.SourceReference);
        Assert.Equal(job.CorrelationId, result.Lifecycle.CorrelationId);
        Assert.Equal(job.Lane, result.Lifecycle.Lane);
        Assert.Equal(job.Payload.ProcessRunId, result.Lifecycle.ProcessRunId);
        Assert.Equal(job.Payload.StepRunId, result.Lifecycle.StepRunId);
        Assert.Equal(job.RequestedAt, result.Lifecycle.StartedAt);
        Assert.True(result.Lifecycle.CompletedAt >= result.Lifecycle.StartedAt);
        Assert.Equal(result.Readback.AuditRecordId, result.Lifecycle.AuditRecordId);
        Assert.True(result.Lifecycle.AuditRecordCount > 0);
        Assert.True(result.Lifecycle.NoMutationPerformed);
        Assert.False(result.Lifecycle.AllowsProcessMutation);
        Assert.False(result.Lifecycle.AllowsTransitionMutation);
        Assert.False(result.Lifecycle.AllowsFinalizerMutation);
        Assert.Equal(ProcessManagerReadOnlyVerificationFacadeStatus.Succeeded, result.Readback.Status);
        Assert.Equal(ProcessDriverVerificationGatewayLane.BusinessAnalysisRead, result.Readback.Lane);
        Assert.Equal(ProcessRuntimeHostContractSurface.SchedulerWorkflowReadOnlyJob, result.Contract.Surface);
        Assert.Equal(ProcessRuntimeHostContractVersion.Current, result.Contract.Version);
        Assert.Equal(job.Id, result.Contract.RequestIdentity?.RequestId);
        Assert.Equal(job.Payload.ProcessRunId, result.Contract.RequestIdentity?.ProcessRunId);
        Assert.Equal(job.Payload.StepRunId, result.Contract.RequestIdentity?.StepRunId);
        Assert.Equal(job.RequestedBy, result.Contract.RequestIdentity?.RequestedBy);
        Assert.True(result.Contract.IsReadOnlySafe);
        Assert.Empty(result.Contract.ValidateReadOnlySafety());
        var auditRecordId = result.Readback.AuditRecordId ??
            throw new InvalidOperationException("Expected scheduler verification job audit id.");
        Assert.Equal($"verification-job:{auditRecordId:N}", result.Contract.AuditReference?.AuditId);
        Assert.Equal(result.Readback.AuditRecordObservationHash, result.Contract.AuditReference?.ContentHash);
        Assert.Equal(ProcessRuntimeHostContractSurface.ManagerReadback, result.Readback.Contract.Surface);
        Assert.NotEmpty(result.Readback.Diagnostics);
        Assert.Contains(result.Readback.AuditRecords, record => record.Id == result.Readback.AuditRecordId);
        Assert.True(result.NoMutationPerformed);
        Assert.False(result.AllowsProcessMutation);
        Assert.False(result.AllowsTransitionMutation);
        Assert.False(result.AllowsFinalizerMutation);

        var workflowJob = new ProcessReadOnlyVerificationJob(
            Guid.NewGuid(),
            ProcessReadOnlyVerificationJobSourceKind.Workflow,
            "workflow:qa-readback",
            "workflow-correlation:qa-readback",
            ProcessDriverVerificationGatewayLane.BusinessAnalysisRead,
            payload,
            ProcessManagerReadOnlyVerificationProjectionMode.Diagnostics,
            "workflow-verifier",
            RequestedAt,
            auditRecordLimit: 10);

        var workflowResult = await runner.RunAsync(workflowJob);

        Assert.Equal(workflowJob.Id, workflowResult.JobId);
        Assert.Equal(ProcessReadOnlyVerificationJobSourceKind.Workflow, workflowResult.SourceKind);
        Assert.Equal("workflow:qa-readback", workflowResult.SourceReference);
        Assert.Equal("workflow-correlation:qa-readback", workflowResult.CorrelationId);
        Assert.Equal(ProcessReadOnlyVerificationJobLifecycleStatus.Completed, workflowResult.Lifecycle.Status);
        Assert.Equal(workflowJob.Id, workflowResult.Lifecycle.JobId);
        Assert.Equal(workflowJob.SourceKind, workflowResult.Lifecycle.SourceKind);
        Assert.Equal(workflowJob.SourceReference, workflowResult.Lifecycle.SourceReference);
        Assert.Equal(workflowJob.CorrelationId, workflowResult.Lifecycle.CorrelationId);
        Assert.Equal(workflowJob.Lane, workflowResult.Lifecycle.Lane);
        Assert.Equal(workflowJob.Payload.ProcessRunId, workflowResult.Lifecycle.ProcessRunId);
        Assert.Equal(workflowJob.Payload.StepRunId, workflowResult.Lifecycle.StepRunId);
        Assert.Equal(workflowJob.RequestedAt, workflowResult.Lifecycle.StartedAt);
        Assert.True(workflowResult.Lifecycle.CompletedAt >= workflowResult.Lifecycle.StartedAt);
        Assert.Equal(workflowResult.Readback.AuditRecordId, workflowResult.Lifecycle.AuditRecordId);
        Assert.True(workflowResult.Lifecycle.AuditRecordCount > 0);
        Assert.True(workflowResult.Lifecycle.NoMutationPerformed);
        Assert.False(workflowResult.Lifecycle.AllowsProcessMutation);
        Assert.False(workflowResult.Lifecycle.AllowsTransitionMutation);
        Assert.False(workflowResult.Lifecycle.AllowsFinalizerMutation);
        Assert.Equal(ProcessManagerReadOnlyVerificationFacadeStatus.Succeeded, workflowResult.Readback.Status);
        Assert.Equal(ProcessRuntimeHostContractSurface.SchedulerWorkflowReadOnlyJob, workflowResult.Contract.Surface);
        Assert.Equal(ProcessRuntimeHostContractVersion.Current, workflowResult.Contract.Version);
        Assert.Equal(workflowJob.Id, workflowResult.Contract.RequestIdentity?.RequestId);
        Assert.Equal(workflowJob.Payload.ProcessRunId, workflowResult.Contract.RequestIdentity?.ProcessRunId);
        Assert.Equal(workflowJob.Payload.StepRunId, workflowResult.Contract.RequestIdentity?.StepRunId);
        Assert.Equal(workflowJob.RequestedBy, workflowResult.Contract.RequestIdentity?.RequestedBy);
        Assert.True(workflowResult.Contract.IsReadOnlySafe);
        Assert.Empty(workflowResult.Contract.ValidateReadOnlySafety());
        var workflowAuditRecordId = workflowResult.Readback.AuditRecordId ??
            throw new InvalidOperationException("Expected workflow verification job audit id.");
        Assert.Equal($"verification-job:{workflowAuditRecordId:N}", workflowResult.Contract.AuditReference?.AuditId);
        Assert.Equal(workflowResult.Readback.AuditRecordObservationHash, workflowResult.Contract.AuditReference?.ContentHash);
        Assert.Equal(ProcessRuntimeHostContractSurface.ManagerReadback, workflowResult.Readback.Contract.Surface);
        Assert.Contains(workflowResult.Readback.AuditRecords, record => record.Id == workflowResult.Readback.AuditRecordId);
        Assert.True(workflowResult.NoMutationPerformed);
        Assert.False(workflowResult.AllowsProcessMutation);
        Assert.False(workflowResult.AllowsTransitionMutation);
        Assert.False(workflowResult.AllowsFinalizerMutation);
    }

    [Fact]
    public void Process_future_execution_sandbox_policy_SB039_INV_001_stays_dry_run_and_denies_all_effectful_surfaces_until_approved() {
        var policy = ProcessExecutionCapableDriverSandboxPolicy.DefaultBlockedDryRun;

        Assert.Equal(ProcessExecutionCapableDriverApprovalStatus.NotApproved, policy.ApprovalStatus);
        Assert.True(policy.DryRunOnly);
        Assert.True(policy.NoMutationPerformed);
        Assert.False(policy.AllowsProcessMutation);
        Assert.False(policy.AllowsTransitionMutation);
        Assert.False(policy.AllowsFinalizerMutation);
        Assert.Empty(policy.AllowListedSurfaces);

        foreach (var surface in Enum.GetValues<ProcessExecutionCapableDriverSurface>()) {
            Assert.False(policy.Allows(surface));
        }
    }

    [Fact]
    public void Process_execution_capable_future_gate_SB042_INV_001_requires_complete_source_backed_approval_before_any_execution_surface_is_allowed() {
        var gate = new ProcessExecutionCapableDriverFutureGate();
        var blockedPolicy = ProcessExecutionCapableDriverSandboxPolicy.DefaultBlockedDryRun;
        var incompleteEvidence = ProcessExecutionCapableDriverApprovalEvidence.None;
        var untrustedApprovedPolicy = new ProcessExecutionCapableDriverSandboxPolicy(
            ProcessExecutionCapableDriverApprovalStatus.Approved,
            DryRunOnly: false,
            [
                ProcessExecutionCapableDriverSurface.CommandExecution,
                ProcessExecutionCapableDriverSurface.WorkspaceWrite
            ]);

        var blockedResult = gate.Evaluate(blockedPolicy, incompleteEvidence);
        var untrustedResult = gate.Evaluate(untrustedApprovedPolicy, incompleteEvidence);

        Assert.Equal(ProcessExecutionCapableDriverGateDecision.Blocked, blockedResult.Decision);
        Assert.Contains(ProcessExecutionCapableDriverFutureGateRequirement.SourceBackedApprovalBundle, blockedResult.MissingRequirements);
        Assert.False(blockedResult.Allows(ProcessExecutionCapableDriverSurface.CommandExecution));
        Assert.True(blockedResult.NoMutationPerformed);
        Assert.False(blockedResult.AllowsProcessMutation);
        Assert.False(blockedResult.AllowsTransitionMutation);
        Assert.False(blockedResult.AllowsFinalizerMutation);

        Assert.Equal(ProcessExecutionCapableDriverGateDecision.Blocked, untrustedResult.Decision);
        Assert.Contains(ProcessExecutionCapableDriverFutureGateRequirement.RedTeamProof, untrustedResult.MissingRequirements);
        Assert.False(untrustedResult.Allows(ProcessExecutionCapableDriverSurface.CommandExecution));
        Assert.False(untrustedResult.Allows(ProcessExecutionCapableDriverSurface.WorkspaceWrite));
    }

    [Fact]
    public void Process_execution_capable_surface_matrix_SB025_INV_001_models_every_effectful_surface_as_denied_by_default()
    {
        var expectedSurfaces = Enum
            .GetValues<ProcessExecutionCapableDriverSurface>()
            .Order()
            .ToArray();
        var actualSurfaces = ProcessExecutionCapableDriverSurfaceMatrix.DefaultDeniedRules
            .Select(rule => rule.Surface)
            .Order()
            .ToArray();

        Assert.Equal(expectedSurfaces, actualSurfaces);
        Assert.All(
            ProcessExecutionCapableDriverSurfaceMatrix.DefaultDeniedRules,
            rule => Assert.True(rule.DeniedByDefault));
        Assert.Contains(
            ProcessExecutionCapableDriverSurfaceMatrix.DefaultDeniedRules,
            rule => rule.Surface == ProcessExecutionCapableDriverSurface.CommandExecution &&
                rule.DriverOperation == ProcessDriverOperation.ExecuteCommand);
        Assert.Contains(
            ProcessExecutionCapableDriverSurfaceMatrix.DefaultDeniedRules,
            rule => rule.Surface == ProcessExecutionCapableDriverSurface.ProcessMutation &&
                rule.DriverOperation == ProcessDriverOperation.MutateProcessState);

        var resolvedSurfaces = ProcessExecutionCapableDriverSurfaceMatrix.ResolveSurfacesForOperations(
            [
                ProcessDriverOperation.ExecuteCommand,
                ProcessDriverOperation.WriteWorkspaceStorage,
                ProcessDriverOperation.ReturnDiagnostics
            ]);

        Assert.Contains(ProcessExecutionCapableDriverSurface.CommandExecution, resolvedSurfaces);
        Assert.Contains(ProcessExecutionCapableDriverSurface.WorkspaceWrite, resolvedSurfaces);
        Assert.Contains(ProcessExecutionCapableDriverSurface.StorageWrite, resolvedSurfaces);
        Assert.DoesNotContain(ProcessExecutionCapableDriverSurface.OfficeGraphCall, resolvedSurfaces);
    }

    [Fact]
    public async Task Process_dry_run_execution_host_SB024_INV_001_denies_effectful_requests_with_structured_plan_without_mutation()
    {
        var services = new ServiceCollection();
        services.AddProcessVerificationRuntimeHost();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var host = scope.ServiceProvider.GetRequiredService<IProcessDryRunExecutionHost>();
        var request = new ProcessDryRunExecutionRequest(
            Guid.Parse("aaaaaaaa-1111-1111-1111-aaaaaaaaaaaa"),
            ProcessRunId,
            StepRunId,
            "process-manager",
            "dry-run command and workspace write preview",
            [
                ProcessExecutionCapableDriverSurface.CommandExecution,
                ProcessExecutionCapableDriverSurface.WorkspaceWrite
            ],
            [
                ProcessDriverOperation.ExecuteCommand,
                ProcessDriverOperation.WriteWorkspaceStorage,
                ProcessDriverOperation.ReturnDiagnostics
            ],
            ProcessExecutionCapableDriverSandboxPolicy.DefaultBlockedDryRun,
            ProcessExecutionCapableDriverApprovalEvidence.None,
            RequestedAt);

        var result = await host.EvaluateAsync(request);

        Assert.Equal(ProcessDryRunExecutionHostDecision.Denied, result.Decision);
        Assert.Equal(ProcessVerificationHostCapabilityCatalog.DryRunExecutionGateKey, result.CapabilityKey);
        Assert.Equal(ProcessExecutionCapableDriverGateDecision.Blocked, result.GateResult.Decision);
        Assert.Contains(ProcessExecutionCapableDriverFutureGateRequirement.SourceBackedApprovalBundle, result.GateResult.MissingRequirements);
        Assert.Contains(ProcessExecutionCapableDriverFutureGateRequirement.AuthorizationApprovalRevocation, result.GateResult.MissingRequirements);
        Assert.Contains(ProcessExecutionCapableDriverSurface.CommandExecution, result.DeniedSurfaces);
        Assert.Contains(ProcessExecutionCapableDriverSurface.WorkspaceWrite, result.DeniedSurfaces);
        Assert.Contains(ProcessDriverOperation.ExecuteCommand, result.DeniedOperations);
        Assert.Contains(ProcessDriverOperation.WriteWorkspaceStorage, result.DeniedOperations);
        Assert.DoesNotContain(ProcessDriverOperation.ReturnDiagnostics, result.DeniedOperations);
        Assert.Contains(ProcessExecutionCapableDriverAuthorizationGap.ApprovalGrantMissing, result.AuthorizationGaps);
        Assert.Contains(ProcessExecutionCapableDriverAuthorizationGap.RevocationCheckMissing, result.AuthorizationGaps);
        Assert.Contains(ProcessExecutionCapableDriverAuthorizationGap.EmergencyStopActiveOrUnknown, result.AuthorizationGaps);
        Assert.Equal("Dry-run request denied; no production effects were executed.", result.Plan.Summary);
        Assert.Contains(result.Plan.Steps, step => step.Kind == ProcessDryRunExecutionPlanStepKind.DenyProductionEffects);
        Assert.Equal(ProcessRuntimeHostContractSurface.DryRunExecution, result.Contract.Surface);
        Assert.Equal(ProcessRuntimeHostContractVersion.Current, result.Contract.Version);
        Assert.Equal(request.RequestId, result.Contract.RequestIdentity?.RequestId);
        Assert.Equal(request.ProcessRunId, result.Contract.RequestIdentity?.ProcessRunId);
        Assert.Equal(request.StepRunId, result.Contract.RequestIdentity?.StepRunId);
        Assert.Equal("process-manager", result.Contract.RequestIdentity?.RequestedBy);
        Assert.NotNull(result.Contract.SandboxDecision);
        var sandboxDecision = result.Contract.SandboxDecision;
        Assert.Equal(ProcessRuntimeHostSandboxDecisionKind.Denied, sandboxDecision.Kind);
        Assert.False(sandboxDecision.ExecutionAllowed);
        Assert.True(sandboxDecision.DryRunOnly);
        Assert.Contains(ProcessRuntimeHostEffectSurface.LocalCommand, sandboxDecision.DeniedSurfaces);
        Assert.Contains(ProcessRuntimeHostEffectSurface.WorkspaceStorage, sandboxDecision.DeniedSurfaces);
        Assert.Contains(sandboxDecision.Denials, denial =>
            denial.Category == ProcessRuntimeHostDenialCategory.SideEffect &&
            denial.Code == "side-effect-denied");
        Assert.Equal(
            ProcessVerificationHostCapabilityCatalog.DryRunExecutionGateKey,
            result.Contract.CapabilityDescriptor?.Key);
        Assert.Equal(
            ProcessRuntimeHostOperationCategory.DryRunPlanning,
            result.Contract.CapabilityDescriptor?.OperationCategory);
        Assert.StartsWith("dry-run:", result.Contract.AuditReference?.AuditId);
        Assert.Matches("^[a-f0-9]{64}$", result.Contract.AuditReference?.ContentHash ?? string.Empty);
        Assert.True(result.NoMutationPerformed);
        Assert.False(result.AllowsProcessMutation);
        Assert.False(result.AllowsTransitionMutation);
        Assert.False(result.AllowsFinalizerMutation);
    }

    [Fact]
    public async Task Process_dry_run_execution_host_SB027_INV_001_requires_authorization_revocation_and_emergency_stop_evidence()
    {
        var host = new ProcessDryRunExecutionHost(new ProcessExecutionCapableDriverFutureGate());
        var policy = new ProcessExecutionCapableDriverSandboxPolicy(
            ProcessExecutionCapableDriverApprovalStatus.Approved,
            DryRunOnly: false,
            [ProcessExecutionCapableDriverSurface.CommandExecution]);
        var evidence = new ProcessExecutionCapableDriverApprovalEvidence(
            SourceBackedApprovalBundle: true,
            LifecycleOwnership: true,
            CancellationTimeoutFailureHandoff: true,
            ImmutableAuditPersistence: true,
            SandboxBoundary: true,
            AuthorizationApprovalRevocation: true,
            PublicApiCompatibility: true,
            MaliciousCorpus: true,
            RedTeamProof: true,
            new ProcessExecutionCapableDriverAuthorizationEvidence(
                ApprovalGrantPresent: true,
                RevocationCheckPassed: true,
                EmergencyStopClear: false));
        var request = new ProcessDryRunExecutionRequest(
            Guid.Parse("bbbbbbbb-1111-1111-1111-bbbbbbbbbbbb"),
            ProcessRunId,
            StepRunId,
            "process-manager",
            "dry-run command preview",
            [ProcessExecutionCapableDriverSurface.CommandExecution],
            [ProcessDriverOperation.ExecuteCommand],
            policy,
            evidence,
            RequestedAt);

        var blocked = await host.EvaluateAsync(request);

        Assert.Equal(ProcessDryRunExecutionHostDecision.Denied, blocked.Decision);
        Assert.Equal(ProcessVerificationHostCapabilityCatalog.DryRunExecutionGateKey, blocked.CapabilityKey);
        Assert.Equal(ProcessExecutionCapableDriverGateDecision.Blocked, blocked.GateResult.Decision);
        Assert.Contains(ProcessExecutionCapableDriverFutureGateRequirement.AuthorizationApprovalRevocation, blocked.GateResult.MissingRequirements);
        Assert.Contains(ProcessExecutionCapableDriverAuthorizationGap.EmergencyStopActiveOrUnknown, blocked.AuthorizationGaps);

        var clearEvidence = evidence with
        {
            AuthorizationEvidence = new ProcessExecutionCapableDriverAuthorizationEvidence(
                ApprovalGrantPresent: true,
                RevocationCheckPassed: true,
                EmergencyStopClear: true)
        };
        var clearRequest = new ProcessDryRunExecutionRequest(
            request.RequestId,
            request.ProcessRunId,
            request.StepRunId,
            request.RequestedBy,
            request.Purpose,
            request.RequestedSurfaces,
            request.RequestedOperations,
            request.RequestedPolicy,
            clearEvidence,
            request.RequestedAt);

        var planned = await host.EvaluateAsync(clearRequest);

        Assert.Equal(ProcessDryRunExecutionHostDecision.DryRunPlanCreated, planned.Decision);
        Assert.Equal(ProcessVerificationHostCapabilityCatalog.DryRunExecutionGateKey, planned.CapabilityKey);
        Assert.Equal(ProcessExecutionCapableDriverGateDecision.ApprovedForFutureExecution, planned.GateResult.Decision);
        Assert.Empty(planned.DeniedSurfaces);
        Assert.Empty(planned.DeniedOperations);
        Assert.Empty(planned.AuthorizationGaps);
        Assert.Equal(ProcessRuntimeHostContractSurface.DryRunExecution, planned.Contract.Surface);
        Assert.Equal(ProcessRuntimeHostContractVersion.Current, planned.Contract.Version);
        Assert.NotNull(planned.Contract.SandboxDecision);
        var plannedSandboxDecision = planned.Contract.SandboxDecision;
        Assert.Equal(ProcessRuntimeHostSandboxDecisionKind.DryRunPlanAccepted, plannedSandboxDecision.Kind);
        Assert.False(plannedSandboxDecision.ExecutionAllowed);
        Assert.True(plannedSandboxDecision.DryRunOnly);
        Assert.Empty(plannedSandboxDecision.DeniedSurfaces);
        Assert.Equal(
            ProcessVerificationHostCapabilityCatalog.DryRunExecutionGateKey,
            planned.Contract.CapabilityDescriptor?.Key);
        Assert.Matches("^[a-f0-9]{64}$", planned.Contract.AuditReference?.ContentHash ?? string.Empty);
        Assert.True(planned.NoMutationPerformed);
        Assert.False(planned.AllowsProcessMutation);
        Assert.False(planned.AllowsTransitionMutation);
        Assert.False(planned.AllowsFinalizerMutation);
    }

    [Fact]
    public async Task Process_manager_runtime_host_readback_SB007_INV_001_projects_dry_run_plan_to_api_ready_json()
    {
        var host = new ProcessDryRunExecutionHost(new ProcessExecutionCapableDriverFutureGate());
        var request = new ProcessDryRunExecutionRequest(
            Guid.Parse("cccccccc-1111-1111-1111-cccccccccccc"),
            ProcessRunId,
            StepRunId,
            "process-operator",
            "operator dry-run readback",
            [
                ProcessExecutionCapableDriverSurface.CommandExecution,
                ProcessExecutionCapableDriverSurface.WorkspaceWrite
            ],
            [
                ProcessDriverOperation.ExecuteCommand,
                ProcessDriverOperation.WriteWorkspaceStorage
            ],
            ProcessExecutionCapableDriverSandboxPolicy.DefaultBlockedDryRun,
            ProcessExecutionCapableDriverApprovalEvidence.None,
            RequestedAt);

        var result = await host.EvaluateAsync(request);
        var readback = ProcessManagerRuntimeHostDryRunReadbackMapper.Project(result);
        var json = System.Text.Json.JsonSerializer.Serialize(
            readback,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(ProcessVerificationHostCapabilityCatalog.DryRunExecutionGateKey, root.GetProperty("capabilityKey").GetString());
        Assert.Equal(request.RequestId.ToString("D"), root.GetProperty("requestId").GetString());
        Assert.Equal(ProcessRunId.ToString("D"), root.GetProperty("processRunId").GetString());
        Assert.Equal(StepRunId.ToString("D"), root.GetProperty("stepRunId").GetString());
        Assert.Equal("process-operator", root.GetProperty("requestedBy").GetString());
        Assert.Equal((int)ProcessDryRunExecutionHostDecision.Denied, root.GetProperty("decision").GetInt32());
        Assert.Contains("no production effects", root.GetProperty("planSummary").GetString(), StringComparison.Ordinal);
        Assert.True(root.GetProperty("planSteps").GetArrayLength() > 0);
        Assert.True(root.GetProperty("deniedSurfaceCount").GetInt32() >= 2);
        Assert.Equal(2, root.GetProperty("deniedOperationCount").GetInt32());
        Assert.Equal(3, root.GetProperty("authorizationGapCount").GetInt32());
        Assert.True(root.GetProperty("denialCount").GetInt32() >= 1);
        Assert.StartsWith("dry-run:", root.GetProperty("auditReferenceId").GetString());
        Assert.Matches("^[a-f0-9]{64}$", root.GetProperty("auditReferenceContentHash").GetString());
        Assert.Equal((int)ProcessRuntimeHostContractSurface.DryRunExecution, root.GetProperty("contract").GetProperty("surface").GetInt32());
        Assert.Equal((int)ProcessRuntimeHostSandboxDecisionKind.Denied, root.GetProperty("contract").GetProperty("sandboxDecision").GetProperty("kind").GetInt32());
        Assert.True(root.GetProperty("noMutationPerformed").GetBoolean());
        Assert.False(root.GetProperty("allowsProcessMutation").GetBoolean());
        Assert.False(root.GetProperty("allowsTransitionMutation").GetBoolean());
        Assert.False(root.GetProperty("allowsFinalizerMutation").GetBoolean());
    }

    [Fact]
    public void Process_dry_run_execution_pipeline_SB003_INV_001_registers_explicit_stage_components()
    {
        var services = new ServiceCollection();
        services.AddProcessVerificationRuntimeHost();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ProcessDryRunExecutionRequestNormalizer>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ProcessDryRunExecutionCapabilityResolver>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ProcessDryRunExecutionSandboxEvaluator>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ProcessDryRunExecutionAuthorizationEvaluator>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ProcessDryRunExecutionPlanBuilder>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ProcessDryRunExecutionAuditMapper>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ProcessDryRunExecutionPipeline>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IProcessDryRunExecutionHost>());
    }

    [Fact]
    public void Process_verification_host_capability_catalog_SB007_INV_001_is_static_readonly_and_complete()
    {
        var descriptors = ProcessVerificationHostCapabilityCatalog.StaticDescriptors;
        var providerDescriptors = ProcessVerificationHostCapabilityCatalog.StaticProvider.ListDescriptors();
        var verificationDescriptors = descriptors
            .Where(item => item.Kind == ProcessVerificationHostCapabilityKind.VerificationLane)
            .ToDictionary(item => item.Key, StringComparer.Ordinal);
        var dryRunDescriptor = ProcessVerificationHostCapabilityCatalog.Require(
            ProcessVerificationHostCapabilityCatalog.DryRunExecutionGateKey);
        var services = new ServiceCollection();
        services.AddProcessVerificationRuntimeHost();

        using var provider = services.BuildServiceProvider();

        Assert.Equal(ProcessDriverVerificationGatewayLaneRules.AllowedLanes.Count + 1, descriptors.Count);
        Assert.Same(descriptors, providerDescriptors);
        Assert.IsType<ProcessStaticVerificationHostCapabilityProvider>(
            provider.GetRequiredService<IProcessVerificationHostCapabilityProvider>());
        Assert.Equal(ProcessVerificationHostCapabilityKind.DryRunExecutionGate, dryRunDescriptor.Kind);
        Assert.Equal(ProcessRuntimeHostContractSurface.DryRunExecution, dryRunDescriptor.ContractSurface);
        Assert.Equal(ProcessRuntimeHostOperationCategory.DryRunPlanning, dryRunDescriptor.OperationCategory);
        Assert.Equal(ProcessDriverPermissionMode.ExecutionCapableFuture, dryRunDescriptor.PermissionMode);
        Assert.All(descriptors, descriptor =>
        {
            Assert.True(descriptor.IsStaticReadOnlyDescriptor);
            Assert.False(descriptor.ReflectionDiscoveryAllowed);
            Assert.False(descriptor.SelfRegistrationAllowed);
            Assert.False(descriptor.ExecutionAllowed);
            Assert.All(descriptor.AllowedOperations, operation =>
                Assert.False(ProcessDriverOperationRules.IsSideEffectOperation(operation)));
            Assert.All(ProcessDriverOperationRules.SideEffectOperations, operation =>
                Assert.Contains(operation, descriptor.DeniedOperations));
        });

        foreach (var lane in ProcessDriverVerificationGatewayLaneRules.AllowedLanes)
        {
            var key = ProcessVerificationHostCapabilityCatalog.VerificationLaneKey(lane.Lane);
            Assert.True(verificationDescriptors.TryGetValue(key, out var descriptor), $"Missing descriptor for {key}.");

            Assert.Equal(ProcessVerificationHostCapabilityKind.VerificationLane, descriptor!.Kind);
            Assert.Equal(ProcessRuntimeHostContractSurface.VerificationHost, descriptor.ContractSurface);
            Assert.Equal(ProcessRuntimeHostOperationCategory.ReadOnlyInspection, descriptor.OperationCategory);
            Assert.Equal(lane.RequiredPermissionMode, descriptor.PermissionMode);
            Assert.Equal(lane.AllowedOperations, descriptor.AllowedOperations);
        }
    }

    [Fact]
    public void Scheduler_workflow_verification_readiness_SB032_INV_001_does_not_call_process_drivers_directly()
    {
        var forbiddenPatterns = new[]
        {
            "CanDoItAll.Processes.Drivers.",
            "ProcessDriverVerificationGateway",
            "IProcessVerificationRuntimeHost",
            "ProcessVerificationRuntimeHost",
            "ProcessReadOnlyVerificationBatchOrchestrator",
            "ProcessReadOnlyVerificationPayloadBuilder"
        };
        var sourceRoots = new[]
        {
            Path.Combine("src", "CanDoItAll.Modules.SchedulerPlanner"),
            Path.Combine("src", "CanDoItAll.Modules.AgentFramework")
        };
        var matches = FindSourceMatches(sourceRoots, forbiddenPatterns);

        Assert.Empty(matches);
    }

    [Fact]
    public async Task Process_verification_runtime_host_SB021_INV_001_di_registration_resolves_host_command_and_shared_audit_boundary()
    {
        var services = new ServiceCollection();
        services.AddProcessVerificationRuntimeHost();
        services.AddInMemoryProcessVerificationAuditStoreForTests();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var host = scope.ServiceProvider.GetRequiredService<IProcessVerificationRuntimeHost>();
        var command = scope.ServiceProvider.GetRequiredService<ProcessManagerReadOnlyVerificationCommandService>();
        var auditStore = provider.GetRequiredService<IProcessVerificationAuditStore>();
        var payload = new ProcessReadOnlyVerificationBatchPayload(
            ProcessRunId,
            StepRunId,
            "process-manager:di-diagnostics",
            RequestedAt,
            transcriptPayloads: [CreateTranscriptPayload()]);

        var responseResult = await host.VerifyAsync(new ProcessVerificationHostRequest(
            ProcessDriverVerificationGatewayLane.DotNetRustTranscriptVerification,
            payload,
            "process-manager",
            RequestedAt));
        var response = responseResult.Response ?? throw new InvalidOperationException("Expected a successful verification host response.");
        var result = command.Run(new ProcessManagerReadOnlyVerificationCommandRequest(
            ProcessDriverVerificationGatewayLane.DotNetRustTranscriptVerification,
            payload,
            ProcessManagerReadOnlyVerificationProjectionMode.EvidenceEnvelope,
            "process-manager",
            RequestedAt));

        Assert.IsType<ProcessVerificationRuntimeHost>(host);
        Assert.True(responseResult.IsSuccess);
        Assert.Equal(ProcessDriverVerificationGatewayLane.DotNetRustTranscriptVerification, response.Lane);
        Assert.Equal(ProcessDriverVerificationGatewayLane.DotNetRustTranscriptVerification, result.Lane);
        Assert.True(response.NoMutationPerformed);
        Assert.True(result.NoMutationPerformed);
        var auditRecords = await auditStore.ListAsync();
        Assert.Contains(auditRecords, record => record.Id == response.AuditRecord.Id);
        Assert.Contains(auditRecords, record => record.Id == result.AuditRecord.Id);
        Assert.NotEqual(response.AuditRecord.Id, result.AuditRecord.Id);
    }

    [Fact]
    public void Process_verification_runtime_host_SB006_INV_002_core_registration_requires_explicit_audit_store()
    {
        var services = new ServiceCollection();
        services.AddProcessVerificationRuntimeHost();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IProcessVerificationRuntimeHost>());
        Assert.Contains(nameof(IProcessVerificationAuditStore), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Process_verification_runtime_host_SB006_INV_001_process_module_uses_ef_audit_store_by_default()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();

        var auditStore = scope.ServiceProvider.GetRequiredService<IProcessVerificationAuditStore>();
        var auditQueryService = scope.ServiceProvider.GetRequiredService<IProcessVerificationAuditQueryService>();

        Assert.IsType<EfCoreProcessVerificationAuditStore>(auditStore);
        Assert.Same(auditStore, auditQueryService);
    }

    [Fact]
    public async Task Process_verification_audit_store_SB006_INV_003_persists_postgresql_audit_records_across_service_scopes()
    {
        await using var application = await TestApplication.CreateAsync();

        ProcessVerificationAuditRecord writtenRecord;
        await using (var writeScope = application.Services.CreateAsyncScope())
        {
            var host = writeScope.ServiceProvider.GetRequiredService<IProcessVerificationRuntimeHost>();
            var result = await host.VerifyAsync(new ProcessVerificationHostRequest(
                ProcessDriverVerificationGatewayLane.BusinessAnalysisRead,
                new ProcessReadOnlyVerificationBatchPayload(
                    ProcessRunId,
                    StepRunId,
                    "process-manager:postgresql-durable-audit",
                    RequestedAt,
                    businessAnalysisPayloads: [CreateBusinessPayload()]),
                "process-manager postgresql-audit",
                RequestedAt));

            Assert.True(result.IsSuccess);
            var response = result.Response ?? throw new InvalidOperationException("Expected PostgreSQL audit host response.");
            writtenRecord = response.AuditRecord;
            Assert.Matches("^[A-F0-9]{64}$", writtenRecord.ObservationHash);
            Assert.True(writtenRecord.NoMutationPerformed);
            Assert.False(writtenRecord.AllowsProcessMutation);
            Assert.False(writtenRecord.AllowsTransitionMutation);
            Assert.False(writtenRecord.AllowsFinalizerMutation);
        }

        await using (var readScope = application.Services.CreateAsyncScope())
        {
            var queryService = readScope.ServiceProvider.GetRequiredService<IProcessVerificationAuditQueryService>();
            var persisted = await queryService.GetAsync(writtenRecord.Id);
            var windowRecords = await queryService.ListAsync(new ProcessVerificationAuditQuery(
                ProcessRunId,
                StepRunId,
                ProcessDriverVerificationGatewayLane.BusinessAnalysisRead,
                limit: 10,
                recordedAtOrAfter: RequestedAt.AddMinutes(-1),
                recordedBefore: RequestedAt.AddMinutes(1)));

            Assert.NotNull(persisted);
            Assert.Equal(writtenRecord.Id, persisted.Id);
            Assert.Equal(writtenRecord.ObservationHash, persisted.ObservationHash);
            Assert.Contains(windowRecords, record => record.Id == writtenRecord.Id);
        }
    }

    [Fact]
    public async Task Process_verification_audit_store_SB023_INV_001_persists_redacted_hashes_and_supports_queries()
    {
        AppDbContextModelRegistry.ConfigureAssemblies([typeof(ProcessesModuleAssemblyMarker).Assembly]);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISecretRedactor, SecretRedactor>();
        services.AddPooledDbContextFactory<AppDbContext>(options =>
            options.UseInMemoryDatabase($"process-verification-audit-{Guid.NewGuid():N}"));
        services.AddProcessVerificationRuntimeHost();
        services.AddEfCoreProcessVerificationAuditStore();

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        ProcessVerificationHostResponse response;
        await using (var scope = provider.CreateAsyncScope())
        {
            var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            await dbContext.Database.EnsureCreatedAsync();

            var host = scope.ServiceProvider.GetRequiredService<IProcessVerificationRuntimeHost>();
            var request = new ProcessVerificationHostRequest(
                ProcessDriverVerificationGatewayLane.BusinessAnalysisRead,
                new ProcessReadOnlyVerificationBatchPayload(
                    ProcessRunId,
                    StepRunId,
                    "process-manager:durable-audit",
                    RequestedAt,
                    businessAnalysisPayloads: [CreateBusinessPayload()]),
                "process-manager api_key=sk-test password=hunter2 Bearer abcdef",
                RequestedAt);

            var result = await host.VerifyAsync(request);

            Assert.True(result.IsSuccess);
            response = result.Response ?? throw new InvalidOperationException("Expected a durable audit verification response.");
            Assert.Matches("^[A-F0-9]{64}$", response.AuditRecord.ObservationHash);
            Assert.DoesNotContain("sk-test", response.AuditRecord.RequestedBy, StringComparison.Ordinal);
            Assert.DoesNotContain("hunter2", response.AuditRecord.RequestedBy, StringComparison.Ordinal);
            Assert.DoesNotContain("abcdef", response.AuditRecord.RequestedBy, StringComparison.Ordinal);
            Assert.Contains("[REDACTED]", response.AuditRecord.RequestedBy, StringComparison.Ordinal);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var queryService = scope.ServiceProvider.GetRequiredService<IProcessVerificationAuditQueryService>();

            var persisted = await queryService.GetAsync(response.AuditRecord.Id);
            var runRecords = await queryService.ListAsync(new ProcessVerificationAuditQuery(
                ProcessRunId,
                StepRunId,
                ProcessDriverVerificationGatewayLane.BusinessAnalysisRead,
                limit: 10,
                recordedAtOrAfter: RequestedAt.AddMinutes(-1),
                recordedBefore: RequestedAt.AddMinutes(1)));
            var emptyWindowRecords = await queryService.ListAsync(new ProcessVerificationAuditQuery(
                ProcessRunId,
                StepRunId,
                ProcessDriverVerificationGatewayLane.BusinessAnalysisRead,
                limit: 10,
                recordedAtOrAfter: RequestedAt.AddMinutes(1),
                recordedBefore: RequestedAt.AddMinutes(2)));

            var record = Assert.Single(runRecords);
            Assert.Empty(emptyWindowRecords);
            Assert.NotNull(persisted);
            Assert.Equal(response.AuditRecord.Id, persisted.Id);
            Assert.Equal(response.AuditRecord.ObservationHash, persisted.ObservationHash);
            Assert.Equal(response.AuditRecord.RequestedBy, persisted.RequestedBy);
            Assert.Equal(response.AuditRecord.Id, record.Id);
            Assert.True(record.NoMutationPerformed);
            Assert.False(record.AllowsProcessMutation);
            Assert.False(record.AllowsTransitionMutation);
            Assert.False(record.AllowsFinalizerMutation);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => new ProcessVerificationAuditQuery(limit: 0));
        Assert.Throws<ArgumentException>(() => new ProcessVerificationAuditQuery(
            recordedAtOrAfter: RequestedAt,
            recordedBefore: RequestedAt));
    }

    [Fact]
    public void Process_verification_audit_store_SB004_INV_001_model_keeps_readback_and_retention_indexes()
    {
        AppDbContextModelRegistry.ConfigureAssemblies([typeof(ProcessesModuleAssemblyMarker).Assembly]);

        var services = new ServiceCollection();
        services.AddSingleton<ISecretRedactor, SecretRedactor>();
        services.AddPooledDbContextFactory<AppDbContext>(options =>
            options.UseInMemoryDatabase($"process-verification-audit-model-{Guid.NewGuid():N}"));

        using var provider = services.BuildServiceProvider();
        using var dbContext = provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
        var entityType = dbContext.Model.FindEntityType(typeof(ProcessVerificationAuditEntry)) ??
            throw new InvalidOperationException("Process verification audit entry is missing from the EF model.");
        var indexes = entityType.GetIndexes()
            .Select(index => string.Join(",", index.Properties.Select(property => property.Name)))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("ProcessRunId,RecordedAtUtc", indexes);
        Assert.Contains("StepRunId,RecordedAtUtc", indexes);
        Assert.Contains(nameof(ProcessVerificationAuditEntry.RecordedAtUtc), indexes);
        Assert.Contains(nameof(ProcessVerificationAuditEntry.Lane), indexes);
        Assert.Contains(nameof(ProcessVerificationAuditEntry.ObservationHash), indexes);
    }

    [Fact]
    public async Task Process_readonly_verification_security_SB049_SB050_INV_001_redacts_malicious_secret_corpus_from_diagnostics_audit_and_readback()
    {
        const string maliciousTranscript = """
            Build failed.
            api_key=sk-malicious-demo
            access_token: test-access-token
            bearer abcdef123456
            password=hunter2
            secret=fixture-secret
            owner@example.invalid
            connectionString=Host=localhost;Password=db-secret
            <script>alert('x')</script>
            """;
        var forbiddenFragments = new[]
        {
            "sk-malicious-demo",
            "test-access-token",
            "abcdef123456",
            "hunter2",
            "fixture-secret",
            "owner@example.invalid",
            "Host=localhost",
            "db-secret"
        };
        var redaction = ProcessDriverRedactionPolicy.Redact(maliciousTranscript);

        Assert.Equal(ProcessDriverRedactionStatus.Redacted, redaction.Descriptor.Status);
        Assert.Contains(ProcessDriverRedactionKind.AccessToken, redaction.Descriptor.AppliedKinds);
        Assert.Contains(ProcessDriverRedactionKind.Secret, redaction.Descriptor.AppliedKinds);
        Assert.Contains(ProcessDriverRedactionKind.EmailAddress, redaction.Descriptor.AppliedKinds);
        Assert.Contains(ProcessDriverRedactionKind.ConnectionString, redaction.Descriptor.AppliedKinds);
        Assert.Matches("^[A-F0-9]{64}$", redaction.Descriptor.RedactedTextHash);
        AssertNoForbiddenFragments(redaction.RedactedText, forbiddenFragments);

        var auditStore = new InMemoryProcessVerificationAuditStore();
        var host = CreateHost(new ProcessVerificationRuntimeHostOptions(), auditStore: auditStore);
        var facade = new ProcessManagerReadOnlyVerificationCommandService(host, auditStore);
        var payload = new ProcessReadOnlyVerificationBatchPayload(
            ProcessRunId,
            StepRunId,
            "process-manager:malicious-secret-corpus",
            RequestedAt,
            transcriptPayloads: [CreateTranscriptPayload(maliciousTranscript)]);
        var request = new ProcessManagerReadOnlyVerificationCommandRequest(
            ProcessDriverVerificationGatewayLane.DotNetRustTranscriptVerification,
            payload,
            ProcessManagerReadOnlyVerificationProjectionMode.Diagnostics,
            "process-manager",
            RequestedAt);

        var readback = await facade.VerifyForReadbackAsync(new ProcessManagerReadOnlyVerificationReadbackRequest(
            request,
            auditRecordLimit: 10));

        Assert.Equal(ProcessManagerReadOnlyVerificationFacadeStatus.Succeeded, readback.Status);
        Assert.Equal(ProcessDriverVerificationGatewayLane.DotNetRustTranscriptVerification, readback.Lane);
        Assert.True(readback.NoMutationPerformed);
        Assert.False(readback.AllowsProcessMutation);
        Assert.False(readback.AllowsTransitionMutation);
        Assert.False(readback.AllowsFinalizerMutation);
        Assert.Null(readback.DenialCategory);
        Assert.Null(readback.DenialCode);
        Assert.NotEmpty(readback.Diagnostics);
        Assert.Contains(
            readback.Diagnostics,
            diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.BuildError);
        Assert.All(
            readback.Diagnostics,
            diagnostic => AssertNoForbiddenFragments(diagnostic.Message, forbiddenFragments));
        var readbackJson = System.Text.Json.JsonSerializer.Serialize(readback);
        AssertNoForbiddenFragments(readbackJson, forbiddenFragments);

        var auditRecord = Assert.Single(readback.AuditRecords);
        Assert.Matches("^[A-F0-9]{64}$", auditRecord.ObservationHash);
        Assert.True(auditRecord.NoMutationPerformed);
        Assert.False(auditRecord.AllowsProcessMutation);
        Assert.False(auditRecord.AllowsTransitionMutation);
        Assert.False(auditRecord.AllowsFinalizerMutation);
        var storedAuditRecord = Assert.Single(await auditStore.ListAsync(CancellationToken.None));
        Assert.Equal(auditRecord.Id, storedAuditRecord.Id);
        AssertNoForbiddenFragments(storedAuditRecord.RequestedBy, forbiddenFragments);
        AssertNoForbiddenFragments(storedAuditRecord.ObservationHash, forbiddenFragments);
    }

    [Fact]
    public void Process_readonly_verification_multi_domain_harness_SB037_SB038_INV_001_proves_current_lane_producers_and_orchestrator_consumer()
    {
        var harness = new ProcessReadOnlyVerificationMultiDomainHarness();
        var payload = new ProcessReadOnlyVerificationBatchPayload(
            ProcessRunId,
            StepRunId,
            "process-consumer:harness-multidomain",
            RequestedAt,
            transcriptPayloads: [CreateTranscriptPayload()],
            runtimeEvidencePayloads: [CreateRuntimePayload()],
            artifactEvidencePayloads: [CreateArtifactPayload()],
            officeEvidencePayloads: [CreateOfficePayload()],
            businessAnalysisPayloads: [CreateBusinessPayload()]);

        var observation = harness.Verify(payload);
        var matrix = harness.AssertCurrentLaneProducerConsumerProof(observation);

        Assert.Equal("process-consumer:harness-multidomain", observation.CallerContext);
        Assert.Contains(matrix, entry =>
            entry.Lane == ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification &&
            entry.ProducerType == typeof(ProcessTranscriptVerificationReadOnlyAdapter));
        Assert.Contains(matrix, entry =>
            entry.Lane == ProcessDriverCapabilityScopeKind.RuntimeFactsRead &&
            entry.ProducerType == typeof(ProcessRuntimeEvidenceVerificationReadOnlyAdapter));
        Assert.Contains(matrix, entry =>
            entry.Lane == ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead &&
            entry.ProducerType == typeof(ProcessArtifactEvidenceReadOnlyAdapter));
        Assert.Contains(matrix, entry =>
            entry.Lane == ProcessDriverCapabilityScopeKind.OfficeEvidenceRead &&
            entry.ProducerType == typeof(ProcessOfficeEvidenceReadOnlyAdapter));
        Assert.Contains(matrix, entry =>
            entry.Lane == ProcessDriverCapabilityScopeKind.BusinessAnalysisRead &&
            entry.ProducerType == typeof(ProcessBusinessAnalysisReadOnlyAdapter));
        Assert.All(matrix, entry =>
            Assert.Equal(typeof(ProcessReadOnlyVerificationBatchOrchestrator), entry.ConsumerType));
    }

    [Fact]
    public void Process_readonly_verification_batch_orchestrator_SB027_INV_002_feeds_artifact_projection_validation_and_satisfaction_descriptors_without_mutation()
    {
        var orchestrator = new ProcessReadOnlyVerificationBatchOrchestrator();
        var expectedSensitiveDeliverable = CreateExpectedArtifact(
            Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            ProcessCoreArtifactKind.Deliverable,
            "fixture-secret release evidence reviewer@example.invalid",
            ProcessCoreArtifactTrustRequirement.HumanApproved,
            ProcessCoreSensitivityLevel.Restricted);
        var expectedEvidence = CreateExpectedArtifact(
            Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
            ProcessCoreArtifactKind.Evidence,
            "Evidence log",
            ProcessCoreArtifactTrustRequirement.None,
            ProcessCoreSensitivityLevel.Public);
        var payload = new ProcessReadOnlyVerificationBatchPayload(
            ProcessRunId,
            StepRunId,
            "process-consumer:artifact-rehearsal",
            RequestedAt,
            artifactEvidencePayloads:
            [
                CreateArtifactPayload(
                    projectionLineage: [CreateArtifactProjectionLineage()],
                    projectionSourceOrder:
                    [
                        ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.CompletedDecision),
                        ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.FileWrite),
                        ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.FileWrite)
                    ],
                    validationRequirements: [CreateArtifactValidationRequirement()],
                    expectedArtifacts: [expectedSensitiveDeliverable, expectedEvidence],
                    artifactRecords:
                    [
                        new CoreArtifactRecordSnapshot(
                            Id: Guid.Parse("11111111-2222-3333-4444-555555555555"),
                            ArtifactExpectationId: expectedSensitiveDeliverable.Id,
                            ArtifactKind: ProcessCoreArtifactKind.Deliverable,
                            Title: expectedSensitiveDeliverable.Title,
                            TrustStatus: ProcessCoreArtifactTrustStatus.ReviewRequired,
                            SensitivityLevel: ProcessCoreSensitivityLevel.Confidential,
                            CreatedAtUtc: DateTimeOffset.Parse("2026-06-08T14:10:00Z")),
                        new CoreArtifactRecordSnapshot(
                            Id: Guid.Parse("66666666-7777-8888-9999-000000000000"),
                            ArtifactExpectationId: expectedEvidence.Id,
                            ArtifactKind: ProcessCoreArtifactKind.Deliverable,
                            Title: expectedEvidence.Title,
                            TrustStatus: ProcessCoreArtifactTrustStatus.TrustedSource,
                            SensitivityLevel: ProcessCoreSensitivityLevel.Public,
                            CreatedAtUtc: DateTimeOffset.Parse("2026-06-08T14:12:00Z"))
                    ])
            ]);

        var observation = orchestrator.Verify(payload);
        var artifactObservation = Assert.Single(observation.ArtifactEvidenceObservations);
        var categories = artifactObservation.Diagnostics.Select(diagnostic => diagnostic.Category).ToHashSet();
        var aggregate = Assert.IsType<ProcessReadOnlyVerificationAggregateObservation>(
            observation.AggregateObservation);
        var artifactSummary = Assert.Single(aggregate.LaneSummaries);

        Assert.Equal(ProcessRunId, observation.ProcessRunId);
        Assert.Equal(StepRunId, observation.StepRunId);
        Assert.Equal("process-consumer:artifact-rehearsal", observation.CallerContext);
        Assert.Equal(1, observation.ResponseCount);
        Assert.True(artifactObservation.Accepted);
        Assert.Equal(ProcessDriverDenialReason.None, artifactObservation.DenialReason);
        Assert.True(artifactObservation.NoMutationPerformed);
        Assert.Equal(ProcessDriverContractVersion.Current, artifactObservation.ContractVersion);
        Assert.Contains(ProcessDriverDiagnosticCategory.ProjectionOrderDrift, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.ArtifactLineageMissing, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.ArtifactTrustSensitivityMismatch, categories);
        Assert.Contains(ProcessDriverDiagnosticCategory.ArtifactSatisfactionInconsistent, categories);
        Assert.DoesNotContain(ProcessDriverDiagnosticCategory.NoIssueDetected, categories);
        Assert.Contains(
            artifactObservation.EvidenceReferences,
            evidenceReference => evidenceReference.CoreDescriptorFamily == ProcessDriverCoreDescriptorFamily.ArtifactProjectionEvidence);
        Assert.Contains(
            artifactObservation.EvidenceReferences,
            evidenceReference => evidenceReference.CoreDescriptorFamily == ProcessDriverCoreDescriptorFamily.ArtifactProjectionValidation);
        AssertReadonlyAuditFacts(
            artifactObservation.AuditFacts,
            ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead,
            ProcessDriverDenialReason.None);
        Assert.Equal(ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead, artifactSummary.Lane);
        Assert.Equal(1, artifactSummary.ResponseCount);
        Assert.Equal(1, artifactSummary.AcceptedCount);
        Assert.Equal(0, artifactSummary.DeniedCount);
        Assert.True(artifactSummary.AllResponsesMutationFree);
        Assert.Contains(ProcessDriverDiagnosticCategory.ProjectionOrderDrift, artifactSummary.DiagnosticCategories);
        Assert.Contains(ProcessDriverDiagnosticCategory.ArtifactLineageMissing, artifactSummary.DiagnosticCategories);
        Assert.Contains(ProcessDriverDiagnosticCategory.ArtifactTrustSensitivityMismatch, artifactSummary.DiagnosticCategories);
        Assert.Contains(ProcessDriverDiagnosticCategory.ArtifactSatisfactionInconsistent, artifactSummary.DiagnosticCategories);
        AssertDiagnosticsAndAuditDoNotContain(
            artifactObservation.Diagnostics,
            artifactObservation.AuditFacts,
            "fixture-secret",
            "reviewer@example.invalid");
        AssertReadOnlyList(observation.ArtifactEvidenceObservations, artifactObservation);
        AssertReadOnlyList(aggregate.LaneSummaries, artifactSummary);
    }

    [Fact]
    public void Process_readonly_verification_batch_orchestrator_SB030_INV_001_feeds_supplied_office_and_business_evidence_without_external_sources()
    {
        var orchestrator = new ProcessReadOnlyVerificationBatchOrchestrator();
        var payload = new ProcessReadOnlyVerificationBatchPayload(
            ProcessRunId,
            StepRunId,
            "process-consumer:office-business-rehearsal",
            RequestedAt,
            officeEvidencePayloads:
            [
                CreateOfficePayload(
                    evidenceUri: "bundle://proof/SB030/office-evidence.json",
                    evidencePayload: OfficeBatchEvidencePayload,
                    items: [CreateOfficeItem(), CreateOfficeDocumentItem()])
            ],
            businessAnalysisPayloads:
            [
                CreateBusinessPayload(
                    evidenceUri: "bundle://proof/SB030/business-analysis.json",
                    items: [CreateBusinessDeliverable(), CreateBusinessSupportingEvidence()])
            ]);

        var observation = orchestrator.Verify(payload);
        var officeObservation = Assert.Single(observation.OfficeEvidenceObservations);
        var businessObservation = Assert.Single(observation.BusinessAnalysisObservations);
        var aggregate = Assert.IsType<ProcessReadOnlyVerificationAggregateObservation>(
            observation.AggregateObservation);
        var officeSummary = Assert.Single(
            aggregate.LaneSummaries,
            summary => summary.Lane == ProcessDriverCapabilityScopeKind.OfficeEvidenceRead);
        var businessSummary = Assert.Single(
            aggregate.LaneSummaries,
            summary => summary.Lane == ProcessDriverCapabilityScopeKind.BusinessAnalysisRead);
        var officeEvidence = Assert.Single(officeObservation.EvidenceReferences);
        var businessEvidence = Assert.Single(businessObservation.EvidenceReferences);

        Assert.Equal(ProcessRunId, observation.ProcessRunId);
        Assert.Equal(StepRunId, observation.StepRunId);
        Assert.Equal("process-consumer:office-business-rehearsal", observation.CallerContext);
        Assert.Equal(2, observation.ResponseCount);
        Assert.All(observation.Responses, response =>
        {
            Assert.True(response.Accepted);
            Assert.True(response.NoMutationPerformed);
            Assert.Equal(ProcessDriverDenialReason.None, response.DenialReason);
            Assert.Equal(ProcessDriverContractVersion.Current, response.ContractVersion);
        });

        Assert.True(officeObservation.Accepted);
        Assert.Equal(ProcessOfficeEvidenceSourceLane.OfficeEvidenceRead, officeObservation.SourceLane);
        Assert.Equal(ProcessDriverEvidenceReferenceKind.OfficeReadonlyArtifact, officeEvidence.Kind);
        Assert.Equal("bundle://proof/SB030/office-evidence.json", officeEvidence.Uri);
        Assert.Equal(ProcessDriverEvidencePolicy.ComputeSha256(OfficeBatchEvidencePayload), officeEvidence.ContentHash);
        Assert.Null(officeEvidence.CoreDescriptorFamily);
        Assert.Contains(
            officeObservation.Diagnostics,
            diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.NoIssueDetected);
        AssertReadonlyAuditFacts(
            officeObservation.AuditFacts,
            ProcessDriverCapabilityScopeKind.OfficeEvidenceRead,
            ProcessDriverDenialReason.None);

        Assert.True(businessObservation.Accepted);
        Assert.Equal(ProcessBusinessAnalysisSourceLane.BusinessAnalysisRead, businessObservation.SourceLane);
        Assert.Equal(ProcessDriverEvidenceReferenceKind.BusinessReadonlyArtifact, businessEvidence.Kind);
        Assert.Equal("bundle://proof/SB030/business-analysis.json", businessEvidence.Uri);
        Assert.Equal(ProcessDriverEvidencePolicy.ComputeSha256(BusinessAnalysisPayload), businessEvidence.ContentHash);
        Assert.Null(businessEvidence.CoreDescriptorFamily);
        Assert.Contains(
            businessObservation.Diagnostics,
            diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.NoIssueDetected);
        AssertReadonlyAuditFacts(
            businessObservation.AuditFacts,
            ProcessDriverCapabilityScopeKind.BusinessAnalysisRead,
            ProcessDriverDenialReason.None);

        Assert.Equal(2, aggregate.ResponseCount);
        Assert.Equal(2, aggregate.AcceptedCount);
        Assert.Equal(0, aggregate.DeniedCount);
        Assert.True(aggregate.AggregationMutationFree);
        Assert.True(aggregate.AllResponsesMutationFree);
        Assert.Equal(1, officeSummary.ResponseCount);
        Assert.Equal(1, officeSummary.AcceptedCount);
        Assert.Equal(0, officeSummary.DeniedCount);
        Assert.True(officeSummary.AllResponsesMutationFree);
        Assert.Equal(1, businessSummary.ResponseCount);
        Assert.Equal(1, businessSummary.AcceptedCount);
        Assert.Equal(0, businessSummary.DeniedCount);
        Assert.True(businessSummary.AllResponsesMutationFree);
        Assert.All(
            officeObservation.AuditFacts.Concat(businessObservation.AuditFacts),
            fact =>
            {
                Assert.False(fact.Scope.AllowsExternalCalls);
                Assert.False(fact.Scope.AllowsProcessMutation);
                Assert.False(fact.Scope.AllowsWorkspaceWrites);
                Assert.False(fact.Scope.AllowsStorageWrites);
            });
        AssertReadOnlyList(observation.OfficeEvidenceObservations, officeObservation);
        AssertReadOnlyList(observation.BusinessAnalysisObservations, businessObservation);
        AssertReadOnlyList(aggregate.LaneSummaries, officeSummary);
    }

    [Fact]
    public void Process_readonly_verification_batch_orchestrator_SB030_INV_002_denies_office_and_business_external_calls_without_mutation()
    {
        var orchestrator = new ProcessReadOnlyVerificationBatchOrchestrator();
        var payload = new ProcessReadOnlyVerificationBatchPayload(
            ProcessRunId,
            StepRunId,
            "process-consumer:office-business-denial-rehearsal",
            RequestedAt,
            officeEvidencePayloads:
            [
                CreateOfficePayload(
                    requestedOperations: [ProcessDriverOperation.CallOfficeGraph],
                    evidenceUri: "bundle://proof/SB030/office-graph-call-denied.json",
                    evidencePayload: OfficeBatchEvidencePayload,
                    items: [CreateOfficeItemWithSensitiveText()])
            ],
            businessAnalysisPayloads:
            [
                CreateBusinessPayload(
                    requestedOperations: [ProcessDriverOperation.CallOfficeGraph],
                    evidenceUri: "bundle://proof/SB030/business-external-call-denied.json",
                    items: [CreateBusinessDeliverableWithSensitiveText(), CreateBusinessSupportingEvidence()]),
                CreateBusinessPayload(
                    requestedOperations: [ProcessDriverOperation.MutateBusinessRecord],
                    evidenceUri: "bundle://proof/SB030/business-record-mutation-denied.json",
                    items: [CreateBusinessDeliverableWithSensitiveText(), CreateBusinessSupportingEvidence()])
            ]);

        var observation = orchestrator.Verify(payload);
        var officeDenied = Assert.Single(observation.OfficeEvidenceObservations);
        var businessExternalDenied = Assert.Single(
            observation.BusinessAnalysisObservations,
            businessObservation => businessObservation.DenialReason == ProcessDriverDenialReason.ExternalCallDenied);
        var businessMutationDenied = Assert.Single(
            observation.BusinessAnalysisObservations,
            businessObservation => businessObservation.DenialReason == ProcessDriverDenialReason.MutationDenied);
        var aggregate = Assert.IsType<ProcessReadOnlyVerificationAggregateObservation>(
            observation.AggregateObservation);
        var officeSummary = Assert.Single(
            aggregate.LaneSummaries,
            summary => summary.Lane == ProcessDriverCapabilityScopeKind.OfficeEvidenceRead);
        var businessSummary = Assert.Single(
            aggregate.LaneSummaries,
            summary => summary.Lane == ProcessDriverCapabilityScopeKind.BusinessAnalysisRead);

        Assert.Equal(3, observation.ResponseCount);
        Assert.All(observation.Responses, response =>
        {
            Assert.False(response.Accepted);
            Assert.True(response.NoMutationPerformed);
            Assert.Equal(ProcessDriverContractVersion.Current, response.ContractVersion);
            Assert.Contains(
                response.Diagnostics,
                diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.MutationAttemptDenied);
            Assert.DoesNotContain(
                response.Diagnostics,
                diagnostic => diagnostic.Category == ProcessDriverDiagnosticCategory.NoIssueDetected);
        });

        Assert.False(officeDenied.Accepted);
        Assert.Equal(ProcessDriverDenialReason.ExternalCallDenied, officeDenied.DenialReason);
        Assert.True(officeDenied.NoMutationPerformed);
        AssertDeniedAuditFacts(
            officeDenied.AuditFacts,
            ProcessDriverCapabilityScopeKind.OfficeEvidenceRead,
            ProcessDriverOperation.CallOfficeGraph,
            ProcessDriverDenialReason.ExternalCallDenied);
        AssertDiagnosticsAndAuditDoNotContain(
            officeDenied.Diagnostics,
            officeDenied.AuditFacts,
            "fixture-secret",
            "reviewer@example.invalid");

        Assert.False(businessExternalDenied.Accepted);
        Assert.Equal(ProcessDriverDenialReason.ExternalCallDenied, businessExternalDenied.DenialReason);
        Assert.True(businessExternalDenied.NoMutationPerformed);
        AssertDeniedAuditFacts(
            businessExternalDenied.AuditFacts,
            ProcessDriverCapabilityScopeKind.BusinessAnalysisRead,
            ProcessDriverOperation.CallOfficeGraph,
            ProcessDriverDenialReason.ExternalCallDenied);
        AssertDiagnosticsAndAuditDoNotContain(
            businessExternalDenied.Diagnostics,
            businessExternalDenied.AuditFacts,
            "fixture-secret",
            "reviewer@example.invalid");

        Assert.False(businessMutationDenied.Accepted);
        Assert.Equal(ProcessDriverDenialReason.MutationDenied, businessMutationDenied.DenialReason);
        Assert.True(businessMutationDenied.NoMutationPerformed);
        AssertDeniedAuditFacts(
            businessMutationDenied.AuditFacts,
            ProcessDriverCapabilityScopeKind.BusinessAnalysisRead,
            ProcessDriverOperation.MutateBusinessRecord,
            ProcessDriverDenialReason.MutationDenied);
        AssertDiagnosticsAndAuditDoNotContain(
            businessMutationDenied.Diagnostics,
            businessMutationDenied.AuditFacts,
            "fixture-secret",
            "reviewer@example.invalid");

        Assert.Equal(3, aggregate.ResponseCount);
        Assert.Equal(0, aggregate.AcceptedCount);
        Assert.Equal(3, aggregate.DeniedCount);
        Assert.True(aggregate.AggregationMutationFree);
        Assert.True(aggregate.AllResponsesMutationFree);
        Assert.Equal(1, officeSummary.ResponseCount);
        Assert.Equal(0, officeSummary.AcceptedCount);
        Assert.Equal(1, officeSummary.DeniedCount);
        Assert.True(officeSummary.AllResponsesMutationFree);
        Assert.Contains(ProcessDriverDiagnosticCategory.MutationAttemptDenied, officeSummary.DiagnosticCategories);
        Assert.Equal(2, businessSummary.ResponseCount);
        Assert.Equal(0, businessSummary.AcceptedCount);
        Assert.Equal(2, businessSummary.DeniedCount);
        Assert.True(businessSummary.AllResponsesMutationFree);
        Assert.Contains(ProcessDriverDiagnosticCategory.MutationAttemptDenied, businessSummary.DiagnosticCategories);
        AssertReadOnlyList(observation.OfficeEvidenceObservations, officeDenied);
        AssertReadOnlyList(observation.BusinessAnalysisObservations, businessExternalDenied);
        AssertReadOnlyList(aggregate.LaneSummaries, officeSummary);
    }

    [Fact]
    public void Process_manager_readonly_projection_SB031_INV_001_projects_supplied_observations_as_diagnostics_without_mutation()
    {
        var observation = CreateManagerProjectionSourceObservation();

        var projection = ProcessManagerReadOnlyVerificationProjectionMapper.Project(
            new ProcessManagerReadOnlyVerificationProjectionRequest(
                observation,
                ProcessManagerReadOnlyVerificationProjectionMode.Diagnostics,
                "process-manager",
                RequestedAt));

        Assert.True(projection.IsAttached);
        Assert.Equal(ProcessManagerReadOnlyVerificationProjectionMode.Diagnostics, projection.Mode);
        Assert.Equal(ProcessManagerReadOnlyVerificationProjectionSource.SuppliedEvidenceOnly, projection.Source);
        Assert.True(projection.NoMutationPerformed);
        Assert.False(projection.AllowsProcessMutation);
        Assert.False(projection.AllowsTransitionMutation);
        Assert.False(projection.AllowsFinalizerMutation);
        Assert.Null(projection.EvidenceEnvelope);
        Assert.NotEmpty(projection.Diagnostics);
        Assert.Contains(
            projection.Diagnostics,
            diagnostic => diagnostic.Lane == ProcessDriverCapabilityScopeKind.BusinessAnalysisRead &&
                diagnostic.Category == ProcessDriverDiagnosticCategory.NoIssueDetected);
        Assert.All(projection.Diagnostics, diagnostic =>
        {
            Assert.Equal(ProcessDriverContractVersion.Current, diagnostic.ContractVersion);
            Assert.NotEmpty(diagnostic.EvidenceReferences);
            Assert.All(
                diagnostic.EvidenceReferences,
                evidenceReference => Assert.StartsWith("bundle://", evidenceReference.Uri, StringComparison.Ordinal));
        });
    }

    [Fact]
    public void Process_manager_readonly_projection_SB032_INV_001_attaches_evidence_envelope_only_when_requested()
    {
        var observation = CreateManagerProjectionSourceObservation();

        var noneProjection = ProcessManagerReadOnlyVerificationProjectionMapper.Project(
            new ProcessManagerReadOnlyVerificationProjectionRequest(
                observation,
                ProcessManagerReadOnlyVerificationProjectionMode.None,
                string.Empty,
                RequestedAt));
        var envelopeProjection = ProcessManagerReadOnlyVerificationProjectionMapper.Project(
            new ProcessManagerReadOnlyVerificationProjectionRequest(
                observation,
                ProcessManagerReadOnlyVerificationProjectionMode.EvidenceEnvelope,
                "process-manager",
                RequestedAt));

        Assert.False(noneProjection.IsAttached);
        Assert.Empty(noneProjection.Diagnostics);
        Assert.Null(noneProjection.EvidenceEnvelope);
        Assert.True(noneProjection.NoMutationPerformed);
        Assert.False(noneProjection.AllowsProcessMutation);
        Assert.False(noneProjection.AllowsTransitionMutation);
        Assert.False(noneProjection.AllowsFinalizerMutation);

        Assert.True(envelopeProjection.IsAttached);
        Assert.Empty(envelopeProjection.Diagnostics);
        Assert.NotNull(envelopeProjection.EvidenceEnvelope);
        var envelope = envelopeProjection.EvidenceEnvelope!;
        Assert.Equal(observation.ResponseCount, envelope.ResponseCount);
        Assert.Equal(observation.ResponseCount, envelope.AcceptedCount);
        Assert.Equal(0, envelope.DeniedCount);
        Assert.True(envelope.AggregationMutationFree);
        Assert.True(envelope.AllResponsesMutationFree);
        Assert.Contains(
            envelope.LaneSummaries,
            summary => summary.Lane == ProcessDriverCapabilityScopeKind.BusinessAnalysisRead);
        Assert.All(
            envelope.EvidenceReferences,
            evidenceReference => Assert.StartsWith("bundle://", evidenceReference.Uri, StringComparison.Ordinal));
    }

    [Fact]
    public void Process_manager_readonly_projection_SB033_INV_001_rejects_unnamed_attached_manager_request()
    {
        var observation = CreateManagerProjectionSourceObservation();

        var exception = Assert.Throws<ArgumentException>(() =>
            ProcessManagerReadOnlyVerificationProjectionMapper.Project(
                new ProcessManagerReadOnlyVerificationProjectionRequest(
                    observation,
                    ProcessManagerReadOnlyVerificationProjectionMode.EvidenceEnvelope,
                    string.Empty,
                    RequestedAt)));

        Assert.Contains("requesting manager identity", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_readonly_payload_builders_SB018_INV_001_create_hash_content_type_and_size_contracts_from_memory()
    {
        var transcriptPayload = CreateTranscriptPayload();
        var runtimePayload = CreateRuntimePayload();
        var artifactPayload = CreateArtifactPayload();
        var officePayload = CreateOfficePayload();
        var businessPayload = CreateBusinessPayload();

        Assert.Equal(ProcessDriverEvidencePolicy.ComputeSha256("Build succeeded."), transcriptPayload.TranscriptReference.TranscriptHash);
        Assert.Single(runtimePayload.EvidenceReferences);
        Assert.Equal(ProcessDriverEvidenceReferenceKind.CoreDescriptor, runtimePayload.EvidenceReferences[0].Kind);
        Assert.Equal(ProcessDriverCoreDescriptorFamily.ExecutionEvidence, runtimePayload.EvidenceReferences[0].CoreDescriptorFamily);
        Assert.True(ProcessDriverEvidencePolicy.IsSha256(runtimePayload.EvidenceReferences[0].ContentHash));
        AssertSuppliedContentContract(
            artifactPayload.SuppliedContent,
            ProcessDriverSuppliedEvidenceContentKind.CoreDescriptorPayload,
            ProcessDriverSuppliedEvidenceContentRules.JsonContentType,
            ArtifactEvidencePayload);
        AssertSuppliedContentContract(
            officePayload.SuppliedContent,
            ProcessDriverSuppliedEvidenceContentKind.OfficeEvidencePayload,
            ProcessDriverSuppliedEvidenceContentRules.JsonContentType,
            OfficeEvidencePayload);
        AssertSuppliedContentContract(
            businessPayload.SuppliedContent,
            ProcessDriverSuppliedEvidenceContentKind.BusinessAnalysisPayload,
            ProcessDriverSuppliedEvidenceContentRules.JsonContentType,
            BusinessAnalysisPayload);
    }

    [Fact]
    public void Process_readonly_verification_cross_lane_SB035_INV_001_preserves_no_mutation_audit_redaction_and_evidence_hashes()
    {
        const string sensitiveTranscript = """
            Build succeeded.
            token=sk-test-secret process.owner@example.com
            """;
        var orchestrator = new ProcessReadOnlyVerificationBatchOrchestrator();
        var payload = new ProcessReadOnlyVerificationBatchPayload(
            ProcessRunId,
            StepRunId,
            "process-consumer:cross-lane-readonly",
            RequestedAt,
            transcriptPayloads: [CreateTranscriptPayload(sensitiveTranscript)],
            runtimeEvidencePayloads: [CreateRuntimePayload()],
            artifactEvidencePayloads: [CreateArtifactPayload()],
            officeEvidencePayloads: [CreateOfficePayload(evidenceUri: "bundle://proof/SB035/office-evidence.json")],
            businessAnalysisPayloads: [CreateBusinessPayload(evidenceUri: "bundle://proof/SB035/business-analysis.json")]);

        var observation = orchestrator.Verify(payload);
        var aggregate = Assert.IsType<ProcessReadOnlyVerificationAggregateObservation>(
            observation.AggregateObservation);

        Assert.Equal(5, observation.ResponseCount);
        Assert.Equal(5, aggregate.ResponseCount);
        Assert.Equal(5, aggregate.AcceptedCount);
        Assert.Equal(0, aggregate.DeniedCount);
        Assert.True(aggregate.AggregationMutationFree);
        Assert.True(aggregate.AllResponsesMutationFree);
        Assert.True(ProcessDriverEvidencePolicy.IsSha256(aggregate.Redaction.RedactedTextHash));
        Assert.Contains(
            observation.Responses,
            response => response.Redaction.Status == ProcessDriverRedactionStatus.Redacted);
        Assert.All(observation.Responses, response =>
        {
            Assert.True(response.NoMutationPerformed);
            Assert.Equal(ProcessDriverContractVersion.Current, response.ContractVersion);
            Assert.NotEmpty(response.Diagnostics);
            Assert.NotEmpty(response.EvidenceReferences);
            Assert.NotEmpty(response.AuditFacts);
            Assert.True(ProcessDriverEvidencePolicy.IsSha256(response.Redaction.RedactedTextHash));
            Assert.All(
                response.Diagnostics,
                diagnostic =>
                {
                    Assert.DoesNotContain("sk-test-secret", diagnostic.Message, StringComparison.Ordinal);
                    Assert.DoesNotContain("process.owner@example.com", diagnostic.Message, StringComparison.Ordinal);
                });
            Assert.All(
                response.EvidenceReferences,
                evidenceReference => Assert.True(ProcessDriverEvidencePolicy.IsSha256(evidenceReference.ContentHash)));
            Assert.All(response.AuditFacts, fact =>
            {
                Assert.False(fact.Scope.AllowsExternalCalls);
                Assert.False(fact.Scope.AllowsProcessMutation);
                Assert.False(fact.Scope.AllowsWorkspaceWrites);
                Assert.False(fact.Scope.AllowsStorageWrites);
                Assert.NotEmpty(fact.EvidenceReferences);
                Assert.True(ProcessDriverEvidencePolicy.IsSha256(fact.OutputHash));
                Assert.DoesNotContain("sk-test-secret", fact.DiagnosticSummary, StringComparison.Ordinal);
                Assert.DoesNotContain("process.owner@example.com", fact.DiagnosticSummary, StringComparison.Ordinal);
            });
        });
    }

    private static ProcessTranscriptVerificationReadOnlyEvidencePayload CreateTranscriptPayload(
        string transcriptText = "Build succeeded.")
    {
        return ProcessReadOnlyVerificationPayloadBuilder.CreateTranscriptPayload(
            new ProcessTranscriptVerificationPayloadFacts(
                CreateIdentity("process-consumer:transcript-readonly"),
                ProcessDriverTranscriptLanguage.DotNet,
                "dotnet",
                "net10.0",
                "bundle://proof/SB015/transcripts/process-supplied-transcript.txt",
                transcriptText,
                [ProcessDriverOperation.InspectExistingEvidence, ProcessDriverOperation.ReturnDiagnostics]));
    }

    private static ProcessRuntimeEvidenceVerificationReadOnlyEvidencePayload CreateRuntimePayload()
    {
        var executionEvidence = CreateExecutionEvidence(ProcessAutomationRunOutcome.Succeeded);
        var finalizerEvidence = CreateFinalizerEvidence(
            hasResult: true,
            shouldApplyTransition: true,
            ProcessStepRunStatus.Completed);

        return ProcessReadOnlyVerificationPayloadBuilder.CreateRuntimeEvidencePayload(
            new ProcessRuntimeEvidenceVerificationPayloadFacts(
                CreateIdentity("process-consumer:runtime-readonly"),
                "bundle://proof/SB015/runtime-evidence-consistency.json",
                executionEvidence,
                finalizerEvidence,
                RetryDiagnostic: null,
                NoProgressDiagnostic: null,
                ProviderRepairDiagnostic: null,
                ProjectionSourceOrder: [],
                [ProcessDriverOperation.ReadProcessFacts, ProcessDriverOperation.ReturnDiagnostics]));
    }

    private static ProcessArtifactEvidenceReadOnlyPayload CreateArtifactPayload(
        IReadOnlyList<ProcessDriverOperation>? requestedOperations = null,
        string projectionEvidenceUri = "bundle://proof/SB021/artifact-projection-evidence.json",
        IReadOnlyList<ProcessArtifactProjectionLineageDescriptor>? projectionLineage = null,
        IReadOnlyList<ProcessArtifactProjectionSourceOrderDescriptor>? projectionSourceOrder = null,
        IReadOnlyList<ProcessProviderNativeBrowserEvidenceDescriptor>? providerNativeBrowserEvidence = null,
        IReadOnlyList<ProcessArtifactValidationRequirementDescriptor>? validationRequirements = null,
        IReadOnlyList<CoreArtifactExpectationSnapshot>? expectedArtifacts = null,
        IReadOnlyList<CoreArtifactRecordSnapshot>? artifactRecords = null)
    {
        return ProcessReadOnlyVerificationPayloadBuilder.CreateArtifactEvidencePayload(
            new ProcessArtifactEvidencePayloadFacts(
                CreateIdentity("process-consumer:artifact-readonly"),
                projectionEvidenceUri,
                ArtifactEvidencePayload,
                "bundle://proof/SB021/artifact-projection-validation.json",
                ArtifactEvidencePayload,
                projectionLineage ?? [CreateArtifactProjectionLineage()],
                projectionSourceOrder ?? [ProcessArtifactProjectionEvidenceDescriptorRules.DescribeSourceOrder(ProcessCoreArtifactProjectionSourceKind.FileWrite)],
                providerNativeBrowserEvidence ?? [],
                validationRequirements ?? [CreateArtifactValidationRequirement()],
                expectedArtifacts ?? [],
                artifactRecords ?? [],
                requestedOperations));
    }

    private static ProcessOfficeEvidenceReadOnlyPayload CreateOfficePayload(
        IReadOnlyList<ProcessDriverOperation>? requestedOperations = null,
        string evidenceUri = "bundle://proof/SB024/office-evidence.json",
        string evidencePayload = OfficeEvidencePayload,
        IReadOnlyList<OfficeEvidenceItem>? items = null)
    {
        return ProcessReadOnlyVerificationPayloadBuilder.CreateOfficeEvidencePayload(
            new ProcessOfficeEvidencePayloadFacts(
                CreateIdentity("process-consumer:office-readonly"),
                evidenceUri,
                evidencePayload,
                items ?? [CreateOfficeItem()],
                requestedOperations));
    }

    private static ProcessBusinessAnalysisReadOnlyPayload CreateBusinessPayload(
        IReadOnlyList<ProcessDriverOperation>? requestedOperations = null,
        string evidenceUri = "bundle://proof/SB024/business-analysis.json",
        string evidencePayload = BusinessAnalysisPayload,
        IReadOnlyList<BusinessAnalysisEvidenceItem>? items = null)
    {
        return ProcessReadOnlyVerificationPayloadBuilder.CreateBusinessAnalysisPayload(
            new ProcessBusinessAnalysisPayloadFacts(
                CreateIdentity("process-consumer:business-readonly"),
                evidenceUri,
                evidencePayload,
                items ?? [CreateBusinessDeliverable(), CreateBusinessSupportingEvidence()],
                requestedOperations));
    }

    private static ProcessVerificationHostRequest CreateBusinessAnalysisHostRequest()
    {
        return new ProcessVerificationHostRequest(
            ProcessDriverVerificationGatewayLane.BusinessAnalysisRead,
            new ProcessReadOnlyVerificationBatchPayload(
                ProcessRunId,
                StepRunId,
                "process-manager:business-analysis-options",
                RequestedAt,
                businessAnalysisPayloads: [CreateBusinessPayload()]),
            "process-manager",
            RequestedAt);
    }

    private static ProcessVerificationRuntimeHost CreateHost(
        ProcessVerificationRuntimeHostOptions options,
        ProcessVerificationLaneRegistry? registry = null,
        IProcessVerificationAuditStore? auditStore = null)
    {
        return new ProcessVerificationRuntimeHost(
            new ProcessReadOnlyVerificationBatchOrchestrator(),
            new ProcessVerificationLaneSelector(registry ?? new ProcessVerificationLaneRegistry()),
            auditStore ?? new InMemoryProcessVerificationAuditStore(),
            Options.Create(options));
    }

    private static async Task<ProcessVerificationHostDenial> CreateDenialForCodeAsync(
        ProcessVerificationHostDenialCode code,
        ProcessVerificationHostRequest request)
    {
        var result = code switch
        {
            ProcessVerificationHostDenialCode.HostDisabled => await CreateHost(new ProcessVerificationRuntimeHostOptions
            {
                Enabled = false
            }).VerifyAsync(request),
            ProcessVerificationHostDenialCode.LaneDisabled => await CreateHost(CreateLaneDisabledOptions()).VerifyAsync(request),
            ProcessVerificationHostDenialCode.UnsupportedLane => await CreateHost(new ProcessVerificationRuntimeHostOptions()).VerifyAsync(new ProcessVerificationHostRequest(
                (ProcessDriverVerificationGatewayLane)999,
                request.Payload,
                request.RequestedBy,
                request.RequestedAt)),
            ProcessVerificationHostDenialCode.MissingLaneRegistration => await CreateHost(
                new ProcessVerificationRuntimeHostOptions(),
                CreateRegistryExcluding(request.Lane)).VerifyAsync(request),
            ProcessVerificationHostDenialCode.MissingLanePayload => await CreateHost(new ProcessVerificationRuntimeHostOptions()).VerifyAsync(new ProcessVerificationHostRequest(
                ProcessDriverVerificationGatewayLane.ArtifactEvidenceConsistency,
                new ProcessReadOnlyVerificationBatchPayload(
                    request.Payload.ProcessRunId,
                    request.Payload.StepRunId,
                    "process-manager:missing-lane-payload",
                    request.RequestedAt,
                    businessAnalysisPayloads: [CreateBusinessPayload()]),
                request.RequestedBy,
                request.RequestedAt)),
            ProcessVerificationHostDenialCode.PayloadLimitExceeded => await CreateHost(new ProcessVerificationRuntimeHostOptions
            {
                MaxPayloadItemsPerLane = 1
            }).VerifyAsync(new ProcessVerificationHostRequest(
                ProcessDriverVerificationGatewayLane.OfficeEvidenceRead,
                new ProcessReadOnlyVerificationBatchPayload(
                    request.Payload.ProcessRunId,
                    request.Payload.StepRunId,
                    "process-manager:payload-count-limit",
                    request.RequestedAt,
                    officeEvidencePayloads: [CreateOfficePayload(), CreateOfficePayload()]),
                request.RequestedBy,
                request.RequestedAt)),
            ProcessVerificationHostDenialCode.SuppliedEvidenceContentLimitExceeded => await CreateHost(new ProcessVerificationRuntimeHostOptions
            {
                MaxSuppliedEvidenceContentBytes = 10
            }).VerifyAsync(request),
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, "This helper only creates pre-orchestration host denials.")
        };

        return result.Denial ?? throw new InvalidOperationException($"Expected verification host denial {code}.");
    }

    private static ProcessVerificationRuntimeHostOptions CreateLaneDisabledOptions()
    {
        var options = new ProcessVerificationRuntimeHostOptions();
        options.Lanes.BusinessAnalysisRead = false;

        return options;
    }

    private static ProcessVerificationLaneRegistry CreateRegistryExcluding(ProcessDriverVerificationGatewayLane excludedLane)
    {
        return new ProcessVerificationLaneRegistry(ProcessDriverVerificationGatewayLaneRules.AllowedLanes
            .Where(descriptor => descriptor.Lane != excludedLane)
            .Select(CreateLaneRegistration));
    }

    private static ProcessVerificationLaneRegistration CreateLaneRegistration(
        ProcessDriverVerificationGatewayLaneDescriptor descriptor)
    {
        return new ProcessVerificationLaneRegistration(
            descriptor.Lane,
            descriptor.RequiredScopeKind,
            descriptor.RequiredPermissionMode,
            descriptor.AllowedOperations);
    }

    private static void AssertHostDenial(
        ProcessVerificationHostResult result,
        ProcessVerificationHostDenialCode expectedCode)
    {
        Assert.True(result.IsDenied);
        var denial = result.Denial ?? throw new InvalidOperationException("Expected a verification host denial.");
        Assert.Equal(expectedCode, denial.Code);
        Assert.True(denial.NoMutationPerformed);
        Assert.False(denial.AllowsProcessMutation);
        Assert.False(denial.AllowsTransitionMutation);
        Assert.False(denial.AllowsFinalizerMutation);
        Assert.Equal(1, denial.AuditRecord.DeniedCount);
    }

    private static string ReadRepositoryFile(params string[] pathParts)
    {
        var root = FindRepositoryRoot();
        var segments = new string[pathParts.Length + 1];
        segments[0] = root;
        pathParts.CopyTo(segments, 1);
        return File.ReadAllText(Path.Combine(segments));
    }

    private static IEnumerable<string> EnumerateRepositorySourceFiles(params string[] pathParts)
    {
        var root = FindRepositoryRoot();
        var segments = new string[pathParts.Length + 1];
        segments[0] = root;
        pathParts.CopyTo(segments, 1);
        var sourceRoot = Path.Combine(segments);
        return Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
                !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> FindSourceMatches(
        IReadOnlyList<string> relativeRoots,
        IReadOnlyList<string> forbiddenPatterns)
    {
        var root = FindRepositoryRoot();
        var matches = new List<string>();

        foreach (var relativeRoot in relativeRoots)
        {
            foreach (var sourceFile in EnumerateRepositorySourceFiles(relativeRoot))
            {
                var source = File.ReadAllText(sourceFile);
                foreach (var pattern in forbiddenPatterns)
                {
                    if (source.Contains(pattern, StringComparison.Ordinal))
                    {
                        matches.Add($"{Path.GetRelativePath(root, sourceFile)} contains {pattern}");
                    }
                }
            }
        }

        return matches;
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        foreach (var startPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory(), Path.GetDirectoryName(sourceFilePath) ?? string.Empty })
        {
            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Unable to locate the repository root.");
    }

    private static ProcessReadOnlyVerificationBatchObservation CreateManagerProjectionSourceObservation()
    {
        var orchestrator = new ProcessReadOnlyVerificationBatchOrchestrator();
        var payload = new ProcessReadOnlyVerificationBatchPayload(
            ProcessRunId,
            StepRunId,
            "process-manager:read-only-verification",
            RequestedAt,
            artifactEvidencePayloads: [CreateArtifactPayload()],
            businessAnalysisPayloads:
            [
                CreateBusinessPayload(
                    evidenceUri: "bundle://proof/SB033/business-analysis-manager-diagnostic.json",
                    items: [CreateBusinessDeliverable(), CreateBusinessSupportingEvidence()])
            ]);

        return orchestrator.Verify(payload);
    }

    private static ProcessDriverVerificationResponse CreateVerificationResponse(
        ProcessDriverCapabilityScopeKind lane,
        bool accepted)
    {
        var evidence = CreateEvidenceReference(lane);
        var denialReason = accepted
            ? ProcessDriverDenialReason.None
            : ProcessDriverDenialReason.MissingEvidence;
        var diagnosticCategory = accepted
            ? ProcessDriverDiagnosticCategory.NoIssueDetected
            : ProcessDriverDiagnosticCategory.InsufficientProof;

        return new ProcessDriverVerificationResponse(
            accepted,
            denialReason,
            [
                new ProcessDriverDiagnostic(
                    accepted ? ProcessDriverDiagnosticSeverity.Info : ProcessDriverDiagnosticSeverity.Error,
                    diagnosticCategory,
                    $"{lane} supplied observation",
                    evidence)
            ],
            [evidence],
            NoRedaction,
            NoMutationPerformed: true,
            [CreateAuditFact(lane, accepted, evidence)],
            ProcessDriverContractVersion.Current);
    }

    private static ProcessDriverAuditFact CreateAuditFact(
        ProcessDriverCapabilityScopeKind lane,
        bool accepted,
        ProcessDriverEvidenceReference evidence)
    {
        var denialReason = accepted
            ? ProcessDriverDenialReason.None
            : ProcessDriverDenialReason.MissingEvidence;

        return new ProcessDriverAuditFact(
            Guid.Parse($"00000000-0000-0000-0000-{(int)lane:000000000000}"),
            RequestedAt,
            accepted ? ProcessDriverAuditFactKind.DiagnosticReturned : ProcessDriverAuditFactKind.OperationDenied,
            "process-consumer:aggregate-readonly",
            CreatePermissionMode(lane),
            CreateScope(lane),
            lane,
            ProcessDriverOperation.InspectExistingEvidence,
            [evidence],
            denialReason,
            NoRedaction,
            $"{lane} supplied observation",
            ProcessDriverEvidencePolicy.ComputeSha256($"{lane} supplied observation"));
    }

    private static ProcessArtifactProjectionLineageDescriptor CreateArtifactProjectionLineage()
    {
        return ProcessArtifactProjectionEvidenceDescriptorRules.DescribeLineage(
            ProcessCoreArtifactProjectionSourceKind.FileWrite,
            sourceExecutionRunId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            recoveryExecutionRunId: null,
            recoveredForExecutionRunId: null,
            projectedExecutionRunId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            workflowRunId: null,
            workflowArtifactId: null,
            subprocessRunId: null,
            sourceArtifactId: Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            reworkPacketId: null,
            sourceExternalReferenceKey: "repo://tests/artifacts/release-notes.md",
            contentHash: ProcessDriverEvidencePolicy.ComputeSha256("release notes"),
            projectionIdentityHash: ProcessDriverEvidencePolicy.ComputeSha256("release notes projection"));
    }

    private static ProcessArtifactValidationRequirementDescriptor CreateArtifactValidationRequirement()
    {
        return new ProcessArtifactValidationRequirementDescriptor(
            ExpectationId: Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            ArtifactKind: ProcessCoreArtifactKind.Deliverable,
            Title: "Release notes",
            IsRequired: true,
            ValidationRequirementSummary: "Runtime proof transcript required.",
            AllowedFutureUsageSummary: "May be used by final closure.",
            Mode: ProcessCoreArtifactExpectationMode.RuntimeProof);
    }

    private static CoreArtifactExpectationSnapshot CreateExpectedArtifact(
        Guid id,
        ProcessCoreArtifactKind artifactKind,
        string title,
        ProcessCoreArtifactTrustRequirement trustRequirement,
        ProcessCoreSensitivityLevel sensitivityLevel)
    {
        return new CoreArtifactExpectationSnapshot(
            Id: id,
            ArtifactKind: artifactKind,
            Title: title,
            IsRequired: true,
            TrustRequirement: trustRequirement,
            SensitivityLevel: sensitivityLevel,
            ValidationRequirementSummary: "Supplied validation requirement.",
            AllowedFutureUsageSummary: "Supplied future usage.");
    }

    private static OfficeEvidenceItem CreateOfficeItem()
    {
        return new OfficeEvidenceItem(
            OfficeEvidenceItemKind.EmailMessage,
            "message-1",
            "Evidence review",
            "manager@example.invalid",
            ["owner@example.invalid"],
            DateTimeOffset.Parse("2026-06-08T12:15:00Z"),
            "Evidence review text was supplied by the caller.");
    }

    private static OfficeEvidenceItem CreateOfficeDocumentItem()
    {
        return new OfficeEvidenceItem(
            OfficeEvidenceItemKind.Document,
            "document-1",
            "Evidence review notes",
            "manager@example.invalid",
            [],
            DateTimeOffset.Parse("2026-06-08T12:20:00Z"),
            "Document evidence text was supplied by the caller.");
    }

    private static OfficeEvidenceItem CreateOfficeItemWithSensitiveText()
    {
        return new OfficeEvidenceItem(
            OfficeEvidenceItemKind.EmailMessage,
            "message-sensitive",
            "Sensitive evidence review",
            "manager@example.invalid",
            ["owner@example.invalid"],
            DateTimeOffset.Parse("2026-06-08T12:25:00Z"),
            "fixture-secret reviewer@example.invalid");
    }

    private static BusinessAnalysisEvidenceItem CreateBusinessDeliverable()
    {
        return new BusinessAnalysisEvidenceItem(
            BusinessAnalysisEvidenceItemKind.Deliverable,
            "analysis-1",
            "Evidence review",
            "Requirement: business analysis evidence review text was supplied by the caller.",
            DateTimeOffset.Parse("2026-06-08T13:15:00Z"));
    }

    private static BusinessAnalysisEvidenceItem CreateBusinessDeliverableWithSensitiveText()
    {
        return new BusinessAnalysisEvidenceItem(
            BusinessAnalysisEvidenceItemKind.Deliverable,
            "analysis-sensitive",
            "Sensitive evidence review",
            "Requirement: fixture-secret reviewer@example.invalid.",
            DateTimeOffset.Parse("2026-06-08T13:17:00Z"));
    }

    private static BusinessAnalysisEvidenceItem CreateBusinessSupportingEvidence()
    {
        return new BusinessAnalysisEvidenceItem(
            BusinessAnalysisEvidenceItemKind.SupportingEvidence,
            "evidence-1",
            "Interview summary",
            "Evidence: supplied interview summary supports the deliverable.",
            DateTimeOffset.Parse("2026-06-08T13:16:00Z"));
    }

    private static ProcessDriverEvidenceReference CreateEvidenceReference(
        ProcessDriverEvidenceReferenceKind kind,
        string uri,
        string contentSeed,
        ProcessDriverCoreDescriptorFamily? coreDescriptorFamily)
    {
        return new ProcessDriverEvidenceReference(
            kind,
            uri,
            ProcessDriverEvidencePolicy.ComputeSha256(contentSeed),
            coreDescriptorFamily);
    }

    private static ProcessDriverEvidenceReference CreateEvidenceReference(ProcessDriverCapabilityScopeKind lane)
    {
        return lane switch
        {
            ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead => CreateEvidenceReference(
                ProcessDriverEvidenceReferenceKind.CoreDescriptor,
                "bundle://proof/SB027/artifact-observation.json",
                "artifact observation",
                ProcessDriverCoreDescriptorFamily.ArtifactProjectionEvidence),
            ProcessDriverCapabilityScopeKind.OfficeEvidenceRead => CreateEvidenceReference(
                ProcessDriverEvidenceReferenceKind.OfficeReadonlyArtifact,
                "bundle://proof/SB027/office-observation.json",
                "office observation",
                coreDescriptorFamily: null),
            ProcessDriverCapabilityScopeKind.BusinessAnalysisRead => CreateEvidenceReference(
                ProcessDriverEvidenceReferenceKind.BusinessReadonlyArtifact,
                "bundle://proof/SB027/business-observation.json",
                "business observation",
                coreDescriptorFamily: null),
            _ => throw new ArgumentOutOfRangeException(nameof(lane), lane, "Unsupported test lane.")
        };
    }

    private static void AssertReadonlyAuditFacts(
        IReadOnlyList<ProcessDriverAuditFact> auditFacts,
        ProcessDriverCapabilityScopeKind expectedLane,
        ProcessDriverDenialReason expectedDenialReason)
    {
        Assert.NotEmpty(auditFacts);
        Assert.All(auditFacts, fact =>
        {
            Assert.Equal(expectedLane, fact.Lane);
            Assert.Equal(expectedLane, fact.Scope.Kind);
            Assert.Equal(expectedDenialReason, fact.DenialReason);
            Assert.NotEmpty(fact.EvidenceReferences);
            Assert.True(fact.Scope is { AllowsProcessMutation: false, AllowsExternalCalls: false });
            Assert.Matches("^[A-F0-9]{64}$", fact.OutputHash);
        });
    }

    private static void AssertDeniedAuditFacts(
        IReadOnlyList<ProcessDriverAuditFact> auditFacts,
        ProcessDriverCapabilityScopeKind expectedLane,
        ProcessDriverOperation expectedOperation,
        ProcessDriverDenialReason expectedDenialReason)
    {
        Assert.NotEmpty(auditFacts);
        Assert.All(auditFacts, fact =>
        {
            Assert.Equal(ProcessDriverAuditFactKind.OperationDenied, fact.Kind);
            Assert.Equal(expectedLane, fact.Lane);
            Assert.Equal(expectedLane, fact.Scope.Kind);
            Assert.Equal(expectedOperation, fact.RequestedOperation);
            Assert.Equal(expectedDenialReason, fact.DenialReason);
            Assert.False(fact.Scope.AllowsExternalCalls);
            Assert.False(fact.Scope.AllowsProcessMutation);
            Assert.False(fact.Scope.AllowsWorkspaceWrites);
            Assert.False(fact.Scope.AllowsStorageWrites);
            Assert.NotEmpty(fact.EvidenceReferences);
            Assert.Matches("^[A-F0-9]{64}$", fact.OutputHash);
        });
    }

    private static void AssertDiagnosticsAndAuditDoNotContain(
        IReadOnlyList<ProcessDriverDiagnostic> diagnostics,
        IReadOnlyList<ProcessDriverAuditFact> auditFacts,
        params string[] forbiddenFragments)
    {
        var diagnosticAndAuditText = diagnostics
            .Select(diagnostic => diagnostic.Message)
            .Concat(auditFacts.Select(fact => fact.DiagnosticSummary))
            .ToArray();

        foreach (var forbiddenFragment in forbiddenFragments.Where(static fragment => !string.IsNullOrWhiteSpace(fragment)))
        {
            Assert.All(diagnosticAndAuditText, value =>
                Assert.DoesNotContain(forbiddenFragment, value, StringComparison.Ordinal));
        }
    }

    private static void AssertNoForbiddenFragments(
        string value,
        IReadOnlyList<string> forbiddenFragments)
    {
        foreach (var forbiddenFragment in forbiddenFragments.Where(static fragment => !string.IsNullOrWhiteSpace(fragment)))
        {
            Assert.DoesNotContain(forbiddenFragment, value, StringComparison.Ordinal);
        }
    }

    private static ProcessDriverCapabilityScope CreateScope(ProcessDriverCapabilityScopeKind lane)
    {
        return new ProcessDriverCapabilityScope(
            lane,
            CreatePermissionMode(lane),
            AllowsProcessMutation: false,
            AllowsExternalCalls: false,
            AllowsWorkspaceWrites: false,
            AllowsStorageWrites: false);
    }

    private static ProcessExecutionEvidenceDescriptor CreateExecutionEvidence(
        ProcessAutomationRunOutcome outcome)
    {
        return new ProcessExecutionEvidenceDescriptor(
            new ProcessExecutionRunEvidenceDescriptor(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                ProcessAutomationExecutionState.Completed,
                outcome,
                IsTerminal: true,
                IsActive: false,
                HasPendingToolApprovals: false,
                DateTimeOffset.Parse("2026-06-08T01:00:00Z"),
                DateTimeOffset.Parse("2026-06-08T01:01:00Z"),
                DateTimeOffset.Parse("2026-06-08T01:05:00Z"),
                outcome == ProcessAutomationRunOutcome.Succeeded
                    ? ProcessCoreExecutionRunObservationKind.Succeeded
                    : ProcessCoreExecutionRunObservationKind.Failed),
            new ProcessExecutionAttemptEvidenceDescriptor(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                AttemptNumber: 1,
                ProcessStepRunStatus.Completed,
                "completed",
                MissingRequiredTools: [],
                HasMissingRequiredTools: false,
                MissingRequiredToolCount: 0,
                HasUnresolvedCriticalToolFailures: false,
                UnresolvedCriticalToolFailureCount: 0,
                SelectedBranchOutcomeId: null),
            new ProcessExecutionCarriedProofDescriptor(
                HasConcreteImplementationProof: true,
                HasRunnableApplicationProof: true,
                HasConcreteProductMutation: false));
    }

    private static ProcessFinalizerEvidenceDescriptor CreateFinalizerEvidence(
        bool hasResult,
        bool shouldApplyTransition,
        ProcessStepRunStatus completionStatus)
    {
        return new ProcessFinalizerEvidenceDescriptor(
            new ProcessFinalizerIntentEvidenceDescriptor(
                ProcessCoreFinalizerKind.DirectAgent,
                ProcessRunId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                StepRunId: Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                completionStatus,
                "finalizer completed",
                SelectedBranchOutcomeId: null,
                ExecutionRunId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                WorkflowRunId: null,
                SubprocessRunId: null,
                ProjectsExecutionArtifacts: true,
                AllowsManagerArtifactRecovery: false,
                Trigger: "test",
                RequiresLeaseRenewal: false,
                RecoveryExecutionRunId: null,
                RecoveredForExecutionRunId: null),
            new ProcessFinalizerResultEvidenceDescriptor(
                hasResult,
                shouldApplyTransition,
                completionStatus,
                "finalizer result",
                ProcessCoreFinalizerBlockCauseKind.None,
                SelectedBranchOutcomeId: null,
                StepRunConcurrencyToken: Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                ArtifactValidationResultCount: 1,
                HasArtifactValidationResults: true));
    }

    private static void AssertReadOnlyList<T>(IReadOnlyList<T> values, T sample)
    {
        Assert.False(values.GetType().IsArray);

        var collection = Assert.IsAssignableFrom<ICollection<T>>(values);

        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => collection.Add(sample));
    }

    private static void AssertSuppliedContentContract(
        ProcessDriverSuppliedEvidenceContent suppliedContent,
        ProcessDriverSuppliedEvidenceContentKind expectedKind,
        string expectedContentType,
        string expectedPayload)
    {
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HasExpectedEnvelope(
            suppliedContent,
            expectedKind,
            expectedContentType));
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HasAllowedSize(suppliedContent));
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HasValidContentHash(suppliedContent));
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HasEvidenceReferenceHashBinding(suppliedContent));
        Assert.True(ProcessDriverSuppliedEvidenceContentRules.HashMatchesSuppliedPayload(
            suppliedContent,
            expectedPayload));
    }

    private static ProcessReadOnlyPayloadIdentity CreateIdentity(string callerContext)
    {
        return new ProcessReadOnlyPayloadIdentity(
            ProcessRunId,
            StepRunId,
            ArtifactId,
            callerContext,
            RequestedAt);
    }

    private static ProcessDriverPermissionMode CreatePermissionMode(ProcessDriverCapabilityScopeKind lane)
    {
        return lane == ProcessDriverCapabilityScopeKind.RuntimeFactsRead
            ? ProcessDriverPermissionMode.ManagerReadonly
            : ProcessDriverPermissionMode.VerificationOnly;
    }

    private static ProcessDriverRedactionDescriptor NoRedaction { get; } = new(
        ProcessDriverRedactionStatus.None,
        [],
        ProcessDriverEvidencePolicy.ComputeSha256(string.Empty));

    private static Guid ProcessRunId { get; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static Guid StepRunId { get; } = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static Guid ArtifactId { get; } = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static DateTimeOffset RequestedAt { get; } = DateTimeOffset.Parse("2026-06-08T17:00:00Z");
}
