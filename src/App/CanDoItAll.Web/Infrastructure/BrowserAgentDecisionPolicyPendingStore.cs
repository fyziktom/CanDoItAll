using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Catalog;
using Microsoft.JSInterop;

namespace CanDoItAll.Web.Infrastructure;

internal sealed class BrowserAgentDecisionPolicyPendingStore(IJSRuntime js, IInteractiveAccessPrincipalProvider principals)
    : IAgentDecisionPolicyPendingStore, IAsyncDisposable {
    internal const string ModulePath = "./js/agentDecisionPolicyPending.js";
    internal const int MaximumRecords = IAgentDecisionPolicyPendingStore.MaximumRecords;
    internal const int MaximumRequestBytes = 8192;
    internal const int MaximumOutcomeBytes = 4096;
    private readonly SemaphoreSlim initialization = new(1, 1);
    private IJSObjectReference? module;
    private string? caller;
    private bool ready;
    private bool disposed;

    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default) {
        await initialization.WaitAsync(cancellationToken);
        try {
            ObjectDisposedException.ThrowIf(disposed, this);
            var current = await CurrentCallerAsync(cancellationToken);
            if (caller is not null && caller != current) {
                throw new InvalidOperationException("The retained policy caller changed.");
            }
            caller = current;
            if (ready) {
                return;
            }
            module ??= await js.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);
            var result = await module.InvokeAsync<Initialization>("initialize", cancellationToken);
            if (result.Schema != 1 || result.MaximumRecords != MaximumRecords || result.MaximumRequestBytes != MaximumRequestBytes ||
                result.MaximumOutcomeBytes != MaximumOutcomeBytes) {
                throw new InvalidOperationException("The retained policy browser protocol is unsupported.");
            }
            await RequireCallerAsync(cancellationToken);
            ready = true;
        } finally {
            initialization.Release();
        }
    }

    public async ValueTask<IReadOnlyList<AgentDecisionPolicyPendingSummary>> ListAsync(AgentExecutionActivityWorkspaceIdentity context,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(context);
        await InitializeAsync(cancellationToken);
        var scope = new BrowserAgentDecisionPolicyPendingScope(caller!, context.DatabaseProfileId.ToString("D"),
            (int)context.WorkspaceScope.Kind, context.WorkspaceScope.Key);
        var rows = await InvokeAsync<Summary[]>("list", cancellationToken, scope);
        if (rows is null || rows.Length > MaximumRecords) {
            throw new InvalidOperationException("The retained policy list exceeds its supported bound.");
        }
        HashSet<Guid> agents = [];
        List<AgentDecisionPolicyPendingSummary> summaries = [];
        foreach (var row in rows) {
            if (row is null || !Guid.TryParseExact(row.AgentId, "D", out var agentId) || agentId == Guid.Empty ||
                    !Guid.TryParseExact(row.OperationId, "D", out var operationId) || operationId == Guid.Empty ||
                    !Enum.IsDefined(row.Stage) || !agents.Add(agentId)) {
                throw new InvalidOperationException("The retained policy list has invalid or duplicate identities.");
            }
            summaries.Add(new(agentId, operationId, row.Stage));
        }
        return summaries;
    }

    public async ValueTask<AgentDecisionPolicyPendingRecord?> ReadAsync(AgentDecisionPolicyPendingAddress context,
        CancellationToken cancellationToken = default) {
        await InitializeAsync(cancellationToken);
        var key = Key(context);
        return BrowserAgentDecisionPolicyPendingCodec.Decode(await InvokeAsync<BrowserAgentDecisionPolicyPendingWire?>(
            "read", cancellationToken, key), key);
    }

    public ValueTask<AgentDecisionPolicyPendingChange> PrepareAsync(ReplaceAgentDecisionDataPolicyCommand command,
        CancellationToken cancellationToken = default) => ChangeAsync(ChangeKind.Prepare, command, null, cancellationToken);

    public ValueTask<AgentDecisionPolicyPendingChange> BeginDispatchAsync(ReplaceAgentDecisionDataPolicyCommand command,
        CancellationToken cancellationToken = default) => ChangeAsync(ChangeKind.BeginDispatch, command, null, cancellationToken);

    public ValueTask<AgentDecisionPolicyPendingChange> RecordOutcomeAsync(ReplaceAgentDecisionDataPolicyCommand command,
        AgentDecisionDataPolicySaveOutcome outcome, CancellationToken cancellationToken = default)
        => ChangeAsync(ChangeKind.RecordOutcome, command, BrowserAgentDecisionPolicyPendingCodec.EncodeOutcome(command, outcome), cancellationToken);

    public ValueTask<AgentDecisionPolicyPendingChange> ClearResolvedAsync(ReplaceAgentDecisionDataPolicyCommand command,
        CancellationToken cancellationToken = default) => ChangeAsync(ChangeKind.ClearResolved, command, null, cancellationToken);

    public ValueTask<AgentDecisionPolicyPendingChange> CancelPreparedAsync(ReplaceAgentDecisionDataPolicyCommand command,
        CancellationToken cancellationToken = default) => ChangeAsync(ChangeKind.CancelPrepared, command, null, cancellationToken);

    private async ValueTask<AgentDecisionPolicyPendingChange> ChangeAsync(ChangeKind kind, ReplaceAgentDecisionDataPolicyCommand command,
        string? outcome, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(command);
        var payload = BrowserAgentDecisionPolicyPendingCodec.Encode(command);
        await InitializeAsync(cancellationToken);
        var key = Key(AgentDecisionPolicyPendingAddress.From(command.Expectation));
        var method = kind switch {
            ChangeKind.Prepare => "prepare",
            ChangeKind.BeginDispatch => "beginDispatch",
            ChangeKind.RecordOutcome => "recordOutcome",
            ChangeKind.ClearResolved => "clearResolved",
            ChangeKind.CancelPrepared => "cancelPrepared",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var result = await InvokeAsync<Change>(method, cancellationToken, key, command.OperationId.ToString("D"), payload, outcome);
        var record = BrowserAgentDecisionPolicyPendingCodec.Decode(result.Record, key);
        if (result.Changed && record is not null && record.Command != command ||
            kind is ChangeKind.Prepare or ChangeKind.BeginDispatch or ChangeKind.RecordOutcome && result.Changed && record is null ||
            kind == ChangeKind.Prepare && result.Changed && record?.Stage != AgentDecisionPolicyPendingStage.Prepared ||
            kind == ChangeKind.BeginDispatch && result.Changed && record?.Stage != AgentDecisionPolicyPendingStage.Dispatching ||
            kind == ChangeKind.RecordOutcome && result.Changed && record?.Outcome is null ||
            kind is ChangeKind.ClearResolved or ChangeKind.CancelPrepared && result.Changed && record is not null) {
            throw new InvalidOperationException("The browser returned an inconsistent policy transition.");
        }
        return new(result.Changed, record);
    }

    private async ValueTask<T> InvokeAsync<T>(string method, CancellationToken cancellationToken, params object?[] arguments) {
        ObjectDisposedException.ThrowIf(disposed, this);
        await RequireCallerAsync(cancellationToken);
        var result = await module!.InvokeAsync<T>(method, cancellationToken, arguments);
        await RequireCallerAsync(cancellationToken);
        ObjectDisposedException.ThrowIf(disposed, this);
        return result;
    }

    private BrowserAgentDecisionPolicyPendingKey Key(AgentDecisionPolicyPendingAddress context) {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Workspace is null || context.AgentId == Guid.Empty) {
            throw new ArgumentException("The retained policy read requires its workspace and Agent identity.", nameof(context));
        }
        return new(caller!, context.Workspace.DatabaseProfileId.ToString("D"), (int)context.Workspace.WorkspaceScope.Kind,
            context.Workspace.WorkspaceScope.Key, context.AgentId.ToString("D"));
    }

    private async ValueTask RequireCallerAsync(CancellationToken cancellationToken) {
        if (await CurrentCallerAsync(cancellationToken) != caller) {
            throw new InvalidOperationException("The retained policy caller changed.");
        }
    }

    private async ValueTask<string> CurrentCallerAsync(CancellationToken cancellationToken) {
        var principal = await principals.GetCurrentAsync(cancellationToken);
        if (principal.Identity?.IsAuthenticated != true) {
            principal = await principals.TryGetTrustedLocalOperatorAsync(cancellationToken)
                ?? throw new InvalidOperationException("An authenticated or trusted local native caller is required for policy recovery.");
        }
        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var issuer = principal.FindFirstValue(JwtRegisteredClaimNames.Iss) ?? string.Empty;
        var authentication = principal.Identity?.AuthenticationType;
        if (principal.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(subject) || subject.Length > 512 ||
            issuer.Length > 512 || string.IsNullOrWhiteSpace(authentication) || authentication.Length > 128) {
            throw new InvalidOperationException("An authenticated native caller is required for policy recovery.");
        }
        return "ap1:" + Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new[] { issuer, subject, authentication })));
    }

    public async ValueTask DisposeAsync() {
        await initialization.WaitAsync();
        try {
            if (disposed) {
                return;
            }
            disposed = true;
            if (module is not null) {
                try {
                    await module.DisposeAsync();
                } catch (JSDisconnectedException) {
                }
            }
        } finally {
            initialization.Release();
        }
    }

    private enum ChangeKind { Prepare, BeginDispatch, RecordOutcome, ClearResolved, CancelPrepared }
    private sealed record Initialization([property: JsonRequired] int Schema, [property: JsonRequired] int MaximumRecords,
        [property: JsonRequired] int MaximumRequestBytes, [property: JsonRequired] int MaximumOutcomeBytes);
    private sealed record Change([property: JsonRequired] bool Changed, [property: JsonRequired] BrowserAgentDecisionPolicyPendingWire? Record);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record Summary([property: JsonRequired] string AgentId, [property: JsonRequired] string OperationId,
        [property: JsonRequired] AgentDecisionPolicyPendingStage Stage);
}
