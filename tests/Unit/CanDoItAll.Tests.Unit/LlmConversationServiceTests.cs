using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.Conversations;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.LlmChats;

/// <summary>
/// SB15: behavior of the ordinary LLM conversation application service. The application transcript is
/// canonical, turns are atomic (admit + complete via optimistic revision CAS), provider/model changes are
/// an explicit policy decision, and the service never touches agents, tools, memory, workspace authority,
/// approvals, or process semantics.
/// </summary>
public sealed class LlmConversationServiceTests
{
    [Fact]
    public async Task StartAsync_uses_a_caller_supplied_conversation_id()
    {
        var harness = ConversationHarness.Create();
        var provider = CreateProviderProfile();
        var conversationId = Guid.NewGuid();

        var conversation = await harness.Service.StartAsync(
            new LlmConversationStartRequest(provider) { ConversationId = conversationId });

        Assert.Equal(conversationId, conversation.ConversationId);
    }

    [Fact]
    public async Task SendAsync_uses_a_caller_supplied_turn_id()
    {
        var harness = ConversationHarness.Create();
        var provider = CreateProviderProfile();
        var conversation = await harness.Service.StartAsync(new LlmConversationStartRequest(provider));
        var turnId = Guid.NewGuid();

        var result = await harness.Service.SendAsync(new LlmConversationTurnRequest(
            conversation.ConversationId,
            conversation.TranscriptRevision,
            "Question",
            provider) { TurnId = turnId });

        Assert.Equal(turnId, result.UserEntry.TurnId);
        Assert.Equal(turnId, result.AssistantEntry.TurnId);
    }

    [Fact]
    public async Task StartAsync_creates_a_conversation_with_provider_snapshot_and_optional_system_entry()
    {
        var harness = ConversationHarness.Create();
        var provider = CreateProviderProfile();

        var conversation = await harness.Service.StartAsync(new LlmConversationStartRequest(
            provider,
            model: "",
            title: "  Planning chat  ",
            systemPrompt: "Answer briefly."));

        Assert.NotEqual(Guid.Empty, conversation.ConversationId);
        Assert.Equal("Planning chat", conversation.Title);
        Assert.Equal(provider.Id, conversation.Provider.ProviderId);
        Assert.Equal(provider.Name, conversation.Provider.ProviderName);
        Assert.Equal(provider.Kind, conversation.Provider.ProviderKind);
        Assert.Equal(provider.DefaultModel, conversation.Provider.Model);
        var systemEntry = Assert.Single(conversation.Entries);
        Assert.Equal(LlmMessageRole.System, systemEntry.Role);
        Assert.Equal("Answer briefly.", systemEntry.Text);
        Assert.Equal(1, conversation.TranscriptRevision);
        Assert.Null(conversation.ActiveTurn);
        Assert.Null(conversation.AccelerationState);
    }

    [Fact]
    public async Task SendAsync_appends_user_and_assistant_entries_in_order_across_multiple_turns()
    {
        var harness = ConversationHarness.Create(respond: (request, _) =>
            Task.FromResult(new LlmInvocationResult(
                request.Model,
                $"reply to: {request.Messages[^1].Text}",
                new LlmUsage(10, 5, 2))));
        var provider = CreateProviderProfile();
        var conversation = await harness.Service.StartAsync(new LlmConversationStartRequest(provider));

        var firstTurn = await harness.Service.SendAsync(new LlmConversationTurnRequest(
            conversation.ConversationId,
            conversation.TranscriptRevision,
            "First question",
            provider));
        var secondTurn = await harness.Service.SendAsync(new LlmConversationTurnRequest(
            conversation.ConversationId,
            firstTurn.Conversation.TranscriptRevision,
            "Second question",
            provider));

        var entries = secondTurn.Conversation.Entries;
        Assert.Equal(4, entries.Length);
        Assert.Collection(
            entries,
            entry => { Assert.Equal(LlmMessageRole.User, entry.Role); Assert.Equal("First question", entry.Text); },
            entry => { Assert.Equal(LlmMessageRole.Assistant, entry.Role); Assert.Equal("reply to: First question", entry.Text); },
            entry => { Assert.Equal(LlmMessageRole.User, entry.Role); Assert.Equal("Second question", entry.Text); },
            entry => { Assert.Equal(LlmMessageRole.Assistant, entry.Role); Assert.Equal("reply to: Second question", entry.Text); });
        Assert.Equal(entries[0].TurnId, entries[1].TurnId);
        Assert.Equal(entries[2].TurnId, entries[3].TurnId);
        Assert.NotEqual(entries[0].TurnId, entries[2].TurnId);
        Assert.True(secondTurn.Conversation.TranscriptRevision > firstTurn.Conversation.TranscriptRevision);
        Assert.Null(secondTurn.Conversation.ActiveTurn);
    }

