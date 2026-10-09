using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "HostPlatform")]
public sealed class ProcessAuthoringCommandCoverageTests {
    [Fact]
    public async Task Role_template_application_and_step_add_commands_are_durable_and_replay_exactly() {
        await using var app = await TestApplication.CreateAsync(new());
        var key = new ProcessDefinitionCatalogItemKey(await ProcessAuthoringPublicationTests.SeedAsync(app.Services));
        await using var scope = app.Services.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<IProcessWorkspaceProjectionClient>();
        var editor = await EditorAsync(client, key, ProcessWorkspaceShellScope.Global);
        var roleEditor = editor.RoleEditor!;
        var added = await client.ExecuteDefinitionRoleEditorCommandAsync(new(ProcessWorkspaceShellScope.Global, key,
            ProcessDefinitionRoleCommandKind.AddRole, roleEditor.VersionToken, roleEditor.SelectedRole!.Draft, roleEditor.TemplateActions[0].ActionKey));
        Assert.Equal(ProcessDefinitionRoleCommandStatus.Accepted, added.Receipt.Status);
        var addedKey = added.Projection.SelectedRoleKey!.Value;
        editor = await EditorAsync(client, key, ProcessWorkspaceShellScope.Global);
        roleEditor = editor.RoleEditor!;
        var draft = roleEditor.Roles.Single(role => role.RoleKey == addedKey).Draft;
        ProcessDefinitionRoleEditorCommand apply = new(ProcessWorkspaceShellScope.Global, key, ProcessDefinitionRoleCommandKind.ApplyTemplate,
            roleEditor.VersionToken, draft, roleEditor.TemplateActions[^1].ActionKey);
        var applied = await client.ExecuteDefinitionRoleEditorCommandAsync(apply);
        Assert.True(applied.Receipt.Status == ProcessDefinitionRoleCommandStatus.Accepted, applied.Receipt.Summary);
        Assert.Equal(applied.Receipt, (await client.ExecuteDefinitionRoleEditorCommandAsync(apply)).Receipt);
        await using var fresh = app.Services.CreateAsyncScope();
        var persisted = await fresh.ServiceProvider.GetRequiredService<ProcessAuthoringWorkspace>().ReadAsync(ProcessWorkspaceShellScope.Global, key, default);
        var savedRole = persisted.Content.Definition.RoleUsages.Single(role => role.Key == addedKey.Value);
        Assert.Equal(applied.Projection.SelectedRole!.Draft.RoleTemplateSourceKey, savedRole.RoleTemplateSourceKey);
        Assert.Equal(applied.Projection.SelectedRole.Draft.Purpose, savedRole.Purpose);
        Assert.Equal(3, persisted.Revision);
        foreach (var kind in new[] { ProcessDefinitionStepCommandKind.AddBranchOutcome, ProcessDefinitionStepCommandKind.AddArtifactExpectation }) {
            editor = await EditorAsync(client, key, ProcessWorkspaceShellScope.Global);
            var command = new ProcessDefinitionStepEditorCommand(ProcessWorkspaceShellScope.Global, key, kind, editor.StepEditor!.VersionToken, editor.StepEditor.SelectedStep!);
            var result = await client.ExecuteDefinitionStepEditorCommandAsync(command);
            Assert.True(result.Receipt.Status == ProcessDefinitionStepCommandStatus.Accepted, result.Receipt.Summary);
            Assert.Equal(result.Receipt, (await client.ExecuteDefinitionStepEditorCommandAsync(command)).Receipt);
        }
        await using var final = app.Services.CreateAsyncScope();
        var completed = await final.ServiceProvider.GetRequiredService<ProcessAuthoringWorkspace>().ReadAsync(ProcessWorkspaceShellScope.Global, key, default);
        Assert.Equal(5, completed.Revision);
        Assert.Equal("work-route", Assert.Single(completed.Content.Definition.Steps[0].BranchOutcomes).Key);
        Assert.Equal("work-artifact", Assert.Single(completed.Content.Definition.Steps[0].ArtifactExpectations).Key);
    }

