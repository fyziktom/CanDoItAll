using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.DataSources.Contracts;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Integration.Persistence;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "HostPlatform")]
public sealed class DataSourcesEditAdmissionTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Catalog_acknowledgement_survives_later_selection_read_failure(bool creating) {
        await using var environment = CanDoItAllTestEnvironment.Create("data-sources-acknowledgement");
        await using var services = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(environment);
        var profiles = services.GetRequiredService<IDatabaseProfileService>();
        Assert.True((await profiles.SaveAsync(TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(environment.CreatePostgreSqlProfile("a"), "A"))).IsSuccess);
        var saved = await profiles.SaveAsync(TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(environment.CreatePostgreSqlProfile("b"), "B"));
        Assert.True(saved.IsSuccess);
        await using var scope = services.CreateAsyncScope();
        var held = new HeldEditorRead(profiles);
        var workspace = ActivatorUtilities.CreateInstance<DatabaseProfileWorkspaceService>(scope.ServiceProvider, held);
        var owner = new WorkspaceDataSourcesOwner(workspace, services.GetRequiredService<ICanonicalRuntimeDatabase>(), NullLogger<WorkspaceDataSourcesOwner>.Instance);
        var values = (await owner.ReadEditorAsync(saved.Value, CancellationToken.None)).Values with {
            Id = creating ? null : saved.Value, DisplayName = "Acknowledged catalog entry"
        };
        var activePath = services.GetRequiredService<IControlPlanePathResolver>().ResolveActiveProfileStateFilePath();
        FileStream? selectionLock = null;
        held.BeforeEditorSave = () => selectionLock = new FileStream(activePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        DataSourceResult result;
        try {
            using var password = new ProfilePasswordIntent(string.Empty);
            result = await owner.SaveAsync(owner.Context, values, password);
        } finally {
            selectionLock?.Dispose();
        }
        Assert.Equal(DataSourceOutcome.Partial, result.Outcome);
        Assert.NotEqual(Guid.Empty, result.ProfileId);
        Assert.Equal(!creating, result.ProfileId == saved.Value);
        Assert.Equal("Acknowledged catalog entry", (await profiles.GetEditorAsync(result.ProfileId)).DisplayName);
        Assert.Equal(creating ? 3 : 2, (await profiles.ListAsync()).Count);
        Assert.DoesNotContain(activePath, result.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Write_acquisition_refuses_deleted_edit_or_newly_pending_delete(bool deleting) {
        await using var environment = CanDoItAllTestEnvironment.Create("data-sources-admission");
        await using var services = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(environment);
        var profiles = services.GetRequiredService<IDatabaseProfileService>();
        var a = await profiles.SaveAsync(TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(environment.CreatePostgreSqlProfile("a"), "A"));
        var target = environment.CreatePostgreSqlProfile("b");
        var b = await profiles.SaveAsync(TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(target, "B"));
        Assert.True(a.IsSuccess && b.IsSuccess);
        await using var scope = services.CreateAsyncScope();
        var held = new HeldEditorRead(profiles);
        var workspace = ActivatorUtilities.CreateInstance<DatabaseProfileWorkspaceService>(scope.ServiceProvider, held);
        var owner = new WorkspaceDataSourcesOwner(workspace, services.GetRequiredService<ICanonicalRuntimeDatabase>(), NullLogger<WorkspaceDataSourcesOwner>.Instance);
        var original = owner.Context;
        var editor = await owner.ReadEditorAsync(b.Value, CancellationToken.None);
        Assert.True(editor.HasPassword);
        var canary = Path.Combine(editor.Values.WorkspaceRoot, "retained.txt");
        Directory.CreateDirectory(editor.Values.WorkspaceRoot);
        await File.WriteAllTextAsync(canary, "retained original files");
        held.Hold = true;
        using var password = new ProfilePasswordIntent(string.Empty);
        var writing = deleting
            ? owner.ExecuteAsync(new(original, b.Value, DataSourceAction.Delete))
            : owner.SaveAsync(original, editor.Values with { DisplayName = "Old editor" }, password);
        await held.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try {
            await using var otherServices = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(environment);
            var otherProfiles = otherServices.GetRequiredService<IDatabaseProfileService>();
            await using var otherScope = otherServices.CreateAsyncScope();
            var otherOwner = new WorkspaceDataSourcesOwner(otherScope.ServiceProvider.GetRequiredService<DatabaseProfileWorkspaceService>(),
                otherServices.GetRequiredService<ICanonicalRuntimeDatabase>(), NullLogger<WorkspaceDataSourcesOwner>.Instance);
            if (deleting) {
                Assert.True((await otherProfiles.ActivateAsync(b.Value)).IsSuccess);
            } else {
                Assert.Equal(DataSourceOutcome.Confirmed, (await otherOwner.ExecuteAsync(new(otherOwner.Context, b.Value, DataSourceAction.Delete))).Outcome);
            }
        } finally {
            held.Release.TrySetResult();
        }
        var result = await writing.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(DataSourceOutcome.Refused, result.Outcome);
        Assert.Equal(b.Value, result.ProfileId);
        var list = await profiles.ListAsync();
        Assert.Equal(deleting ? 2 : 1, list.Count);
        Assert.Equal("A", list.Single(item => item.Id == a.Value).DisplayName);
        Assert.Equal(deleting ? b.Value : a.Value, (await profiles.GetCurrentSelectionAsync()).ActiveProfileId);
        Assert.Equal(original, owner.Context);
        Assert.Equal("retained original files", await File.ReadAllTextAsync(canary));
        if (deleting) {
            Assert.Equal("B", list.Single(item => item.Id == b.Value).DisplayName);
            Assert.True((await owner.ReadEditorAsync(b.Value, CancellationToken.None)).HasPassword);
        } else {
            Assert.DoesNotContain(list, item => item.Id == b.Value);
        }
    }

    private sealed class HeldEditorRead(IDatabaseProfileService inner) : IDatabaseProfileService {
        public Action? BeforeEditorSave { get; set; }
        public bool Hold { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<DatabaseProfileEditorModel> GetEditorAsync(Guid? id = null, CancellationToken cancellationToken = default) {
            var value = await inner.GetEditorAsync(id, cancellationToken);
            if (Hold) {
                Hold = false;
                Entered.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return value;
        }
        public Result Validate(DatabaseProfileEditorModel model) => inner.Validate(model);
        public Task<Result<Guid>> SaveAsync(DatabaseProfileEditorModel model, CancellationToken cancellationToken = default) => inner.SaveAsync(model, cancellationToken);
        public Task<Result<Guid>> SaveEditorAsync(DatabaseProfileEditorModel model, ResolvedDatabaseProfile runtimeProfile, CancellationToken cancellationToken = default) {
            BeforeEditorSave?.Invoke();
            return inner.SaveEditorAsync(model, runtimeProfile, cancellationToken);
        }
        public Task<Result> DeleteEditorAsync(Guid id, ResolvedDatabaseProfile runtimeProfile, CancellationToken cancellationToken = default) => inner.DeleteEditorAsync(id, runtimeProfile, cancellationToken);
        public Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default) => inner.DeleteAsync(id, cancellationToken);
        public Task<Result> ActivateAsync(Guid id, CancellationToken cancellationToken = default) => inner.ActivateAsync(id, cancellationToken);
        public Task<Result> RebindWorkspaceAsync(Guid id, string workspaceRoot, CancellationToken cancellationToken = default) => inner.RebindWorkspaceAsync(id, workspaceRoot, cancellationToken);
        public Task<Result> RollbackWorkspacePathMigrationAsync(CancellationToken cancellationToken = default) => inner.RollbackWorkspacePathMigrationAsync(cancellationToken);
        public Task<DatabaseSelectionStateModel> GetCurrentSelectionAsync(CancellationToken cancellationToken = default) => inner.GetCurrentSelectionAsync(cancellationToken);
        public Task<IReadOnlyList<DatabaseProfileSummary>> ListAsync(CancellationToken cancellationToken = default) => inner.ListAsync(cancellationToken);
    }
}
