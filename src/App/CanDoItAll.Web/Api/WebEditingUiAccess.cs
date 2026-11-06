using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.AspNetCore.Components.Authorization;

namespace CanDoItAll.Web.Api;

internal sealed class WebEditingUiAccess : IEditingUiAccess, IDisposable {
    private readonly WebCurrentPrincipalResolver principals;
    private readonly ICanonicalRuntimeDatabase database;
    private readonly AuthenticationStateProvider authentication;
    private readonly TimeProvider clock;
    private readonly IDatabaseRuntimeState runtimeState;
    private readonly IDatabaseSwitchNotificationService databaseChanges;
    private readonly Dictionary<EditingUiGrant, ITimer?> grants = [];
    private readonly object sync = new();

    public WebEditingUiAccess(WebCurrentPrincipalResolver principals, ICanonicalRuntimeDatabase database,
        AuthenticationStateProvider authentication, TimeProvider clock, IDatabaseRuntimeState runtimeState,
        IDatabaseSwitchNotificationService databaseChanges) {
        this.principals = principals;
        this.database = database;
        this.authentication = authentication;
        this.clock = clock;
        this.runtimeState = runtimeState;
        this.databaseChanges = databaseChanges;
        authentication.AuthenticationStateChanged += OnAuthenticationChanged;
        databaseChanges.Changed += OnDatabaseChanged;
    }

    public async Task<EditingUiGrant> OpenAsync(EditingTopic topic, CancellationToken cancellationToken) {
        if (topic.Scope != CurrentScope || !IsSupported(topic.Owner)) {
            throw new UnauthorizedAccessException("The editing dataset or owner has changed.");
        }
        var principal = await ResolveAsync(cancellationToken);
        await DemandAsync(principal, topic, write: false, cancellationToken);
        var grant = new EditingUiGrant(topic, principal.Stamp, principal.Subject);
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
        try {
            if (topic.Scope != CurrentScope) {
                throw new UnauthorizedAccessException("The editing dataset changed while opening access.");
            }
            return grant;
        } catch {
            grant.Dispose();
            throw;
        }
    }

    public async ValueTask ValidateAsync(EditingUiGrant grant, bool write, CancellationToken cancellationToken) {
        try {
            grant.Lifetime.ThrowIfCancellationRequested();
            if (grant.Topic.Scope != CurrentScope || !IsSupported(grant.Topic.Owner)) {
                throw new UnauthorizedAccessException("The editing dataset or owner has changed.");
            }
            var principal = await ResolveAsync(cancellationToken);
            if (principal.Stamp != grant.PrincipalStamp) {
                throw new UnauthorizedAccessException("Editing access has changed. Reopen this editor.");
            }
            await DemandAsync(principal, grant.Topic, write, cancellationToken);
            grant.Lifetime.ThrowIfCancellationRequested();
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) {
            grant.Revoke();
            throw new UnauthorizedAccessException("Editing access changed. Reopen with current access.", exception);
        }
    }

    private async Task DemandAsync(WebCurrentPrincipal principal, EditingTopic topic, bool write, CancellationToken cancellationToken) {
        if (principal.IsLocalOperator) {
            return;
        }
        try {
            var project = topic.Owner is EditingOwner.Project or EditingOwner.ProjectStructure;
            var (readScope, writeScope) = topic.Owner switch {
                EditingOwner.Project => (ApiAccessScopeNames.ReadProjects, ApiAccessScopeNames.WriteProjects),
                EditingOwner.ProjectStructure => (ApiAccessScopeNames.ReadProjects, ApiAccessScopeNames.WriteProjectStructure),
                EditingOwner.WorkflowDefinition or EditingOwner.WorkflowComponent => (ApiAccessScopeNames.ReadWorkflows, ApiAccessScopeNames.WriteWorkflows),
                EditingOwner.ProcessAuthoring => (ApiAccessScopeNames.ReadProcesses, ApiAccessScopeNames.WriteProcesses),
                EditingOwner.AgentDefinition or EditingOwner.AgentTeam or EditingOwner.AgentCapability or EditingOwner.ProviderProfile or EditingOwner.SharedProviderSource or EditingOwner.AgentVoiceSettings or EditingOwner.FloatingChatSettings => (ApiAccessScopeNames.ReadAgents, ApiAccessScopeNames.WriteAgents),
                EditingOwner.AgentVoicePreview => (ApiAccessScopeNames.ReadAgents, ApiAccessScopeNames.ExecuteAgents),
                EditingOwner.SimpleChatDefinition or EditingOwner.SimpleChatConversation => (ApiAccessScopeNames.ReadLlmChats, ApiAccessScopeNames.ManageLlmChats),
                _ => (ApiAccessScopeNames.ReadCrmHr, ApiAccessScopeNames.WriteCrmHr)
            };
            await principals.RequireScopeAsync(principal, readScope, readScope, cancellationToken);
            if (write || (!project && topic.Owner is not (EditingOwner.WorkflowDefinition or EditingOwner.WorkflowComponent or EditingOwner.ProcessAuthoring or EditingOwner.AgentDefinition or EditingOwner.AgentTeam or EditingOwner.AgentCapability or EditingOwner.ProviderProfile or EditingOwner.SharedProviderSource or EditingOwner.SimpleChatConversation or EditingOwner.AgentVoiceSettings or EditingOwner.FloatingChatSettings or EditingOwner.AgentVoicePreview) && topic.ResourceId.HasValue)) {
                await principals.RequireScopeAsync(principal, writeScope, writeScope, cancellationToken);
            }
        } catch (WebCurrentPrincipalDeniedException exception) {
            throw new UnauthorizedAccessException("Current native editing access is required.", exception);
        }
    }

