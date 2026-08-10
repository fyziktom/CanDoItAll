using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

/// <summary>
/// Contract tests for the SDK-free canonical context records introduced by the
/// MAF refactor: UI observation, context transition, conversation binding,
/// turn context reference, execution authority, and runtime state envelope.
/// </summary>
public sealed class CanonicalContextContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);

    [Fact]
    public void Identifier_value_objects_reject_empty_values()
    {
        Assert.Throws<ArgumentException>(() => new AgentUiObservationId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new AgentContextEpochId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new AgentTurnContextId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new AgentExecutionAuthorityId(Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AgentConversationBindingRevision(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AgentConversationBindingRevision(-1));
    }

    [Fact]
    public void Binding_revision_is_monotonic()
    {
        var revision = new AgentConversationBindingRevision(1);
        Assert.Equal(2, revision.Next().Value);
    }

    [Fact]
    public void Observation_snapshot_normalizes_and_validates_bounded_text()
    {
        var observation = CreateObservation(view: "  gantt  ");

        Assert.Equal("gantt", observation.View);
        Assert.Throws<ArgumentException>(() => CreateObservation(view: new string('v', 101)));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateObservation(publicationVersion: 0));
        Assert.Throws<ArgumentException>(() => new AgentUiObservationFact(
            new AgentChatContextContributorId("view"),
            0,
            new string('x', AgentChatContextFragment.MaximumContentLength + 1)));
    }

    [Fact]
    public void Transition_rejects_invalid_enums_and_oversized_summary()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AgentContextTransition(
            (AgentContextTransitionKind)999,
            AgentContextTransitionDecision.Kept,
            AgentContextEpochBehavior.KeepEpoch));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AgentContextTransition(
            AgentContextTransitionKind.ViewChanged,
            (AgentContextTransitionDecision)999,
            AgentContextEpochBehavior.KeepEpoch));
        Assert.Throws<ArgumentException>(() => new AgentContextTransition(
            AgentContextTransitionKind.ViewChanged,
            AgentContextTransitionDecision.Kept,
            AgentContextEpochBehavior.KeepEpoch,
            summary: new string('s', AgentContextTransition.MaximumSummaryLength + 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AgentContextTransition(
            AgentContextTransitionKind.ViewChanged,
            AgentContextTransitionDecision.Kept,
            AgentContextEpochBehavior.KeepEpoch,
            previousBindingRevision: 0));
    }

    [Fact]
    public void Conversation_binding_requires_an_identity_and_rejects_contradictory_detachment()
    {
        var now = DateTimeOffset.UtcNow;

        // Neither handle nor session.
        Assert.Throws<ArgumentException>(() => new AgentConversationContextBinding(
            AgentConversationContextMode.FollowCurrentSurface,
            AgentContextEpochId.Create(),
            new AgentConversationBindingRevision(1),
            now,
            now));

        // Detached cannot claim a followed source.
        Assert.Throws<ArgumentException>(() => new AgentConversationContextBinding(
            AgentConversationContextMode.Detached,
            AgentContextEpochId.Create(),
            new AgentConversationBindingRevision(1),
            now,
            now,
            handleId: new AgentChatHandleId(Guid.NewGuid()),
            sourceKind: new AgentChatContextSourceKind("project-structure")));

        // Empty chat session id rejected.
        Assert.Throws<ArgumentException>(() => new AgentConversationContextBinding(
            AgentConversationContextMode.FollowCurrentSurface,
            AgentContextEpochId.Create(),
            new AgentConversationBindingRevision(1),
            now,
            now,
            chatSessionId: Guid.Empty));

        var pending = AgentConversationContextBinding.CreatePendingFollow(
            new AgentChatHandleId(Guid.NewGuid()),
            now);
        Assert.False(pending.IsFollowing);
        Assert.Equal(AgentConversationContextMode.FollowCurrentSurface, pending.Mode);
        Assert.Equal(1, pending.Revision.Value);
    }

    [Fact]
    public void Authority_record_rejects_mutation_without_read()
    {
        Assert.Throws<ArgumentException>(() => CreateAuthority(readAllowed: false, mutationAllowed: true));

        var readOnly = CreateAuthority(readAllowed: true, mutationAllowed: false);
        Assert.True(readOnly.ReadAllowed);
        Assert.False(readOnly.MutationAllowed);
    }

    [Fact]
    public void Authority_record_bounds_and_normalizes_entries()
    {
        Assert.Throws<ArgumentException>(() => CreateAuthority(
            allowedOperations: [" "]));
        Assert.Throws<ArgumentException>(() => CreateAuthority(
            allowedOperations: [new string('o', AgentExecutionAuthorityRecord.MaximumEntryLength + 1)]));
        Assert.Throws<ArgumentException>(() => CreateAuthority(
            allowedOperations: [.. Enumerable.Repeat("op", AgentExecutionAuthorityRecord.MaximumAllowedEntryCount + 1)]));

        var authority = CreateAuthority(allowedOperations: [" project.read "]);
        Assert.Equal("project.read", Assert.Single(authority.AllowedOperations));
    }

    [Fact]
    public void Turn_context_reference_binds_digest_and_versions()
    {
        var reference = CreateTurnReference();
        Assert.Equal(AgentTurnContextReference.CurrentSchemaVersion, reference.SchemaVersion);

        Assert.Throws<ArgumentException>(() => CreateTurnReference(digest: " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateTurnReference(observationVersion: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AgentTurnContextReference(
            AgentTurnContextId.Create(),
            AgentContextEpochId.Create(),
            new AgentChatContextSourceKind("project-structure"),
            new AgentChatContextSourceId("project-1"),
            "project-structure",
            "canvas",
            observationVersion: 41,
            modelContextDigest: "ABCDEF",
            capturedAtUtc: DateTimeOffset.UtcNow,
            schemaVersion: 0));
    }

    [Fact]
    public void Turn_context_capture_requires_the_exact_observation_version()
    {
        var observation = CreateObservation(publicationVersion: 41);
        var mismatchedReference = CreateTurnReference(observationVersion: 42);

        Assert.Throws<ArgumentException>(() => new AgentTurnContextCapture(
            mismatchedReference,
            observation,
            AgentContextTransition.None,
            conversationBinding: null,
            CreateAuthority()));

        var capture = new AgentTurnContextCapture(
            CreateTurnReference(observationVersion: 41),
            observation,
            AgentContextTransition.None,
            conversationBinding: null,
            CreateAuthority());
        Assert.Equal(41, capture.Reference.ObservationVersion);
    }

    [Fact]
    public void Runtime_state_envelope_requires_adapter_identity_and_schema()
    {
        Assert.Throws<ArgumentException>(() => CreateEnvelope(adapterId: " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateEnvelope(schemaVersion: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeStateEnvelope(
            RuntimeStateAdapterIds.Maf,
            1,
            "1.0.0",
            Guid.NewGuid(),
            (ProviderTransportKind)999,
            "gpt-test",
            "toolset",
            "policy",
            DateTimeOffset.UtcNow,
            "{}"));

        var envelope = CreateEnvelope();
        Assert.True(envelope.IsCompatibleWith(RuntimeStateAdapterIds.Maf, 1, 1));
        Assert.False(envelope.IsCompatibleWith(RuntimeStateAdapterIds.Maf, 2, 3));
        Assert.False(envelope.IsCompatibleWith("other-adapter", 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => envelope.IsCompatibleWith(RuntimeStateAdapterIds.Maf, 2, 1));
    }

    [Fact]
    public void Contract_records_round_trip_through_json()
    {
        var observation = CreateObservation(publicationVersion: 41);
        var observationJson = JsonSerializer.Serialize(observation, JsonOptions);
        var observationRoundTrip = JsonSerializer.Deserialize<AgentUiObservationSnapshot>(observationJson, JsonOptions);
        Assert.NotNull(observationRoundTrip);
        Assert.Equal(observation.ObservationId, observationRoundTrip.ObservationId);
        Assert.Equal(observation.SourceKind, observationRoundTrip.SourceKind);
        Assert.Equal(observation.PublicationVersion, observationRoundTrip.PublicationVersion);
        Assert.Equal(observation.View, observationRoundTrip.View);

        var transition = new AgentContextTransition(
            AgentContextTransitionKind.ViewChanged,
            AgentContextTransitionDecision.Kept,
            AgentContextEpochBehavior.KeepEpoch,
            previousView: "canvas",
            currentView: "gantt",
            summary: "Canvas -> Gantt");
        var transitionRoundTrip = JsonSerializer.Deserialize<AgentContextTransition>(
            JsonSerializer.Serialize(transition, JsonOptions),
            JsonOptions);
        Assert.Equal(transition, transitionRoundTrip);

        var binding = AgentConversationContextBinding.CreatePendingFollow(
            new AgentChatHandleId(Guid.NewGuid()),
            new DateTimeOffset(2026, 8, 6, 10, 0, 0, TimeSpan.Zero));
        var bindingRoundTrip = JsonSerializer.Deserialize<AgentConversationContextBinding>(
            JsonSerializer.Serialize(binding, JsonOptions),
            JsonOptions);
        Assert.Equal(binding, bindingRoundTrip);

        var reference = CreateTurnReference();
        var referenceRoundTrip = JsonSerializer.Deserialize<AgentTurnContextReference>(
            JsonSerializer.Serialize(reference, JsonOptions),
            JsonOptions);
        Assert.Equal(reference, referenceRoundTrip);

        var authority = CreateAuthority(allowedOperations: ["project.read", "project.write"]);
        var authorityJson = JsonSerializer.Serialize(authority, JsonOptions);
        var authorityRoundTrip = JsonSerializer.Deserialize<AgentExecutionAuthorityRecord>(authorityJson, JsonOptions);
        Assert.NotNull(authorityRoundTrip);
        Assert.Equal(authority.AuthorityId, authorityRoundTrip.AuthorityId);
        Assert.Equal(authority.WorkspaceScope, authorityRoundTrip.WorkspaceScope);
        Assert.Equal(authority.AllowedOperations.AsEnumerable(), authorityRoundTrip.AllowedOperations.AsEnumerable());

        var envelope = CreateEnvelope();
        var envelopeRoundTrip = JsonSerializer.Deserialize<RuntimeStateEnvelope>(
            JsonSerializer.Serialize(envelope, JsonOptions),
            JsonOptions);
        Assert.Equal(envelope, envelopeRoundTrip);
    }

    [Fact]
    public void Old_json_without_optional_v2_fields_deserializes_to_safe_defaults()
    {
        // Simulates a persisted turn reference written before optional fields
        // (observationId) and consumers running a newer reader.
        var minimalReferenceJson = $$"""
            {
              "TurnContextId": { "Value": "{{Guid.NewGuid():D}}" },
              "ContextEpochId": { "Value": "{{Guid.NewGuid():D}}" },
              "SourceKind": { "Value": "project-structure" },
              "SourceId": { "Value": "project-1" },
              "Surface": "project-structure",
              "View": "canvas",
              "ObservationVersion": 41,
              "ModelContextDigest": "ABCDEF",
              "CapturedAtUtc": "2026-08-06T10:00:00+00:00"
            }
            """;

        var reference = JsonSerializer.Deserialize<AgentTurnContextReference>(minimalReferenceJson, JsonOptions);
        Assert.NotNull(reference);
        Assert.Null(reference.ObservationId);
        Assert.Equal(AgentTurnContextReference.CurrentSchemaVersion, reference.SchemaVersion);

        var minimalBindingJson = $$"""
            {
              "Mode": 0,
              "ContextEpochId": { "Value": "{{Guid.NewGuid():D}}" },
              "Revision": { "Value": 3 },
              "AdoptedAtUtc": "2026-08-06T10:00:00+00:00",
              "UpdatedAtUtc": "2026-08-06T10:05:00+00:00",
              "HandleId": { "Value": "{{Guid.NewGuid():D}}" }
            }
            """;
        var binding = JsonSerializer.Deserialize<AgentConversationContextBinding>(minimalBindingJson, JsonOptions);
        Assert.NotNull(binding);
        Assert.Null(binding.ChatSessionId);
        Assert.Null(binding.SourceKind);
        Assert.Equal(string.Empty, binding.DisplayName);
        Assert.Equal(string.Empty, binding.LastTurnContextDigest);
    }

    [Fact]
    public void Durable_records_cannot_carry_opaque_attachment_payloads()
    {
        // Durable context/authority/envelope records must never embed the opaque
        // attachment payload interface; references carry fingerprints only.
        var durableRecordTypes = new[]
        {
            typeof(AgentUiObservationSnapshot),
            typeof(AgentUiObservationAttachmentReference),
            typeof(AgentContextTransition),
            typeof(AgentConversationContextBinding),
            typeof(AgentTurnContextReference),
            typeof(AgentExecutionAuthorityRecord),
            typeof(RuntimeStateEnvelope)
        };

        foreach (var recordType in durableRecordTypes)
        {
            foreach (var property in recordType.GetProperties())
            {
                var propertyType = property.PropertyType;
                var elementTypes = propertyType.IsGenericType
                    ? propertyType.GetGenericArguments()
                    : [propertyType];
                foreach (var candidate in elementTypes.Append(propertyType))
                {
                    Assert.False(
                        typeof(IAgentChatContextAttachment).IsAssignableFrom(candidate),
                        $"{recordType.Name}.{property.Name} must not carry an opaque attachment payload.");
                    Assert.False(
                        candidate == typeof(object),
                        $"{recordType.Name}.{property.Name} must not use an untyped object property.");
                }
            }
        }
    }

    [Fact]
    public void New_contract_files_stay_sdk_and_module_free()
    {
        var root = FindRepoRoot();
        var contractFiles = new[]
        {
            @"src\MAF\Common\CanDoItAll.AgentFramework.Models\Context\AgentUiObservationModels.cs",
            @"src\MAF\Common\CanDoItAll.AgentFramework.Models\Context\AgentContextTransitionModels.cs",
            @"src\MAF\Common\CanDoItAll.AgentFramework.Models\Context\AgentTurnContextModels.cs",
            @"src\MAF\Common\CanDoItAll.AgentFramework.Models\Conversations\AgentConversationContextModels.cs",
            @"src\MAF\Common\CanDoItAll.AgentFramework.Models\Execution\AgentExecutionAuthorityModels.cs",
            @"src\MAF\Common\CanDoItAll.AgentFramework.Models\Runtime\RuntimeStateEnvelopeModels.cs"
        };

        var forbiddenTokens = new[]
        {
            "Microsoft.Agents.AI",
            "Microsoft.Extensions.AI",
            "OpenAI.",
            "Azure.AI",
            "CanDoItAll.Modules.",
            "CanDoItAll.AgentFramework.Core",
            "CanDoItAll.AgentFramework.Maf",
            "Microsoft.AspNetCore",
            "IServiceProvider"
        };

        foreach (var relativePath in contractFiles)
        {
            var fullPath = TestRepositoryPath.Resolve(root, relativePath);
            Assert.True(File.Exists(fullPath), $"Missing contract file: {relativePath}");
            var text = File.ReadAllText(fullPath);
            foreach (var token in forbiddenTokens)
            {
                Assert.DoesNotContain(token, text, StringComparison.Ordinal);
            }
        }
    }

    private static AgentUiObservationSnapshot CreateObservation(
        string view = "canvas",
        long publicationVersion = 41)
        => new(
            AgentUiObservationId.Create(),
            new AgentChatContextSourceKind("project-structure"),
            new AgentChatContextSourceId("project-1"),
            "Project X",
            "project-structure",
            view,
            publicationVersion,
            AgentUiObservationCompleteness.Ready,
            new DateTimeOffset(2026, 8, 6, 10, 0, 0, TimeSpan.Zero),
            expectedWorkspaceScope: WorkspaceScopeDescriptor.Project("project-1"),
            visibleFacts:
            [
                new AgentUiObservationFact(
                    new AgentChatContextContributorId("view"),
                    0,
                    $"Current view: {view.Trim()}")
            ]);

    private static AgentTurnContextReference CreateTurnReference(
        long observationVersion = 41,
        string digest = "ABCDEF0123456789")
        => new(
            AgentTurnContextId.Create(),
            AgentContextEpochId.Create(),
            new AgentChatContextSourceKind("project-structure"),
            new AgentChatContextSourceId("project-1"),
            "project-structure",
            "canvas",
            observationVersion,
            digest,
            new DateTimeOffset(2026, 8, 6, 10, 0, 0, TimeSpan.Zero));

    private static AgentExecutionAuthorityRecord CreateAuthority(
        bool readAllowed = true,
        bool mutationAllowed = false,
        IReadOnlyList<string>? allowedOperations = null)
        => new(
            AgentExecutionAuthorityId.Create(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DatabaseProfileGeneration(1),
            WorkspaceScopeDescriptor.Project("project-1"),
            readAllowed,
            mutationAllowed,
            "v1",
            "policy-fingerprint",
            new DateTimeOffset(2026, 8, 6, 10, 0, 0, TimeSpan.Zero),
            allowedOperations: allowedOperations);

    private static RuntimeStateEnvelope CreateEnvelope(
        string adapterId = RuntimeStateAdapterIds.Maf,
        int schemaVersion = 1)
        => new(
            adapterId,
            schemaVersion,
            "1.0.0",
            Guid.NewGuid(),
            ProviderTransportKind.Responses,
            "gpt-test",
            "toolset-fingerprint",
            "context-policy-fingerprint",
            new DateTimeOffset(2026, 8, 6, 10, 0, 0, TimeSpan.Zero),
            "{\"maf\":true}");

    private static string FindRepoRoot()
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
}
