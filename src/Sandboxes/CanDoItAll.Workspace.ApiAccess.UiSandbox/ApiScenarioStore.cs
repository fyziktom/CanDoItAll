using System.Collections.Immutable;
using CanDoItAll.Modules.Workspace.ApiAccess.Contracts;

namespace CanDoItAll.Workspace.ApiAccess.UiSandbox;

public sealed class ApiScenarioStore : IApiAccessConfigurationOwner, IApiTokenOwner, IApiAccountOwner {
    public const string ReadScope = "fixture.read";
    public const string WriteScope = "fixture.write";
    public const string MachineScope = "fixture.machine";
    private const int Capacity = 128;
    private readonly Dictionary<ApiScenarioOperation, Queue<ApiScenarioGate>> gates = [];
    private readonly Dictionary<ApiScenarioOperation, ApiScenarioFault> faults = [];
    private readonly Dictionary<Guid, ApiAccountMetadata> accounts = [];
    private readonly Dictionary<Guid, ApiTokenMetadata> tokens = [];
    private readonly ApiScenario scenario;

    public ApiScenarioStore(ApiScenario scenario = ApiScenario.Representative) {
        this.scenario = scenario;
        var count = scenario == ApiScenario.Empty ? 0 : scenario == ApiScenario.MultiplePages ? 61 : 3;
        var now = DateTimeOffset.UtcNow;
        for (var index = 0; index < count; index++) {
            var account = new ApiAccountMetadata(Guid.NewGuid(), $"fixture-{index:D2}", $"Example account {index:D2}", index != 2,
                index == 0 ? [] : [ReadScope], 1, now, now);
            accounts.Add(account.Id, account);
            var token = new ApiTokenMetadata(Guid.NewGuid(), $"fixture-{index:D2}", $"Example machine {index:D2}", now.AddMinutes(-index),
                index == 1 ? now.AddMinutes(-1) : now.AddHours(1), [ReadScope], index == 2 ? now : null, ApiTokenCategory.Machine);
            tokens.Add(token.Id, token);
        }
    }

    public event Action? Changed;
    public Guid Identity { get; } = Guid.NewGuid();
    public bool Allowed { get; set; } = true;
    public int DurableWrites { get; private set; }
    public int PendingWrites { get; private set; }
    public IReadOnlyList<ApiAccountMetadata> Accounts => accounts.Values.ToArray();
    public IReadOnlyList<ApiTokenMetadata> Tokens => tokens.Values.ToArray();
    public Dictionary<ApiScenarioOperation, int> Calls { get; } = [];

    public ApiScenarioGate HoldNext(ApiScenarioOperation operation) {
        if (gates.Values.Sum(queue => queue.Count) >= 16) {
            throw new InvalidOperationException("Release queued scenario operations before holding more.");
        }
        if (!gates.TryGetValue(operation, out var queue)) {
            gates.Add(operation, queue = []);
        }
        var gate = new ApiScenarioGate();
        queue.Enqueue(gate);
        return gate;
    }
    public void FaultNext(ApiScenarioOperation operation, ApiScenarioFault fault) => faults[operation] = fault;

    public async Task<ApiAccessConfiguration> ReadAsync(CancellationToken cancellationToken) {
        var fault = TakeFault(ApiScenarioOperation.Status);
        await WaitAsync(ApiScenarioOperation.Status, cancellationToken);
        if (scenario == ApiScenario.StatusUnavailable || fault == ApiScenarioFault.Unavailable) {
            throw new IOException("Synthetic status unavailable.");
        }
        return new(true, true, true, scenario != ApiScenario.AuthorizationOff, scenario != ApiScenario.MissingSigningKey,
            "Non-authenticating fixture", "UI scenarios only", 30, 60, true, true, 30, "fixture-admin", [
                new(ReadScope, "Read fixture data", "Harmless scenario vocabulary; grants no real access.", "fixture", true, true, false),
                new(WriteScope, "Change fixture data", "Mutates this bounded in-memory scenario only.", "fixture", true, true, true),
                new(MachineScope, "Fixture machine capability", "Machine-only fixture vocabulary.", "fixture", false, true, true)
            ], ReadScope);
    }

    public async ValueTask<bool> CanManageAsync(CancellationToken cancellationToken) {
        var fault = TakeFault(ApiScenarioOperation.Access);
        await WaitAsync(ApiScenarioOperation.Access, cancellationToken);
        if (scenario == ApiScenario.AccessUnavailable || fault == ApiScenarioFault.Unavailable) {
            throw new IOException("Synthetic access check unavailable.");
        }
        return Allowed && scenario != ApiScenario.Denied && fault != ApiScenarioFault.Denied;
    }

