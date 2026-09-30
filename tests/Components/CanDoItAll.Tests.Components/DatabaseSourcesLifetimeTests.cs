using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.Pages.Components;
using CanDoItAll.SharedKernel;
using CanDoItAll.Workspace.DataSources.UI;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Components.Workspace;

public sealed class DatabaseSourcesLifetimeTests {
    [Fact]
    public async Task Save_captures_original_fields_and_keeps_later_edits_after_returned_identity() {
        var owner = new Profiles();
        using var context = Context(owner);
        var cut = context.Render<DatabaseSourcesSettingsPanel>();
        cut.Find("[data-testid='database-profile-new-postgres']").Click();
        cut.Find("[data-testid='database-profile-name']").Change("Captured name");
        var form = cut.FindComponent<EditForm>().Instance.EditContext;
        var saving = cut.Find("form").SubmitAsync();
        await owner.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try {
            cut.Find("[data-testid='database-profile-name']").Change("Later draft");
            Assert.Equal("Captured name", owner.Captured!.DisplayName);
        } finally {
            owner.Completion.TrySetResult(Result<Guid>.Success(owner.SavedId));
            await saving;
        }
        Assert.Same(form, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal("Later draft", cut.Find("[data-testid='database-profile-name']").GetAttribute("value"));
        Assert.Contains("database-profile-delete", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Same_acquired_profile_preserves_raw_numeric_input_and_edit_context() {
        var owner = new Profiles();
        using var context = Context(owner);
        var cut = context.Render<DatabaseSourcesSettingsPanel>();
        var form = cut.FindComponent<EditForm>().Instance.EditContext;
        cut.Find("[data-testid='database-profile-postgres-port']").Change(string.Empty);
        cut.Find($"[data-testid='database-profile-row-{owner.Id:N}']").Click();
        Assert.Same(form, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal(string.Empty, cut.Find("[data-testid='database-profile-postgres-port']").GetAttribute("value"));
        Assert.NotEmpty(form!.GetValidationMessages());
    }

    private static BunitContext Context(Profiles profiles) {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<ICanonicalRuntimeDatabase>(profiles);
        context.Services.AddSingleton<DataSourceOperationLedger>();
        context.Services.AddSingleton(new DatabaseProfileWorkspaceService(profiles, profiles, null!, null!, null!, null!, null!, NullLogger<DatabaseProfileWorkspaceService>.Instance));
        return context;
    }

    private sealed class Profiles : IDatabaseProfileService, IDatabaseProfileRuntimeAccessor, ICanonicalRuntimeDatabase {
        public ResolvedDatabaseProfile Profile => ResolveCurrentProfile();
        public long Generation => 0;
        public Guid Id { get; } = Guid.NewGuid();
        public Guid SavedId { get; } = Guid.NewGuid();
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<Result<Guid>> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public DatabaseProfileEditorModel? Captured { get; private set; }
        public ResolvedDatabaseProfile ResolveCurrentProfile() => new(new() {
            Id = Id, DisplayName = "Original", ProviderKind = DatabaseProviderKind.PostgreSql,
            SourceKind = DatabaseProfileSourceKind.PostgresConnection, PostgreSql = new()
        }, DatabaseProfileResolutionSource.PersistedActiveProfile, string.Empty);
        public ResolvedDatabaseProfile ResolveProfile(Guid id) => throw new InvalidOperationException("Controlled unavailable schema read.");
        public Task<IReadOnlyList<DatabaseProfileSummary>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DatabaseProfileSummary>>([
            new(Id, "Original", DatabaseProviderKind.PostgreSql, DatabaseProfileSourceKind.PostgresConnection, "Local fixture", "fixture", true, false, DateTimeOffset.UnixEpoch, null)
        ]);
        public Task<DatabaseProfileEditorModel> GetEditorAsync(Guid? id = null, CancellationToken cancellationToken = default) => Task.FromResult(new DatabaseProfileEditorModel { Id = id, DisplayName = id == Id ? "Original" : "Saved name" });
        public Result Validate(DatabaseProfileEditorModel model) => Result.Success();
        public async Task<Result<Guid>> SaveAsync(DatabaseProfileEditorModel model, CancellationToken cancellationToken = default) {
            Captured = model;
            Started.TrySetResult();
            return await Completion.Task;
        }
        public Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> ActivateAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> RebindWorkspaceAsync(Guid id, string workspaceRoot, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> RollbackWorkspacePathMigrationAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DatabaseSelectionStateModel> GetCurrentSelectionAsync(CancellationToken cancellationToken = default) => Task.FromResult(new DatabaseSelectionStateModel { ActiveProfileId = Id, RuntimeProfileId = Id });
    }
}
