using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

internal static class ProjectsPortfolioUiJourney {
    internal static async Task<Guid> CreateAndEditAsync(LiveUiHost host, IPage page, CrmHrBrowserOracle oracle) {
        await page.SetViewportSizeAsync(1920, 1080);
        await oracle.NavigateAsync($"{host.BaseUrl}/projects");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await Assertions.Expect(page.GetByTestId("projects-workspace")).ToHaveAttributeAsync("data-interactive", "true");
        await page.GetByTestId("projects-new-button").ClickAsync();
        var editor = page.GetByTestId("projects-editor-modal");
        await Assertions.Expect(editor).ToBeVisibleAsync();
        await Assertions.Expect(editor.Locator("[data-wizard-step]")).ToHaveCountAsync(5);
        string name = "Portfolio UI Žluťoučký " + Guid.NewGuid().ToString("N");
        await editor.GetByTestId("project-name-input").FillAsync(name);
        await editor.GetByTestId("project-phase-input").FillAsync("Discovery");
        var identity = editor.Locator("[data-wizard-step='0']");
        await identity.Locator("select").SelectOptionAsync("Active");
        var date = editor.GetByTestId("project-target-date");
        await date.PressAsync("ArrowRight");
        await date.PressAsync("1");
        await date.PressAsync("Tab");
        Assert.True(await date.EvaluateAsync<bool>("element => element.validity.badInput"));
        foreach (string action in new[] { "project-save-button", "project-save-open-button" }) {
            await StepAsync(editor, "5. Review");
            await editor.GetByTestId(action).ClickAsync();
            await Assertions.Expect(date).ToBeFocusedAsync();
            Assert.True(await date.EvaluateAsync<bool>("element => element.validity.badInput"));
            Assert.DoesNotContain(await host.SeedAsync(services => services.GetRequiredService<ProjectsService>().ListAsync()), item => item.Name == name);
        }
        await page.ScreenshotAsync(new() { Path = host.Artifact("projects-invalid-date-off-step.png") });
        await editor.GetByTestId("project-target-date").FillAsync("2027-06-30");
        await identity.Locator("textarea").Nth(0).FillAsync("Five-step persisted browser proof.");
        await identity.Locator("textarea").Nth(1).FillAsync("Keep the same project and child identities.");
        await StepAsync(editor, "2. Dates and phases");
        await editor.GetByRole(AriaRole.Button, new() { Name = "Add phase", Exact = true }).First.ClickAsync();
        await editor.GetByRole(AriaRole.Button, new() { Name = "Add phase", Exact = true }).First.ClickAsync();
        var phases = editor.GetByTestId("project-phase-row");
        await Assertions.Expect(phases).ToHaveCountAsync(2);
        for (int index = 0; index < 2; index++) {
            var row = phases.Nth(index);
            await row.Locator("input:not([type='date'])").FillAsync(index == 0 ? "Discovery" : "Delivery");
            await row.Locator("select").SelectOptionAsync(index == 0 ? "Active" : "Planned");
            await row.Locator("input[type='date']").Nth(0).FillAsync(index == 0 ? "2027-01-01" : "2027-03-01");
            await row.Locator("input[type='date']").Nth(1).FillAsync(index == 0 ? "2027-02-28" : "2027-06-30");
            await row.Locator("textarea").FillAsync("Native phase " + index);
        }
        await StepAsync(editor, "3. Stack profile");
        await editor.GetByTestId("project-option-Language").Locator("input").Nth(0).FillAsync("C#");
        await editor.GetByTestId("project-option-Language").Locator("input").Nth(1).FillAsync("Typed contracts");
        await editor.GetByTestId("project-option-Database").Locator("input").Nth(0).FillAsync("PostgreSQL");
        await editor.GetByTestId("project-option-Database").Locator("input").Nth(1).FillAsync("Owner transactions");
        await StepAsync(editor, "4. Linked objects");
        await editor.GetByRole(AriaRole.Button, new() { Name = "Add planned object", Exact = true }).First.ClickAsync();
        await editor.Locator("[data-wizard-step='3'] select").SelectOptionAsync("Note");
        string starter = "UI starter " + Guid.NewGuid().ToString("N");
        await editor.GetByTestId("project-starter-title").FillAsync(starter);
        await StepAsync(editor, "5. Review");
        await page.ScreenshotAsync(new() { Path = host.Artifact("projects-five-step-review.png") });
        await editor.GetByTestId("project-save-button").ClickAsync();
        await Assertions.Expect(editor).ToContainTextAsync("Project saved and starter objects seeded", new() { Timeout = 60_000 });
        var saved = await host.SeedAsync(async services => {
            var owner = services.GetRequiredService<ProjectsService>();
            var summary = Assert.Single(await owner.ListAsync(), item => item.Name == name);
            return await owner.GetAsync(summary.Id);
        });
        Assert.NotNull(saved.Id);
        Assert.NotNull(saved.ExpectedProjectAdmission);
        Assert.Equal(saved.ExpectedLifetimeId, saved.ExpectedProjectAdmission.LifetimeId);
        Assert.Equal("Five-step persisted browser proof.", saved.Description);
        Assert.Equal("Keep the same project and child identities.", saved.Objective);
        Assert.Equal("Discovery", saved.CurrentPhase);
        Assert.Equal("Active", saved.Status.ToString());
        Assert.Equal(new DateTime(2027, 6, 30), saved.TargetDateUtc?.Date);
        Assert.Equal(["Discovery", "Delivery"], saved.Phases.Select(phase => phase.Name));
        Assert.All(saved.Phases, phase => Assert.NotNull(phase.Id));
        Assert.Contains(saved.Options, option => option.Category == ProjectOptionCategory.Language && option.OptionName == "C#" && option.Notes == "Typed contracts");
        Assert.Contains(saved.Options, option => option.Category == ProjectOptionCategory.Database && option.OptionName == "PostgreSQL" && option.Notes == "Owner transactions");
        var seeded = await host.SeedAsync(services => services.GetRequiredService<ProjectStructureAgentService>().GetStructureAsync(saved.Id.Value, new(IncludeAssets: true)));
        string starterId = Assert.Single(seeded.Nodes, node => node.Title == starter).Id;
        await editor.GetByTestId("project-modal-close-button").ClickAsync();
        await page.GetByTestId("projects-search-input").FillAsync(name);
        await Assertions.Expect(page.GetByTestId("projects-board")).ToContainTextAsync(name);
        await oracle.NavigateAsync($"{host.BaseUrl}/projects?projectId={saved.Id:D}");
        await page.GetByTestId("projects-detail-modal").GetByRole(AriaRole.Button, new() { Name = "Edit project", Exact = true }).ClickAsync();
        await Assertions.Expect(editor.GetByTestId("project-name-input")).ToHaveValueAsync(name);
        await editor.GetByTestId("project-name-input").FillAsync(name + " edited without blur");
        await editor.GetByTestId("project-name-input").PressAsync("Enter");
        await Assertions.Expect(editor).ToContainTextAsync("Project saved.", new() { Timeout = 60_000 });
        var repeated = await host.SeedAsync(services => services.GetRequiredService<ProjectsService>().GetAsync(saved.Id.Value));
        Assert.Equal(name + " edited without blur", repeated.Name);
        Assert.Equal(saved.ExpectedProjectAdmission, repeated.ExpectedProjectAdmission);
        Assert.Equal(saved.Phases.Select(phase => phase.Id), repeated.Phases.Select(phase => phase.Id));
        Assert.Equal(saved.Options.Select(option => option.Id), repeated.Options.Select(option => option.Id));
        var repeatedTree = await host.SeedAsync(services => services.GetRequiredService<ProjectStructureAgentService>().GetStructureAsync(saved.Id.Value, new(IncludeAssets: true)));
        Assert.Equal(starterId, Assert.Single(repeatedTree.Nodes, node => node.Title == starter).Id);
        await File.WriteAllTextAsync(host.Artifact("projects-owner-identities.json"), JsonSerializer.Serialize(new {
            saved.Id, saved.ExpectedProjectAdmission,
            phaseIds = repeated.Phases.Select(phase => phase.Id), optionIds = repeated.Options.Select(option => option.Id),
            starterId, saves = 2, seedBatches = 1, viewport = new { width = 1920, height = 1080 }
        }, new JsonSerializerOptions { WriteIndented = true }));
        await editor.GetByTestId("project-save-open-button").ClickAsync();
        await ProjectFilesUiJourney.ReadyFileCanvasAsync(page);
        await Assertions.Expect(page.GetByTestId("project-structure-canvas-loaded")).ToBeAttachedAsync();
        return saved.Id.Value;
    }

    private static Task StepAsync(ILocator editor, string name)
        => editor.GetByRole(AriaRole.Button, new() { Name = name, Exact = true }).ClickAsync();
}