    [Fact]
    public async Task SendAsync_persists_usage_on_assistant_entries_and_aggregates_totals()
    {
        var harness = ConversationHarness.Create(respond: (request, _) =>
            Task.FromResult(new LlmInvocationResult(request.Model, "ok", new LlmUsage(10, 5, 2))));
        var provider = CreateProviderProfile();
        var conversation = await harness.Service.StartAsync(new LlmConversationStartRequest(provider));

        var firstTurn = await harness.Service.SendAsync(new LlmConversationTurnRequest(
            conversation.ConversationId, conversation.TranscriptRevision, "Q1", provider));
        var secondTurn = await harness.Service.SendAsync(new LlmConversationTurnRequest(
            conversation.ConversationId, firstTurn.Conversation.TranscriptRevision, "Q2", provider));

        Assert.Equal(new LlmUsage(10, 5, 2), firstTurn.AssistantEntry.Usage);
        Assert.Null(firstTurn.UserEntry.Usage);
        var total = secondTurn.Conversation.ComputeTotalUsage();
        Assert.Equal(new LlmUsage(20, 10, 4), total);

        var reloaded = await harness.Service.TryGetAsync(conversation.ConversationId);
        Assert.NotNull(reloaded);
        Assert.Equal(new LlmUsage(20, 10, 4), reloaded!.ComputeTotalUsage());
    }

    [Fact]
    public async Task SendAsync_rejects_a_second_turn_while_one_is_in_flight()
    {
        var portEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePort = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var harness = ConversationHarness.Create(respond: async (request, _) =>
        {
            portEntered.TrySetResult();
            await releasePort.Task;
            return new LlmInvocationResult(request.Model, "slow reply", new LlmUsage(1, 1));
        });
        var provider = CreateProviderProfile();
        var conversation = await harness.Service.StartAsync(new LlmConversationStartRequest(provider));

        var inFlight = harness.Service.SendAsync(new LlmConversationTurnRequest(
            conversation.ConversationId, conversation.TranscriptRevision, "Slow question", provider));
        await portEntered.Task;

        var admitted = await harness.Service.TryGetAsync(conversation.ConversationId);
        var exception = await Assert.ThrowsAsync<LlmConversationException>(() => harness.Service.SendAsync(
            new LlmConversationTurnRequest(
                conversation.ConversationId, admitted!.TranscriptRevision, "Interloper", provider)));
        Assert.Equal(LlmConversationFailureKind.TurnAlreadyActive, exception.Kind);

        releasePort.TrySetResult();
        var completed = await inFlight;
        Assert.Equal(2, completed.Conversation.Entries.Length);
        Assert.Equal("Slow question", completed.Conversation.Entries[0].Text);
        Assert.Equal("slow reply", completed.Conversation.Entries[1].Text);
    }

    [Fact]
    public async Task RenameAsync_rejects_mutation_while_a_turn_is_in_flight()
    {
        var portEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePort = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var harness = ConversationHarness.Create(respond: async (request, _) =>
        {
            portEntered.TrySetResult();
            await releasePort.Task;
            return new LlmInvocationResult(request.Model, "slow reply", new LlmUsage(1, 1));
        });
        var provider = CreateProviderProfile();
        var conversation = await harness.Service.StartAsync(new LlmConversationStartRequest(provider));
        var inFlight = harness.Service.SendAsync(new LlmConversationTurnRequest(
            conversation.ConversationId,
            conversation.TranscriptRevision,
            "Slow question",
            provider));
        await portEntered.Task;

        try
        {
            var admitted = Assert.IsType<LlmConversationDocument>(
                await harness.Service.TryGetAsync(conversation.ConversationId));
            var exception = await Assert.ThrowsAsync<LlmConversationException>(() => harness.Service.RenameAsync(
                conversation.ConversationId,
                "Unsafe rename",
                admitted.TranscriptRevision));

            Assert.Equal(LlmConversationFailureKind.TurnAlreadyActive, exception.Kind);
            var unchanged = Assert.IsType<LlmConversationDocument>(
                await harness.Service.TryGetAsync(conversation.ConversationId));
            Assert.Equal(admitted.Title, unchanged.Title);
            Assert.Equal(admitted.TranscriptRevision, unchanged.TranscriptRevision);
        }
        finally
        {
            releasePort.TrySetResult();
            try
            {
                await inFlight;
            }
            catch (LlmConversationException)
            {
            }
        }
    }

