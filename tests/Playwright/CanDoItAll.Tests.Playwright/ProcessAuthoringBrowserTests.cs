using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Application;
using Microsoft.Extensions.DependencyInjection;
using CanDoItAll.Processes.UI;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class ProcessAuthoringBrowserTests {
    [Fact]
    public Task Native_save_survives_tab_reads_and_keeps_later_input_and_mutation_gate()
        => RunAsync("save-read", async (page, control) => {
            await page.GetByTestId("processes-definition-editor-name").FillAsync("Submitted native draft");
            await page.GetByTestId("processes-definition-save").ClickAsync();
            await control.DefinitionStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var reads = Volatile.Read(ref control.ReadCount);
            await page.GetByTestId("processes-detail-tab-roles").ClickAsync();
            await Assertions.Expect(page.GetByTestId("processes-definition-role-editor")).ToBeVisibleAsync();
            await page.GetByTestId("processes-tab-definitions").ClickAsync();
            await Assertions.Expect(page.GetByTestId("processes-definition-save")).ToBeDisabledAsync();
            await Assertions.Expect(page.GetByTestId("processes-definition-publish")).ToBeDisabledAsync();
            await page.GetByTestId("processes-refresh").ClickAsync();
            await Assertions.Expect(page.GetByTestId("processes-definition-save")).ToBeDisabledAsync();
            await Assertions.Expect(page.GetByTestId("processes-definition-publish")).ToBeDisabledAsync();
            Assert.True(Volatile.Read(ref control.ReadCount) > reads);
            await page.GetByTestId("processes-definition-editor-name").FillAsync("Later local draft");
            await page.GetByTestId("processes-definition-editor-name").PressAsync("Enter");
            Assert.Equal(1, Volatile.Read(ref control.DefinitionCommandCount));
            control.ReleaseDefinition.TrySetResult();
            await Assertions.Expect(page.GetByTestId("processes-definition-editor-receipt")).ToContainTextAsync("Accepted");
            await Assertions.Expect(page.GetByTestId("processes-definition-editor-name")).ToHaveValueAsync("Later local draft");
            Assert.Equal(ProcessDefinitionEditorCommandStatus.Accepted, control.DefinitionResult!.Receipt.Status);
            Assert.Equal("Submitted native draft", control.DefinitionResult.Projection.Identity.Name);
            await page.GetByTestId("processes-refresh").ClickAsync();
            await Assertions.Expect(page.GetByTestId("processes-definition-editor-receipt")).ToContainTextAsync("Accepted");
            await Assertions.Expect(page.GetByTestId("processes-definition-editor-name")).ToHaveValueAsync("Later local draft");
            Assert.Equal(1, Volatile.Read(ref control.DefinitionCommandCount));
        });

    [Fact]
    public Task Native_role_results_select_added_and_surviving_roles_and_clear_last_deletion()
        => RunAsync("role-identity", async (page, control) => {
            control.DelayFirstRoleResponse = true;
            await page.GetByTestId("processes-detail-tab-roles").ClickAsync();
            await page.GetByTestId("processes-role-workflow-owner").ClickAsync();
            await Assertions.Expect(page.GetByTestId("processes-role-display-name")).ToHaveValueAsync("Workflow owner");
            await page.GetByTestId("processes-role-add").ClickAsync();
            await control.RoleStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await Assertions.Expect(page.GetByTestId("processes-role-save")).ToBeDisabledAsync();
            await page.GetByTestId("processes-role-details-dialog").GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).Last.ClickAsync();
            await page.GetByTestId("processes-role-second-owner").ClickAsync();
            await Assertions.Expect(page.GetByTestId("processes-role-display-name")).ToHaveValueAsync("Second owner");
            control.ReleaseRole.TrySetResult();
            await Assertions.Expect(page.GetByTestId("processes-role-card")).ToHaveCountAsync(3);
            Assert.Equal(ProcessDefinitionRoleCommandStatus.Accepted, control.RoleResult!.Receipt.Status);
            await Assertions.Expect(page.GetByTestId("processes-role-display-name")).ToHaveValueAsync("Second owner");
            await Assertions.Expect(page.GetByTestId("processes-role-add")).ToBeEnabledAsync();
            await page.GetByTestId("processes-role-add").ClickAsync();
            await Assertions.Expect(page.GetByTestId("processes-role-card")).ToHaveCountAsync(4);
            var added = control.RoleResult.Projection.SelectedRole!;
            await Assertions.Expect(page.GetByTestId("processes-role-display-name")).ToHaveValueAsync(added.DisplayName);
            await page.ScreenshotAsync(new() { Path = Path.Combine(control.Evidence, "pc2-role-added-dialog.png"), FullPage = true });
            for (var remaining = 3; remaining >= 0; remaining--) {
                await page.GetByTestId("processes-role-delete").ClickAsync();
                await Assertions.Expect(page.GetByTestId("processes-role-card")).ToHaveCountAsync(remaining);
                Assert.Equal(ProcessDefinitionRoleCommandStatus.Accepted, control.RoleResult!.Receipt.Status);
                if (control.RoleResult.Projection.SelectedRole is { } selected) {
                    await Assertions.Expect(page.GetByTestId("processes-role-display-name")).ToHaveValueAsync(selected.DisplayName);
                }
            }
            await Assertions.Expect(page.GetByTestId("processes-role-details-dialog")).ToHaveCountAsync(0);
            await Assertions.Expect(page.GetByText("No roles are defined", new() { Exact = true })).ToBeVisibleAsync();
            Assert.Null(control.RoleResult!.Projection.SelectedRoleKey);
        });

    [Fact]
    public Task Native_import_keeps_captured_target_and_later_library_browsing()
        => RunAsync("import-origin", async (page, control) => {
            control.DelayFirstImportResponse = true;
            await page.GetByTestId("processes-detail-tab-exchange").ClickAsync();
            await page.GetByTestId("processes-template-library-category-artifacts").ClickAsync();
            await Assertions.Expect(page.GetByTestId("processes-template-library-import-artifact")).ToBeEnabledAsync();
            await page.GetByTestId("processes-template-library-artifact-target").SelectOptionAsync("outcome");
            await page.GetByTestId("processes-template-library-import-artifact").ClickAsync();
            await control.ImportStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await Assertions.Expect(page.GetByTestId("processes-template-library-import-artifact")).ToBeDisabledAsync();
            await page.GetByTestId("processes-template-library-artifact-target").SelectOptionAsync("second-step");
            await page.GetByTestId("processes-template-library-preview-tab-json").ClickAsync();
            await Assertions.Expect(page.GetByTestId("processes-template-library-preview-json")).ToBeVisibleAsync();
            await page.GetByTestId("processes-template-library-category-roles").ClickAsync();
            await Assertions.Expect(page.GetByTestId("processes-template-library-import-role")).ToBeDisabledAsync();
            control.ReleaseImport.TrySetResult();
            await Assertions.Expect(page.GetByTestId("processes-template-library-import-receipt")).ToContainTextAsync("Accepted");
            await Assertions.Expect(page.GetByTestId("processes-template-library-import-role")).ToBeEnabledAsync();
            await Assertions.Expect(page.GetByTestId("processes-template-library-preview-json")).ToBeVisibleAsync();
            Assert.Equal(1, Volatile.Read(ref control.ImportCommandCount));
            Assert.Equal(new ProcessDefinitionStepKey("outcome"), control.ImportCommand!.TargetStepKey);
            Assert.Equal(ProcessTemplateImportCommandStatus.Accepted, control.ImportResult!.Receipt.Status);
            await page.ScreenshotAsync(new() { Path = Path.Combine(control.Evidence, "pc3-later-import-browsing.png"), FullPage = true });
        });

    [Fact]
    public Task Native_second_step_save_preserves_distinct_decision_role()
        => RunAsync("step-authority", async (page, control) => {
            await page.GetByTestId("processes-detail-tab-steps").ClickAsync();
            await page.GetByTestId(ProcessStepEditorState.BuildStepItemTestId(new("second-step"))).ClickAsync();
            await Assertions.Expect(page.GetByTestId("processes-step-title")).ToHaveValueAsync("Second step");
            await page.GetByTestId("processes-step-title").FillAsync("Edited second step");
            await page.GetByTestId("processes-step-save").ClickAsync();
            await Assertions.Expect(page.GetByTestId("processes-step-command-receipt")).ToContainTextAsync("Accepted");
            Assert.Equal(new("second-step"), control.StepCommand!.Draft.Basic.StepKey);
            Assert.Equal(new("second-owner"), control.StepCommand.Draft.Basic.DecisionRoleKey);
            Assert.Equal(ProcessDefinitionStepCommandStatus.Accepted, control.StepResult!.Receipt.Status);
            var drafts = control.StepResult.Projection.StepDrafts;
            Assert.Equal(new("workflow-owner"), drafts.Single(d => d.Basic.StepKey.Value == "outcome").Basic.DecisionRoleKey);
            Assert.Equal(new("second-owner"), drafts.Single(d => d.Basic.StepKey.Value == "second-step").Basic.DecisionRoleKey);
            await page.GetByTestId("processes-step-title").ScrollIntoViewIfNeededAsync();
            await page.ScreenshotAsync(new() { Path = Path.Combine(control.Evidence, "pc2-step-edited-form.png"), FullPage = true });
            await page.GetByTestId("processes-detail-tab-runs").ClickAsync();
            await Assertions.Expect(page.GetByTestId("processes-step-editor-form")).ToBeHiddenAsync();
            await page.GetByTestId("processes-detail-tab-steps").ClickAsync();
            await Assertions.Expect(page.GetByTestId("processes-step-editor-form")).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByTestId("processes-step-save")).ToBeEnabledAsync();
            await Assertions.Expect(page.GetByTestId("processes-step-title")).ToHaveValueAsync("Edited second step");
            await page.GetByTestId("processes-step-target-lead-hours").FillAsync("-1");
            await Assertions.Expect(page.GetByTestId("processes-step-numeric-error")).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByTestId("processes-step-save")).ToBeDisabledAsync();
            await page.GetByTestId("processes-detail-tab-runs").ClickAsync();
            await Assertions.Expect(page.GetByTestId("processes-step-editor-form")).ToBeHiddenAsync();
            await page.GetByTestId("processes-detail-tab-steps").ClickAsync();
            await Assertions.Expect(page.GetByTestId("processes-step-editor-form")).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByTestId("processes-step-target-lead-hours")).ToHaveValueAsync("-1");
            await Assertions.Expect(page.GetByTestId("processes-step-numeric-error")).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByTestId("processes-step-save")).ToBeDisabledAsync();
            await page.GetByTestId("processes-step-discard").ClickAsync();
            await Assertions.Expect(page.GetByTestId("processes-step-save")).ToBeEnabledAsync();
        });

    private static async Task RunAsync(string scenario, Func<IPage, ProcessAuthoringBrowserControl, Task> action) {
        await using var wire = await AgentResponseFixture.StartAsync("No external provider is used by authoring.");
        var control = new ProcessAuthoringBrowserControl { IncludeImportArtifact = scenario == "import-origin" };
        await using var host = await ProcessNativeBrowserHost.StartAsync(wire.BaseUrl, authoring: control);
        if (scenario == "role-identity") {
            await host.ReadAsync(async services => {
                var workspace = services.GetRequiredService<ProcessAuthoringWorkspace>();
                var baseline = await workspace.ReadAsync(ProcessWorkspaceShellScope.Global, new(ProcessNativeBrowserHost.CompleteDefinition), default);
                var content = ProcessAuthoringCodec.Read(ProcessAuthoringCodec.Write(baseline.Content));
                foreach (var step in content.Definition.Steps) {
                    step.RoleAssignments.Clear();
                    step.DecisionRoleKey = string.Empty;
                }
                return await workspace.CommitAsync(baseline, Guid.NewGuid(), ProcessAuthoringCodec.Hash("explicit browser fixture unbinding"),
                    content, ProcessAuthoringLifecycle.Draft, false, null, default);
            });
        }
        control.Evidence = host.Evidence;
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        try {
            await page.GotoAsync(host.BaseUrl + "/processes");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            await Assertions.Expect(page.GetByTestId("processes-page-scaffold")).ToHaveAttributeAsync("data-interactive", "true");
            var initial = await host.ReadAsync(provider => provider.GetRequiredService<ProcessAuthoringWorkspace>()
                .ReadAsync(ProcessWorkspaceShellScope.Global, new(ProcessNativeBrowserHost.CompleteDefinition), default));
            var definition = page.GetByTestId("processes-definition-" + ProcessNativeBrowserHost.CompleteDefinition);
            if (await definition.GetAttributeAsync("aria-selected") != "true") {
                await definition.ClickAsync();
            }
            await Assertions.Expect(page.GetByTestId("processes-definition-editor-name")).ToHaveValueAsync(initial.Content.Definition.DisplayName);
            await action(page, control);
            var persisted = await host.ReadAsync(async services => await services.GetRequiredService<ProcessAuthoringWorkspace>()
                .ReadAsync(ProcessWorkspaceShellScope.Global, new(ProcessNativeBrowserHost.CompleteDefinition), default));
            if (control.DefinitionResult is { } definitionResult) {
                Assert.Equal(definitionResult.Projection.Identity.Name, persisted.Content.Definition.DisplayName);
            }
            if (control.StepCommand is { } stepCommand) {
                var saved = persisted.Content.Definition.Steps.Single(step => step.Key == stepCommand.Draft.Basic.StepKey.Value);
                Assert.Equal(stepCommand.Draft.Basic.Title, saved.Title);
                Assert.Equal(stepCommand.Draft.Basic.DecisionRoleKey!.Value.Value, saved.DecisionRoleKey);
            }
            if (scenario == "role-identity") {
                Assert.Empty(persisted.Content.Definition.RoleUsages);
            }
            if (scenario == "import-origin") {
                Assert.Single(persisted.Content.Imports);
                Assert.Single(persisted.Content.Definition.Steps.Single(step => step.Key == "outcome").ArtifactExpectations);
                Assert.Empty(persisted.Content.Definition.Steps.Single(step => step.Key == "second-step").ArtifactExpectations);
            }
            Assert.Empty(errors);
            await Assertions.Expect(page.GetByTestId("processes-command-strip")).ToContainTextAsync("2 definition(s)");
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, "pc2-" + scenario + ".png"), FullPage = true });
        } catch {
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, "pc2-" + scenario + "-failure.png"), FullPage = true });
            throw;
        } finally {
            control.ReleaseDefinition.TrySetResult();
            control.ReleaseRole.TrySetResult();
            control.ReleaseImport.TrySetResult();
            await context.Tracing.StopAsync(new() { Path = Path.Combine(host.Evidence, "pc2-" + scenario + ".zip") });
        }
    }
}
