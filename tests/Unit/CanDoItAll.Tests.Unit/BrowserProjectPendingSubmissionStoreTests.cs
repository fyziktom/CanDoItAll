using System.Text;
using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages.Components;
using CanDoItAll.Web.Infrastructure;
using Microsoft.JSInterop;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class BrowserProjectPendingSubmissionStoreTests {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly ProjectDefinitionRecoveryCodec Codec = new();
    private static readonly ProjectPendingStoreLimits Limits = new(16, 16_777_216, 16, ProjectDefinitionLimits.VersionOneMaximum);

    [Fact]
    public async Task Browser_transport_keeps_int64_epoch_as_a_string_and_submission_as_opaque_json() {
        var frozen = Frozen();
        var runtime = new Runtime((method, _) => Task.FromResult<object?>(method == "prepare"
            ? new { status = "applied", record = Record(frozen) } : null));
        await using var store = new BrowserProjectPendingSubmissionStore(runtime, Codec, Limits);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.PrepareAsync(frozen, false).AsTask());
        await store.InitializeAsync();
        var prepared = await store.PrepareAsync(frozen, false);
        Assert.Equal(ProjectPendingStoreStatus.Applied, prepared.Status);
        Assert.Equal(frozen.Payload, prepared.Record!.Frozen.Payload);
        var call = Assert.Single(runtime.Calls, call => call.Method == "prepare");
        Assert.Equal(long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture), call.Args[0].GetProperty("key").GetProperty("epoch").GetString());
        Assert.Equal(frozen.Payload, call.Args[0].GetProperty("frozenRequest").GetString());
    }

    [Fact]
    public async Task Accepted_mapping_is_checked_against_frozen_submission_before_it_can_be_persisted() {
        var frozen = Frozen();
        var runtime = new Runtime((_, _) => Task.FromResult<object?>(null));
        await using var store = new BrowserProjectPendingSubmissionStore(runtime, Codec, Limits);
        await store.InitializeAsync();
        var receipt = Accepted(frozen);
        var wrong = new ProjectDefinitionAcceptedReceipt.Full(new(receipt.Result.Operation, receipt.Result.Version,
            receipt.Result.Kind, receipt.Result.AcceptedAtUtc, receipt.Result.ReplayUntilUtc,
            receipt.Result.Acknowledgement with { Phases = [receipt.Result.Acknowledgement.Phases[0] with { Id = Guid.NewGuid() }] }));
        await Assert.ThrowsAsync<ArgumentException>(() => store.ObserveAcceptedAsync(frozen, wrong).AsTask());
        Assert.DoesNotContain(runtime.Calls, call => call.Method == "observeAccepted");
    }

    [Fact]
    public async Task Corrupt_stored_acceptance_does_not_escape_as_a_typed_accepted_record() {
        var frozen = Frozen();
        var receipt = Accepted(frozen);
        var invalid = new ProjectDefinitionAccepted<ProjectEditorAcknowledgement>(receipt.Result.Operation, receipt.Result.Version,
            receipt.Result.Kind, receipt.Result.AcceptedAtUtc, receipt.Result.ReplayUntilUtc,
            receipt.Result.Acknowledgement with { Phases = [receipt.Result.Acknowledgement.Phases[0] with { Id = Guid.NewGuid() }] });
        var canonical = Encoding.UTF8.GetString(ProjectDefinitionOperationCodec.EncodeAccepted(invalid, Limits.Definition).ToArray());
        var runtime = new Runtime((method, _) => Task.FromResult<object?>(method == "read" ? Record(frozen, "accepted", canonical) : null));
        await using var store = new BrowserProjectPendingSubmissionStore(runtime, Codec, Limits);
        await store.InitializeAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => store.ReadAsync(frozen.Submission.Operation).AsTask());
    }

    [Fact]
    public async Task Retired_adapter_rejects_a_late_read_and_disposal_never_clears_pending_records() {
        var frozen = Frozen();
        var held = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var runtime = new Runtime((method, _) => method == "read" ? held.Task : Task.FromResult<object?>(null));
        await using var store = new BrowserProjectPendingSubmissionStore(runtime, Codec, Limits);
        await store.InitializeAsync();
        var read = store.ReadAsync(frozen.Submission.Operation).AsTask();
        await store.DisposeAsync();
        held.SetResult(Record(frozen));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => read);
        Assert.DoesNotContain(runtime.Calls, call => call.Method is "clearResolved" or "cancelPrepared");
        Assert.Equal(1, runtime.ReferenceDisposals);
    }

    [Fact]
    public async Task Unsupported_storage_schema_does_not_enable_operations_or_use_another_storage_path() {
        var frozen = Frozen();
        var runtime = new Runtime((_, _) => Task.FromResult<object?>(null)) { Schema = 2 };
        await using var store = new BrowserProjectPendingSubmissionStore(runtime, Codec, Limits);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.InitializeAsync().AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ReadAsync(frozen.Submission.Operation).AsTask());
        Assert.DoesNotContain(runtime.Calls, call => call.Method is "read" or "prepare");
        Assert.All(runtime.Imports, path => Assert.Equal("./js/projectPendingSubmissions.js", path));
    }

    [Fact]
    public async Task Scoped_listing_refuses_foreign_metadata_and_foreign_continuations() {
        var frozen = Frozen();
        var other = Frozen();
        var runtime = new Runtime((method, _) => Task.FromResult<object?>(method == "list" ? new {
            items = new[] { new { key = Key(other.Submission.Operation), revision = 1, stage = "prepared", targetProjectId = other.Submission.TargetProjectId,
                hadStarterPlan = false, hadUnconfirmedDispatch = false } }, nextKey = (object?)null
        } : null));
        await using var store = new BrowserProjectPendingSubmissionStore(runtime, Codec, Limits);
        await store.InitializeAsync();
        var scope = new ProjectPendingScope(frozen.Submission.Operation.Namespace, frozen.Submission.Operation.CallerBinding);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.ListAsync(scope, 1, other.Submission.Operation).AsTask());
        Assert.DoesNotContain(runtime.Calls, call => call.Method == "list");
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ListAsync(scope, 1).AsTask());
    }

    [Fact]
    public async Task Authoritative_closed_absence_remains_valid_after_its_minimum_retention_promise() {
        var frozen = Frozen();
        var closed = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var absence = new ProjectDefinitionLookup.ClosedAbsent(frozen.Submission.Operation, closed, closed.AddDays(9), closed.AddDays(7));
        var runtime = new Runtime((method, _) => Task.FromResult<object?>(method == "observeClosedAbsent"
            ? new { status = "applied", record = Record(frozen, "closedAbsent", absence: JsonSerializer.Serialize(absence, Json)) } : null));
        await using var store = new BrowserProjectPendingSubmissionStore(runtime, Codec, Limits);
        await store.InitializeAsync();
        var result = await store.ObserveClosedAbsentAsync(frozen, absence);
        Assert.Equal(ProjectPendingStoreStatus.Applied, result.Status);
        Assert.Equal(ProjectPendingStage.ClosedAbsent, result.Record!.Stage);
        Assert.Equal(absence, result.Record.Absence);
    }

    [Fact]
    public async Task Administrative_inventory_and_changed_discard_are_metadata_only_and_do_not_decode_foreign_payloads() {
        var operation = Frozen().Submission.Operation;
        var wire = new { key = Key(operation), revision = 12, stage = "unconfirmed", targetProjectId = (Guid?)null,
            hadStarterPlan = true, hadUnconfirmedDispatch = true, payloadBytes = 2345 };
        var runtime = new Runtime((method, _) => Task.FromResult<object?>(method switch {
            "listReferences" => new { items = new[] { wire }, nextKey = (object?)null, count = 1, bytes = 2345 },
            "discardEvidence" => new { status = "changed", reference = wire },
            _ => null
        }));
        await using var store = new BrowserProjectPendingSubmissionStore(runtime, new NoBodyCodec(), Limits);
        await store.InitializeAsync();
        var page = await store.ListReferencesAsync(16);
        var reference = Assert.Single(page.Items);
        Assert.Equal(operation, reference.Operation);
        Assert.Equal(ProjectPendingStage.Unconfirmed, reference.Stage);
        var result = await store.DiscardEvidenceAsync(reference);
        Assert.Equal(ProjectPendingStoreStatus.Changed, result.Status);
        Assert.Equal(reference, result.Reference);
        Assert.DoesNotContain(runtime.Calls, call => call.Method is "read" or "prepare" or "beginDispatch");
        var call = Assert.Single(runtime.Calls, value => value.Method == "discardEvidence");
        Assert.Equal(12, call.Args[1].GetInt32());
        Assert.Equal(long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture), call.Args[0].GetProperty("epoch").GetString());
    }

    [Fact]
    public async Task Administrative_discard_rejects_a_mismatched_operation_or_success_with_reference_body() {
        var requested = new ProjectPendingReference(Frozen().Submission.Operation, new(1), ProjectPendingStage.Prepared, null, false, false, 1234);
        var other = Frozen().Submission.Operation;
        foreach (var status in new[] { "changed", "applied" }) {
            var runtime = new Runtime((method, _) => Task.FromResult<object?>(method == "discardEvidence" ? new {
                status, reference = new { key = Key(other), revision = 2, stage = "accepted", targetProjectId = (Guid?)null,
                    hadStarterPlan = false, hadUnconfirmedDispatch = false, payloadBytes = 2345 }
            } : null));
            await using var store = new BrowserProjectPendingSubmissionStore(runtime, new NoBodyCodec(), Limits);
            await store.InitializeAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.DiscardEvidenceAsync(requested).AsTask());
        }
    }

    private sealed class NoBodyCodec : IProjectDefinitionRecoveryCodec {
        public ProjectDefinitionFrozenSubmission Freeze(ProjectDefinitionSubmission submission, ProjectDefinitionLimits limits) => throw new InvalidOperationException("Metadata administration must not encode bodies.");
        public ProjectDefinitionFrozenSubmission ReadSubmission(string payload, ProjectDefinitionLimits limits) => throw new InvalidOperationException("Metadata administration must not decode bodies.");
        public ProjectDefinitionReceiptPayload EncodeAccepted(ProjectDefinitionSubmission submission, ProjectDefinitionAcceptedReceipt receipt, ProjectDefinitionLimits limits) => throw new InvalidOperationException("Metadata administration must not encode receipts.");
        public ProjectDefinitionReceiptPayload ReadAccepted(ProjectDefinitionSubmission submission, string payload, ProjectDefinitionLimits limits) => throw new InvalidOperationException("Metadata administration must not decode receipts.");
    }

    private static ProjectDefinitionFrozenSubmission Frozen() {
        var operation = new ProjectDefinitionOperation(new(Guid.NewGuid()), new(long.MaxValue), Guid.NewGuid(), new("pc1:" + new string('a', 64)));
        var expected = new ProjectDefinitionExpectation(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid());
        return Codec.Freeze(new ProjectDefinitionSubmission.Replace(new(operation, ProjectDefinitionRequestVersion.VersionOne,
            new(expected, new("Original", "", "", ProjectStatus.Active, "", null),
                [new(Guid.NewGuid(), "Phase", "", ProjectPhaseStatus.Planned, null, null)], [], null))), Limits.Definition);
    }

    private static ProjectDefinitionAcceptedReceipt.Full Accepted(ProjectDefinitionFrozenSubmission frozen) {
        var submission = Assert.IsType<ProjectDefinitionSubmission.Replace>(frozen.Submission);
        var now = new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero);
        return new(new(submission.Operation, ProjectDefinitionResultVersion.VersionOne, ProjectDefinitionCommandKind.Replace,
            now, now.AddDays(7), new(new(submission.Request.Command.Expected.Project, Guid.NewGuid()), "Original", "", "", ProjectStatus.Active,
                "", null, [new(0, submission.Request.Command.Phases[0].Id!.Value, "Phase", "", ProjectPhaseStatus.Planned, null, null)], [], null)));
    }

    private static object Record(ProjectDefinitionFrozenSubmission frozen, string stage = "prepared", string? accepted = null, string? absence = null)
        => new {
            schema = 1, key = Key(frozen.Submission.Operation), revision = 1, stage, frozenRequest = frozen.Payload,
            targetProjectId = frozen.Submission.TargetProjectId, hadStarterPlan = false,
            attemptId = stage == "prepared" ? (Guid?)null : Guid.NewGuid(), hadUnconfirmedDispatch = false,
            acceptedPayload = accepted, absenceProof = absence
        };

    private static object Key(ProjectDefinitionOperation operation) => new {
        @namespace = operation.Namespace.Value.ToString("D"), callerBinding = operation.CallerBinding.Value,
        epoch = operation.Epoch.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), id = operation.Id.ToString("D")
    };

    private sealed class Runtime(Func<string, object?[]?, Task<object?>> invoke) : IJSRuntime, IJSObjectReference {
        public int Schema { get; init; } = 1;
        public int ReferenceDisposals { get; private set; }
        public List<string> Imports { get; } = [];
        public List<(string Method, JsonElement Args)> Calls { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) {
            if (identifier == "import") {
                Imports.Add(Assert.IsType<string>(args![0]));
                return (TValue)(object)this;
            }
            Calls.Add((identifier, JsonSerializer.SerializeToElement(args, Json)));
            var result = identifier == "initialize" ? new { schema = Schema } : await invoke(identifier, args);
            return result is null ? default! : JsonSerializer.Deserialize<TValue>(JsonSerializer.Serialize(result, Json), Json)!;
        }

        public ValueTask DisposeAsync() {
            ReferenceDisposals++;
            return ValueTask.CompletedTask;
        }
    }
}