    [Fact]
    public async Task Project_drafts_inherit_global_publication_then_publish_archive_and_reset_with_explicit_source_revision() {
        await using var app = await TestApplication.CreateAsync(new());
        var key = new ProcessDefinitionCatalogItemKey(await ProcessAuthoringPublicationTests.SeedAsync(app.Services));
        await ProcessAuthoringPublicationTests.ChangeAsync(app.Services, key.Value, ProcessDefinitionEditorCommandKind.Publish, "Global published");
        await using var owned = app.Services.CreateAsyncScope();
        var projectId = Guid.NewGuid();
        Assert.True((await owned.ServiceProvider.GetRequiredService<ProjectsService>().CreateAsync(projectId, new() { Name = "Owned override project" })).IsSuccess);
        var project = ProcessWorkspaceShellScope.ForProject(projectId);
        await ChangeAsync(ProcessDefinitionEditorCommandKind.SaveDraft, "Project draft");
        var catalog = await CatalogAsync();
        Assert.Equal(ProcessDefinitionExecutableSource.GlobalPublication, catalog.ExecutableSource);
        Assert.Equal(2, catalog.ExecutableRevision);
        Assert.Equal("Global published", await ExecutableNameAsync());
        await ChangeAsync(ProcessDefinitionEditorCommandKind.Publish, "Project published");
        catalog = await CatalogAsync();
        Assert.Equal(ProcessDefinitionExecutableSource.ProjectPublication, catalog.ExecutableSource);
        Assert.Equal(2, catalog.ExecutableRevision);
        Assert.Equal("Project published", await ExecutableNameAsync());
        await ProcessAuthoringPublicationTests.ChangeAsync(app.Services, key.Value, ProcessDefinitionEditorCommandKind.Archive, "Archived global");
        Assert.Equal("Project published", await ExecutableNameAsync());
        await ChangeAsync(ProcessDefinitionEditorCommandKind.Archive, "Archived project");
        Assert.False((await CatalogAsync()).CanLaunch);
        await Assert.ThrowsAsync<InvalidOperationException>(ExecutableNameAsync);
        await ChangeAsync(ProcessDefinitionEditorCommandKind.Delete, "Archived project");
        await Assert.ThrowsAsync<InvalidOperationException>(ExecutableNameAsync);
        await ProcessAuthoringPublicationTests.ChangeAsync(app.Services, key.Value, ProcessDefinitionEditorCommandKind.Delete, "Archived global");
        Assert.Equal(ProcessDefinitionExecutableSource.TemplateDefault, (await CatalogAsync()).ExecutableSource);
        Assert.NotEqual("Project published", await ExecutableNameAsync());

        async Task ChangeAsync(ProcessDefinitionEditorCommandKind kind, string name) {
            await using var scope = app.Services.CreateAsyncScope();
            var client = scope.ServiceProvider.GetRequiredService<IProcessWorkspaceProjectionClient>();
            var editor = await EditorAsync(client, key, project);
            var result = await client.ExecuteDefinitionEditorCommandAsync(new(project, key, kind, editor.VersionToken,
                new(key, editor.Identity with { Name = name }, editor.Governance, editor.Contracts, editor.Simulation)));
            Assert.True(result.Receipt.Status == ProcessDefinitionEditorCommandStatus.Accepted, result.Receipt.Summary);
        }
        async Task<ProcessDefinitionCatalogItemProjection> CatalogAsync() {
            await using var scope = app.Services.CreateAsyncScope();
            return Assert.Single(await scope.ServiceProvider.GetRequiredService<ProcessDefinitionCatalogProjectionService>().GetCompleteCatalogItemsAsync(project), item => item.Key == key);
        }
        async Task<string> ExecutableNameAsync() {
            await using var scope = app.Services.CreateAsyncScope();
            var closure = await scope.ServiceProvider.GetRequiredService<ProcessExecutableDefinitionResolver>().ResolveAsync(project, key.Value);
            return ProcessExecutableDefinitionResolver.Decode(closure, key.Value).Definition.DisplayName;
        }
    }

    private static async Task<ProcessDefinitionEditorProjection> EditorAsync(IProcessWorkspaceProjectionClient client, ProcessDefinitionCatalogItemKey key, ProcessWorkspaceShellScope scope)
        => (await client.GetShellAsync(new(scope, new(null, null, null), new(null, key, ProcessDefinitionCatalogScopeKind.All, 50),
            new(null, ProcessTemplateCatalogCategoryKind.All, null, ProcessTemplateCatalogPreviewTabKind.Overview, 100), false))).DefinitionCatalog.SelectedEditor!;
}
