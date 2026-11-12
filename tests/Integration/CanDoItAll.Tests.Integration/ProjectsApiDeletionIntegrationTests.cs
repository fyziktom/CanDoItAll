using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Web.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed class ProjectsApiDeletionIntegrationTests
{
    private static readonly JsonSerializerOptions LifecycleJson = new(JsonSerializerDefaults.Web) {
        Converters = { new ProjectOperationEpochHttpJsonConverter() }
    };
    [Fact]
    public async Task Save_with_an_original_read_of_a_deleted_project_returns_typed_not_found()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        using var created = await host.Client.PostAsJsonAsync("/api/projects/", new ProjectEditorModel {
            Name = "Project read before deletion",
            Objective = "Prove the direct project-save HTTP boundary.",
            CurrentPhase = "Validation"
        });
        created.EnsureSuccessStatusCode();
        var projectId = await created.Content.ReadFromJsonAsync<Guid>();
        var editor = (await host.Client.GetFromJsonAsync<ProjectEditorModel>($"/api/projects/{projectId:D}"))!;
        Assert.Null((await DeleteOriginalAsync(host.Client, projectId)).CompletionIssue);
        editor.Name = "Missing project update";
        using var response = await host.Client.PostAsJsonAsync("/api/projects/", editor);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var error = Assert.Single(body.RootElement.GetProperty("errors").EnumerateArray());
        Assert.Equal(ProjectErrorCodes.NotFound, error.GetProperty("code").GetString());
        Assert.DoesNotContain((await host.Client.GetFromJsonAsync<ProjectSummary[]>("/api/projects/"))!,
            project => project.Id == projectId);
    }

    [Fact]
    public async Task Delete_retains_acceptance_and_exact_recovery_retry_requires_owner_release() {
        var participant = new FailOnceProjectDeletionParticipant();
        var disposal = new OwnerDisposalFault();
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            configureServices: services => {
                services.AddSingleton<IProjectDeletionParticipant>(participant);
                services.AddSingleton<IDbContextFactory<ProjectsDbContext>>(provider => new OwnerFactory(
                    new DbContextOptionsBuilder<ProjectsDbContext>(provider.GetRequiredService<DbContextOptions<ProjectsDbContext>>())
                        .AddInterceptors(new OwnerSave(disposal), new OwnerDisposal(disposal)).Options));
            });
        await using var participantScope = host.App.Services.CreateAsyncScope();
        participant.Releases = participantScope.ServiceProvider.GetRequiredService<IProjectDeletionCompletionQuery>();
        Guid projectId;
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
            var created = await projects.SaveAsync(new ProjectEditorModel
            {
                Name = "API deletion recovery",
                Objective = "Prove HTTP partial-commit semantics.",
                CurrentPhase = "Validation"
            });
            Assert.True(created.IsSuccess);
            projectId = created.Value;
        }

        var partial = await DeleteOriginalAsync(host.Client, projectId);
        using var retryResponse = await host.Client.PostAsync(
            $"/api/projects/{projectId:D}/deletion-cleanups/{participant.Id.Value}/{participant.RecoveryId:D}/retry",
            content: null);

        Assert.Equal(ProjectLifecycleCompletionIssue.PostCommitProcessing, partial.CompletionIssue);
        var committed = Assert.IsType<ProjectLifecycleApiAcknowledgement.DeletionCommitted>(partial.Receipt.Acknowledgement).Completion;
        Assert.Equal(projectId, committed.Original.Project.ProjectId);
        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
        Assert.Equal(1, participant.PrepareCalls);
        Assert.Equal(2, participant.CompleteCalls);
        await using var verificationScope = host.App.Services.CreateAsyncScope();
        Assert.DoesNotContain(
            await verificationScope.ServiceProvider.GetRequiredService<ProjectsService>().ListAsync(),
            project => project.Id == projectId);

        var verifiedProjects = verificationScope.ServiceProvider.GetRequiredService<ProjectsService>();
        var another = await verifiedProjects.SaveAsync(new() { Name = "API acknowledged deletion with failed owner disposal" });
        Assert.True(another.IsSuccess);
        disposal.Armed = true;
        try {
            var owner = await DeleteOriginalAsync(host.Client, another.Value);
            Assert.Equal(ProjectLifecycleCompletionIssue.OwnerReleaseUnconfirmed, owner.CompletionIssue);
            var completion = Assert.IsType<ProjectLifecycleApiAcknowledgement.DeletionCommitted>(owner.Receipt.Acknowledgement).Completion;
            Assert.Equal(another.Value, completion.Original.Project.ProjectId);
            Assert.Contains(completion.Obligations, item => item.Participant == participant.Id && item.RecoveryId == participant.RecoveryId);
            Assert.IsType<ProjectDeletionCompletionObservation.AwaitingOwnerRelease>(await participant.Releases.ObserveAsync(completion));
            using var heldRetry = await host.Client.PostAsync(
                $"/api/projects/{another.Value:D}/deletion-cleanups/{participant.Id.Value}/{participant.RecoveryId:D}/retry", content: null);
            Assert.Equal(HttpStatusCode.Conflict, heldRetry.StatusCode);
            Assert.Equal(1, disposal.Failures);
            Assert.Equal(2, participant.PrepareCalls);
            Assert.Equal(2, participant.CompleteCalls);
            Assert.DoesNotContain(await verifiedProjects.ListAsync(), project => project.Id == another.Value);
            Assert.Contains(await verifiedProjects.ListPendingDeletionCleanupsAsync(), item =>
                item.ProjectId == another.Value && item.ParticipantId == participant.Id && item.RecoveryId == participant.RecoveryId);
        } finally {
            disposal.Armed = false;
        }
    }

    private static async Task<ProjectLifecycleApiResponse.Accepted> DeleteOriginalAsync(HttpClient client, Guid projectId) {
        var definition = (await client.GetFromJsonAsync<ProjectDefinitionReadApiResponse>(
            $"/api/projects/{projectId:D}/definition", LifecycleJson))!;
        var context = (await client.GetFromJsonAsync<ProjectLifecycleWriteContext>("/api/projects/lifecycle-write-context", LifecycleJson))!;
        var original = new ProjectLifecycleApiSubmission(new(context.DatabaseProfileId, context.Namespace,
            context.Epoch, context.CallerBinding, Guid.NewGuid()), ProjectLifecycleInterpretation.VersionOne,
            new ProjectLifecycleApiCommand.Delete(definition.Expectation));
        using var response = await client.PostAsJsonAsync("/api/projects/lifecycle-operations", original, LifecycleJson);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.IsType<ProjectLifecycleApiResponse.Accepted>(
            await response.Content.ReadFromJsonAsync<ProjectLifecycleApiResponse>(LifecycleJson));
    }

    private sealed class OwnerDisposalFault {
        public bool Armed;
        public int Failures;
        public ConcurrentDictionary<Guid, byte> Saved { get; } = new();
    }

    private sealed class OwnerFactory(DbContextOptions<ProjectsDbContext> options) : IDbContextFactory<ProjectsDbContext> {
        public ProjectsDbContext CreateDbContext() => new(options);
        public Task<ProjectsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }

    private sealed class OwnerSave(OwnerDisposalFault fault) : SaveChangesInterceptor {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (fault.Armed && eventData.Context!.ChangeTracker.Entries<Project>().Any(entry => entry.State == EntityState.Deleted)) {
                fault.Saved.TryAdd(eventData.Context!.ContextId.InstanceId, 0);
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class OwnerDisposal(OwnerDisposalFault fault) : DbConnectionInterceptor {
        public override Task ConnectionDisposedAsync(DbConnection connection, ConnectionEndEventData eventData) {
            if (fault.Saved.TryRemove(eventData.Context!.ContextId.InstanceId, out _)) {
                Interlocked.Increment(ref fault.Failures);
                throw new IOException("The deletion owner was disposed, but cleanup acknowledgement failed.");
            }
            return Task.CompletedTask;
        }
    }

    private sealed class FailOnceProjectDeletionParticipant : IProjectDeletionParticipant
    {
        private bool pending;
        private ProjectDeletionPreparationContext? original;
        internal IProjectDeletionCompletionQuery Releases { get; set; } = null!;

        public ProjectDeletionParticipantId Id { get; } = new("api-test");

        public int RecoveryProtocolVersion => 1;

        public IReadOnlyCollection<ProjectDeletionPreparationScopeKey> PreparationScopeKeys { get; } = [];

        public Guid RecoveryId { get; private set; }

        public Guid ProjectId { get; private set; }

        public int PrepareCalls { get; private set; }

        public int CompleteCalls { get; private set; }

        public Task<ProjectDeletionParticipantPreparation?> PrepareAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The HTTP deletion must retain typed original intent.");

        public Task<ProjectDeletionParticipantPreparation?> PrepareAsync(ProjectDeletionPreparationContext context,
            CancellationToken cancellationToken = default)
        {
            original = context;
            RecoveryId = Guid.NewGuid();
            PrepareCalls++;
            ProjectId = context.Project.ProjectId;
            pending = true;
            return Task.FromResult<ProjectDeletionParticipantPreparation?>(
                new(ProjectId, RecoveryId));
        }

        public async Task<ProjectDeletionParticipantCompletion> CompleteAsync(
            ProjectDeletionParticipantPreparation preparation,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(RecoveryId, preparation.RecoveryId);
            if (await Releases.ObserveAsync(original!, new(Id, RecoveryId), cancellationToken)
                    is not ProjectDeletionCompletionObservation.Released) {
                throw new ProjectDeletionParticipantCleanupException(RecoveryId, "The test participant requires original owner release.");
            }
            CompleteCalls++;
            if (CompleteCalls == 1)
            {
                throw new IOException("Injected API cleanup failure.");
            }

            pending = false;
            return ProjectDeletionParticipantCompletion.Empty(RecoveryId);
        }

        public Task<IReadOnlyList<ProjectDeletionParticipantRecovery>> ListPendingRecoveriesAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectDeletionParticipantRecovery>>(
                pending
                    ? [new(
                        ProjectId,
                        RecoveryId,
                        ProjectDeletionRecoveryStatus.Failed,
                        true,
                        null,
                        "Retry the exact test recovery.")]
                    : []);

        public Task<IReadOnlyList<ProjectDeletionParticipantCompletionNotice>> ListCompletionNoticesAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectDeletionParticipantCompletionNotice>>(
                pending || CompleteCalls == 0
                    ? []
                    : [new(
                        ProjectId,
                        RecoveryId,
                        ProjectDeletionCompletionOperation.ProjectDeletion,
                        [])]);
    }
}
