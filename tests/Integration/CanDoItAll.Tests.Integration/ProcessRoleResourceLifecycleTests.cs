using System.Data.Common;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "HostPlatform")]
public sealed class ProcessRoleResourceLifecycleTests {
    private const string SurvivorKey = "pc4-surviving-role";

    [Theory]
    [InlineData(ProcessTemplateImportCommandKind.ImportRole, false)]
    [InlineData(ProcessTemplateImportCommandKind.ImportRole, true)]
    [InlineData(ProcessTemplateImportCommandKind.ImportProcess, false)]
    [InlineData(ProcessTemplateImportCommandKind.ImportProcess, true)]
    public async Task Delete_then_fresh_import_preserves_owned_resources_history_and_lost_response_recovery(
        ProcessTemplateImportCommandKind kind, bool loseDeleteResponse) {
        var fault = new LostCommitResponse();
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = services => {
            services.AddScoped<IProcessAuthoringStore>(provider => {
                var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>();
                AppDbContextOptionsConfigurator.Configure(options, provider.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
                options.AddInterceptors(fault);
                return new EfProcessAuthoringStore(new ContextFactory(options.Options), provider.GetRequiredService<CoordinatedDatabaseTransaction>(),
                    provider.GetRequiredService<IProcessAuthoringAdmissionPolicy>(), TimeProvider.System, options.Options);
            });
        } });
        await using var seed = app.Services.CreateAsyncScope();
        var workspace = seed.ServiceProvider.GetRequiredService<ProcessAuthoringWorkspace>();
        var templates = seed.ServiceProvider.GetRequiredService<ProcessTemplatePackLoader>();
        var source = templates.Load().Definitions.Select(item => workspace.ReadTemplate(item.Key)).First(item => item.RoleResources.Count > 0);
        var roleKey = source.RoleResources.Keys.First();
        ProcessDefinitionCatalogItemKey key = new(source.Definition.Key);
        var sourceJson = ProcessAuthoringCodec.Write(source);
        var content = ProcessAuthoringCodec.Read(sourceJson);
        content.Definition.RoleUsages = [content.Definition.RoleUsages.Single(role => role.Key == roleKey),
            new() { Key = SurvivorKey, DisplayName = "Surviving role", PreferredExecutorKind = "person" }];
        content.Definition.Steps = [new() { Key = "pc4-work", Title = "Retained work", StepKind = "Work", OperationTargetScope = "ManagedProcessArtifactsOnly" }];
        content.Definition.LaunchDriverActivations.Clear();
        content = content with {
            Guidance = new Dictionary<string, IReadOnlyList<ProcessTemplateExecutionGuidanceDocument>>(),
            References = [new("deleted-role-clone", ProcessDefinitionCanvasNodeKind.Role, roleKey, null, 123, 456, true)],
            RoleResources = new Dictionary<string, ProcessTemplateRoleResourceDocument> {
                [roleKey] = content.RoleResources[roleKey],
                [SurvivorKey] = new() { Key = "independent-source", DisplayName = "Surviving role", Summary = "Retain unrelated material" }
            }
        };
        var baseline = await workspace.ReadAsync(ProcessWorkspaceShellScope.Global, key, default);
        content = await seed.ServiceProvider.GetRequiredService<ProcessExecutableDefinitionResolver>().PinPublicationAsync(baseline, content, default);
        var published = await workspace.CommitAsync(baseline, Guid.NewGuid(), ProcessAuthoringCodec.Hash("role lifecycle publication"), content,
            ProcessAuthoringLifecycle.Published, true, null, default);
        var snapshot = published.Snapshot!;
        var authority = await seed.ServiceProvider.GetRequiredService<IProcessLaunchOperatorAuthoritySource>()
            .CaptureLocalAsync(null, ProcessLaunchOperatorSurface.UserInterface);
        ProcessLaunchRequest launchRequest = new(key.Value, null, null, null, null, "role-lifecycle", new Dictionary<string, string>(), false, false) {
            Authority = authority, CallerIntentId = new(Guid.NewGuid())
        };
        var preview = await seed.ServiceProvider.GetRequiredService<ProcessLaunchApplicationService>().PreviewAsync(launchRequest);
        await using var beforeDatabase = await seed.ServiceProvider.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>().CreateDbContextAsync();
        var publicationBefore = await beforeDatabase.AuthoringPublications.AsNoTracking().SingleAsync(item => item.Id == snapshot.PublishedId);
        var receiptBefore = await beforeDatabase.AuthoringReceipts.AsNoTracking().SingleAsync(item => item.OperationId == published.OperationId);
        var preparationBefore = await beforeDatabase.PreparedLaunches.AsNoTracking().SingleAsync(item => item.Id == preview.Observation!.AdmissionId.Value);

