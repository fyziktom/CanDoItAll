using System.Reflection;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class AgentProjectStructureAccessDeletionIntegrationTests
{
    private static readonly ProjectDeletionParticipantId ParticipantId =
        new(AgentProjectStructureAccessDeletionParticipant.ParticipantIdValue);

    [Fact]
    public async Task Bulk_revocation_updates_active_draft_and_template_agents_atomically_and_keeps_allow_all_unchanged()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var projectId = Guid.NewGuid();
        var retainedProjectId = Guid.NewGuid();
        var activeId = await CreateAgentAsync(
            workspaceService,
            "Bulk revoke active",
            AgentLifecycleStatus.Active,
            isTemplate: false,
            [projectId, retainedProjectId]);
        var draftId = await CreateAgentAsync(
            workspaceService,
            "Bulk revoke draft",
            AgentLifecycleStatus.Draft,
            isTemplate: false,
            [projectId]);
        var templateId = await CreateAgentAsync(
            workspaceService,
            "Bulk revoke template",
            AgentLifecycleStatus.Active,
            isTemplate: true,
            [projectId]);
        var allowAllId = await CreateAgentAsync(
            workspaceService,
            "Bulk revoke allow all",
            AgentLifecycleStatus.Active,
            isTemplate: false,
            [],
            allowAllProjects: true);
        var before = (await workspaceService.ListAgentsAsync(includeTemplates: true))
            .Where(agent => new[] { activeId, draftId, templateId, allowAllId }.Contains(agent.Id))
            .ToDictionary(agent => agent.Id);

        var changedAgentCount = await workspaceService
            .RevokeProjectStructureAccessFromAllAgentsAsync(projectId);

        Assert.Equal(3, changedAgentCount);
        var after = (await workspaceService.ListAgentsAsync(includeTemplates: true))
            .Where(agent => before.ContainsKey(agent.Id))
            .ToDictionary(agent => agent.Id);
        foreach (var agentId in new[] { activeId, draftId, templateId })
        {
            var access = AgentProjectStructureAccessMetadata.Read(after[agentId].ConfigurationJson);
            Assert.DoesNotContain(projectId, access.AllowedProjectIds);
            Assert.NotEqual(before[agentId].UpdatedAtUtc, after[agentId].UpdatedAtUtc);
            Assert.Equal(
                "keep-me",
                JsonNode.Parse(after[agentId].ConfigurationJson)!["unrelated"]!["value"]!
                    .GetValue<string>());
            Assert.True(JsonNode.DeepEquals(
                JsonNode.Parse(before[agentId].ConfigurationJson)!["unrelated"],
                JsonNode.Parse(after[agentId].ConfigurationJson)!["unrelated"]));
        }

        Assert.Equal([retainedProjectId],
            AgentProjectStructureAccessMetadata.Read(after[activeId].ConfigurationJson)
                .AllowedProjectIds);
        Assert.True(after[templateId].IsTemplate);
        Assert.Equal(before[allowAllId].ConfigurationJson, after[allowAllId].ConfigurationJson);
        Assert.Equal(before[allowAllId].UpdatedAtUtc, after[allowAllId].UpdatedAtUtc);

        var timestampsAfterFirstRevocation = after.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.UpdatedAtUtc);
        Assert.Equal(
            0,
            await workspaceService.RevokeProjectStructureAccessFromAllAgentsAsync(projectId));
        var repeated = (await workspaceService.ListAgentsAsync(includeTemplates: true))
            .Where(agent => timestampsAfterFirstRevocation.ContainsKey(agent.Id))
            .ToDictionary(agent => agent.Id);
        Assert.All(
            timestampsAfterFirstRevocation,
            pair => Assert.Equal(pair.Value, repeated[pair.Key].UpdatedAtUtc));
    }

    [Fact]
    public async Task Malformed_agent_metadata_fails_closed_without_partially_rewriting_the_catalog()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var projectId = Guid.NewGuid();
        var validAgentId = await CreateAgentAsync(
            workspaceService,
            "Atomic valid agent",
            AgentLifecycleStatus.Active,
            isTemplate: false,
            [projectId]);
        var malformedAgentId = await CreateAgentAsync(
            workspaceService,
            "Atomic malformed agent",
            AgentLifecycleStatus.Active,
            isTemplate: false,
            [projectId]);
        var store = new FileSandboxWorkspaceStore(
            application.ActiveProfile.WorkspaceRootPath,
            workspaceFactory.GetOrganizationScope());
        const string malformedConfiguration =
            "{\"projectStructure\":{\"allowedProjectIds\":[\"not-a-guid\"]}}";
        await store.UpdateCatalogAsync(catalog => catalog with
        {
            Agents = catalog.Agents
                .Select(agent => agent.Id == malformedAgentId
                    ? agent with { ConfigurationJson = malformedConfiguration }
                    : agent)
                .ToList()
        });
        var before = (await workspaceService.ListAgentsAsync(includeTemplates: true))
            .Where(agent => agent.Id == validAgentId || agent.Id == malformedAgentId)
            .ToDictionary(agent => agent.Id);

        await Assert.ThrowsAsync<AgentProjectStructureAccessMetadataException>(() =>
            workspaceService.RevokeProjectStructureAccessFromAllAgentsAsync(projectId));

        var after = (await workspaceService.ListAgentsAsync(includeTemplates: true))
            .Where(agent => before.ContainsKey(agent.Id))
            .ToDictionary(agent => agent.Id);
        Assert.Equal(before[validAgentId].ConfigurationJson, after[validAgentId].ConfigurationJson);
        Assert.Equal(before[validAgentId].UpdatedAtUtc, after[validAgentId].UpdatedAtUtc);
        Assert.Equal(malformedConfiguration, after[malformedAgentId].ConfigurationJson);
        Assert.Equal(before[malformedAgentId].UpdatedAtUtc, after[malformedAgentId].UpdatedAtUtc);
        Assert.Contains(
            projectId,
            AgentProjectStructureAccessMetadata.Read(after[validAgentId].ConfigurationJson)
                .AllowedProjectIds);
    }

    [Fact]
    public async Task Project_deletion_completes_one_durable_revocation_attempt_and_completed_state_cannot_regress()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectId = await CreateProjectAsync(projects, "Agent access deletion");
        var agentId = await CreateAgentAsync(
            workspaceService,
            "Deleted project agent",
            AgentLifecycleStatus.Active,
            isTemplate: false,
            [projectId]);

        await projects.DeleteAsync(projectId);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var record = await verificationContext
            .Set<AgentProjectStructureAccessRevocationRecord>()
            .AsNoTracking()
            .SingleAsync(candidate => candidate.ProjectId == projectId);
        Assert.Equal(AgentProjectStructureAccessRevocationStatus.Completed, record.Status);
        Assert.Equal(1, record.AttemptCount);
        Assert.NotNull(record.LastAttemptAtUtc);
        Assert.NotNull(record.CompletedAtUtc);
        Assert.True(record.UpdatedAtUtc >= record.CreatedAtUtc);
        Assert.Null(record.LastFailureCode);
        var agent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: true),
            candidate => candidate.Id == agentId);
        Assert.DoesNotContain(
            projectId,
            AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson).AllowedProjectIds);
        var completedUpdatedAtUtc = record.UpdatedAtUtc;

        var participant = scope.ServiceProvider
            .GetServices<IProjectDeletionParticipant>()
            .Single(candidate => candidate.Id == ParticipantId);
        await participant.CompleteAsync(
            new ProjectDeletionParticipantPreparation(projectId, record.Id));

        verificationContext.ChangeTracker.Clear();
        var repeated = await verificationContext
            .Set<AgentProjectStructureAccessRevocationRecord>()
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == record.Id);
        Assert.Equal(AgentProjectStructureAccessRevocationStatus.Completed, repeated.Status);
        Assert.Equal(1, repeated.AttemptCount);
        Assert.Equal(completedUpdatedAtUtc, repeated.UpdatedAtUtc);
        Assert.DoesNotContain(
            await participant.ListPendingRecoveriesAsync(),
            recovery => recovery.RecoveryId == record.Id);
        var notice = Assert.Single(
            await participant.ListCompletionNoticesAsync(),
            candidate => candidate.RecoveryId == record.Id);
        Assert.Equal(projectId, notice.ProjectId);
        Assert.Equal(ProjectDeletionCompletionOperation.ProjectDeletion, notice.Operation);
        var publicNotice = Assert.Single(
            await projects.ListDeletionCompletionNoticesAsync(),
            candidate =>
                candidate.ParticipantId == ParticipantId &&
                candidate.RecoveryId == record.Id);
        Assert.Equal("agent-project-structure-access", publicNotice.ParticipantId.Value);
    }

    [Fact]
    public async Task Concurrent_completion_callers_share_one_attempt_and_both_observe_the_completed_record()
    {
        var workspaceService = DispatchProxy.Create<
            IAgentFrameworkWorkspaceService,
            BlockingWorkspaceServiceProxy>();
        var workspaceController = (BlockingWorkspaceServiceProxy)(object)workspaceService;
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            ConfigureServices = services =>
            {
                services.RemoveAll<IAgentFrameworkWorkspaceService>();
                services.AddSingleton(workspaceService);
            }
        });
        var projectId = Guid.NewGuid();
        ProjectDeletionParticipantPreparation preparation;
        await using (var setupScope = application.Services.CreateAsyncScope())
        {
            var participant = setupScope.ServiceProvider
                .GetServices<IProjectDeletionParticipant>()
                .Single(candidate => candidate.Id == ParticipantId);
            var dbContextFactory = setupScope.ServiceProvider
                .GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            preparation = Assert.IsType<ProjectDeletionParticipantPreparation>(
                await participant.PrepareAsync(dbContext, projectId));
            await dbContext.SaveChangesAsync();
        }

        await using var firstScope = application.Services.CreateAsyncScope();
        await using var secondScope = application.Services.CreateAsyncScope();
        var firstParticipant = firstScope.ServiceProvider
            .GetServices<IProjectDeletionParticipant>()
            .Single(candidate => candidate.Id == ParticipantId);
        var secondParticipant = secondScope.ServiceProvider
            .GetServices<IProjectDeletionParticipant>()
            .Single(candidate => candidate.Id == ParticipantId);
        var firstCompletion = firstParticipant.CompleteAsync(preparation);
        await workspaceController.Entered.WaitAsync(TimeSpan.FromSeconds(10));
        var secondCompletion = secondParticipant.CompleteAsync(preparation);
        await Task.Yield();
        Assert.False(secondCompletion.IsCompleted);

        workspaceController.Release();
        var completions = await Task.WhenAll(firstCompletion, secondCompletion)
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.All(
            completions,
            completion => Assert.Equal(preparation.RecoveryId, completion.RecoveryId));
        Assert.Equal(1, workspaceController.InvocationCount);
        await using var verificationScope = application.Services.CreateAsyncScope();
        var verificationFactory = verificationScope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var verificationContext = await verificationFactory.CreateDbContextAsync();
        var record = await verificationContext
            .Set<AgentProjectStructureAccessRevocationRecord>()
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == preparation.RecoveryId);
        Assert.Equal(AgentProjectStructureAccessRevocationStatus.Completed, record.Status);
        Assert.Equal(1, record.AttemptCount);
        Assert.Null(record.LastFailureCode);
        Assert.NotNull(record.CompletedAtUtc);
    }

    [Fact]
    public async Task Failed_cleanup_is_retryable_with_the_exact_recovery_and_second_attempt_completes()
    {
        var proxy = DispatchProxy.Create<IAgentFrameworkWorkspaceService, FailOnceWorkspaceServiceProxy>();
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            ConfigureServices = services =>
            {
                services.RemoveAll<IAgentFrameworkWorkspaceService>();
                services.AddSingleton(proxy);
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectId = await CreateProjectAsync(projects, "Retry agent access deletion");

        var failure = await Assert.ThrowsAsync<ProjectDeletionPartialCommitException>(() =>
            projects.DeleteAsync(projectId));
        var participantFailure = Assert.Single(
            failure.Recovery.Failures,
            candidate => candidate.ParticipantId == ParticipantId);
        Assert.NotNull(participantFailure.RecoveryId);
        var recoveryId = participantFailure.RecoveryId!.Value;
        await using (var failedContext = await dbContextFactory.CreateDbContextAsync())
        {
            var failedRecord = await failedContext
                .Set<AgentProjectStructureAccessRevocationRecord>()
                .AsNoTracking()
                .SingleAsync(candidate => candidate.Id == recoveryId);
            Assert.Equal(AgentProjectStructureAccessRevocationStatus.Failed, failedRecord.Status);
            Assert.Equal(1, failedRecord.AttemptCount);
            Assert.Equal(nameof(InvalidOperationException), failedRecord.LastFailureCode);
            Assert.Null(failedRecord.CompletedAtUtc);
        }

        var pending = Assert.Single(
            await projects.ListPendingDeletionCleanupsAsync(),
            candidate =>
                candidate.ParticipantId == ParticipantId &&
                candidate.RecoveryId == recoveryId);
        Assert.Equal(ProjectDeletionRecoveryStatus.Failed, pending.Status);
        Assert.True(pending.CanRetryNow);
        Assert.Null(pending.RetryAvailableAtUtc);
        Assert.Equal(AgentProjectStructureAccessDeletionParticipant.RetryGuidance, pending.RetryGuidance);

        await projects.RetryDeletionCleanupAsync(projectId, ParticipantId, recoveryId);

        await using var completedContext = await dbContextFactory.CreateDbContextAsync();
        var completedRecord = await completedContext
            .Set<AgentProjectStructureAccessRevocationRecord>()
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == recoveryId);
        Assert.Equal(AgentProjectStructureAccessRevocationStatus.Completed, completedRecord.Status);
        Assert.Equal(2, completedRecord.AttemptCount);
        Assert.Null(completedRecord.LastFailureCode);
        Assert.NotNull(completedRecord.CompletedAtUtc);
    }

    private static async Task<Guid> CreateAgentAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        string name,
        AgentLifecycleStatus status,
        bool isTemplate,
        IReadOnlyCollection<Guid> allowedProjectIds,
        bool allowAllProjects = false)
    {
        var editor = await workspaceService.GetAgentEditorAsync();
        editor.Name = name;
        editor.RoleTitle = "Project access specialist";
        editor.Summary = "Exercises durable project access cleanup.";
        editor.Instructions = "Stay within explicitly granted project scope.";
        editor.Status = status;
        editor.IsTemplate = isTemplate;
        editor.TemplateKey = isTemplate
            ? $"project-access-{Guid.NewGuid():N}"
            : string.Empty;
        editor.ConfigurationJson =
            "{\"unrelated\":{\"value\":\"keep-me\",\"nested\":[1,{\"enabled\":true}]}}";
        editor.ProjectStructureAccess = new AgentProjectStructureAccessSettings
        {
            CanRead = true,
            AllowAllProjects = allowAllProjects,
            AllowedProjectIds = allowedProjectIds.ToList()
        };
        return await workspaceService.SaveAgentAsync(editor);
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projects, string name)
    {
        var result = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Objective = "Validate durable agent-access cleanup.",
            CurrentPhase = "Validation"
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private class FailOnceWorkspaceServiceProxy : DispatchProxy
    {
        private int invocationCount;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name !=
                nameof(IAgentFrameworkWorkspaceService.RevokeProjectStructureAccessFromAllAgentsAsync))
            {
                throw new NotSupportedException(
                    $"Unexpected workspace call '{targetMethod?.Name}'.");
            }

            return Interlocked.Increment(ref invocationCount) == 1
                ? Task.FromException<int>(new InvalidOperationException("Injected bulk revocation failure."))
                : Task.FromResult(0);
        }
    }

    private class BlockingWorkspaceServiceProxy : DispatchProxy
    {
        private readonly TaskCompletionSource entered = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource released = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int invocationCount;

        public Task Entered => entered.Task;

        public int InvocationCount => Volatile.Read(ref invocationCount);

        public void Release()
        {
            released.TrySetResult();
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name !=
                nameof(IAgentFrameworkWorkspaceService.RevokeProjectStructureAccessFromAllAgentsAsync))
            {
                throw new NotSupportedException(
                    $"Unexpected workspace call '{targetMethod?.Name}'.");
            }

            return RevokeAsync();
        }

        private async Task<int> RevokeAsync()
        {
            Interlocked.Increment(ref invocationCount);
            entered.TrySetResult();
            await released.Task;
            return 0;
        }
    }
}