    [Fact]
    public async Task SendAsync_fails_typed_on_stale_transcript_revision_and_leaves_transcript_unchanged()
    {
        var harness = ConversationHarness.Create();
        var provider = CreateProviderProfile();
        var conversation = await harness.Service.StartAsync(new LlmConversationStartRequest(provider));

        var exception = await Assert.ThrowsAsync<LlmConversationException>(() => harness.Service.SendAsync(
            new LlmConversationTurnRequest(
                conversation.ConversationId, conversation.TranscriptRevision + 7, "Stale view", provider)));

        Assert.Equal(LlmConversationFailureKind.RevisionConflict, exception.Kind);
        var reloaded = await harness.Service.TryGetAsync(conversation.ConversationId);
        Assert.Empty(reloaded!.Entries);
        Assert.Equal(conversation.TranscriptRevision, reloaded.TranscriptRevision);
        Assert.Equal(0, harness.Port.CallCount);
    }

    [Fact]
    public async Task SendAsync_with_forbid_policy_rejects_provider_model_change_without_touching_the_transcript()
    {
        var harness = ConversationHarness.Create();
        var providerA = CreateProviderProfile();
        var providerB = CreateProviderProfile(name: "Other provider", model: "other-model");
        var conversation = await harness.Service.StartAsync(new LlmConversationStartRequest(providerA));

        var exception = await Assert.ThrowsAsync<LlmConversationException>(() => harness.Service.SendAsync(
            new LlmConversationTurnRequest(
                conversation.ConversationId, conversation.TranscriptRevision, "Hello", providerB)));

        Assert.Equal(LlmConversationFailureKind.ProviderModelMismatch, exception.Kind);
        var reloaded = await harness.Service.TryGetAsync(conversation.ConversationId);
        Assert.Empty(reloaded!.Entries);
        Assert.Equal(conversation.TranscriptRevision, reloaded.TranscriptRevision);
        Assert.Equal(providerA.Id, reloaded.Provider.ProviderId);
        Assert.Equal(0, harness.Port.CallCount);
    }

    [Fact]
    public async Task SendAsync_with_adopt_policy_updates_the_provider_snapshot_and_clears_acceleration_state()
    {
        var harness = ConversationHarness.Create();
        var providerA = CreateProviderProfile();
        var providerB = CreateProviderProfile(name: "Other provider", model: "other-model");
        var conversation = await harness.Service.StartAsync(new LlmConversationStartRequest(providerA));

        var seeded = conversation with
        {
            AccelerationState = new LlmConversationAccelerationEnvelope(
                "provider-conversation-id", providerA.Name, providerA.DefaultModel, """{"id":"abc"}"""),
            TranscriptRevision = conversation.TranscriptRevision + 1
        };
        await harness.Store.ReplaceAsync(seeded, conversation.TranscriptRevision);

        var turn = await harness.Service.SendAsync(new LlmConversationTurnRequest(
            conversation.ConversationId,
            seeded.TranscriptRevision,
            "Switch provider",
            providerB,
            providerChangePolicy: LlmConversationProviderChangePolicy.Adopt));

        Assert.Equal(providerB.Id, turn.Conversation.Provider.ProviderId);
        Assert.Equal("other-model", turn.Conversation.Provider.Model);
        Assert.Null(turn.Conversation.AccelerationState);
        Assert.Equal(providerB.Id, harness.Port.LastRequest!.Provider.Id);
        Assert.Equal("other-model", harness.Port.LastRequest.Model);
    }

    [Fact]
    public async Task SendAsync_with_adopt_policy_restores_provider_and_acceleration_when_the_port_fails()
    {
        var harness = ConversationHarness.Create(respond: (request, _) =>
            throw new LlmInvocationException(
                LlmInvocationFailureKind.ProviderFailure,
                request.Provider.Name,
                request.Model,
                request.CorrelationId));
        var providerA = CreateProviderProfile();
        var providerB = CreateProviderProfile(name: "Other provider", model: "other-model");
        var conversation = await harness.Service.StartAsync(new LlmConversationStartRequest(providerA));
        var acceleration = new LlmConversationAccelerationEnvelope(
            "provider-conversation-id",
            providerA.Name,
            providerA.DefaultModel,
            """{"id":"abc"}""");
        var seeded = conversation with
        {
            AccelerationState = acceleration,
            TranscriptRevision = conversation.TranscriptRevision + 1
        };
        await harness.Store.ReplaceAsync(seeded, conversation.TranscriptRevision);

        await Assert.ThrowsAsync<LlmInvocationException>(() => harness.Service.SendAsync(
            new LlmConversationTurnRequest(
                conversation.ConversationId,
                seeded.TranscriptRevision,
                "Switch provider",
                providerB,
                providerChangePolicy: LlmConversationProviderChangePolicy.Adopt)));

        var restored = Assert.IsType<LlmConversationDocument>(
            await harness.Service.TryGetAsync(conversation.ConversationId));
        Assert.Equal(providerA.Id, restored.Provider.ProviderId);
        Assert.Equal(providerA.DefaultModel, restored.Provider.Model);
        Assert.Equal(acceleration, restored.AccelerationState);
        Assert.Empty(restored.Entries);
        Assert.Null(restored.ActiveTurn);
    }

