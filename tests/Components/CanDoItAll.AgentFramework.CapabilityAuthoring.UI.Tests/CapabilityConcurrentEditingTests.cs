using CanDoItAll.AgentFramework.CapabilityAuthoring.UI;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.AgentFramework.CapabilityAuthoring.UI.Tests;

public sealed class CapabilityConcurrentEditingTests {
    [Fact]
    public async Task Choosing_peer_configuration_removes_forbidden_plaintext_and_retains_extensions() {
        var current = Model();
        using var session = Session(Operations(current));
        await session.LoadAsync();
        current.ConfigurationJson = """{"transport":"logical","headers":{"Authorization":"forbidden-current-value"},"future":17}""";
        current.ExpectedFingerprint = "peer-fingerprint";
        session.ObserveChanges(1);
        await session.CompareAsync();
        session.Comparison!.Configuration.UseCurrent();
        Assert.True(session.ApplyComparison());
        Assert.DoesNotContain("forbidden-current-value", session.Draft.Model.ConfigurationJson);
        Assert.DoesNotContain("forbidden-current-value", session.Draft.RawConfigurationJson);
        Assert.True(session.Draft.RemovedPlaintext);
        Assert.Contains("17", session.Draft.Model.ConfigurationJson);
        Assert.Equal("peer-fingerprint", session.Draft.Model.ExpectedFingerprint);
    }

    [Fact]
    public async Task Acknowledged_save_keeps_the_editor_open_for_unsubmitted_tag_text() {
        var operations = Operations() with { Save = (submitted, _) => Task.FromResult<CapabilityAuthoringSaveOutcome>(
            new CapabilityAuthoringSaveOutcome.Accepted(submitted with { ExpectedFingerprint = "accepted-pending-tag" })) };
        using var session = Session(operations);
        await session.LoadAsync();
        session.Draft.PendingTagInput = "unfinished tag 東京";
        Assert.False(await session.SaveAsync());
        Assert.NotNull(session.AcceptedId);
        Assert.Equal("unfinished tag 東京", session.Draft.PendingTagInput);
        Assert.Equal("accepted-pending-tag", session.Draft.Model.ExpectedFingerprint);
    }

