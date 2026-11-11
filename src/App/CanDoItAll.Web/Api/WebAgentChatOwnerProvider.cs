using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Api;

internal sealed class WebAgentChatOwnerProvider : IAgentChatOwnerProvider, IDisposable {
    private static readonly AgentChatOwnerId LocalWorkspaceOwner = CreateOwner(OwnerKind.LocalWorkspace, null, "local-workspace");
    private readonly WebCurrentPrincipalResolver principals;
    private readonly AuthenticationStateProvider? authentication;
    private readonly ILogger<WebAgentChatOwnerProvider> logger;
    private readonly bool useLocalWorkspaceOwner;
    private int disposed;

    public WebAgentChatOwnerProvider(WebCurrentPrincipalResolver principals, IOptions<ApiAccessOptions> options,
        ILogger<WebAgentChatOwnerProvider> logger, AuthenticationStateProvider? authentication = null) {
        this.principals = principals;
        this.authentication = authentication;
        this.logger = logger;
        useLocalWorkspaceOwner = !options.Value.Authorization.Enabled && !options.Value.UserAuthentication.Enabled;
        if (authentication is not null) {
            authentication.AuthenticationStateChanged += HandleAuthenticationChanged;
        }
    }

    public event EventHandler? Changed;

    public async ValueTask<AgentChatOwnerId> GetOwnerAsync(CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (useLocalWorkspaceOwner) {
            return LocalWorkspaceOwner;
        }
        var principal = await principals.ResolveAsync(cancellationToken);
        return CreateOwner(principal.IsLocalOperator ? OwnerKind.LocalOperator : OwnerKind.Authenticated,
            principal.Issuer, principal.Subject);
    }

    private static AgentChatOwnerId CreateOwner(OwnerKind kind, string? issuer, string subject) {
        var identity = JsonSerializer.SerializeToUtf8Bytes(new {
            Domain = "agent-chat-owner/v1",
            Kind = kind,
            Issuer = issuer,
            Subject = subject
        });
        return new(Convert.ToHexStringLower(SHA256.HashData(identity)));
    }

    private void HandleAuthenticationChanged(Task<AuthenticationState> state) {
        if (Volatile.Read(ref disposed) != 0) {
            return;
        }
        foreach (EventHandler subscriber in Changed?.GetInvocationList() ?? []) {
            try {
                subscriber(this, EventArgs.Empty);
            } catch (Exception exception) {
                logger.LogWarning(exception, "An Agent chat owner observer failed after authentication changed.");
            }
        }
    }

    public void Dispose() {
        if (Interlocked.Exchange(ref disposed, 1) == 0 && authentication is not null) {
            authentication.AuthenticationStateChanged -= HandleAuthenticationChanged;
        }
    }

    private enum OwnerKind { Authenticated, LocalOperator, LocalWorkspace }
}
