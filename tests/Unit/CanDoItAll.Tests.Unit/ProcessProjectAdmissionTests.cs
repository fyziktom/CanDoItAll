using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessProjectAdmissionTests {
    [Fact]
    public async Task Standalone_unscoped_persistence_resolves_and_commits_without_application_profile_services() {
        var services = new ServiceCollection();
        services.AddDbContext<ProcessPersistenceDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        services.AddScoped<EfProcessRuntimeUnitOfWork>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = provider.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<EfProcessRuntimeUnitOfWork>();
        var request = ProcessProjectAdmissionFixture.Initial();
        Assert.True((await unitOfWork.CommitAsync(request)).Succeeded);
        var loaded = Assert.IsType<ProcessRuntimeStateSnapshot>(await unitOfWork.LoadAsync(request.Mutation.State.RunId));
        Assert.Null(loaded.ProjectAdmission);
        Assert.True((await unitOfWork.CommitAsync(ProcessProjectAdmissionFixture.Cancel(loaded))).Succeeded);
        Assert.Null(scope.ServiceProvider.GetService<ICanonicalRuntimeDatabase>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Admission_rejects_an_empty_identifier(int emptyIndex) {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        ids[emptyIndex] = Guid.Empty;
        Assert.Throws<ArgumentException>(() => new ProcessProjectAdmission(ids[0], ids[1], ids[2]));
    }

    [Fact]
    public void Launch_payload_cannot_supply_a_trusted_project_admission() {
        var admission = new ProcessProjectAdmission(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var request = new ProcessLaunchRequest("fixture", null, null, admission.ProjectId, null, "operator",
            new Dictionary<string, string>(), false, false) { ProjectAdmission = admission };
        var json = JsonSerializer.Serialize(request);
        Assert.DoesNotContain(nameof(ProcessLaunchRequest.ProjectAdmission), json, StringComparison.Ordinal);
        var injected = json.TrimEnd('}') + ",\"ProjectAdmission\":" + JsonSerializer.Serialize(admission) + "}";
        var restored = Assert.IsType<ProcessLaunchRequest>(JsonSerializer.Deserialize<ProcessLaunchRequest>(injected));
        Assert.Null(restored.ProjectAdmission);
        Assert.Equal(admission.ProjectId, restored.ProjectId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Saved_admission_cannot_be_removed_or_retargeted_by_a_later_runtime_command(bool remove) {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>()
            .UseInMemoryDatabase(databaseName, new InMemoryDatabaseRoot()).Options;
        var coordinator = CoordinatedDatabaseTransaction.ForProfile(new(new DatabaseProfileRecord {
            ProviderKind = DatabaseProviderKind.InMemory, SourceKind = DatabaseProfileSourceKind.InMemory
        }, DatabaseProfileResolutionSource.ExplicitOverride, databaseName));
        await using var context = new ProcessPersistenceDbContext(options);
        var policy = new AcceptingPolicy();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(context, coordinatedTransaction: coordinator, projectAdmissionPolicy: policy);
        var original = new ProcessProjectAdmission(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var create = ProcessProjectAdmissionFixture.Initial(original);
        Assert.True((await unitOfWork.CommitAsync(create)).Succeeded);
        Assert.Equal(1, policy.ReadCount);
        var state = Assert.IsType<ProcessRuntimeStateSnapshot>(await unitOfWork.LoadAsync(create.Mutation.State.RunId));
        var changed = remove ? null : new ProcessProjectAdmission(original.DatabaseProfileId, original.ProjectId, Guid.NewGuid());
        var mutation = ProcessProjectAdmissionFixture.WithAdmission(ProcessProjectAdmissionFixture.Cancel(state), changed);
        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.CommitAsync(mutation));
        var retained = Assert.IsType<ProcessRuntimeStateSnapshot>(await unitOfWork.LoadAsync(state.RunId));
        Assert.Equal(original, retained.ProjectAdmission);
        Assert.Equal(ProcessRuntimeStatus.Created, retained.Status);
        Assert.Single(await context.IdempotencyKeys.ToArrayAsync());
        Assert.Single(await context.RuntimeEvents.ToArrayAsync());
        Assert.Equal(1, policy.ReadCount);
    }

    private sealed class AcceptingPolicy : IProcessProjectAdmissionPolicy {
        public int ReadCount { get; private set; }
        public Task RequireForMutationAsync(ProcessProjectAdmission admission, CancellationToken cancellationToken = default) {
            ReadCount++;
            return Task.CompletedTask;
        }
    }
}