    [Fact]
    public async Task Disjoint_comparison_keeps_context_and_extensions_then_requires_a_separate_save() {
        var current = Model();
        var writes = 0;
        var operations = Operations(current) with { Save = (submitted, _) => {
            writes++;
            return Task.FromResult<CapabilityAuthoringSaveOutcome>(new CapabilityAuthoringSaveOutcome.Accepted(submitted with { ExpectedFingerprint = "accepted-next" }));
        } };
        using var session = Session(operations);
        await session.LoadAsync();
        var context = session.Draft.Context;
        session.Draft.Model.Name = "Local name 東京";
        session.Draft.Changed();
        current.Description = "Peer description";
        current.ExpectedFingerprint = "peer-fingerprint";
        session.ObserveChanges(1);
        await session.CompareAsync();
        Assert.True(session.ApplyComparison());
        Assert.Same(context, session.Draft.Context);
        Assert.Equal("Local name 東京", session.Draft.Model.Name);
        Assert.Equal("Peer description", session.Draft.Model.Description);
        Assert.Equal("peer-fingerprint", session.Draft.Model.ExpectedFingerprint);
        Assert.Contains("17", session.Draft.Model.ConfigurationJson);
        Assert.Equal(0, writes);
        await session.SaveAsync();
        Assert.Equal(1, writes);
        Assert.Equal("accepted-next", session.Draft.Model.ExpectedFingerprint);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Newer_hint_or_raw_input_cannot_apply_an_older_comparison(bool hint) {
        using var session = Session(Operations());
        await session.LoadAsync();
        session.Draft.Model.Name = "Retained local";
        await session.CompareAsync();
        if (hint) {
            session.ObserveChanges(1);
        } else {
            session.Draft.Mcp.ArgumentsText = "unfinished raw argument";
            session.Draft.Changed();
        }
        Assert.False(session.ApplyComparison());
        Assert.True(session.Comparison!.IsStale);
        Assert.Equal("Retained local", session.Draft.Model.Name);
        Assert.Equal("original-fingerprint", session.Draft.Model.ExpectedFingerprint);
    }

    [Fact]
    public async Task Incomplete_configuration_requires_explicit_keep_and_preserves_raw_input_and_validation() {
        var current = Model();
        current.Kind = CapabilityKind.Tool;
        current.ConfigurationJson = """{"toolKind":"externalHttp","runtimeToolName":"fixture","implementationKey":"external.fixture","externalHttp":{"endpoint":"https://fixture.invalid/","future":17}}""";
        using var session = Session(Operations(current));
        await session.LoadAsync();
        var context = session.Draft.Context;
        session.Draft.Tool.HttpTimeoutSecondsText = "unfinished 東京";
        session.Draft.Changed();
        current.Description = "Peer description";
        current.ExpectedFingerprint = "peer-fingerprint";
        session.ObserveChanges(1);
        await session.CompareAsync();
        var review = Assert.IsType<CapabilityAuthoringComparison>(session.Comparison);
        Assert.True(review.HasIncompleteConfiguration);
        Assert.False(session.ApplyComparison());
        review.KeepIncompleteConfiguration = true;
        Assert.True(session.ApplyComparison());
        Assert.Same(context, session.Draft.Context);
        Assert.Equal("unfinished 東京", session.Draft.Tool.HttpTimeoutSecondsText);
        Assert.NotEmpty(context.GetValidationMessages());
        Assert.Equal("Peer description", session.Draft.Model.Description);
        Assert.Equal("peer-fingerprint", session.Draft.Model.ExpectedFingerprint);
        Assert.False(await session.SaveAsync());
    }

    [Fact]
    public async Task Known_setup_refusal_is_not_unknown_and_requires_review_without_replaying_setup() {
        var calls = 0;
        var operations = Operations() with { TestMcp = (_, _) => {
            calls++;
            throw new CapabilitySetupNotStartedException();
        } };
        using var session = Session(operations);
        await session.LoadAsync();
        await session.TestSetupAsync();
        await session.TestSetupAsync();
        Assert.False(session.SetupUnknown);
        Assert.True(session.HasExternalChanges);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task A_conflict_retains_the_draft_and_requires_review_before_another_write() {
        var calls = 0;
        var operations = Operations() with { Save = (_, _) => {
            calls++;
            return Task.FromResult<CapabilityAuthoringSaveOutcome>(new CapabilityAuthoringSaveOutcome.Rejected(true));
        } };
        using var session = Session(operations);
        await session.LoadAsync();
        session.Draft.Model.Name = "Retained local 東京";
        await session.SaveAsync();
        await session.SaveAsync();
        Assert.Equal(1, calls);
        Assert.Equal("Retained local 東京", session.Draft.Model.Name);
        Assert.False(session.SaveUnknown);
    }

    [Fact]
    public async Task Save_uses_the_original_owner_lifetime_cancellation() {
        using var owner = new CancellationTokenSource();
        var pending = new TaskCompletionSource<CapabilityAuthoringSaveOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var captured = default(CancellationToken);
        var operations = Operations() with { Save = (_, token) => {
            captured = token;
            return pending.Task;
        } };
        using var session = Session(operations, owner.Token);
        await session.LoadAsync();
        var save = session.SaveAsync();
        owner.Cancel();
        pending.SetResult(new CapabilityAuthoringSaveOutcome.Rejected(false));
        await save;
        Assert.True(captured.IsCancellationRequested);
    }

    private static CapabilityEditorModel Model() => new() {
        ExpectedFingerprint = "original-fingerprint", Name = "Original", Key = "original",
        Kind = CapabilityKind.McpServer, ConfigurationJson = """{"transport":"logical","future":17}"""
    };

    private static CapabilityAuthoringOperations Operations(CapabilityEditorModel? current = null) {
        current ??= Model();
        return new(
        (id, _) => {
            var copy = CapabilityAuthoringSubmission.Capture(current).ToEditorModel();
            copy.Id = id;
            return Task.FromResult(copy);
        },
        (_, _) => throw new InvalidOperationException("Arrange the native save explicitly."),
        (_, _, _) => throw new InvalidOperationException("Setup must not be replayed by comparison."),
        (_, _) => throw new InvalidOperationException("Setup must not be replayed by comparison."),
        StringComparer.Ordinal);
    }

    private static CapabilityAuthoringSession Session(CapabilityAuthoringOperations operations, CancellationToken owner = default)
        => new(operations, Guid.NewGuid(), CapabilityKind.McpServer, owner, new NotificationService(), NullLogger.Instance);
}