        await using var deletion = app.Services.CreateAsyncScope();
        var client = deletion.ServiceProvider.GetRequiredService<IProcessWorkspaceProjectionClient>();
        var editor = (await client.GetShellAsync(Request(key))).DefinitionCatalog.SelectedEditor!;
        ProcessDefinitionRoleEditorCommand delete = new(ProcessWorkspaceShellScope.Global, key, ProcessDefinitionRoleCommandKind.DeleteRole,
            editor.RoleEditor!.VersionToken, editor.RoleEditor.Roles.Single(role => role.RoleKey.Value == roleKey).Draft, null);
        fault.Armed = loseDeleteResponse;
        if (loseDeleteResponse) {
            await Assert.ThrowsAsync<TimeoutException>(() => client.ExecuteDefinitionRoleEditorCommandAsync(delete));
        } else {
            Assert.Equal(ProcessDefinitionRoleCommandStatus.Accepted, (await client.ExecuteDefinitionRoleEditorCommandAsync(delete)).Receipt.Status);
        }

        await using var fresh = app.Services.CreateAsyncScope();
        var reopened = fresh.ServiceProvider.GetRequiredService<IProcessWorkspaceProjectionClient>();
        var recoveredDelete = await reopened.ExecuteDefinitionRoleEditorCommandAsync(delete);
        Assert.Equal(ProcessDefinitionRoleCommandStatus.Accepted, recoveredDelete.Receipt.Status);
        var store = fresh.ServiceProvider.GetRequiredService<IProcessAuthoringStore>();
        var deleted = (await store.ReadAsync(snapshot.Address))!;
        Assert.Equal(snapshot.Revision + 1, deleted.Revision);
        Assert.False(deleted.Content.RoleResources.ContainsKey(roleKey));
        Assert.Empty(deleted.Content.References);
        Assert.Equal("Retain unrelated material", deleted.Content.RoleResources[SurvivorKey].Summary);
        var staleDelete = await reopened.ExecuteDefinitionRoleEditorCommandAsync(delete with { OperationId = Guid.NewGuid() });
        Assert.Equal(ProcessDefinitionRoleCommandStatus.Rejected, staleDelete.Receipt.Status);
        var current = (await reopened.GetShellAsync(Request(key))).DefinitionCatalog.SelectedEditor!;
        var itemKey = kind == ProcessTemplateImportCommandKind.ImportRole ? "role:" + key.Value + ":" + roleKey : "process:" + key.Value;
        ProcessTemplateImportCommand import = new(ProcessWorkspaceShellScope.Global, key, kind, new(itemKey), current.TemplateCatalog!.VersionToken,
            current.TemplateCatalog.Query, null);
        fault.Armed = !loseDeleteResponse;
        if (!loseDeleteResponse) {
            await Assert.ThrowsAsync<TimeoutException>(() => reopened.ExecuteTemplateImportCommandAsync(import));
        } else {
            Assert.Equal(ProcessTemplateImportCommandStatus.Accepted, (await reopened.ExecuteTemplateImportCommandAsync(import)).Receipt.Status);
        }

