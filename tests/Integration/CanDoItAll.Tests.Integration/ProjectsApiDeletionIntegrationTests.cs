using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed class ProjectsApiDeletionIntegrationTests
{
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
        using var deleted = await host.Client.DeleteAsync($"/api/projects/{projectId:D}");
        deleted.EnsureSuccessStatusCode();
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
    public async Task Delete_returns_conflict_and_exact_recovery_retry_completes_cleanup() {
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

        using var partialResponse = await host.Client.DeleteAsync($"/api/projects/{projectId:D}");
        var partialBody = await partialResponse.Content.ReadAsStringAsync();
        using var retryResponse = await host.Client.PostAsync(
            $"/api/projects/{projectId:D}/deletion-cleanups/{participant.Id.Value}/{participant.RecoveryId:D}/retry",
            content: null);

        Assert.Equal(HttpStatusCode.Conflict, partialResponse.StatusCode);
        Assert.Contains("projects.delete-cleanup-pending", partialBody, StringComparison.Ordinal);
        using var ordinaryBody = JsonDocument.Parse(partialBody);
        Assert.False(ordinaryBody.RootElement.GetProperty("recovery").GetProperty("ownerCompletionUnconfirmed").GetBoolean());
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
            using var ownerResponse = await host.Client.DeleteAsync($"/api/projects/{another.Value:D}");
            Assert.Equal(HttpStatusCode.Conflict, ownerResponse.StatusCode);
            using var ownerBody = JsonDocument.Parse(await ownerResponse.Content.ReadAsStringAsync());
            Assert.Equal("projects.delete-cleanup-pending", ownerBody.RootElement.GetProperty("code").GetString());
            var recovery = ownerBody.RootElement.GetProperty("recovery");
            Assert.Equal(another.Value, recovery.GetProperty("projectId").GetGuid());
            Assert.True(recovery.GetProperty("ownerCompletionUnconfirmed").GetBoolean());
            Assert.Contains("operator review", ownerBody.RootElement.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains(recovery.GetProperty("failures").EnumerateArray(), item =>
                item.GetProperty("participantId").GetProperty("value").GetString() == participant.Id.Value &&
                item.GetProperty("recoveryId").GetGuid() == participant.RecoveryId);
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
            if (fault.Armed) {
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

        public ProjectDeletionParticipantId Id { get; } = new("api-test");

        public IReadOnlyCollection<ProjectDeletionPreparationScopeKey> PreparationScopeKeys { get; } = [];

        public Guid RecoveryId { get; } = Guid.NewGuid();

        public Guid ProjectId { get; private set; }

        public int PrepareCalls { get; private set; }

        public int CompleteCalls { get; private set; }

        public Task<ProjectDeletionParticipantPreparation?> PrepareAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            PrepareCalls++;
            ProjectId = projectId;
            pending = true;
            return Task.FromResult<ProjectDeletionParticipantPreparation?>(
                new(projectId, RecoveryId));
        }

        public Task<ProjectDeletionParticipantCompletion> CompleteAsync(
            ProjectDeletionParticipantPreparation preparation,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(RecoveryId, preparation.RecoveryId);
            CompleteCalls++;
            if (CompleteCalls == 1)
            {
                return Task.FromException<ProjectDeletionParticipantCompletion>(
                    new IOException("Injected API cleanup failure."));
            }

            pending = false;
            return Task.FromResult(ProjectDeletionParticipantCompletion.Empty(RecoveryId));
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
