using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed class ProjectsApiDeletionIntegrationTests
{
    [Fact]
    public async Task Save_with_an_explicit_missing_project_id_returns_typed_not_found()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var missingProjectId = Guid.NewGuid();

        using var response = await host.Client.PostAsJsonAsync("/api/projects/", new ProjectEditorModel
        {
            Id = missingProjectId,
            Name = "Missing project update",
            Objective = "Prove the direct project-save HTTP boundary.",
            CurrentPhase = "Validation"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var error = Assert.Single(body.RootElement.GetProperty("errors").EnumerateArray());
        Assert.Equal(ProjectErrorCodes.NotFound, error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Delete_returns_conflict_and_exact_recovery_retry_completes_cleanup()
    {
        var participant = new FailOnceProjectDeletionParticipant();
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            configureServices: services =>
                services.AddSingleton<IProjectDeletionParticipant>(participant));
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
        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
        Assert.Equal(1, participant.PrepareCalls);
        Assert.Equal(2, participant.CompleteCalls);
        await using var verificationScope = host.App.Services.CreateAsyncScope();
        Assert.DoesNotContain(
            await verificationScope.ServiceProvider.GetRequiredService<ProjectsService>().ListAsync(),
            project => project.Id == projectId);
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
            AppDbContext dbContext,
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
