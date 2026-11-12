using System.Data.Common;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.AgentFramework;

internal static class WorkflowProcessCancellationFixture {
    internal static readonly DateTimeOffset Now = new(2026, 10, 23, 0, 0, 0, TimeSpan.Zero);

    internal static async Task<WorkflowRunSnapshot> RunAsync(IServiceProvider services, WorkflowProcessRunId process,
        WorkflowRunId? id = null, WorkspaceScopeDescriptor? scope = null, Guid? profileId = null, bool legacy = false) {
        var canonical = services.GetRequiredService<ICanonicalRuntimeDatabase>();
        var run = new WorkflowRunSnapshot(id ?? WorkflowRunId.New(), WorkflowId.New(), WorkflowVersionId.New(),
            WorkflowRunState.WaitingForInput, WorkflowRuntimeBackendKind.InProcess, "process-cancellation", "Waiting", Now, Now);
        WorkflowLaunchOrigin origin = legacy
            ? new WorkflowLaunchOrigin.ProcessAssignment(process, new WorkflowProcessAssignmentId(Guid.NewGuid()), new("process-cancellation"))
            : new WorkflowLaunchOrigin.ProcessDispatchAssignment(new(profileId ?? canonical.Profile.Profile.Id, process, process,
                new(Guid.NewGuid()), Guid.NewGuid(), "original-owner", "original-contract", run.WorkflowId, run.VersionId, "original-input"), new("process-cancellation"));
        run = run with {
            Origin = origin with {
                AuthorizationScope = scope ?? WorkspaceScopeDescriptor.Process(process.Value.ToString("D")),
                AuthorizationPolicyFingerprint = WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint
            }
        };
        // Seed retained history; native browser tests exercise admission through the actual Process claim.
        await using var database = Context(services);
        database.Set<WorkflowRunRecordEntity>().Add(WorkflowRunRecordEntity.FromSnapshot(run));
        await database.SaveChangesAsync();
        return run;
    }

    internal static async Task<WorkflowExternalRequestRecord> RequestAsync(IServiceProvider services, WorkflowRunSnapshot run, bool native = true) {
        var requestId = WorkflowExternalRequestId.New();
        var checkpoint = await services.GetRequiredService<IWorkflowBackendCheckpointPayloadStore>().CreateAsync(new(
            new(new WorkflowBackendSessionId(run.RunId.Value.ToString("N")), run.RunId, run.WorkflowId, run.VersionId, run.Backend,
                new WorkflowBackendCheckpointFormat("maf-json"), new WorkflowBackendCheckpointFormatVersion(1),
                new WorkflowCompilerContractVersion(1), WorkflowTopologyFingerprint.Create("process-cancellation")),
            Parent: null, WorkflowBackendCheckpointPayload.Create("{\"waiting\":true}")));
        Assert.True(checkpoint.Succeeded);
        var stored = checkpoint.Checkpoint!;
        var request = new WorkflowExternalRequestRecord(requestId, run.RunId, WorkflowExternalRequestKind.HumanInput,
            new WorkflowNodeId("answer"), "Answer", "{\"question\":\"Continue?\"}", string.Empty, Now, null) {
            Version = WorkflowExternalRequestVersion.Initial,
            State = WorkflowExternalRequestState.Pending,
            ResponseContract = new(WorkflowExternalRequestKind.HumanInput, "process-answer", 1, "{\"type\":\"object\"}", 4096),
            Continuation = new(new(requestId, new WorkflowBackendRequestId(requestId.Value.ToString("N")), new WorkflowBackendRequestPortId("answer")),
                stored.Index.Link, stored.Session.CompilerContractVersion, stored.Session.TopologyFingerprint, stored.Payload.Sha256)
        };
        await using var database = Context(services);
        database.Set<WorkflowExternalRequestRecordEntity>().Add(WorkflowExternalRequestRecordEntity.FromRequest(request));
        await database.SaveChangesAsync();
        if (native) {
            Assert.True(WorkflowExternalRequestBoundaryRecord.TryCreate(request, out var boundary));
            Assert.True((await services.GetRequiredService<IWorkflowExternalRequestBoundaryStore>().UpsertAsync(boundary!)).Succeeded);
        }
        return request;
    }

    internal static async Task<WorkflowExternalResponseOperationRecord> ClaimAsync(IServiceProvider services, WorkflowRunSnapshot run,
        WorkflowExternalRequestRecord request) {
        var store = services.GetRequiredService<IWorkflowExternalResponseOperationStore>();
        var actor = new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "workflow-cancellation-test");
        var fingerprint = WorkflowExternalResponseFingerprintFactory.Create(request.Id, request.Version, actor, run.Origin!.AuthorizationScope!,
            WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint, new("process-answer"), "{\"answer\":true}");
        var accepted = await store.CreateOrReplayAsync(new(WorkflowExternalResponseOperationId.New(), request.Id, run.RunId,
            request.Version, fingerprint, actor, new("process-answer"), Now));
        var claimed = await store.TryClaimAsync(new(accepted.Operation!.Id, accepted.Operation.ConcurrencyVersion,
            new("process-response-worker"), Now, Now.AddMinutes(1), MaximumAttempts: 3));
        Assert.NotNull(claimed.Claim);
        return claimed.Operation!;
    }

    internal static WorkflowDbContext Context(IServiceProvider services)
        => services.GetRequiredService<IDbContextFactory<WorkflowDbContext>>().CreateDbContext();

    internal static PersistentWorkflowProcessCancellationRecovery Owner(IServiceProvider services, TimeProvider? clock = null,
        IInterceptor? interceptor = null) {
        var factory = services.GetRequiredService<IDbContextFactory<WorkflowDbContext>>();
        var resumes = services.GetRequiredService<IWorkflowResumeBoundaryStore>();
        if (interceptor is not null) {
            using var database = factory.CreateDbContext();
            factory = new Factory(new DbContextOptionsBuilder<WorkflowDbContext>()
                .UseNpgsql(database.Database.GetConnectionString()).AddInterceptors(interceptor).Options);
            resumes = new PersistentWorkflowResumeBoundaryStore(factory, services.GetRequiredService<IDataProtectionProvider>(),
                services.GetRequiredService<WorkflowHistoryProjection>(), services.GetRequiredService<CoordinatedDatabaseTransaction>());
        }
        return new(factory, services.GetRequiredService<ICanonicalRuntimeDatabase>(), services.GetRequiredService<IWorkflowActiveRunRegistry>(),
            services.GetRequiredService<IWorkflowExternalRequestBoundaryStore>(), resumes, clock ?? new Clock(Now));
    }

    internal sealed class Clock(DateTimeOffset now) : TimeProvider {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Factory(DbContextOptions<WorkflowDbContext> options) : IDbContextFactory<WorkflowDbContext> {
        public WorkflowDbContext CreateDbContext() => new(options);
    }

    internal sealed class LostAcknowledgement : DbTransactionInterceptor {
        internal int Commits { get; private set; }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) {
            Commits++;
            throw new IOException("Injected Workflow cancellation commit acknowledgement loss.");
        }
    }
}
