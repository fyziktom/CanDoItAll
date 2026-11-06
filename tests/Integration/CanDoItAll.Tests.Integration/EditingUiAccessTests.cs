using System.Globalization;
using System.Security.Claims;
using CanDoItAll.Editing;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Api;
using CanDoItAll.Web.Composition;
using CanDoItAll.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration.Api;

public sealed class EditingUiAccessTests {
    [Theory]
    [InlineData(LlmChatConversationAccessKind.Execute, false)]
    [InlineData(LlmChatConversationAccessKind.Execute, true)]
    [InlineData(LlmChatConversationAccessKind.Metadata, false)]
    [InlineData(LlmChatConversationAccessKind.Metadata, true)]
    [InlineData(LlmChatConversationAccessKind.Recovery, false)]
    [InlineData(LlmChatConversationAccessKind.Recovery, true)]
    public async Task Original_conversation_opening_keeps_execute_and_manage_independent_and_rejects_actor_substitution(
        LlmChatConversationAccessKind kind, bool canWrite) {
        await using var host = await ApiTestHost.CreateAsync(true, useInMemoryDatabase: true);
        using var scope = host.App.Services.CreateScope();
        var services = scope.ServiceProvider;
        var source = new PrincipalSource();
        var options = services.GetRequiredService<IOptions<ApiAccessOptions>>().Value;
        var identity = new ClaimsIdentity([
            new Claim("sub", "conversation-original-scope"), new Claim("iss", options.Authorization.Issuer),
            new Claim("exp", DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            new Claim("scope", ApiAccessScopeNames.ReadLlmChats)
        ], "Bearer");
        if (canWrite) {
            identity.AddClaim(new("scope", kind == LlmChatConversationAccessKind.Execute
                ? ApiAccessScopeNames.ExecuteLlmChats : ApiAccessScopeNames.ManageLlmChats));
        }
        source.Set(new(identity));
        using var access = Create(services, source);
        var factory = new WebLlmChatEditorAccess(services.GetRequiredService<EditingChangeFeed>(), access, CreateResolver(services, source),
            services.GetRequiredService<ICanonicalRuntimeDatabase>(), services.GetRequiredService<IDatabaseRuntimeState>(), NullLogger<EditingObservation>.Instance);
        var id = Guid.NewGuid();
        await using var opening = await factory.OpenAsync(id, kind, Guid.NewGuid(), () => Task.CompletedTask, () => Task.CompletedTask, default);
        await opening.AuthorizeAsync(false, default);
        if (canWrite) {
            using var admission = opening.EnterWrite();
            await LlmChatWriteAdmission.RequireTargetAsync(id, default,
                kind == LlmChatConversationAccessKind.Metadata ? LlmChatWriteResource.ConversationMetadata : LlmChatWriteResource.ConversationOperation);
            await LlmChatWriteAdmission.RequireCommitAsync(default);
        } else {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => opening.AuthorizeAsync(true, default).AsTask());
        }
        source.Set(Local());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => opening.AuthorizeAsync(false, default).AsTask());
    }

    [Theory]
    [InlineData(EditingOwner.AgentDefinition, false)]
    [InlineData(EditingOwner.AgentDefinition, true)]
    [InlineData(EditingOwner.AgentTeam, false)]
    [InlineData(EditingOwner.AgentTeam, true)]
    [InlineData(EditingOwner.AgentCapability, false)]
    [InlineData(EditingOwner.AgentCapability, true)]
    [InlineData(EditingOwner.ProcessAuthoring, false)]
    [InlineData(EditingOwner.ProcessAuthoring, true)]
    [InlineData(EditingOwner.SimpleChatConversation, false)]
    [InlineData(EditingOwner.SimpleChatConversation, true)]
    [InlineData(EditingOwner.AgentVoiceSettings, false)]
    [InlineData(EditingOwner.AgentVoiceSettings, true)]
    [InlineData(EditingOwner.FloatingChatSettings, false)]
    [InlineData(EditingOwner.FloatingChatSettings, true)]
    [InlineData(EditingOwner.AgentVoicePreview, false)]
    [InlineData(EditingOwner.AgentVoicePreview, true)]
    public async Task Configuration_observation_demands_its_own_native_read_and_write_scopes(EditingOwner owner, bool write) {
        await using var host = await ApiTestHost.CreateAsync(true, useInMemoryDatabase: true);
        using var scope = host.App.Services.CreateScope();
        var source = new PrincipalSource();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<ApiAccessOptions>>().Value;
        var (readScope, writeScope) = owner switch {
            EditingOwner.AgentDefinition or EditingOwner.AgentTeam or EditingOwner.AgentCapability or EditingOwner.AgentVoiceSettings or EditingOwner.FloatingChatSettings => (ApiAccessScopeNames.ReadAgents, ApiAccessScopeNames.WriteAgents),
            EditingOwner.AgentVoicePreview => (ApiAccessScopeNames.ReadAgents, ApiAccessScopeNames.ExecuteAgents),
            EditingOwner.ProcessAuthoring => (ApiAccessScopeNames.ReadProcesses, ApiAccessScopeNames.WriteProcesses),
            EditingOwner.SimpleChatConversation => (ApiAccessScopeNames.ReadLlmChats, ApiAccessScopeNames.ManageLlmChats),
            _ => throw new ArgumentOutOfRangeException(nameof(owner))
        };
        var identity = new ClaimsIdentity([
            new Claim("sub", "configuration-scope-proof"), new Claim("iss", options.Authorization.Issuer),
            new Claim("exp", DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            new Claim("scope", readScope)
        ], "Bearer");
        if (write) {
            identity.AddClaim(new("scope", writeScope));
        }
        source.Set(new(identity));
        using var access = Create(scope.ServiceProvider, source);
        using var grant = await access.OpenAsync(Topic(scope.ServiceProvider) with { Owner = owner, ResourceId = Guid.NewGuid() }, default);
        await access.ValidateAsync(grant, false, default);
        if (write) {
            await access.ValidateAsync(grant, true, default);
        } else {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.ValidateAsync(grant, true, default).AsTask());
        }
    }

    [Theory]
    [InlineData(EditingOwner.AgentDefinition)]
    [InlineData(EditingOwner.AgentTeam)]
    [InlineData(EditingOwner.AgentCapability)]
    [InlineData(EditingOwner.SimpleChatDefinition)]
    [InlineData(EditingOwner.SimpleChatConversation)]
    [InlineData(EditingOwner.AgentVoiceSettings)]
    [InlineData(EditingOwner.FloatingChatSettings)]
    [InlineData(EditingOwner.AgentVoicePreview)]
    public async Task Native_dataset_switch_signal_retires_original_grants_before_pending_editor_work_can_publish(EditingOwner owner) {
        await using var host = await ApiTestHost.CreateAsync(true, useInMemoryDatabase: true);
        using var scope = host.App.Services.CreateScope();
        var source = new PrincipalSource();
        source.Set(Local());
        using var access = Create(scope.ServiceProvider, source);
        var topic = Topic(scope.ServiceProvider) with { Owner = owner, ResourceId = Guid.NewGuid() };
        using var grant = await access.OpenAsync(topic, default);
        var runtime = Assert.IsType<DatabaseRuntimeState>(scope.ServiceProvider.GetRequiredService<IDatabaseRuntimeState>());
        runtime.PublishRestartObserved(runtime.GetSnapshot(), scope.ServiceProvider.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        Assert.True(grant.Lifetime.IsCancellationRequested);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.ValidateAsync(grant, true, default).AsTask());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.OpenAsync(topic, default));
    }

    [Fact]
    public async Task Disposing_the_native_access_scope_disposes_its_grants_without_claiming_an_authority_revocation() {
        await using var host = await ApiTestHost.CreateAsync(true, useInMemoryDatabase: true);
        using var scope = host.App.Services.CreateScope();
        var source = new PrincipalSource();
        source.Set(Local());
        using var access = Create(scope.ServiceProvider, source);
        using var grant = await access.OpenAsync(Topic(scope.ServiceProvider), default);
        var lifetime = grant.Lifetime;

        access.Dispose();

        Assert.True(grant.IsDisposed);
        Assert.True(lifetime.IsCancellationRequested);
    }

    [Fact]
    public async Task Anonymous_wrong_dataset_and_wrong_activation_cannot_open_a_Crm_observation() {
        await using var host = await ApiTestHost.CreateAsync(true, useInMemoryDatabase: true);
        using var scope = host.App.Services.CreateScope();
        var source = new PrincipalSource();
        using var access = Create(scope.ServiceProvider, source);
        var topic = Topic(scope.ServiceProvider);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.OpenAsync(topic, default));
        source.Set(Local());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.OpenAsync(topic with {
            Scope = topic.Scope with { Id = Guid.NewGuid() }
        }, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.OpenAsync(topic with {
            Scope = topic.Scope with { ActivationGeneration = topic.Scope.ActivationGeneration + 1 }
        }, default));
    }

    [Fact]
    public async Task A_stable_local_principal_keeps_its_grant_and_a_principal_change_retires_it() {
        await using var host = await ApiTestHost.CreateAsync(true, useInMemoryDatabase: true);
        using var scope = host.App.Services.CreateScope();
        var source = new PrincipalSource();
        source.Set(Local());
        using var access = Create(scope.ServiceProvider, source);
        using var grant = await access.OpenAsync(Topic(scope.ServiceProvider) with { ResourceId = Guid.NewGuid() }, default);
        source.Set(Local());
        await access.ValidateAsync(grant, write: true, default);
        Assert.Equal(LocalOperatorAuthenticationStateProvider.ActorId, grant.Actor);
        Assert.False(grant.Lifetime.IsCancellationRequested);

        source.Set(new(new ClaimsIdentity()));

        Assert.True(grant.Lifetime.IsCancellationRequested);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.ValidateAsync(grant, write: true, default).AsTask());
    }

    [Theory]
    [InlineData(EditingOwner.CrmHrParty, false)]
    [InlineData(EditingOwner.CrmHrParty, true)]
    [InlineData(EditingOwner.CrmAccountProfile, false)]
    [InlineData(EditingOwner.CrmAccountProfile, true)]
    [InlineData(EditingOwner.CrmHrAffiliations, false)]
    [InlineData(EditingOwner.CrmHrAffiliations, true)]
    [InlineData(EditingOwner.CrmHrRelationships, false)]
    [InlineData(EditingOwner.CrmHrRelationships, true)]
    [InlineData(EditingOwner.CrmOpportunity, false)]
    [InlineData(EditingOwner.CrmOpportunity, true)]
    [InlineData(EditingOwner.CrmAccountRecords, false)]
    [InlineData(EditingOwner.CrmAccountRecords, true)]
    [InlineData(EditingOwner.CrmWorkforceProfile, false)]
    [InlineData(EditingOwner.CrmWorkforceProfile, true)]
    [InlineData(EditingOwner.CrmWorkforceRecords, false)]
    [InlineData(EditingOwner.CrmWorkforceRecords, true)]
    [InlineData(EditingOwner.CrmSkillCatalog, false)]
    [InlineData(EditingOwner.CrmSkillCatalog, true)]
    [InlineData(EditingOwner.CrmRecruitmentApplication, false)]
    [InlineData(EditingOwner.CrmRecruitmentApplication, true)]
    [InlineData(EditingOwner.CrmRecruitmentInterviews, false)]
    [InlineData(EditingOwner.CrmRecruitmentInterviews, true)]
    [InlineData(EditingOwner.CrmRecruitmentTasks, false)]
    [InlineData(EditingOwner.CrmRecruitmentTasks, true)]
    [InlineData(EditingOwner.CrmAiStewardProfile, false)]
    [InlineData(EditingOwner.CrmAiStewardProfile, true)]
    [InlineData(EditingOwner.CrmStaffingRequest, false)]
    [InlineData(EditingOwner.CrmStaffingRequest, true)]
    [InlineData(EditingOwner.CrmStaffingRecords, false)]
    [InlineData(EditingOwner.CrmStaffingRecords, true)]
    [InlineData(EditingOwner.ProjectAssignment, false)]
    [InlineData(EditingOwner.ProjectAssignment, true)]
    [InlineData(EditingOwner.ProjectAssignmentRecords, false)]
    [InlineData(EditingOwner.ProjectAssignmentRecords, true)]
    public async Task Remote_catalog_and_confidential_editor_reads_demand_their_current_native_scopes(EditingOwner owner, bool canWrite) {
        await using var host = await ApiTestHost.CreateAsync(true, useInMemoryDatabase: true);
        using var scope = host.App.Services.CreateScope();
        var source = new PrincipalSource();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<ApiAccessOptions>>().Value;
        var identity = new ClaimsIdentity([
            new Claim("sub", "crm-reader"), new Claim("iss", options.Authorization.Issuer),
            new Claim("exp", DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            new Claim("scope", ApiAccessScopeNames.ReadCrmHr)
        ], "Bearer");
        if (canWrite) {
            identity.AddClaim(new("scope", ApiAccessScopeNames.WriteCrmHr));
        }
        source.Set(new(identity));
        using var access = Create(scope.ServiceProvider, source);
        var topic = Topic(scope.ServiceProvider) with { Owner = owner };
        using var catalog = await access.OpenAsync(topic, default);
        await access.ValidateAsync(catalog, write: false, default);
        if (!canWrite) {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.OpenAsync(topic with { ResourceId = Guid.NewGuid() }, default));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.ValidateAsync(catalog, write: true, default).AsTask());
            Assert.True(catalog.Lifetime.IsCancellationRequested);
            return;
        }
        using var editor = await access.OpenAsync(topic with { ResourceId = Guid.NewGuid() }, default);
        await access.ValidateAsync(editor, write: true, default);
        Assert.Equal("crm-reader", editor.Actor);
        identity.AddClaim(new("auth_rev", "2"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.ValidateAsync(editor, write: true, default).AsTask());

        Assert.True(editor.Lifetime.IsCancellationRequested);
    }

    private static EditingTopic Topic(IServiceProvider services) {
        var database = services.GetRequiredService<ICanonicalRuntimeDatabase>();
        return new(new(EditingScopeKind.Dataset, database.Profile.Profile.Id, database.Generation), EditingOwner.CrmHrParty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Project_observation_uses_native_project_scopes_independently_of_Crm(bool write) {
        await using var host = await ApiTestHost.CreateAsync(true, useInMemoryDatabase: true);
        using var scope = host.App.Services.CreateScope();
        var source = new PrincipalSource();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<ApiAccessOptions>>().Value;
        var identity = new ClaimsIdentity([
            new Claim("sub", "project-reader"), new Claim("iss", options.Authorization.Issuer),
            new Claim("exp", DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            new Claim("scope", ApiAccessScopeNames.ReadProjects)
        ], "Bearer");
        if (write) {
            identity.AddClaim(new("scope", ApiAccessScopeNames.WriteProjects));
        }
        source.Set(new(identity));
        using var access = Create(scope.ServiceProvider, source);
        var topic = Topic(scope.ServiceProvider) with { Owner = EditingOwner.Project };
        using var catalog = await access.OpenAsync(topic, default);
        using var record = await access.OpenAsync(topic with { ResourceId = Guid.NewGuid() }, default);

        await access.ValidateAsync(record, write: false, default);
        if (write) {
            await access.ValidateAsync(record, write: true, default);
        } else {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.ValidateAsync(record, write: true, default).AsTask());
            Assert.True(record.Lifetime.IsCancellationRequested);
        }
        await access.ValidateAsync(catalog, write: false, default);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.OpenAsync(topic with { Owner = EditingOwner.CrmHrParty }, default));
        identity.AddClaim(new("auth_rev", "2"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.ValidateAsync(catalog, write: false, default).AsTask());
        Assert.True(catalog.Lifetime.IsCancellationRequested);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Structure_editing_requires_its_native_write_scope_independently_of_Project_definition_write(bool structureWrite) {
        await using var host = await ApiTestHost.CreateAsync(true, useInMemoryDatabase: true);
        using var scope = host.App.Services.CreateScope();
        var source = new PrincipalSource();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<ApiAccessOptions>>().Value;
        source.Set(new(new ClaimsIdentity([
            new Claim("sub", "structure-reader"), new Claim("iss", options.Authorization.Issuer),
            new Claim("exp", DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            new Claim("scope", ApiAccessScopeNames.ReadProjects),
            new Claim("scope", structureWrite ? ApiAccessScopeNames.WriteProjectStructure : ApiAccessScopeNames.WriteProjects)
        ], "Bearer")));
        using var access = Create(scope.ServiceProvider, source);
        using var grant = await access.OpenAsync(Topic(scope.ServiceProvider) with { Owner = EditingOwner.ProjectStructure }, default);

        await access.ValidateAsync(grant, write: false, default);
        if (structureWrite) {
            await access.ValidateAsync(grant, write: true, default);
        } else {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.ValidateAsync(grant, write: true, default).AsTask());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Workflow_observation_uses_native_workflow_read_and_write_scopes(bool write) {
        await using var host = await ApiTestHost.CreateAsync(true, useInMemoryDatabase: true);
        using var scope = host.App.Services.CreateScope();
        var source = new PrincipalSource();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<ApiAccessOptions>>().Value;
        var identity = new ClaimsIdentity([
            new Claim("sub", "workflow-reader"), new Claim("iss", options.Authorization.Issuer),
            new Claim("exp", DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            new Claim("scope", ApiAccessScopeNames.ReadWorkflows)
        ], "Bearer");
        if (write) {
            identity.AddClaim(new("scope", ApiAccessScopeNames.WriteWorkflows));
        }
        source.Set(new(identity));
        using var access = Create(scope.ServiceProvider, source);
        var topic = Topic(scope.ServiceProvider) with { Owner = EditingOwner.WorkflowDefinition };
        using var catalog = await access.OpenAsync(topic, default);
        using var record = await access.OpenAsync(topic with { ResourceId = Guid.NewGuid() }, default);
        await access.ValidateAsync(record, write: false, default);
        if (write) {
            await access.ValidateAsync(record, write: true, default);
        } else {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.ValidateAsync(record, write: true, default).AsTask());
        }
        await access.ValidateAsync(catalog, write: false, default);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.OpenAsync(topic with { Owner = EditingOwner.Project }, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.OpenAsync(topic with { Owner = EditingOwner.CrmHrParty }, default));
        identity.AddClaim(new("auth_rev", "2"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.ValidateAsync(catalog, write: false, default).AsTask());
        Assert.True(catalog.Lifetime.IsCancellationRequested);
    }

    private static WebEditingUiAccess Create(IServiceProvider services, PrincipalSource source) {
        return new(CreateResolver(services, source), services.GetRequiredService<ICanonicalRuntimeDatabase>(), source, TimeProvider.System,
            services.GetRequiredService<IDatabaseRuntimeState>(), services.GetRequiredService<IDatabaseSwitchNotificationService>());
    }

    private static WebCurrentPrincipalResolver CreateResolver(IServiceProvider services, PrincipalSource source)
        => new(source, new HttpContextAccessor(), services.GetRequiredService<IAuthorizationService>(),
            services.GetRequiredService<IApiTokenRegistry>(), services.GetRequiredService<ApiSessionService>(),
            services.GetRequiredService<IOptions<ApiAccessOptions>>(), TimeProvider.System);

    private static ClaimsPrincipal Local() => new(new ClaimsIdentity([new Claim("sub", LocalOperatorAuthenticationStateProvider.ActorId)],
        LocalOperatorAuthenticationStateProvider.AuthenticationType));

    private sealed class PrincipalSource : AuthenticationStateProvider, IInteractiveAccessPrincipalProvider {
        private ClaimsPrincipal principal = new(new ClaimsIdentity());
        public bool IsAvailable => true;
        public ValueTask<ClaimsPrincipal> GetCurrentAsync(CancellationToken cancellationToken) => ValueTask.FromResult(principal);
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(principal));
        public void Set(ClaimsPrincipal value) {
            principal = value;
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
    }
}