    public async Task<ApiTokenDisclosure> IssueAsync(ApiTokenIntent intent, CancellationToken cancellationToken) {
        var fault = TakeFault(ApiScenarioOperation.IssueToken);
        if (await RefusalAsync(fault, cancellationToken) is { } refused) {
            return new(refused);
        }
        if (string.IsNullOrWhiteSpace(intent.Subject) || intent.Subject.Trim().Length > 128 || intent.DisplayName.Length > 128 ||
            intent.LifetimeMinutes is < 1 or > 60 || ParseScopes(intent.ScopeText, false) is not { Length: > 0 } scopes || tokens.Count >= Capacity) {
            return new(new(ApiWriteState.Refused, ApiFailure.Invalid));
        }
        var now = DateTimeOffset.UtcNow;
        var token = new ApiTokenMetadata(Guid.NewGuid(), intent.Subject.Trim(), intent.DisplayName.Trim(), now,
            now.AddMinutes(intent.LifetimeMinutes ?? 30), scopes, null, ApiTokenCategory.Machine);
        var failure = await WriteAsync(ApiScenarioOperation.IssueToken, fault, () => {
            if (tokens.Count >= Capacity) {
                return ApiFailure.Unavailable;
            }
            tokens.Add(token.Id, token);
            return ApiFailure.None;
        });
        if (failure != ApiFailure.None) {
            return new(new(ApiWriteState.Refused, failure));
        }
        var outcome = Result(fault, token.Id);
        return new(outcome, outcome.IsCommitted ? token : null, outcome.IsCommitted ? $"NOT-A-CREDENTIAL-{Guid.NewGuid():N}" : null);
    }

    async Task<ApiPage<ApiTokenMetadata>> IApiTokenOwner.SearchAsync(ApiPageQuery query, CancellationToken cancellationToken) {
        await ReadAsync(ApiScenarioOperation.TokenSearch, cancellationToken);
        var matches = tokens.Values.Where(token => Match(query.Search, token.DisplayName, token.Subject, token.Id.ToString(), string.Join(' ', token.Scopes)))
            .OrderByDescending(token => token.IssuedAtUtc).ThenBy(token => token.Id).ToArray();
        return new(matches.Skip(query.Offset).Take(ApiPageQuery.PageSize).ToImmutableArray(), matches.Length);
    }
    async Task<ApiTokenMetadata?> IApiTokenOwner.ObserveAsync(Guid id, CancellationToken cancellationToken) =>
        await ReadAsync(ApiScenarioOperation.TokenObserve, cancellationToken) == ApiScenarioFault.Missing ? null : tokens.GetValueOrDefault(id);

    public async Task<ApiWriteResult> ApplyAsync(ApiTokenAction action, CancellationToken cancellationToken) {
        var operation = action.Action == ApiWriteAction.RevokeToken ? ApiScenarioOperation.RevokeToken : ApiScenarioOperation.DeleteToken;
        var fault = TakeFault(operation);
        if (await RefusalAsync(fault, cancellationToken) is { } refused) {
            return refused with { Identity = action.Id };
        }
        if (action.Kind != ApiTokenCategory.Machine || action.Action is not (ApiWriteAction.RevokeToken or ApiWriteAction.DeleteToken)) {
            return new(ApiWriteState.Refused, ApiFailure.Invalid, action.Id);
        }
        if (!tokens.TryGetValue(action.Id, out var token)) {
            return new(ApiWriteState.Refused, ApiFailure.Missing, action.Id);
        }
        var failure = await WriteAsync(operation, fault, () => {
            if (!tokens.TryGetValue(action.Id, out var current)) {
                return ApiFailure.Missing;
            }
            if (action.Action == ApiWriteAction.DeleteToken) {
                tokens.Remove(action.Id);
            } else {
                tokens[action.Id] = current with { RevokedAtUtc = DateTimeOffset.UtcNow };
            }
            return ApiFailure.None;
        });
        return failure == ApiFailure.None ? Result(fault, action.Id) : new(ApiWriteState.Refused, failure, action.Id);
    }