    [Fact]
    public async Task SendAsync_with_adopt_policy_restores_provider_and_acceleration_when_canceled()
    {
        using var cancellation = new CancellationTokenSource();
        var harness = ConversationHarness.Create(respond: (_, cancellationToken) =>
        {
            cancellation.Cancel();
            return Task.FromCanceled<LlmInvocationResult>(cancellationToken);
        });
        var providerA = CreateProviderProfile();
        var providerB = CreateProviderProfile(name: "Other provider", model: "other-model");
        var conversation = await harness.Service.StartAsync(new LlmConversationStartRequest(providerA));
        var acceleration = new LlmConversationAccelerationEnvelope(
            "provider-conversation-id",
            providerA.Name,
            providerA.DefaultModel,
            """{"id":"cancel"}""");
        var seeded = conversation with
        {
            AccelerationState = acceleration,
            TranscriptRevision = conversation.TranscriptRevision + 1
        };
        await harness.Store.ReplaceAsync(seeded, conversation.TranscriptRevision);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => harness.Service.SendAsync(
            new LlmConversationTurnRequest(
                conversation.ConversationId,
                seeded.TranscriptRevision,
                "Cancel provider switch",
                providerB,
                providerChangePolicy: LlmConversationProviderChangePolicy.Adopt),
            cancellation.Token));

