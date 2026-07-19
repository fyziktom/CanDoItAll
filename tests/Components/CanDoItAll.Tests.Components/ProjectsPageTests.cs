using Bunit;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages;
using CanDoItAll.Modules.Projects.Pages.Components;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectsPageTests
{
    [Fact]
    public async Task Project_files_pilot_accepts_each_registered_viewer_family()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var composition = harness.Context.Services.GetRequiredService<FileInteractionComponentComposition>();
        var coordinator = harness.Context.Services.GetRequiredService<IProjectFilesPilotCoordinator>();
        Guid projectId = await CreateProjectAsync(projectsService, "File viewer profiles");
        string projectFiles = CreateProjectFilesDirectory(harness, projectId);
        string[] fileNames =
        [
            "notes.txt",
            "README.md",
            "data.json",
            "diagram.mermaid",
            "diagram.svg",
            "image.png",
            "manual.pdf"
        ];
        foreach (string fileName in fileNames)
        {
            await File.WriteAllBytesAsync(Path.Combine(projectFiles, fileName), [1, 2, 3]);
        }

        await using ProjectFilesPilotWorkspace workspace = await coordinator.OpenAsync(
            projectId,
            "File viewer profiles");
        await workspace.Browser.InitializeAsync();
        Assert.Equal(fileNames.Length, workspace.Browser.Snapshot.Items.Count);

        foreach (FileBrowserItem item in workspace.Browser.Snapshot.Items)
        {
            await using ProjectFilesPilotInteraction interaction = await coordinator.ActivateAsync(workspace, item.Key);
            FileInteractionResolution resolution = composition.Core.Profiles.Resolve(interaction.Request);

            Assert.True(resolution.IsResolved, item.Name);
            Assert.NotEqual(FileInteractionMatchKind.Fallback, Assert.Single(resolution.Candidates).MatchKind);
            Assert.True(
                composition.Renderers.Resolve(resolution.Profile!.Id, FileInteractionMode.View).IsResolved,
                item.Name);
        }
    }

    [Fact]
    public async Task Project_files_pilot_opens_authorized_markdown_after_browser_replacement()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        Guid projectId = await CreateProjectAsync(projectsService, "File pilot project");
        string projectFiles = CreateProjectFilesDirectory(harness, projectId);
        await File.WriteAllTextAsync(
            Path.Combine(projectFiles, "pilot-readme.md"),
            "# Project file pilot\n\nAuthorized content.");

        var cut = harness.Context.RenderComponent<ProjectsPage>();
        cut.WaitForAssertion(() => Assert.Contains("File pilot project", cut.Markup));
        var projectCard = cut.FindAll("[data-testid='project-card']")
            .Single(card => card.TextContent.Contains("File pilot project", StringComparison.Ordinal));

        projectCard.QuerySelector("[data-testid='project-card-files-button']")!.Click();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='project-files-dialog']"));
            Assert.Contains("pilot-readme.md", cut.Markup);
        });
        var fileButton = cut.FindAll(".ft-file-browser__item-main")
            .Single(button => button.TextContent.Contains("pilot-readme.md", StringComparison.Ordinal));
        await File.WriteAllTextAsync(
            Path.Combine(projectFiles, "pilot-readme.md"),
            "# Project file pilot\n\nReplaced authorized content.");
        fileButton.KeyUp("Enter");

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Replaced authorized content.", cut.Find("[data-testid='interaction-text-view']").TextContent);
            Assert.Empty(cut.FindAll(".ft-file-browser"));
            Assert.True(cut.Find("[data-testid='interaction-mode-edit']").HasAttribute("disabled"));
        });
    }

    [Fact]
    public async Task Project_files_pilot_bounds_rendered_page_and_searches_progressively()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        Guid projectId = await CreateProjectAsync(projectsService, "Bounded file project");
        string projectFiles = CreateProjectFilesDirectory(harness, projectId);
        foreach (int index in Enumerable.Range(0, 120))
        {
            string fileName = index == 119 ? "needle-119.md" : $"project-file-{index:D3}.txt";
            await File.WriteAllTextAsync(Path.Combine(projectFiles, fileName), $"content {index}");
        }

        var cut = harness.Context.RenderComponent<ProjectsPage>();
        cut.WaitForAssertion(() => Assert.Contains("Bounded file project", cut.Markup));
        var projectCard = cut.FindAll("[data-testid='project-card']")
            .Single(card => card.TextContent.Contains("Bounded file project", StringComparison.Ordinal));
        projectCard.QuerySelector("[data-testid='project-card-files-button']")!.Click();

        cut.WaitForAssertion(() => Assert.Equal(50, cut.FindAll(".ft-file-browser__list tbody tr").Count));
        cut.Find("select[aria-label='Search scope']").Change("Progressive");
        cut.Find("input[aria-label='Search files and folders']").Input("needle-119");

        cut.WaitForAssertion(() =>
        {
            Assert.Single(cut.FindAll(".ft-file-browser__list tbody tr"));
            Assert.Contains("needle-119.md", cut.Find(".ft-file-browser__list tbody tr").TextContent);
            Assert.Contains("120 inspected", cut.Markup);
            Assert.Contains("1 retained", cut.Markup);
        }, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Project_files_pilot_interaction_survives_browser_disposal_and_revokes_on_dispose()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        Guid projectId = await CreateProjectAsync(projectsService, "Independent file interaction");
        string projectFiles = CreateProjectFilesDirectory(harness, projectId);
        await File.WriteAllTextAsync(
            Path.Combine(projectFiles, "independent.md"),
            "# Independent content");
        var coordinator = harness.Context.Services.GetRequiredService<IProjectFilesPilotCoordinator>();
        ProjectFilesPilotWorkspace workspace = await coordinator.OpenAsync(projectId, "Independent file interaction");
        await workspace.Browser.InitializeAsync();
        FileBrowserItem item = Assert.Single(workspace.Browser.Snapshot.Items);

        ProjectFilesPilotInteraction interaction = await coordinator.ActivateAsync(workspace, item.Key);
        await workspace.DisposeAsync();
        await using (FileContentLease lease = await interaction.Session.ContentSource.OpenReadAsync(
                         new FileContentReadRequest(interaction.Session.File)))
        using (var reader = new StreamReader(lease.Stream))
        {
            Assert.Equal("# Independent content", await reader.ReadToEndAsync());
        }

        await interaction.DisposeAsync();
        FileAccessDeniedException exception = await Assert.ThrowsAsync<FileAccessDeniedException>(() =>
            interaction.Session.ContentSource.OpenReadAsync(
                new FileContentReadRequest(interaction.Session.File)).AsTask());
        Assert.Equal(FileAccessFailureCode.Revoked, exception.Code);
    }

    [Fact]
    public async Task Project_files_pilot_open_error_waits_for_explicit_retry()
    {
        var coordinator = new FailingProjectFilesPilotCoordinator();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<IProjectFilesPilotCoordinator>();
            services.AddSingleton<IProjectFilesPilotCoordinator>(coordinator);
        });
        Guid projectId = Guid.NewGuid();
        var cut = harness.Context.RenderComponent<ProjectFilesDialog>(parameters => parameters
            .Add(component => component.IsOpen, true)
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.ProjectName, "Revoked project"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Project access was revoked.", cut.Markup);
            Assert.Equal(1, coordinator.OpenCalls);
        });
        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.IsOpen, true)
            .Add(component => component.ProjectId, projectId)
            .Add(component => component.ProjectName, "Revoked project"));
        Assert.Equal(1, coordinator.OpenCalls);

        cut.Find("[data-testid='project-files-retry']").Click();

        cut.WaitForAssertion(() => Assert.Equal(2, coordinator.OpenCalls));
    }

    [Fact]
    public async Task Project_files_pilot_cancellation_releases_granted_handle_without_cancelled_cleanup()
    {
        using var cancellation = new CancellationTokenSource();
        var file = new FileReference("authorized", "cancelled-handle");
        var activator = new CancellingBrowseItemActivator(cancellation, file);
        var releaser = new RecordingKnownFileSessionReleaser();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<IFileToolsBrowseItemActivator>();
            services.AddSingleton<IFileToolsBrowseItemActivator>(activator);
            services.RemoveAll<IFileToolsKnownFileSessionFactory>();
            services.AddSingleton<IFileToolsKnownFileSessionFactory, CancelledKnownFileSessionFactory>();
            services.RemoveAll<IFileToolsKnownFileSessionReleaser>();
            services.AddSingleton<IFileToolsKnownFileSessionReleaser>(releaser);
        });
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        Guid projectId = await CreateProjectAsync(projectsService, "Cancelled activation project");
        var coordinator = harness.Context.Services.GetRequiredService<IProjectFilesPilotCoordinator>();
        await using ProjectFilesPilotWorkspace workspace = await coordinator.OpenAsync(
            projectId,
            "Cancelled activation project");
        var itemKey = new FileBrowserItemKey(new FileBrowserSourceId("cancelled-source"), "cancelled-item");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            coordinator.ActivateAsync(workspace, itemKey, cancellation.Token).AsTask());

        Assert.Equal(file, releaser.ReleasedFile);
        Assert.False(releaser.CleanupTokenWasCancellationRequested);
    }

    [Fact]
    public async Task Project_file_portfolio_replaces_removed_source_location_and_rejects_stale_item()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        Guid alphaId = await CreateProjectAsync(projectsService, "Portfolio Alpha");
        Guid betaId = await CreateProjectAsync(projectsService, "Portfolio Beta");
        string alphaDirectory = CreateProjectFilesDirectory(harness, alphaId);
        string betaDirectory = CreateProjectFilesDirectory(harness, betaId);
        await File.WriteAllTextAsync(Path.Combine(alphaDirectory, "alpha.txt"), "alpha");
        await File.WriteAllTextAsync(Path.Combine(betaDirectory, "beta.txt"), "beta");
        IReadOnlyList<ProjectSummary> projects = await projectsService.ListAsync();
        IReadOnlyList<ProjectHierarchyLinkSummary> links = await projectsService.ListHierarchyLinksAsync();
        ProjectFileFilterProjection allProjects = ProjectFileFilterProjection.Create(
            projects,
            links,
            new ProjectFileFilter());
        var coordinator = harness.Context.Services.GetRequiredService<IProjectFilePortfolioCoordinator>();
        await using ProjectFilePortfolioWorkspace workspace = await coordinator.OpenAsync(allProjects);
        FileBrowserSourceDescriptor betaSource = workspace.Browser.Snapshot.Sources
            .Single(source => source.Description == "Portfolio Beta");

        await workspace.Browser.InitializeAsync(betaSource.Id);
        FileBrowserItem betaFile = workspace.Browser.Snapshot.Items.Single(item => item.Name == "beta.txt");
        ProjectFileFilterProjection alphaOnly = ProjectFileFilterProjection.Create(
            projects,
            links,
            new ProjectFileFilter(hierarchyProjectId: alphaId, includeSubprojects: false));

        Assert.True(await coordinator.UpdateAsync(workspace, alphaOnly));

        Assert.Single(workspace.Browser.Snapshot.Sources);
        Assert.Equal("Portfolio Alpha", workspace.Browser.Snapshot.CurrentSource?.Description);
        Assert.Equal(workspace.Browser.Snapshot.CurrentSource?.Id, workspace.Browser.Snapshot.Location?.Current.Key.SourceId);
        FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(() =>
            coordinator.ActivateAsync(workspace, betaFile.Key).AsTask());
        Assert.Equal(FileBrowserErrorCode.Conflict, exception.Error.Code);
    }

    [Fact]
    public async Task Project_file_portfolio_catalog_revision_replaces_source_set_and_preserves_valid_source()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        Guid projectId = await CreateProjectAsync(projectsService, "Revision Project");
        string projectDirectory = CreateProjectFilesDirectory(harness, projectId);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "revision.txt"), "revision");
        ProjectFileFilterProjection projection = ProjectFileFilterProjection.Create(
            await projectsService.ListAsync(),
            await projectsService.ListHierarchyLinksAsync(),
            new ProjectFileFilter(hierarchyProjectId: projectId, includeSubprojects: false));
        var coordinator = harness.Context.Services.GetRequiredService<IProjectFilePortfolioCoordinator>();
        await using ProjectFilePortfolioWorkspace workspace = await coordinator.OpenAsync(projection);
        FileBrowserSourceDescriptor source = Assert.Single(workspace.Browser.Snapshot.Sources);
        await workspace.Browser.InitializeAsync(source.Id);
        ProjectFilePortfolioRevision firstRevision = workspace.Revision;
        var storageCatalog = harness.Context.Services.GetRequiredService<IStorageCatalogService>();
        StorageCatalogRecord storage = await storageCatalog.EnsureBootstrapFileSystemStorageAsync();
        var changeSink = harness.Context.Services.GetRequiredService<IFileCatalogChangeSink>();
        var scope = new FileToolsSemanticScope(
            FileToolsSemanticScopeKind.Project,
            new FileToolsSemanticScopeId(projectId.ToString("N")),
            "Revision Project");

        changeSink.PublishScopeChanged(scope, storage.Id);

        Assert.True(await coordinator.UpdateAsync(workspace, projection));
        Assert.NotEqual(firstRevision, workspace.Revision);
        Assert.Equal(source.Id, workspace.Browser.Snapshot.CurrentSource?.Id);
        Assert.Equal(source.Id, workspace.Browser.Snapshot.Location?.Current.Key.SourceId);
    }

    [Fact]
    public async Task Project_files_tab_and_cards_share_include_subprojects_projection()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        Guid rootId = await CreateProjectAsync(projectsService, "Shared Root");
        Guid childId = await CreateProjectAsync(projectsService, "Shared Child");
        Guid unrelatedId = await CreateProjectAsync(projectsService, "Shared Unrelated");
        Assert.True((await projectsService.AddSubprojectAsync(rootId, childId)).IsSuccess);
        Assert.NotEqual(Guid.Empty, unrelatedId);
        await File.WriteAllTextAsync(
            Path.Combine(CreateProjectFilesDirectory(harness, rootId), "root.txt"),
            "root");
        await File.WriteAllTextAsync(
            Path.Combine(CreateProjectFilesDirectory(harness, childId), "child.txt"),
            "child");
        var cut = harness.Context.RenderComponent<ProjectsPage>();

        cut.Find("[data-testid='hierarchy-filter-project']").Change(rootId.ToString());
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[data-testid='project-card']").Count));
        cut.FindAll("button[role='tab']")
            .Single(tab => tab.TextContent.Contains("Files", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
        {
            var pane = cut.Find("[data-testid='project-files-portfolio-pane']");
            Assert.Contains("2 project(s) · 2 source(s)", pane.TextContent);
        });

        cut.Find("[data-testid='project-include-subprojects-filter']").Change(false);

        cut.WaitForAssertion(() =>
        {
            var pane = cut.Find("[data-testid='project-files-portfolio-pane']");
            Assert.Contains("1 project(s) · 1 source(s)", pane.TextContent);
        });
        cut.FindAll("button[role='tab']")
            .Single(tab => tab.TextContent.Contains("Cards", StringComparison.Ordinal))
            .Click();
        cut.WaitForAssertion(() =>
        {
            var card = Assert.Single(cut.FindAll("[data-testid='project-card']"));
            Assert.Contains("Shared Root", card.TextContent);
        });
    }

    [Fact]
    public async Task Project_files_portfolio_error_waits_for_explicit_retry()
    {
        var coordinator = new FailingProjectFilePortfolioCoordinator();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<IProjectFilePortfolioCoordinator>();
            services.AddSingleton<IProjectFilePortfolioCoordinator>(coordinator);
        });
        var project = new ProjectSummary(
            Guid.NewGuid(),
            "Denied portfolio",
            ProjectStatus.Active,
            "Discovery",
            PhaseCount: 1,
            ParentCount: 0,
            ChildCount: 0,
            DateTimeOffset.UtcNow);
        ProjectFileFilterProjection projection = ProjectFileFilterProjection.Create(
            [project],
            [],
            new ProjectFileFilter());
        var cut = harness.Context.RenderComponent<ProjectFilesPortfolioPane>(parameters => parameters
            .Add(component => component.Projection, projection));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Portfolio access was revoked.", cut.Markup);
            Assert.Equal(1, coordinator.OpenCalls);
        });
        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.Projection, projection));
        Assert.Equal(1, coordinator.OpenCalls);

        cut.Find("[data-testid='project-files-portfolio-retry']").Click();

        cut.WaitForAssertion(() => Assert.Equal(2, coordinator.OpenCalls));
    }

    [Fact]
    public async Task Project_files_portfolio_opens_authorized_file_after_browser_disposal()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        Guid projectId = await CreateProjectAsync(projectsService, "Portfolio open project");
        string projectDirectory = CreateProjectFilesDirectory(harness, projectId);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "portfolio-open.md"),
            "# Portfolio interaction\n\nAuthorized aggregate content.");
        var cut = harness.Context.RenderComponent<ProjectsPage>();
        cut.WaitForAssertion(() => Assert.Contains("Portfolio open project", cut.Markup));
        cut.FindAll("button[role='tab']")
            .Single(tab => tab.TextContent.Contains("Files", StringComparison.Ordinal))
            .Click();
        cut.WaitForAssertion(() => Assert.Contains("portfolio-open.md", cut.Markup));

        cut.FindAll(".ft-file-browser__item-main")
            .Single(button => button.TextContent.Contains("portfolio-open.md", StringComparison.Ordinal))
            .KeyUp("Enter");

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Authorized aggregate content.", cut.Find("[data-testid='interaction-text-view']").TextContent);
            Assert.Empty(cut.FindAll(".ft-file-browser"));
            Assert.NotNull(cut.Find("[data-testid='project-files-portfolio-back']"));
        });
        cut.Find("[data-testid='project-files-portfolio-back']").Click();
        cut.WaitForAssertion(() => Assert.Contains("portfolio-open.md", cut.Markup));
    }

    [Fact]
    public async Task Saves_project_from_wizard_first_flow()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var cut = harness.Context.RenderComponent<ProjectsPage>();

        cut.Find("[data-testid='projects-new-button']").Click();
        cut.Find("[data-testid='project-name-input']").Change("Wizard Project");
        cut.Find("[data-testid='project-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Wizard Project", cut.Markup);
            Assert.Contains("Project saved", cut.Markup);
        });
    }

    [Fact]
    public async Task Shows_saved_project_as_card_with_dashboard_action()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var cut = harness.Context.RenderComponent<ProjectsPage>();

        cut.Find("[data-testid='projects-new-button']").Click();
        cut.WaitForElement("[data-testid='project-name-input']");
        cut.Find("[data-testid='project-name-input']").Change("Card Modal Project");
        cut.Find("[data-testid='project-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Card Modal Project", cut.Markup);
            Assert.NotEmpty(cut.FindAll("[data-testid='project-card']"));
            Assert.Contains("Open dashboard tab", cut.Markup);
        });
    }

    [Fact]
    public async Task Project_overview_modal_explains_that_header_uses_saved_project_name()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var projectId = await CreateProjectAsync(projectsService, "Explained Project");

        var cut = harness.Context.RenderComponent<ProjectsPage>();
        cut.WaitForAssertion(() => Assert.Contains("Explained Project", cut.Markup));
        var projectCard = cut.FindAll("[data-testid='project-card']")
            .Single(card => card.TextContent.Contains("Explained Project", StringComparison.Ordinal));

        projectCard.QuerySelector("[data-testid='project-card-details-button']")!.Click();

        cut.WaitForAssertion(() =>
        {
            var modal = cut.Find("[data-testid='projects-detail-modal']");

            Assert.Contains("Explained Project", modal.TextContent);
            Assert.Contains("This header uses the saved project name.", modal.TextContent);
            Assert.Contains("Edit name and details", modal.TextContent);
        });

        Assert.NotEqual(Guid.Empty, projectId);
    }

    [Fact]
    public async Task Filters_direct_subprojects_of_the_selected_project()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var parentProjectId = await CreateProjectAsync(projectsService, "Alpha parent");
        var childProjectId = await CreateProjectAsync(projectsService, "Beta child");
        var unrelatedProjectId = await CreateProjectAsync(projectsService, "Gamma unrelated");

        Assert.True((await projectsService.AddSubprojectAsync(parentProjectId, childProjectId)).IsSuccess);
        Assert.NotEqual(Guid.Empty, unrelatedProjectId);

        var cut = harness.Context.RenderComponent<ProjectsPage>();

        cut.Find("[data-testid='hierarchy-filter-project']").Change(parentProjectId.ToString());
        cut.Find("[data-testid='hierarchy-filter-mode']").Change(ProjectHierarchyFilterMode.Children.ToString());

        cut.WaitForAssertion(() =>
        {
            var cards = cut.FindAll("[data-testid='project-card']");

            var card = Assert.Single(cards);
            Assert.Contains("Beta child", card.TextContent);
        });
    }

    [Fact]
    public async Task Shows_only_main_projects_until_a_project_is_selected()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var parentProjectId = await CreateProjectAsync(projectsService, "Main Alpha");
        var childProjectId = await CreateProjectAsync(projectsService, "Nested Beta");
        var unrelatedProjectId = await CreateProjectAsync(projectsService, "Main Gamma");

        Assert.True((await projectsService.AddSubprojectAsync(parentProjectId, childProjectId)).IsSuccess);
        Assert.NotEqual(Guid.Empty, unrelatedProjectId);

        var cut = harness.Context.RenderComponent<ProjectsPage>();

        cut.WaitForAssertion(() =>
        {
            var cards = cut.FindAll("[data-testid='project-card']");
            Assert.Equal(2, cards.Count);
            Assert.Contains(cards, card => card.TextContent.Contains("Main Alpha", StringComparison.Ordinal));
            Assert.Contains(cards, card => card.TextContent.Contains("Main Gamma", StringComparison.Ordinal));
            Assert.DoesNotContain(cards, card => card.TextContent.Contains("Nested Beta", StringComparison.Ordinal));

            var projectSelector = cut.Find("[data-testid='hierarchy-filter-project']");
            Assert.Contains("Main Alpha", projectSelector.InnerHtml);
            Assert.DoesNotContain("Nested Beta", projectSelector.InnerHtml);
        });
    }

    [Fact]
    public async Task Project_tree_selection_filters_selected_project_and_subprojects()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var parentProjectId = await CreateProjectAsync(projectsService, "Scope Alpha");
        var childProjectId = await CreateProjectAsync(projectsService, "Scope Beta");
        var grandchildProjectId = await CreateProjectAsync(projectsService, "Scope Gamma");
        var unrelatedProjectId = await CreateProjectAsync(projectsService, "Scope Delta");

        Assert.True((await projectsService.AddSubprojectAsync(parentProjectId, childProjectId)).IsSuccess);
        Assert.True((await projectsService.AddSubprojectAsync(childProjectId, grandchildProjectId)).IsSuccess);
        Assert.NotEqual(Guid.Empty, unrelatedProjectId);

        var cut = harness.Context.RenderComponent<ProjectsPage>();

        cut.WaitForAssertion(() => Assert.Contains("Scope Alpha", cut.Markup));
        cut.Find($"[data-testid='projects-tree-node-{parentProjectId:N}']").Click();

        cut.WaitForAssertion(() =>
        {
            var cards = cut.FindAll("[data-testid='project-card']");

            Assert.Equal(3, cards.Count);
            Assert.Contains(cards, card => card.TextContent.Contains("Scope Alpha", StringComparison.Ordinal));
            Assert.Contains(cards, card => card.TextContent.Contains("Scope Beta", StringComparison.Ordinal));
            Assert.Contains(cards, card => card.TextContent.Contains("Scope Gamma", StringComparison.Ordinal));
            Assert.DoesNotContain(cards, card => card.TextContent.Contains("Scope Delta", StringComparison.Ordinal));
            Assert.Empty(cut.FindAll("[data-testid='projects-detail-modal']"));
        });
    }

    [Fact]
    public async Task Project_tree_toggle_collapses_root_project_children()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var parentProjectId = await CreateProjectAsync(projectsService, "Collapsible parent");
        var childProjectId = await CreateProjectAsync(projectsService, "Hidden child");

        Assert.True((await projectsService.AddSubprojectAsync(parentProjectId, childProjectId)).IsSuccess);

        var cut = harness.Context.RenderComponent<ProjectsPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll($"[data-testid='projects-tree-children-{parentProjectId:N}']"));
            Assert.NotEmpty(cut.FindAll($"[data-testid='projects-tree-node-{childProjectId:N}']"));
        });

        var parentNode = cut.Find($"[data-testid='projects-tree-node-{parentProjectId:N}']");
        var toggle = parentNode.ParentElement?.QuerySelector("button.cda-treeview__toggle")
            ?? throw new InvalidOperationException("The project tree toggle was not rendered.");

        toggle.Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("false", cut.Find($"[data-testid='projects-tree-node-{parentProjectId:N}']").GetAttribute("aria-expanded"));
            Assert.Empty(cut.FindAll($"[data-testid='projects-tree-children-{parentProjectId:N}']"));
            Assert.Empty(cut.FindAll($"[data-testid='projects-tree-node-{childProjectId:N}']"));
        });
    }

    [Fact]
    public async Task Subprojects_modal_supports_recursive_drill_down()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var parentProjectId = await CreateProjectAsync(projectsService, "Root project");
        var childProjectId = await CreateProjectAsync(projectsService, "Nested child");
        var grandchildProjectId = await CreateProjectAsync(projectsService, "Nested grandchild");

        Assert.True((await projectsService.AddSubprojectAsync(parentProjectId, childProjectId)).IsSuccess);
        Assert.True((await projectsService.AddSubprojectAsync(childProjectId, grandchildProjectId)).IsSuccess);

        var cut = harness.Context.RenderComponent<ProjectsPage>();
        cut.WaitForAssertion(() => Assert.Contains("Root project", cut.Markup));

        var parentCard = cut.FindAll("[data-testid='project-card']")
            .Single(card => card.TextContent.Contains("Root project", StringComparison.Ordinal));
        parentCard.QuerySelector("[data-testid='project-card-subprojects-button']")!.Click();

        cut.WaitForAssertion(() =>
        {
            var modal = cut.Find("[data-testid='projects-hierarchy-modal']");

            Assert.Contains("Nested child", modal.TextContent);
        });

        var childCard = cut.FindAll("[data-testid='hierarchy-subproject-card']")
            .Single(card => card.TextContent.Contains("Nested child", StringComparison.Ordinal));
        childCard.QuerySelector("[data-testid='hierarchy-card-subprojects-button']")!.Click();

        cut.WaitForAssertion(() =>
        {
            var modal = cut.Find("[data-testid='projects-hierarchy-modal']");

            Assert.Contains("Nested grandchild", modal.TextContent);
        });
    }

    [Fact]
    public async Task Project_card_places_gantt_after_structure_and_opens_gantt_tab()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var projectId = await CreateProjectAsync(projectsService, "Gantt tab project");

        var cut = harness.Context.RenderComponent<ProjectsPage>();
        cut.WaitForAssertion(() => Assert.Contains("Gantt tab project", cut.Markup));
        var projectCard = cut.FindAll("[data-testid='project-card']")
            .Single(card => card.TextContent.Contains("Gantt tab project", StringComparison.Ordinal));
        var structureButton = projectCard.QuerySelector("[data-testid='project-card-structure-button']")
            ?? throw new InvalidOperationException("The project card structure button was not rendered.");
        var ganttButton = projectCard.QuerySelector("[data-testid='project-card-gantt-button']")
            ?? throw new InvalidOperationException("The project card Gantt button was not rendered.");

        Assert.Equal("Open Gantt tab", ganttButton.GetAttribute("title"));
        Assert.Equal("Open Gantt tab", ganttButton.GetAttribute("aria-label"));
        Assert.Equal("project-card-gantt-button", structureButton.NextElementSibling?.GetAttribute("data-testid"));

        ganttButton.Click();

        cut.WaitForAssertion(() =>
            Assert.Equal($"{navigation.BaseUri}projects/{projectId:D}/structure?tab=gantt", navigation.Uri));
    }

    [Fact]
    public async Task Package_import_requires_a_package_path()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var cut = harness.Context.RenderComponent<ProjectsPage>();

        Assert.Empty(cut.FindAll("[data-testid='projects-package-path-input']"));
        cut.Find("[data-testid='projects-package-dialog-button']").Click();
        cut.WaitForElement("[data-testid='projects-import-package-button']");
        cut.Find("[data-testid='projects-import-package-button']").Click();

        cut.WaitForAssertion(() =>
        {
            var message = cut.Find("[data-testid='projects-package-message']");

            Assert.Contains("Choose a project package path", message.TextContent);
        });
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description",
            Objective = $"{name} objective",
            CurrentPhase = "Discovery"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static string CreateProjectFilesDirectory(ComponentTestHarness harness, Guid projectId)
    {
        var workspacePaths = harness.Context.Services.GetRequiredService<IWorkspacePathResolver>();
        string path = Path.Combine(
            workspacePaths.ResolveWorkspaceRoot(),
            "managed-files",
            "project-media",
            "files",
            projectId.ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FailingProjectFilesPilotCoordinator : IProjectFilesPilotCoordinator
    {
        public int OpenCalls { get; private set; }

        public ValueTask<ProjectFilesPilotWorkspace> OpenAsync(
            Guid projectId,
            string projectName,
            CancellationToken cancellationToken = default)
        {
            OpenCalls++;
            return ValueTask.FromException<ProjectFilesPilotWorkspace>(new FileBrowserProviderException(
                new FileBrowserError(FileBrowserErrorCode.Forbidden, "Project access was revoked.")));
        }

        public ValueTask<ProjectFilesPilotInteraction> ActivateAsync(
            ProjectFilesPilotWorkspace workspace,
            FileBrowserItemKey itemKey,
            CancellationToken cancellationToken = default)
            => ValueTask.FromException<ProjectFilesPilotInteraction>(new NotSupportedException());
    }

    private sealed class FailingProjectFilePortfolioCoordinator : IProjectFilePortfolioCoordinator
    {
        public int OpenCalls { get; private set; }

        public ValueTask<ProjectFilePortfolioWorkspace> OpenAsync(
            ProjectFileFilterProjection projection,
            CancellationToken cancellationToken = default)
        {
            OpenCalls++;
            return ValueTask.FromException<ProjectFilePortfolioWorkspace>(new FileBrowserProviderException(
                new FileBrowserError(FileBrowserErrorCode.Forbidden, "Portfolio access was revoked.")));
        }

        public ValueTask<bool> UpdateAsync(
            ProjectFilePortfolioWorkspace workspace,
            ProjectFileFilterProjection projection,
            CancellationToken cancellationToken = default)
            => ValueTask.FromException<bool>(new NotSupportedException());

        public ValueTask<ProjectFilesPilotInteraction> ActivateAsync(
            ProjectFilePortfolioWorkspace workspace,
            FileBrowserItemKey itemKey,
            CancellationToken cancellationToken = default)
            => ValueTask.FromException<ProjectFilesPilotInteraction>(new NotSupportedException());
    }

    private sealed class CancellingBrowseItemActivator(
        CancellationTokenSource cancellation,
        FileReference file) : IFileToolsBrowseItemActivator
    {
        public ValueTask<FileToolsKnownFileActivation> ActivateAsync(
            FileToolsSemanticScope scope,
            FileBrowserItemKey itemKey,
            FileToolsKnownFileIntent intent,
            CancellationToken cancellationToken = default)
        {
            cancellation.Cancel();
            return ValueTask.FromResult(new FileToolsKnownFileActivation(
                new FileToolsKnownFileRequest(scope, file, intent),
                "cancelled.txt",
                "text/plain",
                size: 1));
        }
    }

    private sealed class CancelledKnownFileSessionFactory : IFileToolsKnownFileSessionFactory
    {
        public ValueTask<FileToolsKnownFileSession> CreateAsync(
            FileToolsKnownFileRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromCanceled<FileToolsKnownFileSession>(cancellationToken);
    }

    private sealed class RecordingKnownFileSessionReleaser : IFileToolsKnownFileSessionReleaser
    {
        public FileReference? ReleasedFile { get; private set; }

        public bool CleanupTokenWasCancellationRequested { get; private set; }

        public ValueTask ReleaseAsync(
            FileReference file,
            CancellationToken cancellationToken = default)
        {
            ReleasedFile = file;
            CleanupTokenWasCancellationRequested = cancellationToken.IsCancellationRequested;
            return ValueTask.CompletedTask;
        }
    }
}
