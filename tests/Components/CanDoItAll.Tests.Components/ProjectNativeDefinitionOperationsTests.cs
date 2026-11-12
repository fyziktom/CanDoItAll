using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages;
using CanDoItAll.Modules.Projects.Pages.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectNativeDefinitionOperationsTests {
    [Fact]
    public async Task Missing_existing_project_never_becomes_a_new_creation_draft() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var native = harness.Context.Services.GetRequiredService<ProjectNativeDefinitionOperations>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => native.AcquireEditorAsync(Guid.NewGuid()));
        var creation = await native.AcquireEditorAsync(null);
        Assert.Null(creation.Model.Id);
        Assert.NotNull(creation.Scope);
    }

    [Fact]
    public async Task Original_draft_scope_cannot_rebind_to_another_dataset_after_restore() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var native = harness.Context.Services.GetRequiredService<ProjectNativeDefinitionOperations>();
        var acquired = await native.AcquireEditorAsync(null);
        acquired.Model.Name = "Draft opened before restore";
        var context = await native.GetContextAsync();
        var changed = new ProjectDefinitionWriteContext(new(Guid.NewGuid()), context.CallerBinding, context.Epoch,
            context.AdmissionDeadlineUtc, context.ReplayUntilUtc, context.RequestVersions, context.ResultVersions, context.Limits);
        Assert.Throws<InvalidOperationException>(() => native.Freeze(new ProjectEditorDraft(acquired.Model, acquired.Scope).Capture(), acquired.Scope, changed));
        Assert.Empty(await harness.Context.Services.GetRequiredService<ProjectsService>().ListAsync());
    }

    [Fact]
    public void Raw_editor_bounds_reject_oversized_or_invalid_content_before_command_construction() {
        var limits = new ProjectDefinitionLimits(2, 2, 16, 1040, 2048, 2048);
        ProjectEditorModel[] invalid = [
            new() { Name = new string('x', 17) },
            new() { Name = new string('漢', 6) },
            new() { Name = "a\ud800" },
            new() { Name = new string('a', 10), Description = new string('b', 10) },
            new() { Name = "Rows", Phases = [new(), new(), new()] }
        ];
        foreach (var model in invalid) {
            Assert.ThrowsAny<ArgumentException>(() => ProjectDefinitionInputBounds.Validate(model, limits));
        }
        ProjectDefinitionInputBounds.Validate(new ProjectEditorModel { Name = "Valid" }, limits);
    }
}