    async Task<ApiPage<ApiAccountMetadata>> IApiAccountOwner.SearchAsync(ApiPageQuery query, CancellationToken cancellationToken) {
        await ReadAsync(ApiScenarioOperation.AccountSearch, cancellationToken);
        var matches = accounts.Values.Where(account => Match(query.Search, account.UserName, account.DisplayName))
            .OrderBy(account => account.UserName, StringComparer.OrdinalIgnoreCase).ToArray();
        return new(matches.Skip(query.Offset).Take(ApiPageQuery.PageSize).ToImmutableArray(), matches.Length);
    }
    async Task<ApiAccountMetadata?> IApiAccountOwner.ObserveAsync(Guid id, CancellationToken cancellationToken) =>
        await ReadAsync(ApiScenarioOperation.AccountObserve, cancellationToken) == ApiScenarioFault.Missing ? null : accounts.GetValueOrDefault(id);

    public async Task<ApiAccountWriteResult> ApplyAsync(ApiAccountIntent intent, CancellationToken cancellationToken) {
        string? password = intent.TakePassword();
        try {
            var operation = intent.Action switch {
                ApiWriteAction.CreateAccount => ApiScenarioOperation.CreateAccount,
                ApiWriteAction.UpdateAccount => ApiScenarioOperation.UpdateAccount,
                ApiWriteAction.ResetPassword => ApiScenarioOperation.ResetPassword,
                ApiWriteAction.DeleteAccount => ApiScenarioOperation.DeleteAccount,
                _ => throw new ArgumentException("Unsupported scenario account operation.")
            };
            var fault = TakeFault(operation);
            if (await RefusalAsync(fault, cancellationToken) is { } refused) {
                return new(refused with { Identity = intent.Id });
            }
            if (intent.Action is ApiWriteAction.CreateAccount or ApiWriteAction.ResetPassword && password.Length is < 12 or > 256) {
                return new(new(ApiWriteState.Refused, ApiFailure.Invalid));
            }
            ApiAccountMetadata? current = null;
            if (intent.Action != ApiWriteAction.CreateAccount) {
                if (intent.Id is not { } id || !accounts.TryGetValue(id, out current)) {
                    return new(new(ApiWriteState.Refused, ApiFailure.Missing, intent.Id));
                }
                if (current.Version != intent.ExpectedVersion) {
                    return new(new(ApiWriteState.Refused, ApiFailure.Conflict, intent.Id));
                }
            }
            var scopes = ParseScopes(intent.ScopeText, true);
            if (intent.Action is ApiWriteAction.CreateAccount or ApiWriteAction.UpdateAccount &&
                (scopes is null || intent.UserName.Length is < 1 or > 64 || intent.UserName.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '_' or '-' or '@')) ||
                intent.DisplayName.Length is < 1 or > 128 || intent.UserName.Equals("fixture-admin", StringComparison.OrdinalIgnoreCase))) {
                return new(new(ApiWriteState.Refused, ApiFailure.Invalid, intent.Id));
            }
            if (accounts.Values.Any(account => account.Id != intent.Id && account.UserName.Equals(intent.UserName, StringComparison.OrdinalIgnoreCase))) {
                return new(new(ApiWriteState.Refused, ApiFailure.Conflict, intent.Id));
            }
            if (intent.Action == ApiWriteAction.CreateAccount && accounts.Count >= Capacity) {
                return new(new(ApiWriteState.Refused, ApiFailure.Unavailable));
            }
            var now = DateTimeOffset.UtcNow;
            var updated = intent.Action == ApiWriteAction.ResetPassword ? current! with { Version = current.Version + 1, UpdatedAtUtc = now }
                : new ApiAccountMetadata(current?.Id ?? Guid.NewGuid(), intent.UserName, intent.DisplayName, intent.Enabled, scopes ?? [],
                    (current?.Version ?? 0) + 1, current?.CreatedAtUtc ?? now, now);
            var failure = await WriteAsync(operation, fault, () => {
                if (intent.Action != ApiWriteAction.CreateAccount) {
                    if (!accounts.TryGetValue(updated.Id, out var latest)) {
                        return ApiFailure.Missing;
                    }
                    if (latest.Version != intent.ExpectedVersion) {
                        return ApiFailure.Conflict;
                    }
                } else if (accounts.Count >= Capacity) {
                    return ApiFailure.Unavailable;
                }
                if (accounts.Values.Any(account => account.Id != intent.Id && account.UserName.Equals(intent.UserName, StringComparison.OrdinalIgnoreCase))) {
                    return ApiFailure.Conflict;
                }
                if (intent.Action == ApiWriteAction.DeleteAccount) {
                    accounts.Remove(updated.Id);
                } else {
                    accounts[updated.Id] = updated;
                }
                return ApiFailure.None;
            });
            if (failure != ApiFailure.None) {
                return new(new(ApiWriteState.Refused, failure, intent.Id));
            }
            var outcome = Result(fault, updated.Id, intent.Action == ApiWriteAction.DeleteAccount ? intent.ExpectedVersion : updated.Version);
            return new(outcome, outcome.IsCommitted && intent.Action != ApiWriteAction.DeleteAccount ? updated : null);
        } finally {
            password = null;
            intent.Dispose();
        }
    }

    public void ChangeAccountsExternally() {
        foreach (var account in accounts.Values.ToArray()) {
            accounts[account.Id] = account with { Version = account.Version + 1, DisplayName = "Changed externally", UpdatedAtUtc = DateTimeOffset.UtcNow };
        }
        Changed?.Invoke();
    }

    private async Task<ApiScenarioFault> ReadAsync(ApiScenarioOperation operation, CancellationToken cancellationToken) {
        if (!await CanManageAsync(cancellationToken)) {
            throw new UnauthorizedAccessException();
        }
        var fault = TakeFault(operation);
        await WaitAsync(operation, cancellationToken);
        if (fault == ApiScenarioFault.Denied) {
            throw new UnauthorizedAccessException();
        }
        if (scenario == ApiScenario.ReadUnavailable || fault == ApiScenarioFault.Unavailable) {
            throw new IOException("Synthetic read unavailable.");
        }
        return fault;
    }
    private async Task<ApiWriteResult?> RefusalAsync(ApiScenarioFault fault, CancellationToken cancellationToken) {
        try {
            if (!await CanManageAsync(cancellationToken)) {
                return new(ApiWriteState.Refused, ApiFailure.Denied);
            }
            cancellationToken.ThrowIfCancellationRequested();
        } catch (OperationCanceledException) {
            return new(ApiWriteState.Refused, ApiFailure.Denied);
        } catch (Exception) {
            return new(ApiWriteState.Refused, ApiFailure.Unavailable);
        }
        return fault switch {
            ApiScenarioFault.Denied => new(ApiWriteState.Refused, ApiFailure.Denied),
            ApiScenarioFault.Unavailable => new(ApiWriteState.Refused, ApiFailure.Unavailable),
            ApiScenarioFault.Invalid => new(ApiWriteState.Refused, ApiFailure.Invalid),
            ApiScenarioFault.Conflict => new(ApiWriteState.Refused, ApiFailure.Conflict),
            ApiScenarioFault.Missing => new(ApiWriteState.Refused, ApiFailure.Missing),
            _ => null
        };
    }
    private async Task<ApiFailure> WriteAsync(ApiScenarioOperation operation, ApiScenarioFault fault, Func<ApiFailure> mutate) {
        PendingWrites++;
        Changed?.Invoke();
        try {
            await WaitAsync(operation, CancellationToken.None);
            if (fault != ApiScenarioFault.UnknownWithoutCommit) {
                var failure = mutate();
                if (failure != ApiFailure.None) {
                    return failure;
                }
                DurableWrites++;
            }
            return ApiFailure.None;
        } finally {
            PendingWrites--;
            Changed?.Invoke();
        }
    }
    private Task WaitAsync(ApiScenarioOperation operation, CancellationToken cancellationToken) {
        Calls[operation] = Calls.GetValueOrDefault(operation) + 1;
        return gates.TryGetValue(operation, out var queue) && queue.TryDequeue(out var gate) ? gate.WaitAsync(cancellationToken) : Task.CompletedTask;
    }
    private ApiScenarioFault TakeFault(ApiScenarioOperation operation) => faults.Remove(operation, out var fault) ? fault : ApiScenarioFault.None;
    private static ApiWriteResult Result(ApiScenarioFault fault, Guid id, long? version = null) => fault switch {
        ApiScenarioFault.UnknownAfterCommit or ApiScenarioFault.UnknownWithoutCommit => new(ApiWriteState.Unknown, ApiFailure.UnknownAcknowledgement, id),
        ApiScenarioFault.CommittedWarning => new(ApiWriteState.CommittedWithWarning, ApiFailure.Diagnostic, id, version),
        _ => new(ApiWriteState.Committed, Identity: id, Version: version)
    };
    private static bool Match(string search, params string[] values) => values.Any(value => value.Contains(search, StringComparison.OrdinalIgnoreCase));
    private static ImmutableArray<string>? ParseScopes(string text, bool forUser) {
        var values = text.Split([' ', ',', ';', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToImmutableArray();
        return values.All(value => value is ReadScope or WriteScope || !forUser && value == MachineScope) ? values : null;
    }
}
