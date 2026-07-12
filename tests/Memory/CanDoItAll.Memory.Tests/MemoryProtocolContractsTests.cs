using System.Text.Json;
using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Tests;

public sealed class MemoryProtocolContractsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void MP001_Query_envelope_round_trips_structured_context()
    {
        var envelope = MemoryOperationEnvelope.Create(
            MemoryProviderInstanceId.Parse("provider.programming"),
            MemoryOperationKind.ContextQuery,
            MemoryRequesterContext.Agent(
                requesterId: "user-42",
                reason: "answer a project question",
                agentId: "agent-dev",
                agentRole: "developer",
                sessionId: "session-7"),
            new MemoryWorkspaceContext(
                WorkspaceId: "workspace-1",
                WorkspaceName: "CanDoItAll",
                CustomerId: "customer-1",
                Domain: "software",
                Tags: ["dotnet", "memory"]),
            new MemoryExecutionContext(
                ProjectId: "project-1",
                ProjectName: "Memory extraction",
                ProcessId: "process-1",
                ProcessStepId: "step-3",
                ProcessStepName: "design",
                WorkflowId: "workflow-1",
                WorkflowNodeId: "node-query",
                ArtifactIds: ["artifact-1"]),
            new MemoryPolicyContext(
                MemorySensitivity.Internal,
                MemoryRetentionPolicy.Default,
                AllowedSourceScopes: [MemorySourceScope.Project, MemorySourceScope.Workflow],
                ApprovalPosture: MemoryApprovalPosture.RequireApproval,
                RedactionLevel: MemoryRedactionLevel.SummaryOnly),
            MemoryBudget.Default,
            new MemoryContextQueryRequest(
                Query: "How should provider selection work?",
                RequestedCapabilities: [MemoryCapabilityId.Parse("context.query.sync")],
                SourceProvenance: new MemorySourceProvenance(
                    SourceSnapshotId: MemorySourceSnapshotId.Parse("snapshot-project-1"),
                    SourceModule: "Workbench",
                    SourceRecordIds: ["structure-node-12"],
                    Citations: ["repo://src/Modules/CanDoItAll.Modules.Workbench"])),
            MemoryExtensionData.From(("host.candoitall.trace", JsonDocument.Parse("""{"value":"kept"}""").RootElement.Clone())));

        var roundTripped = RoundTrip(envelope);

        Assert.Equal(MemoryProtocolVersion.Current, roundTripped.MemoryProtocolVersion);
        Assert.Equal("provider.programming", roundTripped.ProviderInstanceId.Value);
        Assert.Equal(MemoryOperationKind.ContextQuery, roundTripped.OperationKind);
        Assert.Equal("agent-dev", roundTripped.RequestedBy.AgentId);
        Assert.Equal("Memory extraction", roundTripped.ExecutionContext.ProjectName);
        Assert.Equal("step-3", roundTripped.ExecutionContext.ProcessStepId);
        Assert.Equal(MemoryApprovalPosture.RequireApproval, roundTripped.PolicyContext.ApprovalPosture);
        Assert.Equal("How should provider selection work?", roundTripped.Payload.Query);
        Assert.Contains(MemoryCapabilityId.Parse("context.query.sync"), roundTripped.Payload.RequestedCapabilities);
        Assert.True(roundTripped.ExtensionData.Values.ContainsKey("host.candoitall.trace"));
    }

    [Fact]
    public void MP002_All_required_envelopes_round_trip()
    {
        var sourceRequest = CreateEnvelope(
            MemoryOperationKind.SourceRequest,
            new MemorySourceRequest(
                SourceRequestId: MemorySourceRequestId.Parse("source-request-1"),
                RequestedScopes: [MemorySourceScope.Project, MemorySourceScope.Process],
                Purpose: "hydrate context pack",
                ProviderVisibleReason: "provider asked for project facts"));

        var ingestion = CreateEnvelope(
            MemoryOperationKind.Ingestion,
            new MemoryIngestionRequest(
                SourceSnapshotId: MemorySourceSnapshotId.Parse("snapshot-2"),
                SourceKind: MemorySourceKind.Project,
                Payload: MemoryPayload.FromText("project facts"),
                RequestedCapabilities: [MemoryCapabilityId.Parse("ingestion.snapshot")]));

        var feedback = CreateEnvelope(
            MemoryOperationKind.Feedback,
            new MemoryFeedbackRequest(
                ContextPackId: MemoryContextPackId.New(),
                Outcome: MemoryFeedbackOutcome.Useful,
                Comment: "helped workflow",
                EconomicImpact: new MemoryEconomicImpact("USD", 1200m)));

        var acknowledge = CreateEnvelope(
            MemoryOperationKind.EventAcknowledge,
            new MemoryEventAcknowledgeRequest(
                EventId: MemoryProviderEventId.New(),
                Accepted: true,
                Reason: "accepted for verification"));

        var status = CreateEnvelope(
            MemoryOperationKind.OperationStatus,
            new MemoryOperationStatusRequest(
                OperationId: MemoryOperationId.New()));

        Assert.Equal(MemorySourceScope.Process, RoundTrip(sourceRequest).Payload.RequestedScopes[1]);
        Assert.Equal(MemorySourceKind.Project, RoundTrip(ingestion).Payload.SourceKind);
        Assert.Equal(MemoryFeedbackOutcome.Useful, RoundTrip(feedback).Payload.Outcome);
        Assert.True(RoundTrip(acknowledge).Payload.Accepted);
        Assert.Equal(status.Payload.OperationId, RoundTrip(status).Payload.OperationId);
    }

    [Fact]
    public void MP003_Provider_response_models_round_trip()
    {
        var contextPack = new MemoryContextPack(
            MemoryContextPackId.New(),
            Summary: "Use provider profiles for selection.",
            Sections:
            [
                new MemoryContextSection(
                    Title: "Provider selection",
                    Text: "Programming provider is selected for developer agents.",
                    Citations: [new MemoryCitation("repo://src/Memory", "memory contracts")],
                    Confidence: 0.87m)
            ],
            Warnings: [new MemoryWarning(MemoryWarningKind.PolicyLimited, "Project-only sources were allowed.")],
            ProviderConfidence: 0.81m,
            FeedbackHandle: MemoryFeedbackHandle.Parse("feedback-1"));

        var accepted = new MemoryOperationAccepted(
            OperationId: MemoryOperationId.New(),
            StatusPath: "/memory/operations/1",
            ExpiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(5),
            PollAfter: TimeSpan.FromSeconds(10),
            CallbackAvailable: true);

        var result = new MemoryOperationResult(
            accepted.OperationId,
            MemoryOperationStatus.Succeeded,
            Output: MemoryPayload.FromJson(JsonDocument.Parse("""{"done":true}""").RootElement.Clone()),
            Warnings: [],
            FeedbackHandles: [MemoryFeedbackHandle.Parse("feedback-2")],
            SourceRefs: ["repo://src/Memory"]);

        var providerEvent = new MemoryProviderEvent(
            EventId: MemoryProviderEventId.New(),
            EventKind: MemoryProviderEventKind.VerificationRequest,
            CorrelationId: MemoryCorrelationId.New(),
            CausationId: MemoryCausationId.New(),
            Message: "Verify claim",
            Payload: MemoryPayload.FromText("claim"));

        var health = new MemoryProviderHealth(
            MemoryProviderHealthStatus.Degraded,
            LastErrorCategory: "timeout",
            CapabilitySnapshot: new MemoryProviderManifest(
                ProviderKind: MemoryProviderKind.Parse("memory.http"),
                ProtocolVersion: MemoryProtocolVersion.Current,
                Capabilities:
                [
                    new MemoryCapabilityDescriptor(
                        MemoryCapabilityId.Parse("context.query.async"),
                        Version: "1.0",
                        Supported: true)
                ],
                InteractionSupport: new MemoryProviderInteractionSupport(
                    SupportsSynchronousQueries: true,
                    SupportsAsynchronousOperations: true,
                    SupportsSourceRequests: true,
                    SupportsFeedback: true,
                    SupportsProviderEvents: true),
                UiSurfaces:
                [
                    new MemoryProviderUiSurface(
                        MemoryProviderUiSurfaceKind.Iframe,
                        Name: "Provider Console",
                        ComponentKey: null,
                        UrlSettingKey: "Memory:Providers:provider:ConsoleUrl",
                        CapabilityId: MemoryCapabilityId.Parse("ui.iframe"))
                ],
                Limits: new MemoryProviderLimits(
                    maxContextSections: 8,
                    maxSourceItems: 50,
                    maxInFlightOperations: 2,
                    operationTimeout: TimeSpan.FromMinutes(5)),
                Extensions: MemoryExtensionData.From(
                    ("provider.vendor.latency", JsonDocument.Parse("""{"p95Milliseconds":2500}""").RootElement.Clone()))));

        Assert.Equal("Use provider profiles for selection.", RoundTrip(contextPack).Summary);
        Assert.True(RoundTrip(accepted).CallbackAvailable);
        Assert.Equal(MemoryOperationStatus.Succeeded, RoundTrip(result).Status);
        Assert.Equal(MemoryProviderEventKind.VerificationRequest, RoundTrip(providerEvent).EventKind);
        Assert.Equal(MemoryProviderHealthStatus.Degraded, RoundTrip(health).Status);
        Assert.Contains(RoundTrip(health).CapabilitySnapshot.Capabilities, capability => capability.Id.Value == "context.query.async");
        Assert.True(RoundTrip(health).CapabilitySnapshot.InteractionSupport.SupportsProviderEvents);
        Assert.Equal(MemoryProviderUiSurfaceKind.Iframe, RoundTrip(health).CapabilitySnapshot.UiSurfaces[0].Kind);
        Assert.Equal(2, RoundTrip(health).CapabilitySnapshot.Limits.MaxInFlightOperations);
        Assert.True(RoundTrip(health).CapabilitySnapshot.Extensions.Values.ContainsKey("provider.vendor.latency"));
    }

    [Fact]
    public void MP004_Invalid_identifiers_and_protocol_versions_fail_predictably()
    {
        var missingCorrelation = Assert.Throws<ArgumentException>(() => new MemoryCorrelationId(Guid.Empty));
        Assert.Contains("must not be empty", missingCorrelation.Message);

        var unsupportedVersion = Assert.Throws<NotSupportedException>(() => new MemoryProtocolVersion("memory-protocol.v0"));
        Assert.Contains("Unsupported memory protocol version", unsupportedVersion.Message);

        var invalidCapability = Assert.Throws<ArgumentException>(() => MemoryCapabilityId.Parse("native Cognitive Memory"));
        Assert.Contains("Capability ids must use dotted lowercase tokens", invalidCapability.Message);

        var controlCharacterProvider = Assert.Throws<ArgumentException>(() =>
            MemoryProviderInstanceId.Parse("provider.safe\r\nprovider.injected"));
        Assert.Contains("control characters", controlCharacterProvider.Message, StringComparison.Ordinal);

        var oversizedProvider = Assert.Throws<ArgumentException>(() =>
            MemoryProviderInstanceId.Parse(new string('p', 257)));
        Assert.Contains("at most 256", oversizedProvider.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MP005_Native_extension_namespace_is_stored_without_generic_branching()
    {
        var extensionData = MemoryExtensionData.From(
            ("native.cognitiveMemory.probe", JsonDocument.Parse("""{"enabled":true}""").RootElement.Clone()),
            ("provider.vendor.score", JsonDocument.Parse("""{"value":0.91}""").RootElement.Clone()));

        var envelope = CreateEnvelope(
            MemoryOperationKind.ContextQuery,
            new MemoryContextQueryRequest(
                Query: "probe",
                RequestedCapabilities: [MemoryCapabilityId.Parse("native.probe")],
                SourceProvenance: MemorySourceProvenance.None),
            extensionData);

        var roundTripped = RoundTrip(envelope);

        Assert.True(roundTripped.ExtensionData.Values.ContainsKey("native.cognitiveMemory.probe"));
        Assert.True(roundTripped.ExtensionData.Values.ContainsKey("provider.vendor.score"));
        Assert.Equal(
            roundTripped.ExtensionData.Values["native.cognitiveMemory.probe"].GetRawText(),
            envelope.ExtensionData.Values["native.cognitiveMemory.probe"].GetRawText());
    }

    [Fact]
    public void MP006_Unqualified_extension_namespaces_are_rejected()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            MemoryExtensionData.From(("random.native.flag", JsonDocument.Parse("""{"value":true}""").RootElement.Clone())));

        Assert.Contains("Extension keys must start with one of the reserved namespaces", exception.Message);
    }

    private static MemoryOperationEnvelope<TPayload> CreateEnvelope<TPayload>(
        MemoryOperationKind operationKind,
        TPayload payload,
        MemoryExtensionData? extensionData = null) =>
        MemoryOperationEnvelope.Create(
            MemoryProviderInstanceId.Parse("provider.default"),
            operationKind,
            MemoryRequesterContext.User("user-1", "unit test"),
            MemoryWorkspaceContext.None,
            MemoryExecutionContext.None,
            MemoryPolicyContext.InternalDefault,
            MemoryBudget.Default,
            payload,
            extensionData ?? MemoryExtensionData.Empty);

    private static T RoundTrip<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        var result = JsonSerializer.Deserialize<T>(json, JsonOptions);
        Assert.NotNull(result);
        return result;
    }
}