    private async Task<WebCurrentPrincipal> ResolveAsync(CancellationToken cancellationToken) {
        try {
            return await principals.ResolveAsync(cancellationToken);
        } catch (WebCurrentPrincipalDeniedException exception) {
            throw new UnauthorizedAccessException("Current editing authority is required.", exception);
        }
    }

    private EditingScope CurrentScope {
        get {
            var active = runtimeState.GetSnapshot();
            if (active.ActiveProfileId != database.Profile.Profile.Id || active.Generation != database.Generation ||
                !string.Equals(active.ActiveFingerprint, database.Profile.Profile.Runtime.Fingerprint, StringComparison.Ordinal)) {
                throw new UnauthorizedAccessException("The original web runtime dataset or activation is no longer current.");
            }
            return new(EditingScopeKind.Dataset, database.Profile.Profile.Id, database.Generation);
        }
    }
    private static bool IsSupported(EditingOwner owner) => owner is EditingOwner.AgentVoiceSettings or EditingOwner.FloatingChatSettings or EditingOwner.AgentVoicePreview or EditingOwner.SimpleChatConversation or EditingOwner.SimpleChatDefinition or EditingOwner.ProviderProfile or EditingOwner.SharedProviderSource or EditingOwner.AgentDefinition or EditingOwner.AgentTeam or EditingOwner.AgentCapability or EditingOwner.ProcessAuthoring or EditingOwner.WorkflowDefinition or EditingOwner.WorkflowComponent or EditingOwner.Project or EditingOwner.ProjectStructure or EditingOwner.CrmHrParty or EditingOwner.CrmAccountProfile or EditingOwner.CrmHrAffiliations or EditingOwner.CrmHrRelationships or EditingOwner.CrmOpportunity or EditingOwner.CrmAccountRecords or EditingOwner.CrmWorkforceProfile or EditingOwner.CrmWorkforceRecords or EditingOwner.CrmSkillCatalog or EditingOwner.CrmRecruitmentApplication or EditingOwner.CrmRecruitmentInterviews or EditingOwner.CrmRecruitmentTasks or EditingOwner.CrmAiStewardProfile or EditingOwner.CrmStaffingRequest or EditingOwner.CrmStaffingRecords or EditingOwner.ProjectAssignment or EditingOwner.ProjectAssignmentRecords;

    private void OnAuthenticationChanged(Task<AuthenticationState> state) {
        EditingUiGrant[] current;
        lock (sync) {
            current = [.. grants.Keys];
        }
        foreach (var grant in current) {
            _ = RevalidateAsync(grant);
        }
    }

    private void OnDatabaseChanged(object? sender, DatabaseProfileChangedNotification notification) {
        EditingUiGrant[] current;
        lock (sync) {
            current = [.. grants.Keys];
        }
        foreach (var grant in current) {
            if (grant.Topic.Scope.Id != notification.CurrentProfileId || grant.Topic.Scope.ActivationGeneration != notification.Generation) {
                grant.Revoke();
            }
        }
    }

    private async Task RevalidateAsync(EditingUiGrant grant) {
        try {
            await ValidateAsync(grant, write: false, CancellationToken.None);
        } catch (UnauthorizedAccessException) {
        }
    }

    public void Dispose() {
        authentication.AuthenticationStateChanged -= OnAuthenticationChanged;
        databaseChanges.Changed -= OnDatabaseChanged;
        KeyValuePair<EditingUiGrant, ITimer?>[] current;
        lock (sync) {
            current = [.. grants];
            grants.Clear();
        }
        foreach (var (grant, timer) in current) {
            timer?.Dispose();
            grant.Dispose();
        }
    }
}
