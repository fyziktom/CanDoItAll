using System.Security.Claims;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Catalog;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Infrastructure;
using Microsoft.JSInterop;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class BrowserAgentDecisionPolicyPendingTests {
    [Fact]
    public void Original_large_revision_and_historical_warning_round_trip_exactly() {
        var command = Command();
        var key = Key(command);
        var payload = BrowserAgentDecisionPolicyPendingCodec.Encode(command);
        Assert.Contains("\"9007199254740993\"", payload, StringComparison.Ordinal);
        var outcome = new AgentDecisionDataPolicySaveOutcome.Accepted(command,
            new(new(command.Expectation.Workspace, command.Expectation.AgentId, command.Expectation.CatalogRevision.Next()), command.Policy), true);
        var record = BrowserAgentDecisionPolicyPendingCodec.Decode(new(1, key, command.OperationId.ToString("D"), payload,
            AgentDecisionPolicyPendingStage.Accepted, BrowserAgentDecisionPolicyPendingCodec.EncodeOutcome(command, outcome)), key)!;
        Assert.Equal(command, record.Command);
        Assert.Equal(outcome, record.Outcome);
        Assert.Throws<ArgumentException>(() => BrowserAgentDecisionPolicyPendingCodec.EncodeOutcome(command,
            new AgentDecisionDataPolicySaveOutcome.Unconfirmed(command)));
    }

    [Theory]
    [InlineData(Corruption.Duplicate)]
    [InlineData(Corruption.UnknownMember)]
    [InlineData(Corruption.Oversized)]
    [InlineData(Corruption.OtherCaller)]
    [InlineData(Corruption.OtherOperation)]
    [InlineData(Corruption.MissingOutcome)]
    public void Corrupt_or_mismatched_retention_never_becomes_a_dispatchable_command(Corruption corruption) {
        var command = Command();
        var key = Key(command);
        var wire = new BrowserAgentDecisionPolicyPendingWire(1, key, command.OperationId.ToString("D"),
            BrowserAgentDecisionPolicyPendingCodec.Encode(command), AgentDecisionPolicyPendingStage.Dispatching, null);
        wire = corruption switch {
            Corruption.Duplicate => wire with { Payload = wire.Payload.Insert(1, $"\"operationId\":\"{command.OperationId:D}\",") },
            Corruption.UnknownMember => wire with { Payload = wire.Payload.Insert(1, "\"unexpected\":true,") },
            Corruption.Oversized => wire with { Payload = new('x', BrowserAgentDecisionPolicyPendingStore.MaximumRequestBytes + 1) },
            Corruption.OtherCaller => wire with { Key = key with { Caller = "ap1:" + new string('b', 64) } },
            Corruption.OtherOperation => wire with { OperationId = Guid.NewGuid().ToString("D") },
            Corruption.MissingOutcome => wire with { Stage = AgentDecisionPolicyPendingStage.Accepted },
            _ => throw new ArgumentOutOfRangeException(nameof(corruption))
        };
        var failure = Record.Exception(() => BrowserAgentDecisionPolicyPendingCodec.Decode(wire, key));
        Assert.True(failure is ArgumentException or JsonException or InvalidOperationException, failure?.GetType().Name ?? "No rejection");
    }

    [Fact]
    public async Task Changed_caller_after_browser_await_rejects_result_and_new_reads() {
        var principal = new Principal();
        var runtime = new BrowserRuntime();
        await using var store = new BrowserAgentDecisionPolicyPendingStore(runtime, principal);
        await store.InitializeAsync();
        runtime.Module.AfterRead = () => principal.Subject = "another-operator";
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.ReadAsync(Command().Expectation));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.ReadAsync(Command().Expectation));
        Assert.Equal(1, runtime.Module.Reads);
    }

    [Fact]
    public async Task Unauthenticated_caller_cannot_initialize_browser_storage() {
        var runtime = new BrowserRuntime();
        await using var store = new BrowserAgentDecisionPolicyPendingStore(runtime, new Principal { Authenticated = false });
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.InitializeAsync());
        Assert.Equal(0, runtime.Imports);
    }

    [Fact]
    public async Task Trusted_local_identity_uses_existing_provider_when_api_authentication_is_disabled() {
        var runtime = new BrowserRuntime();
        await using var store = new BrowserAgentDecisionPolicyPendingStore(runtime,
            new Principal { Authenticated = false, TrustedLocal = true });
        Assert.Null(await store.ReadAsync(Command().Expectation));
        Assert.Equal(1, runtime.Imports);
        Assert.Equal(1, runtime.Module.Reads);
    }

    [Fact]
    public async Task Unsupported_browser_bounds_fail_before_read_or_write() {
        var runtime = new BrowserRuntime();
        runtime.Module.MaximumRecords = 129;
        await using var store = new BrowserAgentDecisionPolicyPendingStore(runtime, new Principal());
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.PrepareAsync(Command()));
        Assert.Equal(0, runtime.Module.Reads);
        Assert.Equal(0, runtime.Module.Writes);
    }

    private static ReplaceAgentDecisionDataPolicyCommand Command() => new(Guid.NewGuid(),
        new(new(Guid.NewGuid(), WorkspaceScopeDescriptor.Sandbox, new(3)), Guid.NewGuid(), new(9007199254740993)),
        new(ReadBasisMode.RunSnapshot, MaximumReplans: 2, MaximumWorkDuration: TimeSpan.FromMinutes(3)));
    private static BrowserAgentDecisionPolicyPendingKey Key(ReplaceAgentDecisionDataPolicyCommand command)
        => new("ap1:" + new string('a', 64), command.Expectation.Workspace.DatabaseProfileId.ToString("D"),
            (int)command.Expectation.Workspace.WorkspaceScope.Kind, command.Expectation.Workspace.WorkspaceScope.Key, command.Expectation.AgentId.ToString("D"));
    public enum Corruption { Duplicate, UnknownMember, Oversized, OtherCaller, OtherOperation, MissingOutcome }

    private sealed class Principal : IInteractiveAccessPrincipalProvider {
        public string Subject { get; set; } = "operator";
        public bool Authenticated { get; set; } = true;
        public bool TrustedLocal { get; set; }
        public bool IsAvailable => true;
        public ValueTask<ClaimsPrincipal> GetCurrentAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, Subject)], Authenticated ? "test" : null)));
        public ValueTask<ClaimsPrincipal?> TryGetTrustedLocalOperatorAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(TrustedLocal ? new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "local-operator")],
                LocalOperatorAuthenticationStateProvider.AuthenticationType)) : null);
    }
    private sealed class BrowserRuntime : IJSRuntime {
        public BrowserModule Module { get; } = new();
        public int Imports { get; private set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, default, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) {
            Assert.Equal("import", identifier);
            Assert.Equal(BrowserAgentDecisionPolicyPendingStore.ModulePath, Assert.Single(args!));
            Imports++;
            return ValueTask.FromResult((TValue)(object)Module);
        }
    }
    private sealed class BrowserModule : IJSObjectReference {
        public int MaximumRecords { get; set; } = BrowserAgentDecisionPolicyPendingStore.MaximumRecords;
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public Action? AfterRead { get; set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, default, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) {
            if (identifier == "initialize") {
                return ValueTask.FromResult(JsonSerializer.Deserialize<TValue>(JsonSerializer.Serialize(new {
                    schema = 1, maximumRecords = MaximumRecords, maximumRequestBytes = 8192, maximumOutcomeBytes = 4096
                }), new JsonSerializerOptions(JsonSerializerDefaults.Web))!);
            }
            if (identifier == "read") {
                Reads++;
                AfterRead?.Invoke();
                return ValueTask.FromResult(default(TValue)!);
            }
            Writes++;
            throw new InvalidOperationException("Unexpected browser write.");
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
