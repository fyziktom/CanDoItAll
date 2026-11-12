using System.Globalization;
using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages.Components;
using CanDoItAll.Web.Infrastructure;
using Microsoft.JSInterop;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class BrowserProjectLifecyclePendingStoreTests {
    private static readonly ProjectLifecycleRecoveryCodec Codec = new();
    private static readonly ProjectLifecyclePendingStoreLimits Limits = new(16, 1024 * 1024, 16);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Original_profile_epoch_and_exact_command_bytes_survive_browser_transport() {
        var frozen = Frozen();
        var runtime = new Runtime((method, _) => Task.FromResult<object?>(method == "prepare" ? new { status = "applied", record = Record(frozen) } : null));
        await using var store = new BrowserProjectLifecyclePendingStore(runtime, Codec, Limits);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.PrepareAsync(frozen).AsTask());
        await store.InitializeAsync();
        var result = await store.PrepareAsync(frozen);
        Assert.Equal(frozen.Payload, result.Record!.Frozen.Payload);
        var call = Assert.Single(runtime.Calls, value => value.Method == "prepare").Args[0];
        Assert.Equal(long.MaxValue.ToString(CultureInfo.InvariantCulture), call.GetProperty("key").GetProperty("epoch").GetString());
        Assert.Equal(frozen.Submission.Operation.DatabaseProfileId.ToString("D"), call.GetProperty("key").GetProperty("databaseProfileId").GetString());
        Assert.Equal(frozen.Payload, call.GetProperty("frozenRequest").GetString());
        Assert.Equal(1, call.GetProperty("kind").GetInt32());
        Assert.Equal(Assert.Single(ProjectLifecyclePendingTargets.Capture(frozen.Submission.Command)), call.GetProperty("targetProjectIds")[0].GetGuid());
        Assert.All(runtime.Imports, value => Assert.Equal("./js/projectLifecyclePendingSubmissions.js", value));
    }

    [Fact]
    public async Task Accepted_and_absence_evidence_are_bound_before_persistence_and_corrupt_stored_evidence_is_refused() {
        var frozen = Frozen();
        var other = Frozen();
        var accepted = Accepted(other);
        object? retained = Record(frozen, "accepted", Codec.EncodeAccepted(other.Submission, accepted).Payload);
        var runtime = new Runtime((method, _) => Task.FromResult(method == "read" ? retained : null));
        await using var store = new BrowserProjectLifecyclePendingStore(runtime, Codec, Limits);
        await store.InitializeAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => store.ObserveAcceptedAsync(frozen, accepted).AsTask());
        await Assert.ThrowsAsync<InvalidDataException>(() => store.ObserveClosedAbsentAsync(frozen,
            new(other.Submission.Operation, Now, Now.AddMinutes(1), Now.AddDays(1))).AsTask());
        Assert.DoesNotContain(runtime.Calls, value => value.Method is "observeAccepted" or "observeClosedAbsent");
        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(frozen.Submission.Operation).AsTask());
        var valid = Codec.EncodeAccepted(frozen.Submission, Accepted(frozen)).Payload;
        retained = Record(frozen, "accepted", valid + " ");
        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(frozen.Submission.Operation).AsTask());
    }

    [Fact]
    public async Task Listing_rejects_foreign_profile_duplicate_addresses_and_incomplete_target_metadata() {
        var frozen = Frozen();
        var operation = frozen.Submission.Operation;
        var scope = new ProjectLifecyclePendingScope(operation.DatabaseProfileId, operation.Namespace, operation.CallerBinding);
        var foreign = new ProjectLifecycleOperation(Guid.NewGuid(), operation.Namespace, operation.Epoch, operation.CallerBinding, operation.Id);
        object[] items = [Metadata(foreign, frozen)];
        var runtime = new Runtime((method, _) => Task.FromResult<object?>(method == "list" ? new { items, nextKey = (object?)null } : null));
        await using var store = new BrowserProjectLifecyclePendingStore(runtime, Codec, Limits);
        await store.InitializeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ListAsync(scope, 2).AsTask());
        items = [Metadata(operation, frozen), Metadata(operation, frozen)];
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ListAsync(scope, 2).AsTask());
        items = [new { key = Key(operation), revision = 1, stage = "unconfirmed", kind = 4,
            targetProjectIds = ProjectLifecyclePendingTargets.Capture(frozen.Submission.Command), hadUnconfirmedDispatch = true }];
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ListAsync(scope, 2).AsTask());
    }

    [Fact]
    public async Task Retired_adapter_does_not_return_a_late_record_or_clear_the_durable_original() {
        var frozen = Frozen();
        var held = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var runtime = new Runtime((method, _) => method == "read" ? held.Task : Task.FromResult<object?>(null));
        await using var store = new BrowserProjectLifecyclePendingStore(runtime, Codec, Limits);
        await store.InitializeAsync();
        var pending = store.ReadAsync(frozen.Submission.Operation).AsTask();
        Exception? primary = null;
        try {
            await store.DisposeAsync();
            held.TrySetResult(Record(frozen));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => pending);
            Assert.DoesNotContain(runtime.Calls, value => value.Method is "clearResolved" or "cancelPrepared");
            Assert.Equal(1, runtime.Disposals);
        } catch (Exception failure) {
            primary = failure;
            throw;
        } finally {
            held.TrySetResult(Record(frozen));
            try {
                await pending;
            } catch (ObjectDisposedException) {
            } catch (Exception terminal) when (primary is not null && !ReferenceEquals(primary, terminal)) {
                throw new AggregateException(primary, terminal);
            }
        }
    }

    private static ProjectLifecycleFrozenSubmission Frozen() {
        var profile = Guid.NewGuid();
        return Codec.Freeze(new(new(profile, new(Guid.NewGuid()), new(long.MaxValue), new("pc1:" + new string('a', 64)), Guid.NewGuid()),
            ProjectLifecycleInterpretation.VersionOne, new ProjectLifecycleCommand.Delete(new(new(profile, Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()))));
    }

    private static ProjectLifecycleAccepted Accepted(ProjectLifecycleFrozenSubmission frozen) {
        var command = Assert.IsType<ProjectLifecycleCommand.Delete>(frozen.Submission.Command);
        return new(frozen.Submission, 1, Now, Now.AddDays(7), new ProjectLifecycleAcknowledgement.DeletionCommitted(new(
            new(new(frozen.Submission.Operation.DatabaseProfileId, frozen.Submission.Operation.Namespace, new(Guid.NewGuid())), command.Expected.Project), [])));
    }

    private static object Record(ProjectLifecycleFrozenSubmission frozen, string stage = "prepared", string? accepted = null) => new {
        schema = 1, key = Key(frozen.Submission.Operation), revision = 1, stage, frozenRequest = frozen.Payload,
        kind = (int)frozen.Submission.Command.Kind, targetProjectIds = ProjectLifecyclePendingTargets.Capture(frozen.Submission.Command),
        attemptId = (Guid?)null, hadUnconfirmedDispatch = false, acceptedPayload = accepted, absenceProof = (string?)null
    };

    private static object Metadata(ProjectLifecycleOperation operation, ProjectLifecycleFrozenSubmission frozen) => new {
        key = Key(operation), revision = 1, stage = "prepared", kind = (int)frozen.Submission.Command.Kind,
        targetProjectIds = ProjectLifecyclePendingTargets.Capture(frozen.Submission.Command), hadUnconfirmedDispatch = false
    };

    private static object Key(ProjectLifecycleOperation operation) => new {
        databaseProfileId = operation.DatabaseProfileId.ToString("D"), @namespace = operation.Namespace.Value.ToString("D"),
        callerBinding = operation.CallerBinding.Value, epoch = operation.Epoch.Value.ToString(CultureInfo.InvariantCulture), id = operation.Id.ToString("D")
    };

    private sealed class Runtime(Func<string, object?[]?, Task<object?>> invoke) : IJSRuntime, IJSObjectReference {
        public List<(string Method, JsonElement Args)> Calls { get; } = [];
        public List<string> Imports { get; } = [];
        public int Disposals { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) {
            if (identifier == "import") {
                Imports.Add(Assert.IsType<string>(args![0]));
                return (TValue)(object)this;
            }
            Calls.Add((identifier, JsonSerializer.SerializeToElement(args, Json)));
            var result = identifier == "initialize" ? new { schema = 1 } : await invoke(identifier, args);
            return result is null ? default! : JsonSerializer.Deserialize<TValue>(JsonSerializer.Serialize(result, Json), Json)!;
        }

        public ValueTask DisposeAsync() {
            Disposals++;
            return ValueTask.CompletedTask;
        }
    }
}
