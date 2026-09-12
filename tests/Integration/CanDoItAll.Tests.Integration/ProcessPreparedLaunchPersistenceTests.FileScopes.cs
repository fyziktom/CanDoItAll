using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.FileTools.Desktop;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessPreparedLaunchPersistenceTests {
    [Fact]
    public async Task Process_files_and_Structure_browse_download_and_local_open_use_original_organization_root_after_restart() {
        await using var environment = CanDoItAllTestEnvironment.Create("process-file-original-scope");
        var profile = environment.CreatePostgreSqlProfile("original");
        var launcher = new FileScopeLauncher();
        var harness = FileScopeHarness(environment, profile, launcher);
        ProcessPreparedLaunchSnapshot saved;
        string evidence;
        await using (var app = await TestApplication.CreateAsync(harness)) {
            await using var scope = app.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            saved = await CreateAcceptedProjectLaunchAsync(services);
            var delivery = await Delivery(services, Coordinator(services)).DeliverAsync(saved.Preparation.AdmissionId);
            Assert.Equal(ProcessLaunchLinkDeliveryState.Delivered, delivery.State);
            await CancelFileRunAsync(services, saved);
            saved = (await Store(services).GetAsync(saved.Preparation.AdmissionId))!;
            var root = FileRoot(saved);
            var write = services.GetRequiredService<IWorkspaceFileService>().WriteTextFile(root + "/proof.md", "Original Process output");
            Assert.True(write.Succeeded, write.Message);
            var project = saved.Preparation.Authority!.ProjectAdmission!;
            var decoy = WorkspaceScopeDescriptor.Project(project.ProjectId.ToString("D"))
                .CombineArtifactPath("process-runs", RunId(saved).ToString("D"));
            var workspace = services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();
            var decoyDirectory = Path.Combine(workspace, decoy.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(decoyDirectory);
            await File.WriteAllTextAsync(Path.Combine(decoyDirectory, "decoy.md"), "Wrong project-scoped output");
            evidence = await ReadFileRunEvidenceAsync(services, saved);
            await AssertActualFilePathsAsync(services, saved, launcher);
            Assert.Equal(evidence, await ReadFileRunEvidenceAsync(services, saved));
        }
        await using (var restarted = await TestApplication.CreateAsync(harness)) {
            await using var scope = restarted.Services.CreateAsyncScope();
            await AssertActualFilePathsAsync(scope.ServiceProvider, saved, launcher);
            Assert.Equal(evidence, await ReadFileRunEvidenceAsync(scope.ServiceProvider, saved));
        }
        Assert.Equal(2, launcher.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Process_file_scope_refuses_deleted_or_recreated_original_project_without_rebinding(bool recreate) {
        await using var environment = CanDoItAllTestEnvironment.Create("process-file-retired-scope");
        var profile = environment.CreatePostgreSqlProfile("original");
        var harness = Harness(environment, profile);
        ProcessPreparedLaunchSnapshot saved;
        FileToolsSemanticScope oldScope;
        string retained;
        await using (var app = await TestApplication.CreateAsync(harness)) {
            await using var scope = app.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            saved = await CreateAcceptedProjectLaunchAsync(services);
            await CancelFileRunAsync(services, saved);
            oldScope = Assert.Single((await services.GetRequiredService<IProcessRunFileScopeProvider>().ResolveAsync(RunId(saved))).Scopes);
            var project = saved.Preparation.Authority!.ProjectAdmission!;
            await services.GetRequiredService<ProjectsService>().DeleteAsync(project.ProjectId);
            if (recreate) {
                Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(project.ProjectId,
                    new() { Name = "Different project lifetime" })).IsSuccess);
                var current = Assert.IsType<ProjectWriteAdmission>(await services.GetRequiredService<ProjectWriteAdmissionService>()
                    .CaptureAsync(project.ProjectId));
                Assert.NotEqual(project.LifetimeId, current.LifetimeId);
            }
            retained = await ReadFileRunEvidenceAsync(services, saved);
            await AssertFileLifetimeDeniedAsync(services, saved, oldScope);
        }
        await using (var restarted = await TestApplication.CreateAsync(harness)) {
            await using var scope = restarted.Services.CreateAsyncScope();
            await AssertFileLifetimeDeniedAsync(scope.ServiceProvider, saved, oldScope);
            Assert.Equal(retained, await ReadFileRunEvidenceAsync(scope.ServiceProvider, saved));
        }
    }

    [Fact]
    public async Task Process_file_scope_rejects_original_run_loaded_under_another_current_profile_before_storage_access() {
        await using var environment = CanDoItAllTestEnvironment.Create("process-file-profile-scope");
        var firstProfile = environment.CreatePostgreSqlProfile("first");
        var secondProfile = environment.CreatePostgreSqlProfile("second");
        await using var first = await TestApplication.CreateAsync(Harness(environment, firstProfile));
        await using var firstScope = first.Services.CreateAsyncScope();
        var original = firstScope.ServiceProvider;
        var saved = await CreateAcceptedProjectLaunchAsync(original);
        await CancelFileRunAsync(original, saved);
        var before = await ReadFileRunEvidenceAsync(original, saved);
        await using var second = await TestApplication.CreateAsync(Harness(environment, secondProfile));
        await using var secondScope = second.Services.CreateAsyncScope();
        var currentAdmissions = secondScope.ServiceProvider.GetRequiredService<ProjectWriteAdmissionService>();
        Assert.NotEqual(saved.Preparation.Authority!.DatabaseProfileId, currentAdmissions.DatabaseProfileId);
        var staleReader = new ProcessRunFileScopeProvider(original.GetRequiredService<IProcessRuntimeStateStore>(),
            original.GetRequiredService<IProcessRuntimeStepAssignmentStore>(), original.GetRequiredService<IStorageCatalogService>(),
            original.GetRequiredService<IProcessPreparedLaunchStore>(), currentAdmissions);
        var error = await Assert.ThrowsAsync<FileBrowserProviderException>(() => staleReader.ResolveRootAsync(
            RunId(saved), FileRoot(saved), saved.Preparation.Authority.ProjectAdmission!.ProjectId).AsTask());
        Assert.Equal(FileBrowserErrorCode.Forbidden, error.Error.Code);
        Assert.Equal(before, await ReadFileRunEvidenceAsync(original, saved));
    }

    [Fact]
    public async Task Process_file_scope_rejects_another_project_and_unrecorded_product_root() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var saved = await CreateAcceptedProjectLaunchAsync(services);
        var other = await CreateProjectAsync(services);
        var files = services.GetRequiredService<IProcessRunFileScopeProvider>();
        var before = await ReadFileRunEvidenceAsync(services, saved);
        var wrongProject = await Assert.ThrowsAsync<FileBrowserProviderException>(() => files.ResolveRootAsync(
            RunId(saved), FileRoot(saved), other.ProjectId).AsTask());
        Assert.Equal(FileBrowserErrorCode.Forbidden, wrongProject.Error.Code);
        var inventedRoot = await Assert.ThrowsAsync<FileBrowserProviderException>(() => files.ResolveRootAsync(
            RunId(saved), $"output/process-runs/{RunId(saved):D}/not-recorded",
            saved.Preparation.Authority!.ProjectAdmission!.ProjectId).AsTask());
        Assert.Equal(FileBrowserErrorCode.Conflict, inventedRoot.Error.Code);
        Assert.Equal(before, await ReadFileRunEvidenceAsync(services, saved));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Legacy_Process_file_scope_without_original_launch_or_profile_evidence_refuses_without_rewriting_history(bool prepared) {
        await using var environment = CanDoItAllTestEnvironment.Create("process-file-legacy-scope");
        var profile = environment.CreatePostgreSqlProfile("legacy");
        var harness = Harness(environment, profile);
        Guid runId;
        string original;
        await using (var app = await TestApplication.CreateAsync(harness)) {
            await using var scope = app.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            await using var context = new ProcessPersistenceDbContext(ProcessOptions(services));
            ProcessRuntimeCommitRequest request;
            if (prepared) {
                var saved = await Store(services).PrepareAsync(ProcessPreparedLaunchFixture.Create());
                request = ProcessPreparedLaunchFixture.Commit(saved);
            } else {
                request = ProcessProjectAdmissionFixture.Initial();
            }
            var owner = new EfProcessRuntimeUnitOfWork(context, coordinatedTransaction: Coordinator(services));
            Assert.True((await owner.CommitAsync(request)).Succeeded);
            runId = request.Mutation.State.RunId.Value;
            original = JsonSerializer.Serialize(await context.RuntimeStates.AsNoTracking().SingleAsync(item => item.RunId == runId));
        }
        await using (var restarted = await TestApplication.CreateAsync(harness)) {
            await using var scope = restarted.Services.CreateAsyncScope();
            var error = await Assert.ThrowsAsync<FileBrowserProviderException>(() =>
                scope.ServiceProvider.GetRequiredService<IProcessRunFileScopeProvider>().ResolveAsync(runId).AsTask());
            Assert.Equal(FileBrowserErrorCode.Forbidden, error.Error.Code);
            await using var context = new ProcessPersistenceDbContext(ProcessOptions(scope.ServiceProvider));
            Assert.Equal(original, JsonSerializer.Serialize(await context.RuntimeStates.AsNoTracking().SingleAsync(item => item.RunId == runId)));
        }
    }

    [Fact]
    public async Task Process_file_scope_rejects_a_pointer_to_another_valid_prepared_run_after_restart() {
        await using var environment = CanDoItAllTestEnvironment.Create("process-file-receipt-scope");
        var profile = environment.CreatePostgreSqlProfile("receipts");
        var harness = Harness(environment, profile);
        ProcessPreparedLaunchSnapshot saved;
        string retained;
        await using (var app = await TestApplication.CreateAsync(harness)) {
            await using var scope = app.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            saved = await CreateAcceptedProjectLaunchAsync(services);
            var other = await CreateAcceptedProjectLaunchAsync(services);
            await using var context = new ProcessPersistenceDbContext(ProcessOptions(services));
            var row = await context.RuntimeStates.SingleAsync(item => item.RunId == RunId(saved));
            row.LaunchAdmissionId = other.Preparation.AdmissionId.Value;
            await context.SaveChangesAsync();
            retained = await ReadFileRunEvidenceAsync(services, saved);
        }
        await using (var restarted = await TestApplication.CreateAsync(harness)) {
            await using var scope = restarted.Services.CreateAsyncScope();
            var error = await Assert.ThrowsAsync<FileBrowserProviderException>(() =>
                scope.ServiceProvider.GetRequiredService<IProcessRunFileScopeProvider>().ResolveAsync(RunId(saved)).AsTask());
            Assert.Equal(FileBrowserErrorCode.Forbidden, error.Error.Code);
            Assert.Equal(retained, await ReadFileRunEvidenceAsync(scope.ServiceProvider, saved));
        }
    }

    private static TestHarnessOptions FileScopeHarness(CanDoItAllTestEnvironment environment, TestDatabaseProfile profile,
        FileScopeLauncher launcher) {
        var harness = Harness(environment, profile);
        return new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile, ConfigureServices = services => {
            harness.ConfigureServices?.Invoke(services);
            services.RemoveAll<IDesktopFileLauncher>();
            services.AddSingleton<IDesktopFileLauncher>(launcher);
        } };
    }

    private static Guid RunId(ProcessPreparedLaunchSnapshot saved) => saved.Preparation.InitialCommit.Mutation.State.RunId.Value;
    private static string FileRoot(ProcessPreparedLaunchSnapshot saved) => $"artifacts/process-runs/{RunId(saved):D}";

    private static async Task CancelFileRunAsync(IServiceProvider services, ProcessPreparedLaunchSnapshot saved) {
        await using var context = new ProcessPersistenceDbContext(ProcessOptions(services));
        var coordinator = Coordinator(services);
        var owner = new EfProcessRuntimeUnitOfWork(context, coordinatedTransaction: coordinator,
            projectAdmissionPolicy: ProjectPolicy(services, coordinator));
        var state = Assert.IsType<ProcessRuntimeStateSnapshot>(await owner.LoadAsync(new(RunId(saved))));
        Assert.True((await owner.CommitAsync(ProcessProjectAdmissionFixture.Cancel(state))).Succeeded);
    }

    private static async Task<string> ReadFileRunEvidenceAsync(IServiceProvider services, ProcessPreparedLaunchSnapshot saved) {
        await using var context = new ProcessPersistenceDbContext(ProcessOptions(services));
        Assert.True(context.Database.IsNpgsql());
        var runId = RunId(saved);
        return JsonSerializer.Serialize(new {
            Preparation = await context.PreparedLaunches.AsNoTracking().SingleAsync(item => item.Id == saved.Preparation.AdmissionId.Value),
            Runtime = await context.RuntimeStates.AsNoTracking().SingleAsync(item => item.RunId == runId),
            Assignments = await context.RuntimeStepAssignments.AsNoTracking().Where(item => item.RunId == runId)
                .OrderBy(item => item.StepInstanceId).ToArrayAsync()
        });
    }

    private static async Task AssertFileLifetimeDeniedAsync(IServiceProvider services, ProcessPreparedLaunchSnapshot saved,
        FileToolsSemanticScope oldScope) {
        var files = services.GetRequiredService<IProcessRunFileScopeProvider>();
        var current = await Assert.ThrowsAsync<FileBrowserProviderException>(() => files.ResolveRootAsync(RunId(saved), FileRoot(saved),
            saved.Preparation.Authority!.ProjectAdmission!.ProjectId).AsTask());
        Assert.Equal(FileBrowserErrorCode.Forbidden, current.Error.Code);
        var stale = await Assert.ThrowsAsync<FileBrowserProviderException>(() => services.GetRequiredService<IFileToolsStorageBindingProvider>()
            .ResolveAsync(oldScope).AsTask());
        Assert.Equal(FileBrowserErrorCode.Forbidden, stale.Error.Code);
    }

    private static async Task AssertActualFilePathsAsync(IServiceProvider services, ProcessPreparedLaunchSnapshot saved, FileScopeLauncher launcher) {
        var project = saved.Preparation.Authority!.ProjectAdmission!;
        var owner = services.GetRequiredService<IProcessRunFileScopeProvider>();
        var expected = WorkspaceScopeDescriptor.Organization(project.DatabaseProfileId.ToString("N"))
            .CombineArtifactPath("process-runs", RunId(saved).ToString("D"));
        var binding = await owner.ResolveRootAsync(RunId(saved), FileRoot(saved), project.ProjectId);
        Assert.Equal(expected, binding.Root.Value);
        Assert.Equal(FileToolsHostBrowseCacheMode.Disabled, binding.HostCacheMode);
        var processScope = Assert.Single((await owner.ResolveAsync(RunId(saved))).Scopes);
        var structure = await services.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(project.ProjectId);
        var nodeId = ProjectStructureProcessNodeKeys.BuildProcessRunOutputNodeKey(RunId(saved), FileRoot(saved));
        var node = Assert.Single(structure.Nodes, item => item.Id == nodeId);
        var structureScope = await services.GetRequiredService<IProjectStructureNodeFileScopeProvider>()
            .ResolveNodeCollectionAsync(project.ProjectId, node.Id);
        foreach (var scope in new[] { processScope, structureScope }) {
            var sourceBinding = Assert.Single(await services.GetRequiredService<IFileToolsStorageBindingProvider>().ResolveAsync(scope));
            Assert.Equal(binding.Root, sourceBinding.Root);
            var session = await services.GetRequiredService<IFileToolsBrowseSessionFactory>().CreateAsync(scope);
            var provider = Assert.Single(session.Providers);
            var root = await provider.GetRootAsync(FileBrowserMetadataRequest.Standard);
            var page = await provider.BrowseAsync(new FileBrowserBrowseRequest(root.Key, pageSize: 50,
                sort: new FileBrowserSortDescriptor(FileBrowserSortField.ProviderNative, FoldersFirst: false)));
            var item = Assert.Single(page.Items);
            Assert.Equal("proof.md", item.Name);
            await using var download = await services.GetRequiredService<IFileToolsBrowseItemActionService>().AuthorizeDownloadAsync(scope, item.Key);
            await using var content = await download.OpenReadAsync();
            using var reader = new StreamReader(content.Stream);
            Assert.Equal("Original Process output", await reader.ReadToEndAsync());
        }
        var opener = services.GetRequiredService<IProjectStructureLocalFileOpener>();
        Assert.True(opener.CanOpen(node));
        var opened = await opener.OpenAsync(node);
        Assert.True(opened.IsSuccess, opened.Message);
        var workspace = services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();
        Assert.Equal(Path.GetFullPath(Path.Combine(workspace, expected.Replace('/', Path.DirectorySeparatorChar))), launcher.LastPath);
    }

    private sealed class FileScopeLauncher : IDesktopFileLauncher {
        public bool IsAvailable => true;
        public int Calls { get; private set; }
        public string? LastPath { get; private set; }
        public ValueTask<DesktopFileLaunchResult> LaunchAsync(DesktopFileLaunchRequest request, CancellationToken cancellationToken = default) {
            Calls++;
            LastPath = request.TargetPath;
            return ValueTask.FromResult(DesktopFileLaunchResult.Success(request.TargetPath));
        }
    }
}
