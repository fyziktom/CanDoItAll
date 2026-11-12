using CanDoItAll.Editing;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.AspNetCore.Components.Authorization;

namespace CanDoItAll.Web.Api;

internal sealed class WebPromptGalleryUiAccess : IPromptGalleryUiAccess, IDisposable {
    private readonly WebCurrentPrincipalResolver principals;
    private readonly PromptGalleryChanges changes;
    private readonly AuthenticationStateProvider authentication;
    private readonly TimeProvider clock;
    private readonly Dictionary<PromptGalleryUiGrant, ITimer?> grants = [];
    private readonly object sync = new();

    public WebPromptGalleryUiAccess(WebCurrentPrincipalResolver principals, PromptGalleryChanges changes,
        AuthenticationStateProvider authentication, TimeProvider clock) {
        this.principals = principals;
        this.changes = changes;
        this.authentication = authentication;
        this.clock = clock;
        authentication.AuthenticationStateChanged += OnAuthenticationChanged;
    }

    public async Task<PromptGalleryUiGrant> OpenAsync(EditingScope scope, CancellationToken cancellationToken) {
        if (scope != changes.Scope) {
            throw new UnauthorizedAccessException("The Gallery dataset has changed.");
        }
        var principal = await principals.ResolveAsync(cancellationToken);
        await DemandAsync(principal, write: false, cancellationToken);
        var grant = new PromptGalleryUiGrant(scope, principal.Stamp);
        ITimer? expiry = null;
        if (long.TryParse(principal.Principal.FindFirst("exp")?.Value, out var seconds)) {
            var remaining = DateTimeOffset.FromUnixTimeSeconds(seconds) - clock.GetUtcNow();
            expiry = clock.CreateTimer(_ => grant.Revoke(), null, remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }
        lock (sync) {
            grants.Add(grant, expiry);
        }
        grant.Disposed += () => {
            lock (sync) {
                if (grants.Remove(grant, out var timer)) {
                    timer?.Dispose();
                }
            }
        };
        return grant;
    }

    public async ValueTask ValidateAsync(PromptGalleryUiGrant grant, bool write, CancellationToken cancellationToken) {
        try {
            grant.Lifetime.ThrowIfCancellationRequested();
            if (grant.Scope != changes.Scope) {
                throw new UnauthorizedAccessException("The Gallery dataset has changed.");
            }
            var principal = await principals.ResolveAsync(cancellationToken);
            if (principal.Stamp != grant.PrincipalStamp) {
                throw new UnauthorizedAccessException("Gallery access has changed. Reopen this editor.");
            }
            await DemandAsync(principal, write, cancellationToken);
            grant.Lifetime.ThrowIfCancellationRequested();
        } catch (Exception exception) {
            grant.Revoke();
            throw new UnauthorizedAccessException("Gallery access changed. Reopen with current access.", exception);
        }
    }

    private async Task DemandAsync(WebCurrentPrincipal principal, bool write, CancellationToken cancellationToken) {
        if (principal.IsLocalOperator) {
            return;
        }
        var scope = write ? ApiAccessScopeNames.WritePrompts : ApiAccessScopeNames.ReadPrompts;
        await principals.RequireScopeAsync(principal, scope, scope, cancellationToken);
    }

    private void OnAuthenticationChanged(Task<AuthenticationState> state) {
        PromptGalleryUiGrant[] current;
        lock (sync) {
            current = [.. grants.Keys];
        }
        foreach (var grant in current) {
            _ = RevalidateAsync(grant);
        }
    }

    private async Task RevalidateAsync(PromptGalleryUiGrant grant) {
        try {
            await ValidateAsync(grant, write: false, CancellationToken.None);
        } catch (UnauthorizedAccessException) {
        }
    }

    public void Dispose() {
        authentication.AuthenticationStateChanged -= OnAuthenticationChanged;
        KeyValuePair<PromptGalleryUiGrant, ITimer?>[] current;
        lock (sync) {
            current = [.. grants];
            grants.Clear();
        }
        foreach (var (grant, timer) in current) {
            timer?.Dispose();
            grant.Revoke();
        }
    }
}
