using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessDecisionMaterialPersistenceTests {
    [Theory]
    [InlineData(ProcessDecisionChildOwner.Agent)]
    [InlineData(ProcessDecisionChildOwner.Workflow)]
    public async Task Shared_content_storage_does_not_fabricate_a_capture_executor(ProcessDecisionChildOwner executor) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var request = await OriginalAsync(context, app.Services, sharedContent: true, executor: executor);
        await Owner(context, app.Services).CommitAsync(request);
        var material = request.DecisionMaterial!;
        material.RequireOriginal((await Reader(context, app.Services).ReadAsync(material.Scope, material.Reference))!);
        var scope = await context.DecisionMaterialScopes.SingleAsync();
        Assert.Null(scope.Executor);
        Assert.Equal(ProcessDecisionMaterialProtocol.SharedContentV1, scope.MaterialProtocol);
        Assert.Equal(material.ContentContract!.SchemaId, scope.ContractId);
        Assert.Null(material.ExecutionContract);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Shared_content_must_match_its_original_schema_and_protocol_admission(int change) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var request = await OriginalAsync(context, app.Services, sharedContent: change != 3);
        var original = request.DecisionMaterial!;
        var content = ProcessDecisionPolicyFixture.ContentContract();
        var changed = change switch {
            0 => Copy(original, content: content with { SchemaId = Guid.NewGuid() }),
            1 => Copy(original, content: content with { Version = 2 }),
            2 => new ProcessDecisionMaterial(original.Scope, original.Reference, Contract(), original.CapturedAtUtc, original.PayloadJson),
            _ => ProcessDecisionMaterial.FromContent(original.Scope, original.Reference, content, original.CapturedAtUtc, original.PayloadJson)
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => Owner(context, app.Services).CommitAsync(request with {
            DecisionMaterial = changed, Decision = request.Decision! with { Material = changed.Stamp }
        }));
        Assert.Empty(await context.DecisionMaterialScopes.ToArrayAsync());
        Assert.Empty(await context.DecisionMaterials.ToArrayAsync());
    }

    [Fact]
    public async Task Legacy_scope_migration_round_trip_preserves_original_payload_capture_and_fingerprint() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var request = await OriginalAsync(context, app.Services);
        await Owner(context, app.Services).CommitAsync(request);
        await MigrateAsync(context, true, sharedOnly: true);
        await MigrateAsync(context, false, sharedOnly: true);
        var material = request.DecisionMaterial!;
        material.RequireOriginal((await Reader(context, app.Services).ReadAsync(material.Scope, material.Reference))!);
        var row = await context.DecisionMaterialScopes.AsNoTracking().SingleAsync();
        Assert.Equal(ProcessDecisionMaterialProtocol.CanonicalSnapshotV1, row.MaterialProtocol);
        Assert.Equal(material.ExecutionContract!.Executor, row.Executor);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Shared_admission_blocks_downgrade_before_and_after_capture(bool captured) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var request = await OriginalAsync(context, app.Services, sharedContent: true);
        if (captured) {
            await Owner(context, app.Services).CommitAsync(request);
        }
        var failure = await Assert.ThrowsAsync<PostgresException>(() => MigrateAsync(context, true, sharedOnly: true));
        Assert.Equal(PostgresErrorCodes.RaiseException, failure.SqlState);
        Assert.Equal(captured ? 1 : 0, await context.DecisionMaterials.CountAsync());
    }

    [Fact]
    public async Task SQL_rejects_invalid_shared_shapes_and_reader_refuses_changed_content_identity() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var request = await OriginalAsync(context, app.Services, sharedContent: true);
        await Owner(context, app.Services).CommitAsync(request);
        var material = request.DecisionMaterial!;
        var unknown = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE process_decision_material_scopes SET \"MaterialProtocol\" = 99 WHERE \"ScopeId\" = {material.Scope.Id.Value}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, unknown.SqlState);
        var executor = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE process_decision_material_scopes SET \"Executor\" = 0 WHERE \"ScopeId\" = {material.Scope.Id.Value}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, executor.SqlState);
        var scope = await context.DecisionMaterialScopes.SingleAsync();
        scope.ContractVersion++;
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => Reader(context, app.Services).ReadAsync(material.Scope, material.Reference));
        await Assert.ThrowsAsync<InvalidDataException>(() => Owner(context, app.Services).CommitAsync(request));
    }

    [Fact]
    public async Task Even_a_substituted_assignment_cannot_reinterpret_an_existing_executor_bound_scope() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var request = await OriginalAsync(context, app.Services);
        var started = await Owner(context, app.Services).CommitAsync(request);
        var refresh = await RefreshRequestAsync(context, started.State, request.DecisionMaterial!);
        var next = refresh.DecisionMaterial!;
        var shared = ProcessDecisionMaterial.FromContent(next.Scope, next.Reference, ProcessDecisionPolicyFixture.ContentContract(), next.CapturedAtUtc, next.PayloadJson);
        var assignment = await context.RuntimeStepAssignments.SingleAsync();
        var admission = ProcessDecisionDataPolicyAdmissionCodec.Deserialize(assignment.DecisionDataPolicyJson!);
        assignment.DecisionDataPolicyJson = ProcessDecisionDataPolicyAdmissionCodec.Serialize(admission with {
            MaterialProtocol = ProcessDecisionMaterialProtocol.SharedContentV1, ContentContract = ProcessDecisionPolicyFixture.ContentContract()
        });
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => Owner(context, app.Services).CommitAsync(refresh with {
            DecisionMaterial = shared, Decision = refresh.Decision! with { Material = shared.Stamp }
        }));
        Assert.Equal(1, await context.DecisionMaterials.CountAsync());
        var original = request.DecisionMaterial!;
        original.RequireOriginal((await Reader(context, app.Services).ReadAsync(original.Scope, original.Reference))!);
    }
}
