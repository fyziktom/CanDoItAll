using System.Data;
using System.Net;
using System.Text.Json;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed class ProjectDeletionIntegrationTests
{
    private const string ProjectDeletionScopeNodeKey = "project";
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);
    private static readonly ProjectDeletionParticipantId WorkbenchParticipantId =
        new("workbench");

    [Fact]
    public async Task Historical_node_deletion_payload_reports_delete_owned_files_disposition()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectId = await CreateProjectAsync(projects, "Historical node deletion payload");
        const string rootNodeId = "legacy-deleted-node";
        var timestamp = DateTimeOffset.UtcNow;
        var mutation = new ProjectCrossModuleMutationRecord
        {
            ProjectId = projectId,
            ScopeNodeKey = rootNodeId,
            MutationKind = ProjectCrossModuleMutationKind.DeleteSubtree,
            Status = ProjectCrossModuleMutationStatus.Failed,
            ApprovalState = ProjectCrossModuleMutationApprovalState.NotRequired,
            PayloadJson = """
                {
                  "rootNodeKey": "legacy-deleted-node",
                  "deletedNodeKeys": ["legacy-deleted-node"],
                  "linkCount": 0,
                  "managedStorageObjects": [],
                  "managedStorageOutcomes": [],
                  "managedStorageCandidates": []
                }
                """,
            ErrorMessage = "Historical cleanup is pending.",
            AttemptCount = 1,
            CreatedAtUtc = timestamp,
            UpdatedAtUtc = timestamp
        };
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Set<ProjectCrossModuleMutationRecord>().Add(mutation);
            await dbContext.SaveChangesAsync();
        }

        var recovery = Assert.Single(
            await workbench.ListPendingDeletionRecoveriesAsync(projectId));

        Assert.Equal(rootNodeId, recovery.RootNodeId);
        Assert.Equal(
            ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles,
            recovery.ManagedStorageDisposition);
    }

    [Fact]
    public async Task Exact_project_cleanup_retry_rejects_a_whitespace_participant_with_a_typed_400()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true);
        var projectId = Guid.NewGuid();
        var recoveryId = Guid.NewGuid();

        using var response = await host.Client.PostAsync(
            $"/api/projects/{projectId:D}/deletion-cleanups/%20%20%20/{recoveryId:D}/retry",
            content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var error = Assert.Single(body.RootElement.GetProperty("errors").EnumerateArray());
        Assert.Equal(
            "projects.delete-cleanup-participant-invalid",
            error.GetProperty("code").GetString());
        Assert.DoesNotContain(
            nameof(ArgumentException),
            body.RootElement.GetRawText(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Node_deletion_preserves_shared_media_until_the_final_binding_then_deletes_the_bytes_once()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var workspacePathResolver = scope.ServiceProvider.GetRequiredService<IWorkspacePathResolver>();
        var projectId = await CreateProjectAsync(projects, "Shared media deletion");
        var asset = await CreateImageAsync(workbench, projectId, "Shared image");
        var copied = await workbench.CopySubtreesAsync(
            projectId,
            [asset.Id],
            BuildProjectRootNodeKey(projectId));
        var copiedNodeId = copied.NodeIdMap[asset.Id];
        var physicalPath = Path.Combine(
            workspacePathResolver.ResolveWorkspaceRoot(),
            asset.MediaRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(physicalPath));

        Assert.Equal(1, await workbench.DeleteObjectAsync(projectId, asset.Id));
        Assert.True(File.Exists(physicalPath));

        Assert.Equal(1, await workbench.DeleteObjectAsync(projectId, copiedNodeId));
        Assert.False(File.Exists(physicalPath));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        Assert.Empty(await dbContext.Set<ProjectNodeBindingRecord>()
            .Where(binding => binding.MediaRelativePath == asset.MediaRelativePath)
            .ToListAsync());
        var mutations = await dbContext.Set<ProjectCrossModuleMutationRecord>()
            .Where(record =>
                record.ProjectId == projectId &&
                record.MutationKind == ProjectCrossModuleMutationKind.DeleteSubtree)
            .OrderBy(record => record.CreatedAtUtc)
            .ToListAsync();
        Assert.Equal(2, mutations.Count);
        var firstPayload = Deserialize<DeleteSubtreeMutationPayload>(mutations[0].PayloadJson);
        var finalPayload = Deserialize<DeleteSubtreeMutationPayload>(mutations[1].PayloadJson);
        Assert.Equal(
            ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles,
            firstPayload.ManagedStorageDisposition);
        Assert.Equal(
            ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles,
            finalPayload.ManagedStorageDisposition);
        Assert.Empty(firstPayload.ManagedStorageObjects ?? []);
        Assert.Single(finalPayload.ManagedStorageObjects ?? []);
        Assert.Single(finalPayload.ManagedStorageOutcomes ?? []);
    }

    [Fact]
    public async Task Retain_managed_files_deletes_a_node_even_when_its_creation_workspace_was_retargeted()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var workspacePathResolver = scope.ServiceProvider.GetRequiredService<IWorkspacePathResolver>();
        var projectId = await CreateProjectAsync(projects, "Retargeted node-only deletion");
        var asset = await CreateImageAsync(workbench, projectId, "Retargeted image");
        var physicalPath = Path.Combine(
            workspacePathResolver.ResolveWorkspaceRoot(),
            asset.MediaRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(physicalPath));

        var historicalRoot = Path.Combine(
            Path.GetTempPath(),
            nameof(ProjectDeletionIntegrationTests),
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(historicalRoot);
        Guid assetRecordId = default;
        try
        {
            await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
            {
                var assetRecord = await dbContext.Set<ProjectObjectRecord>()
                    .SingleAsync(record =>
                        record.ProjectId == projectId &&
                        record.NodeKey == asset.Id);
                assetRecordId = assetRecord.Id;
                var binding = await dbContext.Set<ProjectNodeBindingRecord>()
                    .SingleAsync(record => record.ProjectObjectId == assetRecordId);
                var reference = StorageJson.ParseReference(binding.StorageObjectReferenceJson)
                    ?? throw new InvalidOperationException("The generated test asset has no storage reference.");
                var currentStorage = await dbContext.Set<StorageCatalogRecord>()
                    .SingleAsync(storage => storage.Id == reference.StorageId);
                var historicalStorage = new StorageCatalogRecord
                {
                    Id = currentStorage.Id,
                    Name = currentStorage.Name,
                    ProviderKind = currentStorage.ProviderKind,
                    EndpointOrRoot = historicalRoot,
                    IsEnabled = true,
                    IsSystemDefault = true
                };
                StorageCatalogHostBindingPolicy.BindCurrent(
                    historicalStorage,
                    historicalRoot,
                    DateTimeOffset.UtcNow);
                var historicalIdentityPolicy = new ProjectManagedStoragePhysicalIdentityPolicy(
                    new FileSystemStoragePathPolicy(
                        new StaticWorkspacePathResolver(historicalRoot)),
                    new PhysicalFileSystemPathPolicyFactory());
                var historicalReference = ProjectManagedStorageProvenancePolicy.Stamp(
                    reference with { MetadataJson = "{}" },
                    reference.Locator,
                    historicalStorage,
                    historicalIdentityPolicy);
                binding.StorageObjectReferenceJson = StorageJson.SerializeReference(historicalReference);
                await dbContext.SaveChangesAsync();
            }

            await Assert.ThrowsAsync<ProjectManagedStorageBindingException>(() =>
                workbench.DeleteObjectDetailedAsync(
                    projectId,
                    asset.Id,
                    ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles));

            var retained = await workbench.DeleteObjectDetailedAsync(
                projectId,
                asset.Id,
                ProjectStructureManagedStorageDisposition.RetainManagedFiles);

            Assert.Equal(1, retained.DeletedNodeCount);
            Assert.True(File.Exists(physicalPath));
            await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
            Assert.False(await verificationContext.Set<ProjectObjectRecord>()
                .AnyAsync(record => record.ProjectId == projectId && record.NodeKey == asset.Id));
            Assert.False(await verificationContext.Set<ProjectNodeBindingRecord>()
                .AnyAsync(record => record.ProjectObjectId == assetRecordId));
            var mutation = await verificationContext.Set<ProjectCrossModuleMutationRecord>()
                .SingleAsync(record =>
                    record.ProjectId == projectId &&
                    record.ScopeNodeKey == asset.Id &&
                    record.MutationKind == ProjectCrossModuleMutationKind.DeleteSubtree);
            var payload = Deserialize<DeleteSubtreeMutationPayload>(mutation.PayloadJson);
            Assert.Equal(
                ProjectStructureManagedStorageDisposition.RetainManagedFiles,
                payload.ManagedStorageDisposition);
            Assert.Empty(payload.ManagedStorageObjects ?? []);
        }
        finally
        {
            Directory.Delete(historicalRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Batch_deletion_detaches_a_user_link_from_a_non_hideable_projected_node()
    {
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            ConfigureServices = services =>
                services.AddSingleton<IProjectStructureProjectionContributor,
                    NonHideablePhaseProjectionContributor>()
        });
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var coordinator = scope.ServiceProvider
            .GetRequiredService<ProjectStructureBatchDeletionCoordinator>();
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectId = await CreateProjectAsync(projects, "Projected link detachment");
        var note = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Linked note",
                string.Empty,
                string.Empty,
                BuildProjectRootNodeKey(projectId),
                200,
                200));
        await workbench.LinkObjectsAsync(
            projectId,
            note.Id,
            NonHideablePhaseProjectionContributor.NodeKey,
            ProjectObjectLinkKind.DependsOn);

        var result = await coordinator.DeleteNodesAsync(
            projectId,
            [NonHideablePhaseProjectionContributor.NodeKey],
            ProjectStructureManagedStorageDisposition.RetainManagedFiles);

        Assert.Equal(1, result.DeletedNodeCount);
        Assert.DoesNotContain(
            (await workbench.GetStructureAsync(projectId)).Nodes,
            node => node.Id == NonHideablePhaseProjectionContributor.NodeKey);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        Assert.False(await dbContext.Set<ProjectObjectLinkRecord>().AnyAsync(link =>
            link.ProjectId == projectId &&
            !link.IsSystemManaged &&
            link.SourceNodeKey == note.Id &&
            link.TargetNodeKey == NonHideablePhaseProjectionContributor.NodeKey));
    }

    [Fact]
    public async Task Project_deletion_removes_project_workbench_search_routing_and_physical_media()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var workspacePathResolver = scope.ServiceProvider.GetRequiredService<IWorkspacePathResolver>();
        var storageCatalog = scope.ServiceProvider.GetRequiredService<IStorageCatalogService>();
        var partyIntegration = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var projectId = await CreateProjectAsync(projects, "Complete project deletion");
        var asset = await CreateImageAsync(workbench, projectId, "Project image");
        var note = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Project note",
                string.Empty,
                "Delete every Workbench row.",
                BuildProjectRootNodeKey(projectId),
                400,
                300));
        await workbench.LinkObjectsAsync(
            projectId,
            note.Id,
            asset.Id,
            ProjectObjectLinkKind.DerivedFrom);
        var physicalPath = Path.Combine(
            workspacePathResolver.ResolveWorkspaceRoot(),
            asset.MediaRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var storage = await storageCatalog.EnsureBootstrapFileSystemStorageAsync();
        var party = await partyIntegration.CreatePartyAsync(new ProjectPartyQuickCreateRequest
        {
            ProjectId = projectId,
            PartyKind = ProjectPartyQuickCreateKind.Person,
            DisplayName = "Deletion stakeholder",
            Summary = "CRM assignment removed by durable project cleanup."
        });
        Assert.True(
            party.IsSuccess,
            string.Join(" ", party.Errors.Select(error => error.Message)));
        var createdParty = Assert.IsType<ProjectPartyQuickCreateResult>(party.Value);
        var assignment = await partyIntegration.SaveAssignmentAsync(
            new ProjectPartyAssignmentUpsertRequest
            {
                ProjectId = projectId,
                PartyId = createdParty.PartyId,
                Role = ProjectPartyAssignmentRole.Stakeholder,
                IsPrimary = true,
                Source = "project-deletion-integration-test"
            });
        Assert.True(
            assignment.IsSuccess,
            string.Join(" ", assignment.Errors.Select(error => error.Message)));

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var assetRecord = await dbContext.Set<ProjectObjectRecord>()
                .SingleAsync(record => record.ProjectId == projectId && record.NodeKey == asset.Id);
            dbContext.AddRange(
                new ProjectWorkbenchViewStateRecord
                {
                    ProjectId = projectId,
                    SurfaceKind = "canvas",
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                },
                new ProjectStructureProjectionLayoutRecord
                {
                    ProjectId = projectId,
                    NodeKey = "projected:test",
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                },
                new ProjectStructureOperationAnalyticsRecord
                {
                    ProjectId = projectId,
                    OperationName = "delete-test",
                    OccurredAtUtc = DateTimeOffset.UtcNow
                },
                new ProjectStructureLeaseRecord
                {
                    ScopeKind = ProjectStructureLeaseScopeKind.Project,
                    ScopeKey = projectId.ToString("D"),
                    LeaseToken = Guid.NewGuid().ToString("N"),
                    ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5)
                },
                new ProjectNodeReferenceRecord
                {
                    ProjectObjectId = assetRecord.Id,
                    ReferenceKind = "delete-test",
                    ReferenceId = Guid.NewGuid().ToString("D")
                },
                new SearchDocument
                {
                    ProjectId = projectId,
                    SourceType = "workbench-node",
                    SourceKey = asset.Id,
                    Category = "Project",
                    Title = "Project-bound search document",
                    Route = "/projects"
                },
                new StorageRoutingRule
                {
                    Name = "Project deletion route",
                    ScopeKind = StorageRoutingScopeKind.Project,
                    ProjectId = projectId,
                    PreferredStorageId = storage.Id,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });
            await dbContext.SaveChangesAsync();
        }

        await projects.DeleteAsync(projectId);

        Assert.False(File.Exists(physicalPath));
        var missingProjectFailure = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            workbench.CreateObjectAsync(
                projectId,
                new ProjectObjectCreateRequest(
                    ProjectObjectType.Note,
                    "Resurrection must fail",
                    string.Empty,
                    "The deleted project must reject new structure writes.",
                    BuildProjectRootNodeKey(projectId),
                    100,
                    100)));
        Assert.Equal(404, missingProjectFailure.StatusCode);
        Assert.Equal("ProjectNotFound", missingProjectFailure.ErrorCode);

        await using var verificationScope = application.Services.CreateAsyncScope();
        var verificationProjects = verificationScope.ServiceProvider
            .GetRequiredService<ProjectsService>();
        var verificationPartyIntegration = verificationScope.ServiceProvider
            .GetRequiredService<IProjectPartyIntegrationBridge>();
        var verificationDbContextFactory = verificationScope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var verificationContext =
            await verificationDbContextFactory.CreateDbContextAsync();
        Assert.False(await verificationContext.Set<Project>().AnyAsync(record => record.Id == projectId));
        Assert.False(await verificationContext.Set<ProjectObjectRecord>().AnyAsync(record => record.ProjectId == projectId));
        Assert.False(await verificationContext.Set<ProjectObjectLinkRecord>().AnyAsync(record => record.ProjectId == projectId));
        Assert.False(await verificationContext.Set<ProjectWorkbenchViewStateRecord>().AnyAsync(record => record.ProjectId == projectId));
        Assert.False(await verificationContext.Set<ProjectStructureProjectionLayoutRecord>().AnyAsync(record => record.ProjectId == projectId));
        Assert.False(await verificationContext.Set<ProjectStructureOperationAnalyticsRecord>().AnyAsync(record => record.ProjectId == projectId));
        Assert.False(await verificationContext.Set<SearchDocument>().AnyAsync(record => record.ProjectId == projectId));
        Assert.False(await verificationContext.Set<StorageRoutingRule>().AnyAsync(record => record.ProjectId == projectId));
        Assert.Empty(await verificationPartyIntegration.ListAssignmentsDetailedAsync(projectId));
        var deletionMutation = await verificationContext.Set<ProjectCrossModuleMutationRecord>()
            .SingleAsync(record =>
                record.ProjectId == projectId &&
                record.MutationKind == ProjectCrossModuleMutationKind.DeleteProject);
        Assert.Equal(ProjectCrossModuleMutationStatus.Completed, deletionMutation.Status);
        Assert.DoesNotContain(
            await verificationProjects.ListPendingDeletionCleanupsAsync(),
            cleanup => cleanup.ProjectId == projectId);
        var completionNotice = Assert.Single(
            await verificationProjects.ListDeletionCompletionNoticesAsync(),
            notice =>
                notice.ProjectId == projectId &&
                notice.RecoveryId == deletionMutation.Id);
        Assert.Equal(ProjectDeletionCompletionOperation.ProjectDeletion, completionNotice.Operation);
        Assert.Empty(completionNotice.Warnings);
    }

    [Fact]
    public async Task Node_deletion_failure_is_typed_and_exact_recovery_resumes_the_durable_mutation()
    {
        await using var application = await CreateFailOnceDeletionApplicationAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var workspacePathResolver = scope.ServiceProvider.GetRequiredService<IWorkspacePathResolver>();
        var registry = scope.ServiceProvider.GetRequiredService<ObservedStorageDriverRegistry>();
        var projectId = await CreateProjectAsync(projects, "Node delete retry");
        var asset = await CreateImageAsync(workbench, projectId, "Retry image");
        var physicalPath = Path.Combine(
            workspacePathResolver.ResolveWorkspaceRoot(),
            asset.MediaRelativePath.Replace('/', Path.DirectorySeparatorChar));

        var failure = await Assert.ThrowsAsync<ProjectStructureDeletionPartialCommitException>(() =>
            workbench.DeleteObjectAsync(projectId, asset.Id));

        Assert.Equal(projectId, failure.Recovery.ProjectId);
        Assert.Equal(asset.Id, failure.Recovery.RootNodeId);
        Assert.Equal(
            ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles,
            failure.Recovery.ManagedStorageDisposition);
        Assert.True(File.Exists(physicalPath));
        await using (var failedContext = await dbContextFactory.CreateDbContextAsync())
        {
            Assert.False(await failedContext.Set<ProjectObjectRecord>()
                .AnyAsync(record => record.ProjectId == projectId && record.NodeKey == asset.Id));
            var mutation = await failedContext.Set<ProjectCrossModuleMutationRecord>()
                .SingleAsync(record => record.Id == failure.Recovery.DurableMutationId);
            Assert.Equal(ProjectCrossModuleMutationStatus.Failed, mutation.Status);
        }

        var mismatch = await Assert.ThrowsAsync<ProjectStructureDeletionDispositionMismatchException>(() =>
            workbench.RetryDeletionCleanupDetailedAsync(
                projectId,
                asset.Id,
                failure.Recovery.DurableMutationId,
                ProjectStructureManagedStorageDisposition.RetainManagedFiles));
        Assert.Equal(
            ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles,
            mismatch.PersistedDisposition);
        Assert.Equal(1, mismatch.CompletedNodeCount);

        Assert.Equal(
            1,
            await workbench.RetryDeletionCleanupAsync(
                projectId,
                asset.Id,
                failure.Recovery.DurableMutationId));

        Assert.False(File.Exists(physicalPath));
        Assert.Equal(2, registry.FileSystemDeleteCalls);
        await using var completedContext = await dbContextFactory.CreateDbContextAsync();
        Assert.Equal(
            ProjectCrossModuleMutationStatus.Completed,
            (await completedContext.Set<ProjectCrossModuleMutationRecord>()
                .SingleAsync(record => record.Id == failure.Recovery.DurableMutationId)).Status);
    }

    [Fact]
    public async Task Project_deletion_failure_commits_database_and_crm_cleanup_then_exact_retry_finishes_media_cleanup()
    {
        await using var application = await CreateFailOnceDeletionApplicationAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var partyIntegration = scope.ServiceProvider
            .GetRequiredService<IProjectPartyIntegrationBridge>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var workspacePathResolver = scope.ServiceProvider.GetRequiredService<IWorkspacePathResolver>();
        var registry = scope.ServiceProvider.GetRequiredService<ObservedStorageDriverRegistry>();
        var projectId = await CreateProjectAsync(projects, "Project delete retry");
        var asset = await CreateImageAsync(workbench, projectId, "Retry project image");
        var physicalPath = Path.Combine(
            workspacePathResolver.ResolveWorkspaceRoot(),
            asset.MediaRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var party = await partyIntegration.CreatePartyAsync(new ProjectPartyQuickCreateRequest
        {
            ProjectId = projectId,
            PartyKind = ProjectPartyQuickCreateKind.Person,
            DisplayName = "Retry-window stakeholder",
            Summary = "Proves desired-state CRM deletion replay."
        });
        Assert.True(
            party.IsSuccess,
            string.Join(" ", party.Errors.Select(error => error.Message)));
        var createdParty = Assert.IsType<ProjectPartyQuickCreateResult>(party.Value);
        var assignment = await partyIntegration.SaveAssignmentAsync(
            new ProjectPartyAssignmentUpsertRequest
            {
                ProjectId = projectId,
                PartyId = createdParty.PartyId,
                Role = ProjectPartyAssignmentRole.Stakeholder,
                IsPrimary = true,
                Source = "project-deletion-replay-integration-test"
            });
        Assert.True(
            assignment.IsSuccess,
            string.Join(" ", assignment.Errors.Select(error => error.Message)));

        var failure = await Assert.ThrowsAsync<ProjectDeletionPartialCommitException>(() =>
            projects.DeleteAsync(projectId));

        var workbenchFailure = Assert.Single(failure.Recovery.Failures);
        Assert.Equal(new ProjectDeletionParticipantId("workbench"), workbenchFailure.ParticipantId);
        Assert.NotNull(workbenchFailure.RecoveryId);
        Assert.True(File.Exists(physicalPath));
        Assert.Empty(await partyIntegration.ListAssignmentsDetailedAsync(projectId));
        await using (var failedContext = await dbContextFactory.CreateDbContextAsync())
        {
            Assert.False(await failedContext.Set<Project>().AnyAsync(record => record.Id == projectId));
            Assert.False(await failedContext.Set<ProjectObjectRecord>().AnyAsync(record => record.ProjectId == projectId));
            Assert.Equal(
                ProjectCrossModuleMutationStatus.Failed,
                (await failedContext.Set<ProjectCrossModuleMutationRecord>()
                    .SingleAsync(record => record.Id == workbenchFailure.RecoveryId)).Status);
        }

        var retryResult = await projects.RetryDeletionCleanupAsync(
            projectId,
            workbenchFailure.ParticipantId,
            workbenchFailure.RecoveryId!.Value);

        Assert.False(File.Exists(physicalPath));
        Assert.Equal(2, registry.FileSystemDeleteCalls);
        Assert.Empty(await partyIntegration.ListAssignmentsDetailedAsync(projectId));
        Assert.Empty(retryResult.Warnings);
        var replayResult = await projects.RetryDeletionCleanupAsync(
            projectId,
            workbenchFailure.ParticipantId,
            workbenchFailure.RecoveryId!.Value);
        Assert.Equal(projectId, replayResult.ProjectId);
        Assert.Empty(replayResult.Warnings);
        Assert.Equal(2, registry.FileSystemDeleteCalls);

        await using var completedContext = await dbContextFactory.CreateDbContextAsync();
        Assert.Equal(
            ProjectCrossModuleMutationStatus.Completed,
            (await completedContext.Set<ProjectCrossModuleMutationRecord>()
                .SingleAsync(record => record.Id == workbenchFailure.RecoveryId)).Status);
        await using var reloadedScope = application.Services.CreateAsyncScope();
        var reloadedProjects = reloadedScope.ServiceProvider
            .GetRequiredService<ProjectsService>();
        Assert.DoesNotContain(
            await reloadedProjects.ListPendingDeletionCleanupsAsync(),
            cleanup => cleanup.RecoveryId == workbenchFailure.RecoveryId);
        var completionNotice = Assert.Single(
            await reloadedProjects.ListDeletionCompletionNoticesAsync(),
            notice => notice.RecoveryId == workbenchFailure.RecoveryId);
        Assert.Equal(ProjectDeletionCompletionOperation.ProjectDeletion, completionNotice.Operation);
        Assert.Empty(completionNotice.Warnings);
    }

    [Fact]
    public async Task Exact_project_cleanup_retry_keeps_the_parent_recovery_pending_when_a_dependency_is_missing()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectId = Guid.NewGuid();
        var missingDependencyId = Guid.NewGuid();
        var recoveryId = await SeedProjectDeletionMutationAsync(
            dbContextFactory,
            projectId,
            ProjectCrossModuleMutationStatus.WorkbenchCommitted,
            new DeleteProjectMutationPayload(
                [],
                [],
                OutstandingMutationIds: [missingDependencyId]));

        var failure = await Assert.ThrowsAsync<ProjectDeletionPartialCommitException>(() =>
            projects.RetryDeletionCleanupAsync(
                projectId,
                WorkbenchParticipantId,
                recoveryId));

        var failedOperation = Assert.Single(failure.Recovery.Failures);
        Assert.Equal(ProjectDeletionRecoveryOperation.ParticipantCleanup, failedOperation.Operation);
        Assert.Equal(WorkbenchParticipantId, failedOperation.ParticipantId);
        Assert.Equal(recoveryId, failedOperation.RecoveryId);
        Assert.Contains("exact participant and recovery id", failure.Recovery.RetryGuidance);
        var pending = Assert.Single(
            await projects.ListPendingDeletionCleanupsAsync(),
            cleanup => cleanup.RecoveryId == recoveryId);
        Assert.Equal(projectId, pending.ProjectId);
        Assert.Equal(WorkbenchParticipantId, pending.ParticipantId);
        Assert.Equal(ProjectDeletionRecoveryStatus.Pending, pending.Status);
        Assert.True(pending.CanRetryNow);
        Assert.Null(pending.RetryAvailableAtUtc);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var retainedMutation = await verificationContext.Set<ProjectCrossModuleMutationRecord>()
            .AsNoTracking()
            .SingleAsync(record => record.Id == recoveryId);
        Assert.Equal(
            ProjectCrossModuleMutationStatus.WorkbenchCommitted,
            retainedMutation.Status);
        Assert.Contains(
            missingDependencyId,
            Deserialize<DeleteProjectMutationPayload>(retainedMutation.PayloadJson)
                .OutstandingMutationIds ?? []);
    }

    [Fact]
    public async Task Processing_recovery_exposes_lease_availability_and_only_stale_claim_is_reclaimed()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var processingOptions = scope.ServiceProvider
            .GetRequiredService<ProjectCrossModuleMutationProcessingOptions>();
        var now = TruncateToPostgreSqlPrecision(DateTimeOffset.UtcNow);
        var freshProjectId = Guid.NewGuid();
        var staleProjectId = Guid.NewGuid();
        var freshAttemptAtUtc = now;
        var staleAttemptAtUtc = now - processingOptions.LeaseDuration -
                                TimeSpan.FromSeconds(1);
        var freshRecoveryId = await SeedProjectDeletionMutationAsync(
            dbContextFactory,
            freshProjectId,
            ProjectCrossModuleMutationStatus.Processing,
            new DeleteProjectMutationPayload([], []),
            lastAttemptAtUtc: freshAttemptAtUtc,
            attemptCount: 1,
            errorMessage: $"processing:{Guid.NewGuid():N}");
        var staleRecoveryId = await SeedProjectDeletionMutationAsync(
            dbContextFactory,
            staleProjectId,
            ProjectCrossModuleMutationStatus.Processing,
            new DeleteProjectMutationPayload([], []),
            lastAttemptAtUtc: staleAttemptAtUtc,
            attemptCount: 1,
            errorMessage: $"processing:{Guid.NewGuid():N}");

        var pending = await projects.ListPendingDeletionCleanupsAsync();
        var fresh = Assert.Single(
            pending,
            cleanup => cleanup.RecoveryId == freshRecoveryId);
        Assert.Equal(ProjectDeletionRecoveryStatus.Processing, fresh.Status);
        Assert.False(fresh.CanRetryNow);
        Assert.Equal(
            freshAttemptAtUtc + processingOptions.LeaseDuration,
            fresh.RetryAvailableAtUtc);
        var stale = Assert.Single(
            pending,
            cleanup => cleanup.RecoveryId == staleRecoveryId);
        Assert.Equal(ProjectDeletionRecoveryStatus.Processing, stale.Status);
        Assert.True(stale.CanRetryNow);
        Assert.Equal(
            staleAttemptAtUtc + processingOptions.LeaseDuration,
            stale.RetryAvailableAtUtc);

        var activeClaimFailure = await Assert.ThrowsAsync<ProjectDeletionPartialCommitException>(() =>
            projects.RetryDeletionCleanupAsync(
                freshProjectId,
                WorkbenchParticipantId,
                freshRecoveryId));
        Assert.Equal(
            freshRecoveryId,
            Assert.Single(activeClaimFailure.Recovery.Failures).RecoveryId);

        var staleCompletion = await projects.RetryDeletionCleanupAsync(
            staleProjectId,
            WorkbenchParticipantId,
            staleRecoveryId);
        Assert.Equal(staleProjectId, staleCompletion.ProjectId);
        Assert.Empty(staleCompletion.Warnings);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var mutations = await verificationContext.Set<ProjectCrossModuleMutationRecord>()
            .AsNoTracking()
            .Where(record =>
                record.Id == freshRecoveryId ||
                record.Id == staleRecoveryId)
            .ToDictionaryAsync(record => record.Id);
        Assert.Equal(
            ProjectCrossModuleMutationStatus.Processing,
            mutations[freshRecoveryId].Status);
        Assert.Equal(1, mutations[freshRecoveryId].AttemptCount);
        Assert.Equal(
            ProjectCrossModuleMutationStatus.Completed,
            mutations[staleRecoveryId].Status);
        Assert.Equal(2, mutations[staleRecoveryId].AttemptCount);
    }

    [Fact]
    public async Task Immutable_media_warning_is_replayed_by_exact_retry_and_reloaded_from_terminal_history()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectId = Guid.NewGuid();
        var reference = new StorageObjectReference(
            null,
            StorageProviderKind.Ipfs,
            StorageLocatorKind.ContentAddress,
            "bafybeigdyrzt5sfp7udm7hu76uh7y26nf3udb5xywp5ly4uqj3u4eu6u4i",
            "retained.png",
            "image/png");
        var candidate = new ProjectManagedStorageDeletionCandidate(
            reference,
            ProjectManagedStorageOwnershipBasis.ImmutableContentAddress,
            string.Empty,
            string.Empty);
        var recoveryId = await SeedProjectDeletionMutationAsync(
            dbContextFactory,
            projectId,
            ProjectCrossModuleMutationStatus.WorkbenchCommitted,
            new DeleteProjectMutationPayload(
                [],
                [reference],
                ManagedStorageCandidates: [candidate]));

        var completion = await projects.RetryDeletionCleanupAsync(
            projectId,
            WorkbenchParticipantId,
            recoveryId);
        Assert.Equal(projectId, completion.ProjectId);
        var warning = Assert.Single(completion.Warnings);
        AssertRetainedByProviderWarning(warning, recoveryId, reference);

        var replay = await projects.RetryDeletionCleanupAsync(
            projectId,
            WorkbenchParticipantId,
            recoveryId);
        var replayedWarning = Assert.Single(replay.Warnings);
        AssertRetainedByProviderWarning(
            replayedWarning,
            recoveryId,
            reference);
        Assert.Equal(warning, replayedWarning);

        await using var reloadScope = application.Services.CreateAsyncScope();
        var reloadedProjects = reloadScope.ServiceProvider
            .GetRequiredService<ProjectsService>();
        var notice = Assert.Single(
            await reloadedProjects.ListDeletionCompletionNoticesAsync(),
            item => item.RecoveryId == recoveryId);
        Assert.Equal(ProjectDeletionCompletionOperation.ProjectDeletion, notice.Operation);
        AssertRetainedByProviderWarning(
            Assert.Single(notice.Warnings),
            recoveryId,
            reference);
        Assert.DoesNotContain(
            await reloadedProjects.ListPendingDeletionCleanupsAsync(),
            cleanup => cleanup.RecoveryId == recoveryId);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var terminalMutation = await verificationContext.Set<ProjectCrossModuleMutationRecord>()
            .AsNoTracking()
            .SingleAsync(record => record.Id == recoveryId);
        Assert.Equal(ProjectCrossModuleMutationStatus.Completed, terminalMutation.Status);
        Assert.NotNull(terminalMutation.CompletedAtUtc);
        var outcome = Assert.Single(
            Deserialize<DeleteProjectMutationPayload>(terminalMutation.PayloadJson)
                .ManagedStorageOutcomes ?? []);
        Assert.Equal(
            ProjectManagedStorageDeletionOutcomeKind.RetainedByProvider,
            outcome.Kind);
        Assert.Equal(reference, outcome.Reference);
    }

    [Fact]
    public async Task Completed_project_cleanup_with_residual_rows_creates_and_returns_a_durable_follow_up_recovery()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectId = Guid.NewGuid();
        var originalRecoveryId = await SeedProjectDeletionMutationAsync(
            dbContextFactory,
            projectId,
            ProjectCrossModuleMutationStatus.Completed,
            new DeleteProjectMutationPayload([], []));
        var residualNodeKey = $"custom:{Guid.NewGuid():N}";
        var timestamp = DateTimeOffset.UtcNow;
        await using (var setupContext = await dbContextFactory.CreateDbContextAsync())
        {
            setupContext.Set<ProjectObjectRecord>().Add(new ProjectObjectRecord
            {
                ProjectId = projectId,
                NodeKey = residualNodeKey,
                ObjectType = ProjectObjectType.Note,
                Title = "Residual project row",
                Notes = "Simulates state discovered after the original terminal mutation.",
                ParentNodeKey = BuildProjectRootNodeKey(projectId),
                CreatedAtUtc = timestamp,
                UpdatedAtUtc = timestamp
            });
            await setupContext.SaveChangesAsync();
        }

        var participant = scope.ServiceProvider
            .GetServices<IProjectDeletionParticipant>()
            .Single(candidate => candidate.Id == WorkbenchParticipantId);
        var completion = await participant.CompleteAsync(
            new ProjectDeletionParticipantPreparation(projectId, originalRecoveryId));

        Assert.NotEqual(originalRecoveryId, completion.RecoveryId);
        Assert.Empty(completion.Warnings);
        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        Assert.False(await verificationContext.Set<ProjectObjectRecord>()
            .AnyAsync(record =>
                record.ProjectId == projectId &&
                record.NodeKey == residualNodeKey));
        var original = await verificationContext.Set<ProjectCrossModuleMutationRecord>()
            .AsNoTracking()
            .SingleAsync(record => record.Id == originalRecoveryId);
        var followUp = await verificationContext.Set<ProjectCrossModuleMutationRecord>()
            .AsNoTracking()
            .SingleAsync(record => record.Id == completion.RecoveryId);
        Assert.Equal(ProjectCrossModuleMutationStatus.Completed, original.Status);
        Assert.Equal(ProjectCrossModuleMutationStatus.Completed, followUp.Status);
        Assert.Equal(ProjectCrossModuleMutationKind.DeleteProject, followUp.MutationKind);
        Assert.Contains(
            residualNodeKey,
            Deserialize<DeleteProjectMutationPayload>(followUp.PayloadJson)
                .DeletedNodeKeys);

        await using var reloadScope = application.Services.CreateAsyncScope();
        var reloadedProjects = reloadScope.ServiceProvider
            .GetRequiredService<ProjectsService>();
        var notice = Assert.Single(
            await reloadedProjects.ListDeletionCompletionNoticesAsync(),
            item => item.RecoveryId == completion.RecoveryId);
        Assert.Equal(ProjectDeletionCompletionOperation.ProjectDeletion, notice.Operation);
        Assert.Empty(notice.Warnings);
    }

    [Fact]
    public async Task Concurrent_exact_project_cleanup_callers_observe_one_terminal_mutation()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var setupScope = application.Services.CreateAsyncScope();
        var dbContextFactory = setupScope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectId = Guid.NewGuid();
        var recoveryId = await SeedProjectDeletionMutationAsync(
            dbContextFactory,
            projectId,
            ProjectCrossModuleMutationStatus.WorkbenchCommitted,
            new DeleteProjectMutationPayload([], []));
        await using var firstScope = application.Services.CreateAsyncScope();
        await using var secondScope = application.Services.CreateAsyncScope();
        var firstProjects = firstScope.ServiceProvider.GetRequiredService<ProjectsService>();
        var secondProjects = secondScope.ServiceProvider.GetRequiredService<ProjectsService>();
        var start = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<ProjectDeletionResult> RetryAsync(ProjectsService projects)
        {
            await start.Task;
            return await projects.RetryDeletionCleanupAsync(
                projectId,
                WorkbenchParticipantId,
                recoveryId);
        }

        var first = RetryAsync(firstProjects);
        var second = RetryAsync(secondProjects);
        start.SetResult();
        var results = await Task.WhenAll(first, second)
            .WaitAsync(TimeSpan.FromSeconds(15));

        Assert.All(results, result =>
        {
            Assert.Equal(projectId, result.ProjectId);
            Assert.Empty(result.Warnings);
        });
        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var terminalMutation = await verificationContext.Set<ProjectCrossModuleMutationRecord>()
            .AsNoTracking()
            .SingleAsync(record => record.Id == recoveryId);
        Assert.Equal(ProjectCrossModuleMutationStatus.Completed, terminalMutation.Status);
        Assert.Equal(1, terminalMutation.AttemptCount);
    }

    [Fact]
    public async Task Project_deletion_waits_for_the_participant_binding_gate_before_planning_or_deleting_bytes()
    {
        await using var application = await CreateObservedDeletionApplicationAsync(
            failFirstFileSystemDelete: false);
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var workspacePathResolver = scope.ServiceProvider
            .GetRequiredService<IWorkspacePathResolver>();
        var registry = scope.ServiceProvider
            .GetRequiredService<ObservedStorageDriverRegistry>();
        var projectId = await CreateProjectAsync(projects, "Project deletion binding gate");
        var asset = await CreateImageAsync(workbench, projectId, "Binding gate image");
        var physicalPath = Path.Combine(
            workspacePathResolver.ResolveWorkspaceRoot(),
            asset.MediaRelativePath.Replace('/', Path.DirectorySeparatorChar));
        string connectionString;
        await using (var providerContext = await dbContextFactory.CreateDbContextAsync())
        {
            Assert.Equal(
                "Npgsql.EntityFrameworkCore.PostgreSQL",
                providerContext.Database.ProviderName);
            connectionString = providerContext.Database.GetConnectionString()
                ?? throw new InvalidOperationException(
                    "The PostgreSQL integration context did not expose a connection string.");
        }

        await using var lockConnection = new NpgsqlConnection(connectionString);
        await lockConnection.OpenAsync();
        await using var lockTransaction = await lockConnection.BeginTransactionAsync();
        await using (var lockCommand = lockConnection.CreateCommand())
        {
            lockCommand.Transaction = lockTransaction;
            lockCommand.CommandText =
                "select pg_advisory_xact_lock(hashtextextended(@scope_key, 0));";
            lockCommand.Parameters.AddWithValue(
                "scope_key",
                ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey);
            await lockCommand.ExecuteNonQueryAsync();
        }

        await using var monitoringConnection = new NpgsqlConnection(connectionString);
        await monitoringConnection.OpenAsync();
        var deletion = projects.DeleteAsync(projectId);
        await WaitForAdvisoryWaitersAsync(monitoringConnection, expectedCount: 1);

        bool projectExistedWhileBlocked;
        bool projectObjectExistedWhileBlocked;
        bool bindingExistedWhileBlocked;
        bool deletionMutationExistedWhileBlocked;
        await using (var blockedContext = await dbContextFactory.CreateDbContextAsync())
        {
            projectExistedWhileBlocked = await blockedContext.Set<Project>()
                .AnyAsync(record => record.Id == projectId);
            var projectObjectId = await blockedContext.Set<ProjectObjectRecord>()
                .Where(record =>
                    record.ProjectId == projectId &&
                    record.NodeKey == asset.Id)
                .Select(record => (Guid?)record.Id)
                .SingleOrDefaultAsync();
            projectObjectExistedWhileBlocked = projectObjectId.HasValue;
            bindingExistedWhileBlocked = projectObjectId.HasValue &&
                await blockedContext.Set<ProjectNodeBindingRecord>()
                    .AnyAsync(record => record.ProjectObjectId == projectObjectId.Value);
            deletionMutationExistedWhileBlocked = await blockedContext
                .Set<ProjectCrossModuleMutationRecord>()
                .AnyAsync(record =>
                    record.ProjectId == projectId &&
                    record.MutationKind == ProjectCrossModuleMutationKind.DeleteProject);
        }

        var deletionCompletedWhileBlocked = deletion.IsCompleted;
        var physicalBytesExistedWhileBlocked = File.Exists(physicalPath);
        var deleteCallsWhileBlocked = registry.FileSystemDeleteCalls;

        await lockTransaction.CommitAsync();
        var result = await deletion.WaitAsync(TimeSpan.FromSeconds(15));

        Assert.False(deletionCompletedWhileBlocked);
        Assert.True(projectExistedWhileBlocked);
        Assert.True(projectObjectExistedWhileBlocked);
        Assert.True(bindingExistedWhileBlocked);
        Assert.False(deletionMutationExistedWhileBlocked);
        Assert.True(physicalBytesExistedWhileBlocked);
        Assert.Equal(0, deleteCallsWhileBlocked);
        Assert.Equal(projectId, result.ProjectId);
        Assert.Empty(result.Warnings);
        Assert.False(File.Exists(physicalPath));
        Assert.Equal(1, registry.FileSystemDeleteCalls);

        await using var completedContext = await dbContextFactory.CreateDbContextAsync();
        Assert.False(await completedContext.Set<Project>()
            .AnyAsync(record => record.Id == projectId));
        Assert.False(await completedContext.Set<ProjectObjectRecord>()
            .AnyAsync(record => record.ProjectId == projectId));
        Assert.False(await completedContext.Set<ProjectNodeBindingRecord>()
            .AnyAsync(record => record.MediaRelativePath == asset.MediaRelativePath));
        Assert.Equal(
            ProjectCrossModuleMutationStatus.Completed,
            (await completedContext.Set<ProjectCrossModuleMutationRecord>()
                .SingleAsync(record =>
                    record.ProjectId == projectId &&
                    record.MutationKind == ProjectCrossModuleMutationKind.DeleteProject)).Status);
    }

    [Fact]
    public async Task PostgreSql_advisory_scope_blocks_owned_keys_allows_independent_keys_and_releases_on_commit()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var ownedProjectKey = ProjectMutationScopeKeys.ForProject(Guid.NewGuid());
        var independentProjectKey = ProjectMutationScopeKeys.ForProject(Guid.NewGuid());
        var bindingGateKey =
            ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey;
        await using var ownerContext = await dbContextFactory.CreateDbContextAsync();
        Assert.Equal(
            "Npgsql.EntityFrameworkCore.PostgreSQL",
            ownerContext.Database.ProviderName);
        await using var ownerScope = await SerializableMutationScope.BeginAsync(
            ownerContext,
            [bindingGateKey, ownedProjectKey],
            CancellationToken.None);

        await AssertRelationalScopeBlockedAsync(dbContextFactory, ownedProjectKey);
        await AssertRelationalScopeBlockedAsync(dbContextFactory, bindingGateKey);
        await AcquireRelationalScopeAsync(
            dbContextFactory,
            [independentProjectKey],
            CancellationToken.None);

        await ownerScope.CommitAsync(CancellationToken.None);
        await AcquireRelationalScopeAsync(
            dbContextFactory,
            [ownedProjectKey],
            CancellationToken.None);
        await AcquireRelationalScopeAsync(
            dbContextFactory,
            [bindingGateKey],
            CancellationToken.None);
    }

    [Fact]
    public async Task PostgreSql_reverse_multi_key_requests_complete_without_deadlock()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var firstKey = ProjectMutationScopeKeys.ForProject(Guid.NewGuid());
        var secondKey = ProjectMutationScopeKeys.ForProject(Guid.NewGuid());
        var start = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var forward = AcquireRelationalScopeAfterSignalAsync(
            dbContextFactory,
            [firstKey, secondKey],
            start.Task);
        var reverse = AcquireRelationalScopeAfterSignalAsync(
            dbContextFactory,
            [secondKey, firstKey],
            start.Task);
        start.SetResult();

        await Task.WhenAll(forward, reverse)
            .WaitAsync(TimeSpan.FromSeconds(15));
    }

    private static Task<TestApplication> CreateFailOnceDeletionApplicationAsync()
        => CreateObservedDeletionApplicationAsync(failFirstFileSystemDelete: true);

    private static Task<TestApplication> CreateObservedDeletionApplicationAsync(
        bool failFirstFileSystemDelete)
    {
        return TestApplication.CreateAsync(new TestHarnessOptions
        {
            ConfigureServices = services =>
            {
                services.RemoveAll<IStorageDriverRegistry>();
                services.AddSingleton(serviceProvider =>
                    new ObservedStorageDriverRegistry(
                        serviceProvider.GetServices<IStorageDriver>(),
                        failFirstFileSystemDelete));
                services.AddSingleton<IStorageDriverRegistry>(serviceProvider =>
                    serviceProvider.GetRequiredService<ObservedStorageDriverRegistry>());
            }
        });
    }

    private static async Task<Guid> SeedProjectDeletionMutationAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        Guid projectId,
        ProjectCrossModuleMutationStatus status,
        DeleteProjectMutationPayload payload,
        DateTimeOffset? lastAttemptAtUtc = null,
        int attemptCount = 0,
        string errorMessage = "")
    {
        var timestamp = DateTimeOffset.UtcNow;
        var mutation = new ProjectCrossModuleMutationRecord
        {
            ProjectId = projectId,
            ScopeNodeKey = ProjectDeletionScopeNodeKey,
            MutationKind = ProjectCrossModuleMutationKind.DeleteProject,
            Status = status,
            ApprovalState = ProjectCrossModuleMutationApprovalState.NotRequired,
            PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
            ErrorMessage = errorMessage,
            AttemptCount = attemptCount,
            LastAttemptAtUtc = lastAttemptAtUtc,
            CompletedAtUtc = status == ProjectCrossModuleMutationStatus.Completed
                ? timestamp
                : null,
            CreatedAtUtc = timestamp,
            UpdatedAtUtc = timestamp
        };
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        dbContext.Set<ProjectCrossModuleMutationRecord>().Add(mutation);
        await dbContext.SaveChangesAsync();
        return mutation.Id;
    }

    private static DateTimeOffset TruncateToPostgreSqlPrecision(DateTimeOffset value)
        => value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMicrosecond));

    private static void AssertRetainedByProviderWarning(
        ProjectDeletionWarning warning,
        Guid recoveryId,
        StorageObjectReference reference)
    {
        Assert.Equal(
            ProjectDeletionWarningKind.ManagedStorageRetainedByProvider,
            warning.Kind);
        Assert.Equal(WorkbenchParticipantId, warning.ParticipantId);
        Assert.Equal(recoveryId, warning.RecoveryId);
        Assert.Equal(reference.ProviderKind, warning.RetainedObject.Provider);
        Assert.Equal(reference.StorageId, warning.RetainedObject.StorageId);
        Assert.Equal(reference.LocatorKind, warning.RetainedObject.LocatorKind);
        Assert.Equal(reference.Locator, warning.RetainedObject.Locator);
        Assert.NotEmpty(warning.RetainedObject.Reason);
        Assert.NotEmpty(warning.Message);
        Assert.NotEmpty(warning.Remediation);
    }

    private static async Task AssertRelationalScopeBlockedAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        string scopeKey)
    {
        await using var contenderContext =
            await dbContextFactory.CreateDbContextAsync();
        await using var transaction = await contenderContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);
        using var cancellationSource = new CancellationTokenSource();
        var acquisition = SerializableMutationScope.AcquireRelationalScopeLocksAsync(
            contenderContext,
            [scopeKey],
            cancellationSource.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(250));
        Assert.False(acquisition.IsCompleted);
        await cancellationSource.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await acquisition);
    }

    private static async Task AcquireRelationalScopeAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IReadOnlyCollection<string> scopeKeys,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(
            cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        await SerializableMutationScope.AcquireRelationalScopeLocksAsync(
            dbContext,
            scopeKeys,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task AcquireRelationalScopeAfterSignalAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IReadOnlyCollection<string> scopeKeys,
        Task startSignal)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable);
        await startSignal;
        await SerializableMutationScope.AcquireRelationalScopeLocksAsync(
            dbContext,
            scopeKeys,
            CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        await transaction.CommitAsync();
    }

    private static async Task WaitForAdvisoryWaitersAsync(
        NpgsqlConnection connection,
        int expectedCount)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                select count(*)
                from pg_locks
                where locktype = 'advisory'
                  and not granted
                  and database = (
                      select oid
                      from pg_database
                      where datname = current_database());
                """;
            var count = Convert.ToInt32(await command.ExecuteScalarAsync());
            if (count >= expectedCount)
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException(
            $"Expected {expectedCount} advisory-lock waiter(s) before the test timeout.");
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projects, string name)
    {
        var result = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Objective = "Validate coordinated deletion.",
            CurrentPhase = "Validation"
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static Task<ProjectStructureNode> CreateImageAsync(
        ProjectWorkbenchService workbench,
        Guid projectId,
        string title)
    {
        return workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ImageAsset,
                title,
                string.Empty,
                "Managed image for deletion coverage.",
                BuildProjectRootNodeKey(projectId),
                240,
                180,
                Media: new ProjectObjectMediaPayload(
                    "pixel.png",
                    "image/png",
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=")));
    }

    private static string BuildProjectRootNodeKey(Guid projectId)
        => $"project:{projectId}";

    private sealed class NonHideablePhaseProjectionContributor : IProjectStructureProjectionContributor
    {
        public const string NodeKey = "projection:non-hideable-phase";

        public Task ContributeAsync(
            ProjectStructureProjectionContext context,
            CancellationToken cancellationToken)
        {
            context.AddNode(new ProjectObjectRecord
            {
                NodeKey = NodeKey,
                ParentNodeKey = BuildProjectRootNodeKey(context.ProjectId),
                ObjectType = ProjectObjectType.Phase,
                Title = "Projected phase",
                CreatedAtUtc = context.AssembledAtUtc,
                UpdatedAtUtc = context.AssembledAtUtc
            });
            return Task.CompletedTask;
        }
    }

    private static TPayload Deserialize<TPayload>(string json)
        => JsonSerializer.Deserialize<TPayload>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
           ?? throw new InvalidOperationException($"Unable to deserialize {typeof(TPayload).Name}.");

    private sealed class StaticWorkspacePathResolver(string workspaceRoot) : IWorkspacePathResolver
    {
        public string ResolveWorkspaceRoot() => workspaceRoot;

        public string ResolveManagedFilesRoot() => Path.Combine(workspaceRoot, "managed-files");

        public string ResolveExportsRoot() => Path.Combine(workspaceRoot, "exports");

        public string ResolveEvidenceRoot() => Path.Combine(workspaceRoot, "evidence");

        public string ResolveManagerArtifactsRoot() => Path.Combine(workspaceRoot, "manager-artifacts");
    }

    private sealed class ObservedStorageDriverRegistry : IStorageDriverRegistry
    {
        private readonly IReadOnlyDictionary<StorageProviderKind, IStorageDriver> drivers;
        private readonly bool failFirstFileSystemDelete;
        private int fileSystemDeleteCalls;

        public ObservedStorageDriverRegistry(
            IEnumerable<IStorageDriver> registeredDrivers,
            bool failFirstFileSystemDelete)
        {
            this.failFirstFileSystemDelete = failFirstFileSystemDelete;
            drivers = registeredDrivers.ToDictionary(
                driver => driver.ProviderKind,
                driver => driver.ProviderKind == StorageProviderKind.FileSystem
                    ? new ObservedDeleteStorageDriver(driver, OnFileSystemDelete)
                    : driver);
        }

        public int FileSystemDeleteCalls => Volatile.Read(ref fileSystemDeleteCalls);

        public IReadOnlyCollection<StorageProviderKind> RegisteredKinds => drivers.Keys.ToArray();

        public bool TryResolve(StorageProviderKind providerKind, out IStorageDriver driver)
            => drivers.TryGetValue(providerKind, out driver!);

        public IStorageDriver Resolve(StorageProviderKind providerKind)
            => drivers.TryGetValue(providerKind, out var driver)
                ? driver
                : throw new InvalidOperationException($"Storage provider '{providerKind}' is not registered.");

        private bool OnFileSystemDelete()
        {
            var callCount = Interlocked.Increment(ref fileSystemDeleteCalls);
            return failFirstFileSystemDelete && callCount == 1;
        }
    }

    private sealed class ObservedDeleteStorageDriver(
        IStorageDriver inner,
        Func<bool> shouldFail) : IStorageDriver
    {
        public StorageProviderKind ProviderKind => inner.ProviderKind;

        public StorageCapability SupportedCapabilities => inner.SupportedCapabilities;

        public Task<StorageConnectionTestResult> TestConnectionAsync(
            StorageCatalogRecord storage,
            string? secretValue,
            CancellationToken cancellationToken = default)
            => inner.TestConnectionAsync(storage, secretValue, cancellationToken);

        public Task<StorageWriteResult> SaveAsync(
            StorageCatalogRecord storage,
            StorageWriteRequest request,
            CancellationToken cancellationToken = default)
            => inner.SaveAsync(storage, request, cancellationToken);

        public Task<Stream> OpenReadAsync(
            StorageCatalogRecord storage,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
            => inner.OpenReadAsync(storage, reference, cancellationToken);

        public Task DeleteAsync(
            StorageCatalogRecord storage,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
        {
            return shouldFail()
                ? Task.FromException(new IOException("Injected first-delete failure."))
                : inner.DeleteAsync(storage, reference, cancellationToken);
        }
    }
}