        await using var independent = app.Services.CreateAsyncScope();
        var finalClient = independent.ServiceProvider.GetRequiredService<IProcessWorkspaceProjectionClient>();
        var imported = await finalClient.ExecuteTemplateImportCommandAsync(import);
        Assert.Equal(ProcessTemplateImportCommandStatus.Accepted, imported.Receipt.Status);
        Assert.Equal(import.OperationId, imported.Receipt.ReceiptId);
        var finalStore = independent.ServiceProvider.GetRequiredService<IProcessAuthoringStore>();
        var saved = (await finalStore.ReadAsync(snapshot.Address))!;
        Assert.Equal(snapshot.Revision + 2, saved.Revision);
        var materializedKey = Assert.Single(saved.Content.Imports).Remap["role:" + roleKey];
        Assert.Equal(roleKey, materializedKey);
        Assert.Equal(source.RoleResources[roleKey].Summary, saved.Content.RoleResources[roleKey].Summary);
        Assert.Equal(source.RoleResources[roleKey].Key, saved.Content.RoleResources[roleKey].Key);
        Assert.Equal("Retain unrelated material", saved.Content.RoleResources[SurvivorKey].Summary);
        var finalEditor = (await finalClient.GetShellAsync(Request(key))).DefinitionCatalog.SelectedEditor!;
        var actual = finalEditor.RoleEditor!.Roles.Single(role => role.RoleKey.Value == roleKey);
        var expected = templates.ProjectDefinition(source.Definition, source.RoleResources).RoleAuthoringDefaults.Roles.Single(role => role.Key == roleKey);
        Assert.Equal(expected.DisplayName, actual.Draft.DisplayName);
        Assert.Equal(expected.Purpose, actual.Draft.Purpose);
        Assert.Equal(expected.StaffingIntent, actual.Draft.StaffingIntent);
        Assert.Equal(expected.SnapshotSummary, actual.Draft.SnapshotSummary);
        Assert.DoesNotContain(saved.Content.References, item => item.Key == "deleted-role-clone");
        Assert.Equal(imported.Receipt, (await finalClient.ExecuteTemplateImportCommandAsync(import)).Receipt);
        Assert.Equal(saved.Revision, (await finalStore.ReadAsync(snapshot.Address))!.Revision);
        Assert.Equal(sourceJson, ProcessAuthoringCodec.Write(workspace.ReadTemplate(key.Value)));
        await using var database = await independent.ServiceProvider.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>().CreateDbContextAsync();
        Assert.Equal(publicationBefore.ContentJson, (await database.AuthoringPublications.SingleAsync(item => item.Id == publicationBefore.Id)).ContentJson);
        Assert.Equal(publicationBefore.ContentHash, (await database.AuthoringPublications.SingleAsync(item => item.Id == publicationBefore.Id)).ContentHash);
        Assert.Equal(receiptBefore.ReceiptJson, (await database.AuthoringReceipts.SingleAsync(item => item.OperationId == published.OperationId)).ReceiptJson);
        Assert.Equal(preparationBefore.PayloadJson, (await database.PreparedLaunches.SingleAsync(item => item.Id == preparationBefore.Id)).PayloadJson);
        Assert.Equal(preparationBefore.PreparationFingerprint, (await database.PreparedLaunches.SingleAsync(item => item.Id == preparationBefore.Id)).PreparationFingerprint);
        var accepted = await independent.ServiceProvider.GetRequiredService<ProcessLaunchApplicationService>()
            .LaunchAsync(launchRequest with { PreparedAdmissionId = preview.Observation!.AdmissionId });
        Assert.Equal(preview.LaunchPlan.PlanId, accepted.LaunchPlan.PlanId);
        Assert.Equal(preview.LaunchPlan.PlanHash, accepted.LaunchPlan.PlanHash);
        Assert.NotNull(accepted.RunId);
        Assert.True(fault.Reached);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_then_add_reuses_the_role_key_without_inheriting_obsolete_materialized_defaults(bool projectScoped) {
        await using var app = await TestApplication.CreateAsync(new());
        await using var seed = app.Services.CreateAsyncScope();
        var services = seed.ServiceProvider;
        var projectId = Guid.NewGuid();
        Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(projectId, new() { Name = "Role resource key reuse" })).IsSuccess);
        var scope = projectScoped ? ProcessWorkspaceShellScope.ForProject(projectId) : ProcessWorkspaceShellScope.Global;
        var workspace = services.GetRequiredService<ProcessAuthoringWorkspace>();
        var templates = services.GetRequiredService<ProcessTemplatePackLoader>();
        var key = new ProcessDefinitionCatalogItemKey(templates.Load().Definitions[0].Key);
        var otherKey = new ProcessDefinitionCatalogItemKey(templates.Load().Definitions[1].Key);
        var unrelatedBefore = ProcessAuthoringCodec.Write((await workspace.ReadAsync(scope, otherKey, default)).Content);
        var sourceBefore = ProcessAuthoringCodec.Write(workspace.ReadTemplate(key.Value));
        var client = services.GetRequiredService<IProcessWorkspaceProjectionClient>();
        var initial = (await client.GetShellAsync(Request(key, scope))).DefinitionCatalog.SelectedEditor!.RoleEditor!;
        var action = initial.TemplateActions.First();
        var roleKey = action.KeyPrefix + "-1";
        var baseline = await workspace.ReadAsync(scope, key, default);
        var content = ProcessAuthoringCodec.Read(ProcessAuthoringCodec.Write(baseline.Content));
        content.Definition.RoleUsages = [new() { Key = roleKey, RoleResourceKey = "retired-source", DisplayName = "Retired role", PreferredExecutorKind = "person" }];
        content.Definition.Steps = [new() { Key = "unbound-work", Title = "Unbound work", StepKind = "Work", OperationTargetScope = "ManagedProcessArtifactsOnly" }];
        content = content with { RoleResources = new Dictionary<string, ProcessTemplateRoleResourceDocument> {
            [roleKey] = new() { Key = "retired-source", Purpose = "Obsolete resource purpose", StaffingIntent = "Obsolete staffing", SnapshotSummary = "Obsolete snapshot" }
        } };
        var seeded = await workspace.CommitAsync(baseline, Guid.NewGuid(), ProcessAuthoringCodec.Hash("resource-backed generated key"), content,
            ProcessAuthoringLifecycle.Draft, false, null, default);
        var editor = (await client.GetShellAsync(Request(key, scope))).DefinitionCatalog.SelectedEditor!.RoleEditor!;
        var old = Assert.Single(editor.Roles);
        Assert.Equal("Obsolete resource purpose", old.Draft.Purpose);
        ProcessDefinitionRoleEditorCommand delete = new(scope, key, ProcessDefinitionRoleCommandKind.DeleteRole, editor.VersionToken, old.Draft, null);
        Assert.Equal(ProcessDefinitionRoleCommandStatus.Accepted, (await client.ExecuteDefinitionRoleEditorCommandAsync(delete)).Receipt.Status);

        await using var independent = app.Services.CreateAsyncScope();
        var reopened = independent.ServiceProvider.GetRequiredService<IProcessWorkspaceProjectionClient>();
        var afterDelete = (await reopened.GetShellAsync(Request(key, scope))).DefinitionCatalog.SelectedEditor!.RoleEditor!;
        Assert.Empty(afterDelete.Roles);
        ProcessDefinitionRoleEditorCommand add = new(scope, key, ProcessDefinitionRoleCommandKind.AddRole, afterDelete.VersionToken, old.Draft, action.ActionKey);
        var added = await reopened.ExecuteDefinitionRoleEditorCommandAsync(add);
        Assert.Equal(ProcessDefinitionRoleCommandStatus.Accepted, added.Receipt.Status);
        Assert.Equal(roleKey, added.Projection.SelectedRoleKey!.Value.Value);
        Assert.Equal(action.Summary, added.Projection.SelectedRole!.Draft.Purpose);
        Assert.NotEqual(old.Draft.StaffingIntent, added.Projection.SelectedRole.Draft.StaffingIntent);
        Assert.NotEqual(old.Draft.SnapshotSummary, added.Projection.SelectedRole.Draft.SnapshotSummary);
        var store = independent.ServiceProvider.GetRequiredService<IProcessAuthoringStore>();
        var saved = (await store.ReadAsync(seeded.Snapshot!.Address))!;
        Assert.Equal(seeded.Snapshot.Revision + 2, saved.Revision);
        Assert.Empty(saved.Content.RoleResources);
        Assert.Equal(added.Receipt, (await reopened.ExecuteDefinitionRoleEditorCommandAsync(add)).Receipt);
        Assert.Equal(saved.Revision, (await store.ReadAsync(saved.Address))!.Revision);
        var freshWorkspace = independent.ServiceProvider.GetRequiredService<ProcessAuthoringWorkspace>();
        Assert.Equal(unrelatedBefore, ProcessAuthoringCodec.Write((await freshWorkspace.ReadAsync(scope, otherKey, default)).Content));
        Assert.Equal(sourceBefore, ProcessAuthoringCodec.Write(freshWorkspace.ReadTemplate(key.Value)));
    }

    private static ProcessWorkspaceShellRequest Request(ProcessDefinitionCatalogItemKey key, ProcessWorkspaceShellScope? scope = null)
        => new(scope ?? ProcessWorkspaceShellScope.Global, new(null, null, null), new(null, key, ProcessDefinitionCatalogScopeKind.All, 50),
            new(null, ProcessTemplateCatalogCategoryKind.All, null, ProcessTemplateCatalogPreviewTabKind.Overview, 500), false);

    private sealed class ContextFactory(DbContextOptions<ProcessPersistenceDbContext> options) : IDbContextFactory<ProcessPersistenceDbContext> {
        public ProcessPersistenceDbContext CreateDbContext() => new(options);
    }

    private sealed class LostCommitResponse : DbTransactionInterceptor {
        public bool Armed { get; set; }
        public bool Reached { get; private set; }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (Armed) {
                Armed = false;
                Reached = true;
                throw new TimeoutException("The role authoring commit completed before the response was lost.");
            }
            return Task.CompletedTask;
        }
    }
}
