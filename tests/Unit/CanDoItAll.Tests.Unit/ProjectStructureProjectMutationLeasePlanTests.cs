using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureProjectMutationLeasePlanTests
{
    [Fact]
    public void Create_orders_projects_and_normalizes_lease_tokens()
    {
        var firstProjectId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var secondProjectId = Guid.Parse("00000000-0000-0000-0000-000000000002");

        var plan = ProjectStructureProjectMutationLeasePlan.Create(
        [
            new ProjectStructureProjectMutationLeaseRequest(secondProjectId, " second-token "),
            new ProjectStructureProjectMutationLeaseRequest(firstProjectId, " first-token ")
        ]);

        Assert.Collection(
            plan,
            request =>
            {
                Assert.Equal(firstProjectId, request.ProjectId);
                Assert.Equal("first-token", request.LeaseToken);
            },
            request =>
            {
                Assert.Equal(secondProjectId, request.ProjectId);
                Assert.Equal("second-token", request.LeaseToken);
            });
    }

    [Fact]
    public void Create_merges_duplicate_project_requests_with_the_same_token()
    {
        var projectId = Guid.NewGuid();

        var plan = ProjectStructureProjectMutationLeasePlan.Create(
        [
            new ProjectStructureProjectMutationLeaseRequest(projectId),
            new ProjectStructureProjectMutationLeaseRequest(projectId, "lease-token"),
            new ProjectStructureProjectMutationLeaseRequest(projectId, " lease-token ")
        ]);

        var request = Assert.Single(plan);
        Assert.Equal(projectId, request.ProjectId);
        Assert.Equal("lease-token", request.LeaseToken);
    }

    [Fact]
    public void Create_rejects_conflicting_tokens_for_one_project()
    {
        var projectId = Guid.NewGuid();

        var exception = Assert.Throws<ArgumentException>(() =>
            ProjectStructureProjectMutationLeasePlan.Create(
            [
                new ProjectStructureProjectMutationLeaseRequest(projectId, "lease-one"),
                new ProjectStructureProjectMutationLeaseRequest(projectId, "lease-two")
            ]));

        Assert.Contains(projectId.ToString("D"), exception.Message, StringComparison.Ordinal);
    }
}

public sealed class ProjectStructureMutationLeaseCleanupTests
{
    [Fact]
    public async Task CompleteAsync_attempts_every_release_in_reverse_order_after_a_release_failure()
    {
        var firstLease = CreateLease("first-project");
        var secondLease = CreateLease("second-project");
        var releaseFailure = new InvalidOperationException("The second project lease could not be released.");
        var releaseOrder = new List<string>();

        var exception = await Assert.ThrowsAsync<AggregateException>(() =>
            ProjectStructureMutationLeaseCleanup.CompleteAsync(
                [firstLease, secondLease],
                lease =>
                {
                    releaseOrder.Add(lease.ScopeKey);
                    return lease == secondLease
                        ? Task.FromException(releaseFailure)
                        : Task.CompletedTask;
                },
                operationFailure: null));

        Assert.Equal([secondLease.ScopeKey, firstLease.ScopeKey], releaseOrder);
        Assert.Same(releaseFailure, Assert.Single(exception.InnerExceptions));
    }

    [Fact]
    public async Task CompleteAsync_aggregates_the_operation_failure_and_every_release_failure()
    {
        var firstLease = CreateLease("first-project");
        var secondLease = CreateLease("second-project");
        var operationFailure = new InvalidOperationException("The project mutation failed.");
        var secondReleaseFailure = new InvalidOperationException("The second release failed.");
        var firstReleaseFailure = new InvalidOperationException("The first release failed.");

        var exception = await Assert.ThrowsAsync<AggregateException>(() =>
            ProjectStructureMutationLeaseCleanup.CompleteAsync(
                [firstLease, secondLease],
                lease => Task.FromException(
                    lease == secondLease
                        ? secondReleaseFailure
                        : firstReleaseFailure),
                operationFailure));

        Assert.Collection(
            exception.InnerExceptions,
            failure => Assert.Same(operationFailure, failure),
            failure => Assert.Same(secondReleaseFailure, failure),
            failure => Assert.Same(firstReleaseFailure, failure));
    }

    [Fact]
    public async Task CompleteAsync_rethrows_the_operation_failure_when_every_release_succeeds()
    {
        var operationFailure = new InvalidOperationException("The project mutation failed.");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ProjectStructureMutationLeaseCleanup.CompleteAsync(
                [CreateLease("project")],
                _ => Task.CompletedTask,
                operationFailure));

        Assert.Same(operationFailure, exception);
    }

    private static ProjectStructureLeaseSnapshot CreateLease(string scopeKey)
    {
        var now = DateTimeOffset.UtcNow;
        return new ProjectStructureLeaseSnapshot(
            ProjectStructureLeaseScopeKind.Project,
            scopeKey,
            $"{scopeKey}-token",
            "agent",
            "Agent",
            "machine",
            string.Empty,
            string.Empty,
            "Test lease",
            now,
            now,
            now.AddMinutes(5),
            IsActive: true);
    }
}