        var restored = Assert.IsType<LlmConversationDocument>(
            await harness.Service.TryGetAsync(conversation.ConversationId));
        Assert.Equal(providerA.Id, restored.Provider.ProviderId);
        Assert.Equal(acceleration, restored.AccelerationState);
        Assert.Empty(restored.Entries);
        Assert.Null(restored.ActiveTurn);
        Assert.True(restored.TranscriptRevision > seeded.TranscriptRevision);
    }

    [Fact]
    public async Task AbandonActiveTurnAsync_restores_durable_adoption_compensation()
    {
        using var cancellation = new CancellationTokenSource();
        var portEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var harness = ConversationHarness.Create(respond: async (_, cancellationToken) =>
        {
            portEntered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The canceled provider call must not complete.");
        });
        var providerA = CreateProviderProfile();
        var providerB = CreateProviderProfile(name: "Other provider", model: "other-model");
        var conversation = await harness.Service.StartAsync(new LlmConversationStartRequest(providerA));
        var acceleration = new LlmConversationAccelerationEnvelope(
            "provider-conversation-id",
            providerA.Name,
            providerA.DefaultModel,
            """{"id":"crash"}""");
        var seeded = conversation with
        {
            AccelerationState = acceleration,
            TranscriptRevision = conversation.TranscriptRevision + 1
        };
        await harness.Store.ReplaceAsync(seeded, conversation.TranscriptRevision);
        var inFlight = harness.Service.SendAsync(
            new LlmConversationTurnRequest(
                conversation.ConversationId,
                seeded.TranscriptRevision,
                "Adopt before crash",
                providerB,
                providerChangePolicy: LlmConversationProviderChangePolicy.Adopt),
            cancellation.Token);
        await portEntered.Task;
        var admitted = Assert.IsType<LlmConversationDocument>(
            await harness.Service.TryGetAsync(conversation.ConversationId));

        var recovered = await harness.Service.AbandonActiveTurnAsync(
            conversation.ConversationId,
            admitted.ActiveTurn!.TurnId);

        Assert.Equal(providerA.Id, recovered.Provider.ProviderId);
        Assert.Equal(acceleration, recovered.AccelerationState);
        Assert.Empty(recovered.Entries);
        Assert.Null(recovered.ActiveTurn);
        Assert.True(recovered.TranscriptRevision > admitted.TranscriptRevision);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => inFlight);
    }

    [Fact]
    public async Task New_service_instance_recovers_persisted_adoption_compensation_after_a_crash()
    {
        var root = Path.Combine(Path.GetTempPath(), "cdia-llm-conv-recovery-" + Guid.NewGuid().ToString("N"));
        using var cancellation = new CancellationTokenSource();
        var portEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var firstStore = new FileLlmConversationStore(root);
            var firstPort = new RecordingConversationPort(async (_, cancellationToken) =>
            {
                portEntered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("The simulated crashed call must not complete.");
            });
            var firstService = new LlmConversationService(
                firstPort, firstStore, new RecencyBoundedContextWindowPolicy(), TimeProvider.System);
            var providerA = CreateProviderProfile();
            var providerB = CreateProviderProfile(name: "Other provider", model: "other-model");
            var conversation = await firstService.StartAsync(new LlmConversationStartRequest(providerA));
            var acceleration = new LlmConversationAccelerationEnvelope(
                "provider-conversation-id", providerA.Name, providerA.DefaultModel, """{"id":"durable"}""");
            var seeded = conversation with
            {
                AccelerationState = acceleration,
                TranscriptRevision = conversation.TranscriptRevision + 1
            };
            await firstStore.ReplaceAsync(seeded, conversation.TranscriptRevision);
            var inFlight = firstService.SendAsync(
                new LlmConversationTurnRequest(
                    conversation.ConversationId,
                    seeded.TranscriptRevision,
                    "Persist compensation",
                    providerB,
                    providerChangePolicy: LlmConversationProviderChangePolicy.Adopt),
                cancellation.Token);
            await portEntered.Task;
            var admitted = Assert.IsType<LlmConversationDocument>(
                await firstStore.TryGetAsync(conversation.ConversationId));

            var recoveryStore = new FileLlmConversationStore(root);
            var recoveryService = new LlmConversationService(
                new RecordingConversationPort((request, _) => Task.FromResult(
                    new LlmInvocationResult(request.Model, "unused", new LlmUsage(0, 0)))),
                recoveryStore,
                new RecencyBoundedContextWindowPolicy(),
                TimeProvider.System);
            var recovered = await recoveryService.AbandonActiveTurnAsync(
                conversation.ConversationId, admitted.ActiveTurn!.TurnId);

            Assert.Equal(providerA.Id, recovered.Provider.ProviderId);
            Assert.Equal(acceleration, recovered.AccelerationState);
            Assert.Empty(recovered.Entries);
            Assert.Null(recovered.ActiveTurn);
            Assert.True(recovered.TranscriptRevision > admitted.TranscriptRevision);

            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => inFlight);
        }
        finally
        {
            cancellation.Cancel();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SendAsync_rejects_insufficient_transcript_capacity_before_invoking_the_provider()
    {
        var harness = ConversationHarness.Create();
        var provider = CreateProviderProfile();
        var conversation = await harness.Service.StartAsync(new LlmConversationStartRequest(provider));
        var timestamp = DateTimeOffset.Parse("2026-08-08T10:00:00Z");
        var entries = Enumerable.Range(0, LlmConversationDocument.MaximumEntries - 1)
            .Select(index => new LlmConversationTranscriptEntry(
                Guid.NewGuid(),
                Guid.NewGuid(),
                LlmMessageRole.Assistant,
                $"History {index}",
                timestamp.AddTicks(index)))
            .ToImmutableArray();
        var seeded = conversation with
        {
            Entries = entries,
            TranscriptRevision = conversation.TranscriptRevision + 1
        };
        await harness.Store.ReplaceAsync(seeded, conversation.TranscriptRevision);

        var exception = await Assert.ThrowsAsync<LlmConversationException>(() => harness.Service.SendAsync(
            new LlmConversationTurnRequest(
                conversation.ConversationId,
                seeded.TranscriptRevision,
                "One entry too many for an atomic turn",
                provider)));

        Assert.Equal(LlmConversationFailureKind.InvalidRequest, exception.Kind);
        Assert.Equal(0, harness.Port.CallCount);
        var unchanged = Assert.IsType<LlmConversationDocument>(
            await harness.Service.TryGetAsync(conversation.ConversationId));
        Assert.Equal(seeded.TranscriptRevision, unchanged.TranscriptRevision);
        Assert.Equal(entries, unchanged.Entries);
    }

    [Fact]
    public async Task SendAsync_rolls_the_transcript_back_when_the_port_fails()
    {
        var harness = ConversationHarness.Create(respond: (request, _) =>
            throw new LlmInvocationException(
                LlmInvocationFailureKind.ProviderFailure, request.Provider.Name, request.Model, request.CorrelationId));
        var provider = CreateProviderProfile();
        var conversation = await harness.Service.StartAsync(new LlmConversationStartRequest(provider));

        await Assert.ThrowsAsync<LlmInvocationException>(() => harness.Service.SendAsync(
            new LlmConversationTurnRequest(
                conversation.ConversationId, conversation.TranscriptRevision, "Doomed question", provider)));

        var reloaded = await harness.Service.TryGetAsync(conversation.ConversationId);
        Assert.Empty(reloaded!.Entries);
        Assert.Null(reloaded.ActiveTurn);
        Assert.True(reloaded.TranscriptRevision > conversation.TranscriptRevision);
    }

    [Fact]
    public async Task SendAsync_fails_closed_when_the_context_window_policy_drops_the_pending_user_message()
    {
        var harness = ConversationHarness.Create(contextWindowPolicy: new DroppingContextWindowPolicy());
        var provider = CreateProviderProfile();
        var conversation = await harness.Service.StartAsync(new LlmConversationStartRequest(provider));

        var exception = await Assert.ThrowsAsync<LlmConversationException>(() => harness.Service.SendAsync(
            new LlmConversationTurnRequest(
                conversation.ConversationId, conversation.TranscriptRevision, "Dropped", provider)));

        Assert.Equal(LlmConversationFailureKind.InvalidRequest, exception.Kind);
        Assert.Equal(0, harness.Port.CallCount);
        var reloaded = await harness.Service.TryGetAsync(conversation.ConversationId);
        Assert.Empty(reloaded!.Entries);
        Assert.Null(reloaded.ActiveTurn);
    }

    [Fact]
    public async Task AbandonActiveTurnAsync_removes_the_orphaned_user_entry_and_clears_the_marker()
    {
        var harness = ConversationHarness.Create();
        var provider = CreateProviderProfile();
        var conversation = await harness.Service.StartAsync(new LlmConversationStartRequest(provider));

        var turnId = Guid.NewGuid();
        var pendingEntry = new LlmConversationTranscriptEntry(
            Guid.NewGuid(), turnId, LlmMessageRole.User, "Crashed turn", DateTimeOffset.UtcNow);
        var crashed = conversation with
        {
            Entries = conversation.Entries.Add(pendingEntry),
            ActiveTurn = new LlmConversationActiveTurn(
                turnId,
                pendingEntry.EntryId,
                pendingEntry.CreatedAtUtc,
                conversation.TranscriptRevision + 1),
            TranscriptRevision = conversation.TranscriptRevision + 1
        };
        await harness.Store.ReplaceAsync(crashed, conversation.TranscriptRevision);

        var recovered = await harness.Service.AbandonActiveTurnAsync(conversation.ConversationId, turnId);

        Assert.Empty(recovered.Entries);
        Assert.Null(recovered.ActiveTurn);

        var wrongTurn = await Assert.ThrowsAsync<LlmConversationException>(
            () => harness.Service.AbandonActiveTurnAsync(conversation.ConversationId, Guid.NewGuid()));
        Assert.Equal(LlmConversationFailureKind.TurnNotActive, wrongTurn.Kind);
    }

    [Fact]
    public void Conversation_document_rejects_duplicate_entry_ids()
    {
        var provider = CreateProviderProfile();
        var entry = new LlmConversationTranscriptEntry(
            Guid.NewGuid(), Guid.NewGuid(), LlmMessageRole.User, "Duplicate", DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentException>(() => new LlmConversationDocument(
            Guid.NewGuid(),
            "Invalid",
            LlmConversationProviderSnapshot.FromProfile(provider),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            1,
            [entry, entry]));
    }

    [Fact]
    public void Conversation_document_rejects_active_turn_whose_entry_role_or_turn_identity_does_not_match()
    {
        var provider = CreateProviderProfile();
        var timestamp = DateTimeOffset.UtcNow;
        var entry = new LlmConversationTranscriptEntry(
            Guid.NewGuid(), Guid.NewGuid(), LlmMessageRole.Assistant, "Not pending user input", timestamp);

        Assert.Throws<ArgumentException>(() => new LlmConversationDocument(
            Guid.NewGuid(),
            "Invalid",
            LlmConversationProviderSnapshot.FromProfile(provider),
            timestamp,
            timestamp,
            1,
            [entry],
            new LlmConversationActiveTurn(Guid.NewGuid(), entry.EntryId, timestamp, 1)));
    }

    [Fact]
    public async Task DeleteAsync_is_terminal_even_while_a_turn_is_active()
    {
        using var cancellation = new CancellationTokenSource();
        var portEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var harness = ConversationHarness.Create(respond: async (_, cancellationToken) =>
        {
            portEntered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The canceled provider call must not complete.");
        });
        var provider = CreateProviderProfile();
        var conversation = await harness.Service.StartAsync(new LlmConversationStartRequest(provider));
        var inFlight = harness.Service.SendAsync(
            new LlmConversationTurnRequest(
                conversation.ConversationId, conversation.TranscriptRevision, "Delete this turn", provider),
            cancellation.Token);
        await portEntered.Task;

        await harness.Service.DeleteAsync(conversation.ConversationId);

        Assert.Null(await harness.Service.TryGetAsync(conversation.ConversationId));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => inFlight);
        Assert.Null(await harness.Service.TryGetAsync(conversation.ConversationId));
    }

    [Fact]
    public async Task SendAsync_and_rename_fail_typed_for_an_unknown_conversation()
    {
        var harness = ConversationHarness.Create();
        var provider = CreateProviderProfile();

        var send = await Assert.ThrowsAsync<LlmConversationException>(() => harness.Service.SendAsync(
            new LlmConversationTurnRequest(Guid.NewGuid(), 0, "Hello", provider)));
        Assert.Equal(LlmConversationFailureKind.NotFound, send.Kind);

        var rename = await Assert.ThrowsAsync<LlmConversationException>(
            () => harness.Service.RenameAsync(Guid.NewGuid(), "New title", 0));
        Assert.Equal(LlmConversationFailureKind.NotFound, rename.Kind);
    }

    [Fact]
    public async Task ListAsync_returns_summaries_ordered_by_most_recent_update()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.Parse("2026-08-08T10:00:00Z"));
        var harness = ConversationHarness.Create(timeProvider: timeProvider);
        var provider = CreateProviderProfile();

        var older = await harness.Service.StartAsync(new LlmConversationStartRequest(provider, title: "Older"));
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        var newer = await harness.Service.StartAsync(new LlmConversationStartRequest(provider, title: "Newer"));

        var summaries = await harness.Service.ListAsync();

        Assert.Equal(2, summaries.Count);
        Assert.Equal(newer.ConversationId, summaries[0].ConversationId);
        Assert.Equal(older.ConversationId, summaries[1].ConversationId);
        Assert.Equal("Newer", summaries[0].Title);
        Assert.False(summaries[0].HasActiveTurn);
    }

    [Fact]
    public void Default_context_window_policy_keeps_system_entries_and_newest_entries_within_bounds()
    {
        var turnId = Guid.NewGuid();
        var baseTime = DateTimeOffset.Parse("2026-08-08T10:00:00Z");
        var entries = ImmutableArray.Create(
            Entry(LlmMessageRole.System, "system rules", baseTime),
            Entry(LlmMessageRole.User, "u1", baseTime.AddMinutes(1)),
            Entry(LlmMessageRole.Assistant, "a1", baseTime.AddMinutes(2)),
            Entry(LlmMessageRole.User, "u2", baseTime.AddMinutes(3)),
            Entry(LlmMessageRole.Assistant, "a2", baseTime.AddMinutes(4)),
            Entry(LlmMessageRole.User, "u3", baseTime.AddMinutes(5)));
        var policy = new RecencyBoundedContextWindowPolicy();

        var window = policy.SelectWindow(new LlmConversationContextWindowRequest(entries, 4, 400_000));

        Assert.Equal(4, window.Count);
        Assert.Equal(LlmMessageRole.System, window[0].Role);
        Assert.Equal("system rules", window[0].Text);
        Assert.Equal("u2", window[1].Text);
        Assert.Equal("a2", window[2].Text);
        Assert.Equal("u3", window[3].Text);

        LlmConversationTranscriptEntry Entry(LlmMessageRole role, string text, DateTimeOffset at)
            => new(Guid.NewGuid(), turnId, role, text, at);
    }

    [Fact]
    public void Default_context_window_policy_enforces_the_character_budget_but_always_keeps_the_newest_entry()
    {
        var turnId = Guid.NewGuid();
        var baseTime = DateTimeOffset.Parse("2026-08-08T10:00:00Z");
        var entries = ImmutableArray.Create(
            Entry(LlmMessageRole.User, new string('x', 300), baseTime),
            Entry(LlmMessageRole.Assistant, new string('y', 300), baseTime.AddMinutes(1)),
            Entry(LlmMessageRole.User, new string('z', 100), baseTime.AddMinutes(2)));
        var policy = new RecencyBoundedContextWindowPolicy();

        var window = policy.SelectWindow(new LlmConversationContextWindowRequest(entries, 200, 150));

        var single = Assert.Single(window);
        Assert.Equal(new string('z', 100), single.Text);

        LlmConversationTranscriptEntry Entry(LlmMessageRole role, string text, DateTimeOffset at)
            => new(Guid.NewGuid(), turnId, role, text, at);
    }

    [Fact]
    public async Task AddLlmConversations_composes_a_resolvable_service_backed_by_the_file_store()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "cdia-llm-conv-di-" + Guid.NewGuid().ToString("N"));
        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<ILlmInvocationPort>(new RecordingConversationPort((request, _) =>
                Task.FromResult(new LlmInvocationResult(request.Model, "ok", new LlmUsage(1, 1)))));
            services.AddLlmConversations(_ => storageRoot);
            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();

            var service = scope.ServiceProvider.GetRequiredService<ILlmConversationService>();
            var store = scope.ServiceProvider.GetRequiredService<ILlmConversationStore>();

            Assert.IsType<FileLlmConversationStore>(store);
            var provider = CreateProviderProfile();
            var conversation = await service.StartAsync(new LlmConversationStartRequest(provider, title: "DI proof"));
            Assert.True(File.Exists(Path.Combine(
                storageRoot, "conversations", conversation.ConversationId.ToString("N") + ".json")));
        }
        finally
        {
            if (Directory.Exists(storageRoot))
            {
                Directory.Delete(storageRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Production_module_composition_does_not_register_the_conversation_foundation()
    {
        var moduleSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src", "Modules", "CanDoItAll.Modules.AgentFramework", "Services",
            "AgentFrameworkModuleServiceCollectionExtensions.cs"));

        Assert.DoesNotContain("AddLlmConversations(", moduleSource, StringComparison.Ordinal);
        Assert.DoesNotContain("llm-conversations", moduleSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_ordinary_conversation_consumers_are_confined_to_llm_chat_persistence()
    {
        var sourceRoot = Path.Combine(FindRepositoryRoot(), "src");
        var conversationLibrarySegment = Path.Combine(
            "MAF",
            "Common",
            "CanDoItAll.AgentFramework.Llm.Conversations");
        var abstractionLibrarySegment = Path.Combine(
            "MAF",
            "Common",
            "CanDoItAll.AgentFramework.Llm.Abstractions");
        var consumers = Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(conversationLibrarySegment, StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(abstractionLibrarySegment, StringComparison.OrdinalIgnoreCase))
            .Where(path =>
            {
                var source = File.ReadAllText(path);
                return source.Contains("ILlmConversationService", StringComparison.Ordinal) ||
                       source.Contains("ILlmConversationStore", StringComparison.Ordinal) ||
                       source.Contains("AddLlmConversations(", StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(sourceRoot, path))
            .ToArray();

        Assert.NotEmpty(consumers);
        var allowedRoot = Path.Combine("Modules", "CanDoItAll.Modules.LlmChats.Persistence");
        Assert.All(
            consumers,
            consumer => Assert.StartsWith(allowedRoot, consumer, StringComparison.OrdinalIgnoreCase));
        var registrationSource = File.ReadAllText(Path.Combine(
            sourceRoot,
            allowedRoot,
            "LlmChatsPersistenceServiceCollectionExtensions.cs"));
        Assert.DoesNotContain("AddLlmConversations(", registrationSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Conversations_project_references_only_lightweight_llm_projects()
    {
        var csprojPath = Path.Combine(
            FindRepositoryRoot(),
            "src", "MAF", "Common", "CanDoItAll.AgentFramework.Llm.Conversations",
            "CanDoItAll.AgentFramework.Llm.Conversations.csproj");
        Assert.True(File.Exists(csprojPath), $"Expected project file: {csprojPath}");
        var csproj = File.ReadAllText(csprojPath);

        foreach (var forbiddenReference in new[]
                 {
                     "AgentFramework.Core", "AgentFramework.Maf", "AgentFramework.Tools",
                     "AgentFramework.Memory", "AgentFramework.Workflows", "AgentFramework.Persistence",
                     "AgentFramework.Providers", "AgentFramework.Capabilities", "Modules.", "Processes."
                 })
        {
            Assert.False(
                csproj.Contains(forbiddenReference, StringComparison.Ordinal),
                $"Conversations project must stay agent-free but references {forbiddenReference}");
        }
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

    private static ProviderProfile CreateProviderProfile(
        string name = "Conversation test provider",
        string model = "gpt-conversation-test")
        => new(
            Guid.NewGuid(),
            name,
            ProviderKind.OpenAi,
            "https://example.invalid/v1",
            "CONVERSATION_TEST_API_KEY",
            model,
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: false,
            SupportsTools: false,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: [model]);

    private sealed class TestTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan delta) => _now += delta;
    }

    private sealed class DroppingContextWindowPolicy : ILlmConversationContextWindowPolicy
    {
        public IReadOnlyList<LlmMessage> SelectWindow(LlmConversationContextWindowRequest request) => [];
    }

    private sealed class RecordingConversationPort(
        Func<LlmInvocationRequest, CancellationToken, Task<LlmInvocationResult>> respond) : ILlmInvocationPort
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public LlmInvocationRequest? LastRequest { get; private set; }

        public Task<LlmInvocationResult> InvokeAsync(
            LlmInvocationRequest request,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            LastRequest = request;
            return respond(request, cancellationToken);
        }
    }

    private sealed class ConversationHarness
    {
        private ConversationHarness(
            LlmConversationService service, RecordingConversationPort port, InMemoryLlmConversationStore store)
        {
            Service = service;
            Port = port;
            Store = store;
        }

        public LlmConversationService Service { get; }

        public RecordingConversationPort Port { get; }

        public InMemoryLlmConversationStore Store { get; }

        public static ConversationHarness Create(
            Func<LlmInvocationRequest, CancellationToken, Task<LlmInvocationResult>>? respond = null,
            ILlmConversationContextWindowPolicy? contextWindowPolicy = null,
            TimeProvider? timeProvider = null)
        {
            var port = new RecordingConversationPort(respond ?? ((request, _) =>
                Task.FromResult(new LlmInvocationResult(request.Model, "default reply", new LlmUsage(1, 1)))));
            var store = new InMemoryLlmConversationStore();
            var service = new LlmConversationService(
                port,
                store,
                contextWindowPolicy ?? new RecencyBoundedContextWindowPolicy(),
                timeProvider ?? TimeProvider.System);
            return new ConversationHarness(service, port, store);
        }
    }
}
