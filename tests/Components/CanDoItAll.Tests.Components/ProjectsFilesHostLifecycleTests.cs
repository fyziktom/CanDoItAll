using Bunit;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileBrowser.Components;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages.Components;
using CanDoItAll.Projects.Files.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Components.Web;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectsFilesHostLifecycleTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Same_activation_real_browser_preserves_newer_preview_or_failure(bool portfolio, bool newerFails) {
        var control = new PreviewControl(newerFails);
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            Decorate<IProjectFilesPilotCoordinator>(services, owner => new HeldPreview(owner, control));
            Decorate<IProjectFilePortfolioCoordinator>(services, owner => new HeldPortfolioPreview(owner, control));
        });
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        Guid project = (await projects.SaveAsync(new() { Name = "Preview ordering" })).Value;
        string root = harness.Context.Services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();
        string directory = Path.Combine(root, "managed-files", "project-media", "files", project.ToString("N"));
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "alpha.txt"), "Original preview bytes");
        await File.WriteAllTextAsync(Path.Combine(directory, "beta.txt"), "Accepted successor bytes");
        var projection = ProjectFileFilterProjection.Create(await projects.ListAsync(), [], new());
        IRenderedComponent<Microsoft.AspNetCore.Components.ComponentBase> cut = portfolio
            ? harness.Context.Render<ProjectFilesPortfolioPane>(p => p.Add(x => x.Projection, projection))
            : harness.Context.Render<ProjectFilesDialog>(p => p.Add(x => x.IsOpen, true).Add(x => x.ProjectId, project).Add(x => x.ProjectName, "Preview ordering"));
        try {
            cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".ft-file-browser__item-main").Count));
            var browser = cut.FindComponent<FileBrowser>().Instance.Session;
            var pending = cut.InvokeAsync(() => cut.FindAll(".ft-file-browser__item-main").Single(x => x.TextContent.Contains("alpha.txt")).KeyUpAsync(new KeyboardEventArgs { Key = "Enter" }));
            await control.Started.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await cut.InvokeAsync(() => cut.FindAll(".ft-file-browser__item-main").Single(x => x.TextContent.Contains("beta.txt")).KeyUpAsync(new KeyboardEventArgs { Key = "Enter" }));
            var newest = View().Preview;
            Assert.Same(browser, View().Browse!.Session);
            control.Release.TrySetResult();
            await pending;
            Assert.Equal(2, control.Calls);
            Assert.Equal(newerFails ? "Newest native preview was revoked." : null, View().ActivationError);
            Assert.Same(newest, View().Preview);
            if (newerFails) {
                Assert.Contains("Newest native preview was revoked.", cut.Markup);
                await cut.InvokeAsync(() => cut.FindAll(".ft-file-browser__item-main").Single(x => x.TextContent.Contains("beta.txt")).KeyUpAsync(new KeyboardEventArgs { Key = "Enter" }));
                newest = View().Preview;
                Assert.Null(View().ActivationError);
            }
            Assert.NotNull(newest);
            Assert.Equal("beta.txt", newest.Request.FileName);
            await using var lease = await newest.ContentSource.OpenReadAsync(new(newest.Request.File));
            using var reader = new StreamReader(lease.Stream);
            Assert.Equal("Accepted successor bytes", await reader.ReadToEndAsync());
        } finally {
            control.Release.TrySetResult();
            await harness.Context.DisposeRenderedComponentsAsync();
        }

        ProjectFilesViewState View() => portfolio
            ? cut.FindComponent<ProjectFilesPortfolioPaneView>().Instance.State
            : cut.FindComponent<ProjectFilesDialogView>().Instance.State;
    }

    [Fact]
    public async Task Native_portfolio_accepts_64_sources_and_refuses_65_without_truncation() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        for (int index = 0; index < 65; index++) {
            await projects.SaveAsync(new() { Name = $"Bounded source {index:D2}" });
        }
        var summaries = await projects.ListAsync();
        var coordinator = harness.Context.Services.GetRequiredService<IProjectFilePortfolioCoordinator>();
        await using var accepted = await coordinator.OpenAsync(ProjectFileFilterProjection.Create(summaries.Take(64).ToArray(), [], new()));
        Assert.Equal(64, accepted.ProjectCount);
        Assert.Equal(64, accepted.SourceCount);
        Assert.Equal(64, accepted.Browser.Snapshot.Sources.Count);
        var failure = await Assert.ThrowsAsync<FileBrowserProviderException>(() => coordinator.OpenAsync(ProjectFileFilterProjection.Create(summaries, [], new())).AsTask());
        Assert.Equal(FileBrowserErrorCode.InvalidOperation, failure.Error.Code);
        Assert.Equal(64, accepted.Browser.Snapshot.Sources.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Overlapping_real_portfolios_keep_sources_scopes_actions_and_preview_in_the_accepted_workspace(bool failOld) {
        var control = new PortfolioControl { FailOld = failOld };
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            Decorate<IProjectFilePortfolioCoordinator>(services, owner => new HeldPortfolio(owner, control));
            services.AddSingleton<ILogger<ProjectFilesPortfolioPane>>(control);
        });
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        Guid alpha = (await projects.SaveAsync(new() { Name = "Native Alpha" })).Value;
        Guid beta = (await projects.SaveAsync(new() { Name = "Native Beta" })).Value;
        foreach (var (id, name) in new[] { (alpha, "alpha"), (beta, "beta") }) {
            string root = harness.Context.Services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();
            string directory = Path.Combine(root, "managed-files", "project-media", "files", id.ToString("N"));
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, name + ".txt"), "Authorized native " + name);
        }
        var summaries = await projects.ListAsync();
        var all = ProjectFileFilterProjection.Create(summaries, [], new());
        var one = ProjectFileFilterProjection.Create(summaries, [], new(hierarchyProjectId: alpha, includeSubprojects: false));
        var cut = harness.Context.Render<ProjectFilesPortfolioPane>(p => p.Add(x => x.Projection, all));
        try {
            await control.Started.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await cut.InvokeAsync(() => cut.Render(p => p.Add(x => x.Projection, one)));
            cut.WaitForAssertion(() => Assert.Single(View().Browse!.Session.Snapshot.Sources));
            await cut.InvokeAsync(() => cut.Render(p => p.Add(x => x.Projection, all)));
            cut.WaitForAssertion(() => Assert.Equal(2, View().Browse!.Session.Snapshot.Sources.Count));
            var accepted = control.Opened[2];
            var acceptedView = View().Browse!;
            Assert.Same(accepted.Browser, acceptedView.Session);
            var betaSource = accepted.Browser.Snapshot.Sources.Single(source => source.Description == "Native Beta");
            await cut.InvokeAsync(() => accepted.Browser.ChangeSourceAsync(betaSource.Id).AsTask());
            var betaItem = Assert.Single(accepted.Browser.Snapshot.Items);
            control.Release.TrySetResult();
            if (failOld) {
                cut.WaitForAssertion(() => Assert.Single(control.Failures));
            } else {
                cut.WaitForAssertion(() => Assert.True(control.Opened[0].IsDisposed));
            }
            Assert.Same(acceptedView.Session, View().Browse!.Session);
            Assert.Equal(2, accepted.ProjectCount);
            Assert.Equal(2, accepted.SourceCount);
            Assert.Contains(accepted.Revision.Value[..12], View().SourceSummary);
            Assert.Equal(betaSource.Id, accepted.Browser.Snapshot.Location!.Current.Key.SourceId);
            Assert.Null(View().OpenError);
            Assert.Equal(0, control.Updates);
            await cut.InvokeAsync(() => View().Browse!.ItemInvoked.InvokeAsync(new(betaItem, FileBrowserInvocationKind.Keyboard)));
            var preview = View().Preview!;
            await using (var lease = await preview.ContentSource.OpenReadAsync(new(preview.Request.File))) {
                using var reader = new StreamReader(lease.Stream);
                Assert.Equal("Authorized native beta", await reader.ReadToEndAsync());
            }
            Assert.Empty(cut.FindAll(".ft-file-browser"));
            var empty = ProjectFileFilterProjection.Create(summaries, [], new(search: "No matching native project"));
            await cut.InvokeAsync(() => cut.Render(p => p.Add(x => x.Projection, empty)));
            cut.WaitForAssertion(() => Assert.True(View().IsEmpty));
            Assert.Null(View().OpenError);
            Assert.Null(View().Preview);
            var denied = await Assert.ThrowsAsync<FileAccessDeniedException>(() => preview.ContentSource.OpenReadAsync(new(preview.Request.File)).AsTask());
            Assert.Equal(FileAccessFailureCode.Revoked, denied.Code);
        } finally {
            control.Release.TrySetResult();
            await harness.Context.DisposeRenderedComponentsAsync();
        }

        ProjectFilesViewState View() => cut.FindComponent<ProjectFilesPortfolioPaneView>().Instance.State;
    }

    [Fact]
    public async Task Actual_preview_grant_is_revoked_when_construction_and_cleanup_both_report_failure() {
        FailingSessionFactory capture = null!;
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            Decorate<IFileToolsKnownFileSessionFactory>(services, inner => capture = new(inner));
            Decorate<IFileToolsKnownFileSessionReleaser>(services, inner => new FailingRelease(inner));
        });
        Guid project = (await harness.Context.Services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Partial preview construction" })).Value;
        string root = harness.Context.Services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();
        string directory = Path.Combine(root, "managed-files", "project-media", "files", project.ToString("N"));
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "construction.txt"), "Original native bytes");
        var coordinator = harness.Context.Services.GetRequiredService<IProjectFilesPilotCoordinator>();
        await using var workspace = await coordinator.OpenAsync(project, "Partial preview construction");
        await workspace.Browser.InitializeAsync();
        var failure = await Assert.ThrowsAsync<AggregateException>(() => coordinator.ActivateAsync(workspace, workspace.Browser.Snapshot.Items[0].Key).AsTask());
        Assert.Collection(failure.InnerExceptions,
            primary => Assert.Equal("Injected primary construction failure", primary.Message),
            cleanup => Assert.Equal("Injected cleanup acknowledgement failure", cleanup.Message));
        Assert.NotNull(capture.Request);
        var denied = await Assert.ThrowsAsync<FileAccessDeniedException>(() => capture.Original.CreateAsync(capture.Request).AsTask());
        Assert.Equal(FileAccessFailureCode.Revoked, denied.Code);
    }

    private static void Decorate<T>(IServiceCollection services, Func<T, T> decorate) where T : class {
        var descriptor = services.Last(item => item.ServiceType == typeof(T));
        services.RemoveAll<T>();
        services.Add(ServiceDescriptor.Describe(typeof(T), provider => decorate((T)(descriptor.ImplementationInstance
            ?? descriptor.ImplementationFactory?.Invoke(provider)
            ?? ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!))), descriptor.Lifetime));
    }

    private sealed class HeldPortfolio(IProjectFilePortfolioCoordinator inner, PortfolioControl control) : IProjectFilePortfolioCoordinator {
        public async ValueTask<ProjectFilePortfolioWorkspace> OpenAsync(ProjectFileFilterProjection projection, CancellationToken cancellationToken = default) {
            var workspace = await inner.OpenAsync(projection, cancellationToken);
            control.Opened.Add(workspace);
            if (control.Opened.Count == 1) {
                control.Started.TrySetResult();
                await control.Release.Task;
                if (control.FailOld) {
                    await workspace.DisposeAsync();
                    throw new IOException("Injected late native source failure");
                }
            }
            return workspace;
        }
        public ValueTask<bool> UpdateAsync(ProjectFilePortfolioWorkspace workspace, ProjectFileFilterProjection projection, CancellationToken cancellationToken = default) {
            control.Updates++;
            return inner.UpdateAsync(workspace, projection, cancellationToken);
        }
        public ValueTask<ProjectFilesPilotInteraction> ActivateAsync(ProjectFilePortfolioWorkspace workspace, FileBrowserItemKey key, CancellationToken cancellationToken = default)
            => inner.ActivateAsync(workspace, key, cancellationToken);
    }

    private sealed class PortfolioControl : ILogger<ProjectFilesPortfolioPane> {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<ProjectFilePortfolioWorkspace> Opened { get; } = [];
        public List<Exception> Failures { get; } = [];
        public bool FailOld { get; init; }
        public int Updates { get; set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            if (exception is not null) {
                Failures.Add(exception);
            }
        }
    }

    private sealed class FailingSessionFactory(IFileToolsKnownFileSessionFactory original) : IFileToolsKnownFileSessionFactory {
        public IFileToolsKnownFileSessionFactory Original { get; } = original;
        public FileToolsKnownFileRequest? Request { get; private set; }
        public ValueTask<FileToolsKnownFileSession> CreateAsync(FileToolsKnownFileRequest request, CancellationToken cancellationToken = default) {
            Request = request;
            return ValueTask.FromException<FileToolsKnownFileSession>(new IOException("Injected primary construction failure"));
        }
    }

    private sealed class PreviewControl(bool newerFails) {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public async ValueTask BeforeAsync() {
            Calls++;
            if (Calls == 1) {
                Started.TrySetResult();
                await Release.Task;
                throw new IOException("Retired native preview failure");
            }
            if (Calls == 2 && newerFails) {
                throw new FileAccessDeniedException(FileAccessFailureCode.Revoked, "Newest native preview was revoked.");
            }
        }
    }

    private sealed class HeldPreview(IProjectFilesPilotCoordinator inner, PreviewControl control) : IProjectFilesPilotCoordinator {
        public ValueTask<ProjectFilesPilotWorkspace> OpenAsync(Guid projectId, string projectName, CancellationToken cancellationToken = default)
            => inner.OpenAsync(projectId, projectName, cancellationToken);
        public async ValueTask<ProjectFilesPilotInteraction> ActivateAsync(ProjectFilesPilotWorkspace workspace, FileBrowserItemKey itemKey, CancellationToken cancellationToken = default) {
            await control.BeforeAsync();
            return await inner.ActivateAsync(workspace, itemKey, cancellationToken);
        }
    }

    private sealed class HeldPortfolioPreview(IProjectFilePortfolioCoordinator inner, PreviewControl control) : IProjectFilePortfolioCoordinator {
        public ValueTask<ProjectFilePortfolioWorkspace> OpenAsync(ProjectFileFilterProjection projection, CancellationToken cancellationToken = default)
            => inner.OpenAsync(projection, cancellationToken);
        public ValueTask<bool> UpdateAsync(ProjectFilePortfolioWorkspace workspace, ProjectFileFilterProjection projection, CancellationToken cancellationToken = default)
            => inner.UpdateAsync(workspace, projection, cancellationToken);
        public async ValueTask<ProjectFilesPilotInteraction> ActivateAsync(ProjectFilePortfolioWorkspace workspace, FileBrowserItemKey itemKey, CancellationToken cancellationToken = default) {
            await control.BeforeAsync();
            return await inner.ActivateAsync(workspace, itemKey, cancellationToken);
        }
    }

    private sealed class FailingRelease(IFileToolsKnownFileSessionReleaser inner) : IFileToolsKnownFileSessionReleaser {
        public async ValueTask ReleaseAsync(FileReference file, CancellationToken cancellationToken = default) {
            Assert.False(cancellationToken.IsCancellationRequested);
            await inner.ReleaseAsync(file, cancellationToken);
            throw new IOException("Injected cleanup acknowledgement failure");
        }
    }
}
