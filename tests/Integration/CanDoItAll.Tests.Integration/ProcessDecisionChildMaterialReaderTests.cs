using System.Data.Common;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionRunScopeFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessDecisionChildMaterialReaderTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot, false, ProcessDecisionChildOwner.Agent)]
    [InlineData(ReadBasisMode.RunSnapshot, true, ProcessDecisionChildOwner.Agent)]
    [InlineData(ReadBasisMode.RunSnapshot, true, ProcessDecisionChildOwner.Workflow)]
    [InlineData(ReadBasisMode.StepSnapshot, false, ProcessDecisionChildOwner.Agent)]
    [InlineData(ReadBasisMode.StepSnapshot, false, ProcessDecisionChildOwner.Workflow)]
    public async Task Composed_reader_reopens_original_material_policy_and_grant_without_new_allocations(ReadBasisMode mode,
        bool descendant, ProcessDecisionChildOwner executor) {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var context = Context(services);
        var fixture = await ProcessDecisionChildMaterialFixture.CreateAsync(context, services, mode, descendant, executor);
        var allocations = await context.DecisionBudgetAllocations.CountAsync();
        await using var fresh = app.Services.CreateAsyncScope();
        Assert.Same(fresh.ServiceProvider.GetRequiredService<EfProcessDecisionChildQuery>(),
            fresh.ServiceProvider.GetRequiredService<IProcessDecisionChildReader>());
        var original = ProcessDecisionChildMaterialBindingCodec.Deserialize(ProcessDecisionChildMaterialBindingCodec.Serialize(fixture.Binding));
        var read = await Reader(fresh.ServiceProvider).ReadOriginalAsync(original, original.ExecutionContract, fixture.Material.ContentContract);
        fixture.Material.RequireOriginal(read.Material);
        Assert.Equal(original.Child, read.Child.Admission);
        Assert.True(read.Child.IsCurrent);
        Assert.Null(read.Child.Closure);
        Assert.Null(read.Child.Settlement);
        Assert.Equal(executor, read.Assignment.DecisionDataPolicy!.ExecutionContract!.Executor);
        Assert.Equal(mode, read.Assignment.DecisionDataPolicy.Policy.ReadBasisMode);
        Assert.Equal(allocations, await context.DecisionBudgetAllocations.CountAsync());
        Assert.Single(await context.DecisionMaterials.ToArrayAsync());
        if (descendant) {
            Assert.NotEqual(read.Child.Admission.Address.RunId, read.Material.Scope.OwnerRunId);
            Assert.Equal(read.Child.Admission.Address.RootRunId, read.Material.Scope.OwnerRunId);
        }
    }

    [Theory]
    [InlineData("child")]
    [InlineData("grant")]
    [InlineData("namespace")]
    [InlineData("profile")]
    [InlineData("stamp")]
    [InlineData("material")]
    [InlineData("contract")]
    [InlineData("supported-contract")]
    [InlineData("content-contract")]
    public async Task Canonical_resolution_refuses_substituted_original_data(string change) {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var context = Context(services);
        var fixture = await ProcessDecisionChildMaterialFixture.CreateAsync(context, services);
        var original = fixture.Binding;
        var otherMaterial = new ProcessDecisionMaterialId(Guid.NewGuid());
        var changed = change switch {
            "child" => original with { Child = original.Child with { Address = original.Child.Address with { ChildId = new(Guid.NewGuid()) } } },
            "grant" => original with { Child = original.Child with { Grant = original.Child.Grant with { WorkTicks = original.Child.Grant.WorkTicks + 1 } } },
            "namespace" => original with { Scope = original.Scope with { WorkspaceNamespaceId = Guid.NewGuid() } },
            "profile" => original with { Scope = original.Scope with { DatabaseProfileId = Guid.NewGuid() } },
            "stamp" => original with { Material = original.Material with { Fingerprint = new string('A', 64) } },
            "material" => original with { Child = original.Child with { MaterialId = otherMaterial },
                Material = original.Material with { Reference = original.Material.Reference with { MaterialId = otherMaterial } } },
            "contract" => original with { ExecutionContract = original.ExecutionContract with { Version = 2 } },
            _ => original
        };
        var supported = change == "supported-contract" ? original.ExecutionContract with { Version = 2 } : original.ExecutionContract;
        var content = change == "content-contract" ? fixture.Material.ContentContract! with { Version = 2 } : fixture.Material.ContentContract;
        var allocations = await context.DecisionBudgetAllocations.CountAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => Reader(services).ReadOriginalAsync(changed, supported, content));
        Assert.Equal(allocations, await context.DecisionBudgetAllocations.CountAsync());
        fixture.Material.RequireOriginal((await Reader(services).ReadOriginalAsync(original, original.ExecutionContract,
            fixture.Material.ContentContract)).Material);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Historical_read_preserves_closed_or_reconciling_child_without_restoring_dispatch(bool close) {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var context = Context(services);
        var fixture = await ProcessDecisionChildMaterialFixture.CreateAsync(context, services);
        if (close) {
            await Owner(context, services).CommitAsync(await ClosureRequestAsync(context, fixture.Enrolled.State,
                fixture.Enrolled.Decision!.Child!, NoEffect(), 7));
        } else {
            var materialScope = (await Scopes(context, services).ReadAsync(fixture.Binding.Scope.Id))!;
            await Owner(context, services).CommitAsync(ScopeRequest(fixture.Enrolled.State,
                new(materialScope.Expectation, ProcessDecisionRunScopeCommandKind.BeginReconciliation)));
        }
        var read = await Reader(services).ReadOriginalAsync(fixture.Binding, fixture.Binding.ExecutionContract, fixture.Material.ContentContract);
        fixture.Material.RequireOriginal(read.Material);
        Assert.Equal(fixture.Binding.Child, read.Child.Admission);
        if (close) {
            Assert.Equal(NoEffect(), read.Child.Closure);
            Assert.Equal(7, read.Child.Settlement!.Consumed.WorkTicks);
        } else {
            Assert.False(read.Child.IsCurrent);
            Assert.Null(read.Child.Closure);
            Assert.Null(read.Child.Settlement);
        }
    }

    private static ProcessDecisionChildMaterialReader Reader(IServiceProvider services)
        => services.GetRequiredService<ProcessDecisionChildMaterialReader>();

    [Theory]
    [InlineData(0)]
    [InlineData(12)]
    public async Task Exact_resolution_uses_three_bounded_reads_independent_of_sibling_count(int siblings) {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var context = Context(services);
        var fixture = await ProcessDecisionChildMaterialFixture.CreateAsync(context, services);
        var state = fixture.Enrolled.State;
        for (var index = 0; index < siblings; index++) {
            state = (await Owner(context, services).CommitAsync(await EnrollmentRequestAsync(context, state))).State;
        }
        var probe = new ReadProbe();
        await using var readContext = Context(services, probe);
        var reader = new ProcessDecisionChildMaterialReader(new EfProcessDecisionChildQuery(new ContextFactory(Options(services, probe))),
            new EfProcessDecisionMaterialStore(readContext, services.GetRequiredService<ICanonicalRuntimeDatabase>()),
            new EfProcessRuntimeStepAssignmentStore(readContext));
        var read = await reader.ReadOriginalAsync(fixture.Binding, fixture.Binding.ExecutionContract, fixture.Material.ContentContract);
        fixture.Material.RequireOriginal(read.Material);
        Assert.Equal(3, probe.Commands.Count);
        Assert.All(probe.Commands, command => {
            Assert.Contains("WHERE", command, StringComparison.Ordinal);
            Assert.Contains("LIMIT 2", command, StringComparison.Ordinal);
        });
        Assert.Equal(siblings + 1, read.Child.Generation.ChildCount);
    }

    private sealed class ReadProbe : DbCommandInterceptor {
        internal List<string> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ContextFactory(DbContextOptions<ProcessPersistenceDbContext> options) : IDbContextFactory<ProcessPersistenceDbContext> {
        public ProcessPersistenceDbContext CreateDbContext() => new(options);
    }
}
