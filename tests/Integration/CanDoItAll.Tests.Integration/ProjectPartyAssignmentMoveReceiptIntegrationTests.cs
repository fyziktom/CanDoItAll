using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectPartyAssignmentMoveReceiptIntegrationTests
{
    [Fact]
    public async Task Commit_then_processor_failure_and_exact_retry_preserve_moved_assignments_once()
    {
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            ConfigureServices = services =>
            {
                services.AddSingleton<CommitThenThrowMoveState>();
                services.AddScoped<IProjectPartyIntegrationBridge>(serviceProvider =>
                {
                    var proxy = DispatchProxy.Create<
                        IProjectPartyIntegrationBridge,
                        CommitThenThrowMoveProxy>();
                    var configured = (CommitThenThrowMoveProxy)(object)proxy;
                    configured.Inner = serviceProvider
                        .GetRequiredService<ProjectPartyIntegrationService>();
                    configured.State = serviceProvider
                        .GetRequiredService<CommitThenThrowMoveState>();
                    return proxy;
                });
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var processor = scope.ServiceProvider
            .GetRequiredService<ProjectCrossModuleMutationProcessor>();
        var now = DateTimeOffset.UtcNow;
        var sourceProjectId = Guid.NewGuid();
        var targetProjectId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var sourceAssignmentId = Guid.NewGuid();
        var staleTargetAssignmentId = Guid.NewGuid();
        var mutation = new ProjectCrossModuleMutationRecord
        {
            ProjectId = sourceProjectId,
            ScopeNodeKey = "node-1",
            MutationKind = ProjectCrossModuleMutationKind.MoveSelectedNodes,
            Status = ProjectCrossModuleMutationStatus.WorkbenchCommitted,
            ApprovalState = ProjectCrossModuleMutationApprovalState.NotRequired,
            PayloadJson = JsonSerializer.Serialize(new MoveDescendantsMutationPayload(
                sourceProjectId,
                targetProjectId,
                "node-1",
                ["node-1"],
                ["node-1"])),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Set<Project>().AddRange(
                CreateProject(sourceProjectId, "Move source", now),
                CreateProject(targetProjectId, "Move target", now));
            dbContext.Set<Party>().Add(new Party
            {
                Id = partyId,
                PartyType = PartyType.Person,
                LifecycleStatus = PartyLifecycleStatus.Active,
                DisplayName = "Move receipt party",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            dbContext.Set<ProjectPartyAssignment>().AddRange(
                new ProjectPartyAssignment
                {
                    Id = sourceAssignmentId,
                    ProjectId = sourceProjectId,
                    PartyId = partyId,
                    AssignmentKind = ProjectPartyAssignmentKind.TeamMember,
                    NodeKey = "node-1"
                },
                new ProjectPartyAssignment
                {
                    Id = staleTargetAssignmentId,
                    ProjectId = targetProjectId,
                    PartyId = partyId,
                    AssignmentKind = ProjectPartyAssignmentKind.TeamMember,
                    NodeKey = "node-1"
                });
            dbContext.Set<ProjectCrossModuleMutationRecord>().Add(mutation);
            await dbContext.SaveChangesAsync();
        }

        Assert.Equal(
            ProjectCrossModuleMutationStatus.Failed,
            await processor.ProcessAsync(mutation.Id));
        await AssertMoveStateAsync(
            dbContextFactory,
            mutation.Id,
            sourceProjectId,
            targetProjectId,
            partyId,
            sourceAssignmentId,
            staleTargetAssignmentId,
            ProjectCrossModuleMutationStatus.Failed);

        Assert.Equal(
            ProjectCrossModuleMutationStatus.Completed,
            await processor.ProcessAsync(mutation.Id));
        await AssertMoveStateAsync(
            dbContextFactory,
            mutation.Id,
            sourceProjectId,
            targetProjectId,
            partyId,
            sourceAssignmentId,
            staleTargetAssignmentId,
            ProjectCrossModuleMutationStatus.Completed);
    }

    private static Project CreateProject(
        Guid projectId,
        string name,
        DateTimeOffset now)
        => new()
        {
            Id = projectId,
            Name = name,
            Slug = $"{name.ToLowerInvariant().Replace(' ', '-')}-{projectId:N}",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

    private static async Task AssertMoveStateAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        Guid mutationId,
        Guid sourceProjectId,
        Guid targetProjectId,
        Guid partyId,
        Guid sourceAssignmentId,
        Guid staleTargetAssignmentId,
        ProjectCrossModuleMutationStatus expectedStatus)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var receipts = await dbContext
            .Set<ProjectPartyAssignmentMoveReceipt>()
            .AsNoTracking()
            .Where(item => item.OperationId == mutationId)
            .ToListAsync();
        var receipt = Assert.Single(receipts);
        Assert.Equal(mutationId, receipt.OperationId);
        Assert.Equal(sourceProjectId, receipt.SourceProjectId);
        Assert.Equal(targetProjectId, receipt.TargetProjectId);
        Assert.Equal(BuildNodeSetFingerprint("node-1"), receipt.NodeSetFingerprint);
        var assignments = await dbContext.Set<ProjectPartyAssignment>()
            .AsNoTracking()
            .Where(item => item.NodeKey == "node-1")
            .ToListAsync();
        var assignment = Assert.Single(assignments);
        Assert.Equal(sourceAssignmentId, assignment.Id);
        Assert.Equal(targetProjectId, assignment.ProjectId);
        Assert.Equal(partyId, assignment.PartyId);
        Assert.Equal(ProjectPartyAssignmentKind.TeamMember, assignment.AssignmentKind);
        Assert.Equal("node-1", assignment.NodeKey);
        Assert.DoesNotContain(assignments, item => item.Id == staleTargetAssignmentId);
        Assert.Equal(
            expectedStatus,
            (await dbContext.Set<ProjectCrossModuleMutationRecord>()
                .AsNoTracking()
                .SingleAsync(item => item.Id == mutationId)).Status);
    }

    private static string BuildNodeSetFingerprint(params string[] nodeKeys)
    {
        var canonicalPayload = JsonSerializer.Serialize(
            nodeKeys.Order(StringComparer.Ordinal));
        return Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload)));
    }

    private sealed class CommitThenThrowMoveState
    {
        public int FailureInjected;
    }

    private class CommitThenThrowMoveProxy : DispatchProxy
    {
        public IProjectPartyIntegrationBridge Inner { get; set; } = null!;

        public CommitThenThrowMoveState State { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            ArgumentNullException.ThrowIfNull(args);
            if (targetMethod.Name ==
                nameof(IProjectPartyIntegrationBridge.MoveAssignmentsToProjectAsync))
            {
                return CommitThenThrowAsync(targetMethod, args);
            }

            try
            {
                return targetMethod.Invoke(Inner, args);
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }

        private async Task CommitThenThrowAsync(
            MethodInfo targetMethod,
            object?[] args)
        {
            var operation = (Task?)targetMethod.Invoke(Inner, args)
                ?? throw new InvalidOperationException(
                    "The project-party assignment move did not return a task.");
            await operation;
            if (Interlocked.Exchange(ref State.FailureInjected, 1) == 0)
            {
                throw new SimulatedPostCommitFailureException();
            }
        }
    }

    private sealed class SimulatedPostCommitFailureException : Exception
    {
    }
}
