using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class ProjectLifecycleOperationTests {
    [Fact]
    public Task Hierarchy_capture_keeps_exact_edge_and_endpoint_lifetimes_and_checks_empty_original_targets() =>
        WithFixtureAsync(async fixture => {
            var parent = await fixture.SeedAsync("Hierarchy original parent");
            var child = await fixture.SeedAsync("Hierarchy original child");
            var isolated = await fixture.SeedAsync("Hierarchy isolated original");
            var intent = await fixture.SubmitIntentAsync(new ProjectLifecycleCommand.EnsureLink(parent.Project, child.Project));
            Assert.IsType<ProjectLifecycleOutcome.Accepted>(await fixture.Writer.SubmitAsync(intent, fixture.Invocation));
            var context = await HierarchyContextAsync(fixture);
            var reader = HierarchyReader(fixture);
            var before = await fixture.StateAsync();
            var edge = await reader.ReadEdgeAsync(context, parent.Project, child.Project);
            Assert.Equal(Assert.Single(before.Edges).Id, edge.EdgeId);
            Assert.Equal(parent.Project, edge.Parent);
            Assert.Equal(child.Project, edge.Child);
            Assert.Equal(edge, Assert.Single(await reader.ReadHierarchyAsync(context, parent.Project)));
            Assert.Equal(edge, Assert.Single(await reader.ReadHierarchyAsync(context, child.Project)));
            Assert.Empty(await reader.ReadHierarchyAsync(context, isolated.Project));

            var stale = new ProjectWriteAdmission(context.DatabaseProfileId, isolated.Project.ProjectId, Guid.NewGuid());
            var staleFailure = await Assert.ThrowsAsync<ProjectLifecycleHierarchyReadRefusedException>(() => reader.ReadHierarchyAsync(context, stale));
            Assert.Equal(ProjectLifecycleHierarchyReadRefusal.OriginalProjectChanged, staleFailure.Reason);
            var absent = new ProjectWriteAdmission(context.DatabaseProfileId, Guid.NewGuid(), Guid.NewGuid());
            var absentFailure = await Assert.ThrowsAsync<ProjectLifecycleHierarchyReadRefusedException>(() => reader.ReadHierarchyAsync(context, absent));
            Assert.Equal(ProjectLifecycleHierarchyReadRefusal.OriginalProjectChanged, absentFailure.Reason);
            var wrongNamespace = context with { Namespace = new DatasetNamespaceId(Guid.NewGuid()) };
            var namespaceFailure = await Assert.ThrowsAsync<ProjectLifecycleHierarchyReadRefusedException>(() => reader.ReadHierarchyAsync(wrongNamespace, isolated.Project));
            Assert.Equal(ProjectLifecycleHierarchyReadRefusal.OriginalContextChanged, namespaceFailure.Reason);
            var wrongChild = new ProjectWriteAdmission(context.DatabaseProfileId, child.Project.ProjectId, Guid.NewGuid());
            var edgeFailure = await Assert.ThrowsAsync<ProjectLifecycleHierarchyReadRefusedException>(() => reader.ReadEdgeAsync(context, parent.Project, wrongChild));
            Assert.Equal(ProjectLifecycleHierarchyReadRefusal.OriginalEdgeChanged, edgeFailure.Reason);
            before.RequireEqual(await fixture.StateAsync());

            await fixture.MutateAsync(async db => {
                db.Remove(await db.Set<ProjectHierarchyLink>().SingleAsync(row => row.Id == edge.EdgeId));
                await db.SaveChangesAsync();
            });
            var afterRemoval = await fixture.StateAsync();
            var removedFailure = await Assert.ThrowsAsync<ProjectLifecycleHierarchyReadRefusedException>(() => reader.ReadEdgeAsync(context, parent.Project, child.Project));
            Assert.Equal(ProjectLifecycleHierarchyReadRefusal.OriginalEdgeChanged, removedFailure.Reason);
            Assert.Empty(await reader.ReadHierarchyAsync(context, parent.Project));
            afterRemoval.RequireEqual(await fixture.StateAsync());
        });

    [Fact]
    public Task Hierarchy_capture_bounds_incident_degree_before_returning_witnesses_and_excludes_unrelated_edges() =>
        WithFixtureAsync(async fixture => {
            var original = await fixture.SeedAsync("Bounded hierarchy original");
            var context = await HierarchyContextAsync(fixture);
            var reader = HierarchyReader(fixture);
            var now = DateTimeOffset.UtcNow;
            var neighbors = Enumerable.Range(0, ProjectLifecycleHierarchyReader.MaximumIncidentEdges + 3)
                .Select(index => new Project { Name = $"Bounded hierarchy fixture {index}", CreatedAtUtc = now, UpdatedAtUtc = now }).ToArray();
            var edges = neighbors.Take(ProjectLifecycleHierarchyReader.MaximumIncidentEdges).Select((neighbor, index) => new ProjectHierarchyLink {
                ParentProjectId = index % 2 == 0 ? original.Project.ProjectId : neighbor.Id,
                ChildProjectId = index % 2 == 0 ? neighbor.Id : original.Project.ProjectId,
                CreatedAtUtc = now
            }).ToArray();
            await fixture.MutateAsync(async db => {
                db.AddRange(neighbors);
                db.AddRange(edges);
                db.Add(new ProjectHierarchyLink {
                    ParentProjectId = neighbors[^2].Id, ChildProjectId = neighbors[^1].Id, CreatedAtUtc = now
                });
                await db.SaveChangesAsync();
            });
            var before = await fixture.StateAsync();
            var captured = await reader.ReadHierarchyAsync(context, original.Project);
            Assert.Equal(ProjectLifecycleHierarchyReader.MaximumIncidentEdges, captured.Count);
            Assert.Equal(edges.Select(edge => edge.Id).Order(), captured.Select(edge => edge.EdgeId).Order());
            foreach (var edge in captured) {
                var neighbor = neighbors.Single(row => row.Id == (edge.Parent.ProjectId == original.Project.ProjectId ? edge.Child.ProjectId : edge.Parent.ProjectId));
                var actualNeighbor = edge.Parent.ProjectId == original.Project.ProjectId ? edge.Child : edge.Parent;
                Assert.Equal(new ProjectWriteAdmission(context.DatabaseProfileId, neighbor.Id, neighbor.LifetimeId), actualNeighbor);
                Assert.Equal(original.Project, edge.Parent.ProjectId == original.Project.ProjectId ? edge.Parent : edge.Child);
            }
            before.RequireEqual(await fixture.StateAsync());
            await fixture.MutateAsync(async db => {
                db.Add(new ProjectHierarchyLink {
                    ParentProjectId = original.Project.ProjectId, ChildProjectId = neighbors[^3].Id, CreatedAtUtc = now
                });
                await db.SaveChangesAsync();
            });
            var afterGrowth = await fixture.StateAsync();
            var refusal = await Assert.ThrowsAsync<ProjectLifecycleHierarchyReadRefusedException>(() => reader.ReadHierarchyAsync(context, original.Project));
            Assert.Equal(ProjectLifecycleHierarchyReadRefusal.DegreeLimitExceeded, refusal.Reason);
            afterGrowth.RequireEqual(await fixture.StateAsync());
        });

    [Fact]
    public Task Native_lifecycle_adapter_retains_original_submission_and_can_read_acceptance_after_target_deletion() =>
        WithFixtureAsync(async fixture => {
            var target = await fixture.SeedAsync("Native lifecycle original target");
            var services = fixture.Services;
            var codec = new ProjectLifecycleRecoveryCodec();
            var adapter = new ProjectNativeLifecycleOperations(services.GetRequiredService<ProjectLifecycleOperationProtocol>(),
                fixture.Writer, fixture.Reader, HierarchyReader(fixture), codec,
                services.GetRequiredService<ICanonicalRuntimeDatabase>(), services.GetRequiredService<DatasetActivationService>());
            var context = await adapter.GetContextAsync();
            var scope = ProjectNativeLifecycleOperations.Scope(context);
            var operationId = Guid.NewGuid();
            var frozen = adapter.Freeze(new ProjectLifecycleCommand.Delete(target), scope, context, operationId);
            Assert.Equal(operationId, frozen.Submission.Operation.Id);
            Assert.Equal(context.Epoch, frozen.Submission.Operation.Epoch);
            Assert.Equal(target, Assert.IsType<ProjectLifecycleCommand.Delete>(frozen.Submission.Command).Expected);
            Assert.Throws<InvalidOperationException>(() => adapter.Freeze(frozen.Submission.Command, scope,
                context with { Namespace = new DatasetNamespaceId(Guid.NewGuid()) }, operationId));
            fixture.Faults.Armed = true;
            var accepted = Assert.IsType<ProjectLifecycleOutcome.Accepted>(await adapter.DispatchAsync(frozen.Submission));
            Assert.Equal(frozen.Submission, accepted.Result.Original);
            var after = await fixture.StateAsync();
            Assert.DoesNotContain(target.Project.ProjectId, after.Projects);
            var found = Assert.IsType<ProjectLifecycleLookup.Found>(await adapter.LookupAsync(frozen.Submission.Operation));
            Assert.Equal(ProjectLifecycleOperationCodec.EncodeAccepted(accepted.Result), ProjectLifecycleOperationCodec.EncodeAccepted(found.Accepted));
            after.RequireEqual(await fixture.StateAsync());
        });

    private static ProjectLifecycleHierarchyReader HierarchyReader(LifecycleFixture fixture) => new(
        fixture.Services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>(),
        fixture.Services.GetRequiredService<ICanonicalRuntimeDatabase>(),
        fixture.Services.GetRequiredService<DatasetActivationService>());

    private static Task<ProjectLifecycleWriteContext> HierarchyContextAsync(LifecycleFixture fixture) =>
        fixture.Services.GetRequiredService<ProjectLifecycleOperationProtocol>().GetWriteContextAsync(ProjectMutationCallerScope.LocalWorkspace);
}
