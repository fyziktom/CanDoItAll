using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.AgentFramework.Workflows.Runtime;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class WorkflowFileProviderDisclosureIntegrationTests {
    [Theory]
    [InlineData(false, FileReadKind.WorkspaceText, FileReadChange.None, false)]
    [InlineData(false, FileReadKind.SourceIngestion, FileReadChange.None, false)]
    [InlineData(true, FileReadKind.WorkspaceText, FileReadChange.None, false)]
    [InlineData(true, FileReadKind.SourceIngestion, FileReadChange.None, false)]
    [InlineData(false, FileReadKind.WorkspaceText, FileReadChange.SourceRevoked, false)]
    [InlineData(true, FileReadKind.SourceIngestion, FileReadChange.SourceRevoked, false)]
    [InlineData(true, FileReadKind.WorkspaceText, FileReadChange.WorkspaceRebound, false)]
    [InlineData(true, FileReadKind.SourceIngestion, FileReadChange.WorkspaceRebound, false)]
    [InlineData(true, FileReadKind.SourceIngestion, FileReadChange.ContentReplaced, false)]
    [InlineData(true, FileReadKind.WorkspaceText, FileReadChange.ReparseReplacement, false)]
    [InlineData(true, FileReadKind.AbsoluteSource, FileReadChange.None, false)]
    [InlineData(true, FileReadKind.AliasedSource, FileReadChange.RegistryReplaced, false)]
    [InlineData(true, FileReadKind.WorkspaceText, FileReadChange.None, true)]
    [InlineData(true, FileReadKind.SourceIngestion, FileReadChange.None, true)]
    [InlineData(true, FileReadKind.WorkspaceList, FileReadChange.None, false)]
    [InlineData(true, FileReadKind.WorkspaceList, FileReadChange.OriginalRemoved, false)]
    [InlineData(true, FileReadKind.WorkspaceList, FileReadChange.ReparseReplacement, false)]
    [InlineData(true, FileReadKind.WorkspaceDirectory, FileReadChange.None, false)]
    [InlineData(true, FileReadKind.WorkspaceDirectory, FileReadChange.OriginalRemoved, false)]
    [InlineData(true, FileReadKind.WorkspaceDirectory, FileReadChange.ReparseReplacement, false)]
    [InlineData(true, FileReadKind.WorkspaceTree, FileReadChange.None, false)]
    [InlineData(true, FileReadKind.WorkspaceTree, FileReadChange.OriginalRemoved, false)]
    [InlineData(true, FileReadKind.WorkspaceTree, FileReadChange.ReparseReplacement, false)]
    [InlineData(true, FileReadKind.WorkspaceSearch, FileReadChange.None, false)]
    [InlineData(true, FileReadKind.WorkspaceSearch, FileReadChange.OriginalRemoved, false)]
    [InlineData(true, FileReadKind.WorkspaceSearch, FileReadChange.ReparseReplacement, false)]
    [InlineData(true, FileReadKind.WorkspaceListShorthand, FileReadChange.None, false)]
    [InlineData(true, FileReadKind.WorkspaceListShorthand, FileReadChange.OriginalRemoved, false)]
    [InlineData(true, FileReadKind.WorkspaceListShorthand, FileReadChange.ReparseReplacement, false)]
    [InlineData(true, FileReadKind.WorkspaceTreeShorthand, FileReadChange.None, false)]
    [InlineData(true, FileReadKind.WorkspaceTreeShorthand, FileReadChange.OriginalRemoved, false)]
    [InlineData(true, FileReadKind.WorkspaceTreeShorthand, FileReadChange.ReparseReplacement, false)]
    public async Task PostgreSql_ActualFileReadWaitAndRestartRetainOriginalProviderDisclosureAuthority(bool restart,
        FileReadKind kind, FileReadChange change, bool legacy) {
        await using var environment = CanDoItAllTestEnvironment.Create("workflow-file-disclosure");
        var profile = environment.CreatePostgreSqlProfile("primary");
        var port = new FileDisclosurePort();
        var pause = new FileDisclosurePause();
        var api = new FileApiOptions();
        IExternalTargetPathRegistry registry = new ExternalTargetPathRegistry();
        string? replacementWorkspace = null;
        string originalFile;
        string originalRoot;
        string originalSelection;
        string originalSettings;
        string? decoyFile = null;
        WorkflowRunSnapshot started;
        WorkflowExternalRequestRecord? waiting = null;
        WorkflowCompletedNodeRead? savedRead = null;
        void Configure(IServiceCollection services) {
            foreach (var descriptor in services.Where(item => item.ImplementationType == typeof(ProjectStructureWorkflowDeliveryWorker)).ToArray()) {
                services.Remove(descriptor);
            }
            services.RemoveAll<IOptionsMonitor<ApiAccessOptions>>();
            services.AddSingleton<IOptionsMonitor<ApiAccessOptions>>(api);
            services.RemoveAll<ILlmInvocationPort>();
            services.AddSingleton<ILlmInvocationPort>(port);
            services.RemoveAll<IProviderRuntimeProfileSource>();
            services.AddSingleton<IProviderRuntimeProfileSource>(new FileDisclosureProvider());
            services.RemoveAll<IExternalTargetPathRegistry>();
            services.AddSingleton(registry);
            if (replacementWorkspace is { } replacedRoot) {
                services.RemoveAll<IWorkspaceFileService>();
                services.AddScoped<IWorkspaceFileService>(provider => new WorkspaceFileService(replacedRoot,
                    provider.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>(), Organization(provider), registry));
                services.RemoveAll<IWorkspacePathResolutionService>();
                services.AddScoped<IWorkspacePathResolutionService>(provider => new WorkspacePathResolutionService(replacedRoot,
                    provider.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>(), Organization(provider), registry));
            }
            services.RemoveAll<IWorkflowProviderInputAdmission>();
            services.AddScoped<IWorkflowProviderInputAdmission>(provider => new FileDisclosureGate(new WorkflowProviderInputAdmission(
                provider.GetRequiredService<IWorkflowRunStore>(), provider.GetRequiredService<IWorkflowExecutorCatalog>(),
                provider.GetServices<IWorkflowProviderDisclosurePolicy>()), pause));
        }
        await using (var first = await TestApplication.CreateAsync(new() { TestEnvironment = environment, ActiveProfile = profile, ConfigureServices = Configure })) {
            await using var scope = first.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var files = services.GetRequiredService<IWorkspaceFileService>();
            originalRoot = files.ExecutionScope.WorkspaceRoot;
            Assert.True(files.ExecutionScope.SharesIdentityWith(services.GetRequiredService<IWorkspacePathResolutionService>().ExecutionScope));
            Assert.Equal(Organization(services), files.ExecutionScope.Scope);
            string selectedPath;
            if (kind is FileReadKind.AbsoluteSource or FileReadKind.AliasedSource) {
                var external = Path.Combine(environment.RootPath, "operator-selected-source");
                Directory.CreateDirectory(external);
                originalFile = Path.Combine(external, "original.txt");
                await File.WriteAllTextAsync(originalFile, Content);
                if (kind == FileReadKind.AliasedSource) {
                    Assert.True(registry.TryCreateAlias(originalFile, out var alias));
                    selectedPath = alias;
                } else {
                    selectedPath = originalFile;
                }
            } else if (IsCollectionRead(kind)) {
                Assert.True(files.CreateDirectory("sources").Succeeded);
                var sourceDirectory = services.GetRequiredService<IWorkspacePathResolutionService>().ResolveDirectoryPath("sources", false).FullPath;
                originalFile = Path.Combine(sourceDirectory, OperatingSystem.IsWindows() ? "literal-name.txt" : "literal\\name.txt");
                await File.WriteAllTextAsync(originalFile, Content);
                Directory.CreateDirectory(Path.Combine(sourceDirectory, "literal"));
                decoyFile = Path.Combine(sourceDirectory, "literal", "name.txt");
                await File.WriteAllTextAsync(decoyFile, "fixture-file-decoy");
                await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "ignored.txt"), "ignored");
                selectedPath = kind switch {
                    FileReadKind.WorkspaceListShorthand => "sources/**",
                    FileReadKind.WorkspaceTreeShorthand => "sources/**/*.txt",
                    _ => "sources"
                };
            } else {
                Assert.True(files.CreateDirectory("sources").Succeeded);
                Assert.True(files.WriteTextFile("sources/original.txt", Content, true).Succeeded);
                originalFile = services.GetRequiredService<IWorkspacePathResolutionService>().ResolveFilePath("sources/original.txt", false).FullPath;
                selectedPath = "sources/original.txt";
            }
            originalSelection = selectedPath;
            var (definition, component) = await CreateDefinitionAsync(services, kind, selectedPath, restart);
            var readNode = Assert.Single(definition.Graph.Nodes, item => item.Id.Value == "read");
            originalSettings = readNode.Settings.ExecutorSettingsJson!;
            if (IsWorkspaceRead(kind)) {
                Assert.Equal(selectedPath, WorkflowExecutorJson.Deserialize<WorkflowStorageFileExecutorSettings>(originalSettings).Path);
            }
            var origin = new WorkflowLaunchOrigin.Api(new(WorkflowLaunchActorKind.User, "file-operator"), new("file-disclosure")) {
                AuthorizationScope = Organization(services), AuthorizationPolicyFingerprint = WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint
            };
            var payload = IsWorkspaceRead(kind) ? "{}" : JsonSerializer.Serialize(new { sources = new[] {
                new { key = "original", label = "Selected original file", kind = "filePath", value = selectedPath, isEnabled = true }
            } });
            var startRequest = new WorkflowRunStartRequest(definition.Id, definition.VersionId, payload,
                WorkflowRuntimeBackendKind.InProcess, null, null) { Origin = origin };
            var pending = legacy ? StartLegacyAsync(services, definition, component, startRequest) :
                services.GetRequiredService<IWorkflowRuntimeManager>().StartAsync(definition, startRequest);
            if (!restart) {
                try {
                    await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(45));
                    await ApplyChangeAsync(change, api, originalFile, decoyFile);
                } finally {
                    pause.Release.TrySetResult();
                }
            }
            started = await pending.WaitAsync(TimeSpan.FromSeconds(45));
            var store = services.GetRequiredService<IWorkflowRunStore>();
            var history = await store.ReadProviderDisclosureAsync(started.RunId);
            if (legacy) {
                Assert.Null(history.Declaration);
                Assert.Empty(history.Completions);
            } else {
                savedRead = Assert.Single(history.Completions, item => item.Proof.NodeId.Value == "read");
                Assert.Equal(WorkflowWorkspaceProviderReadEvidence.Owner, savedRead.Evidence[0].Owner);
                Assert.Equal(WorkflowProviderDisclosureContent.Settings(readNode), savedRead.Proof.SettingsHash);
                using var header = JsonDocument.Parse(savedRead.Evidence[0].PayloadJson);
                Assert.Equal(originalRoot, header.RootElement.GetProperty("scope").GetProperty("workspaceRoot").GetString());
                Assert.Contains(savedRead.Evidence.Skip(1), item => {
                    using var target = JsonDocument.Parse(item.PayloadJson);
                    return target.RootElement.GetProperty("target").GetProperty("fullPath").GetString() == originalFile;
                });
                Assert.All(savedRead.Evidence, item => Assert.DoesNotContain(Content, item.PayloadJson, StringComparison.Ordinal));
            }
            var publicEvents = await store.ListEventsAsync(started.RunId);
            Assert.DoesNotContain(publicEvents, item => item.Kind == WorkflowEventKind.ProviderReadEvidence);
            Assert.Contains(publicEvents, item => item.NodeId?.Value == "read" && item.Kind == WorkflowEventKind.ExecutorCompleted);
            if (!IsCollectionRead(kind) || kind == FileReadKind.WorkspaceSearch) {
                Assert.Contains(publicEvents, item => item.NodeId?.Value == "read" && item.PayloadJson.Contains(Content, StringComparison.Ordinal));
            } else {
                Assert.Contains(publicEvents, item => item.NodeId?.Value == "read" && item.PayloadJson.Contains("name.txt", StringComparison.Ordinal));
            }
            Assert.DoesNotContain(WorkflowWorkspaceProviderReadEvidence.Owner.Value, JsonSerializer.Serialize(publicEvents), StringComparison.Ordinal);
            if (!restart) {
                AssertFinished(change, legacy, port, pause, started.State == WorkflowRunState.Completed);
                Assert.True(File.Exists(originalFile));
                return;
            }
            Assert.Equal(WorkflowRunState.WaitingForInput, started.State);
            Assert.Equal(0, port.Calls);
            waiting = Assert.Single(await store.ListPendingExternalRequestsAsync(started.RunId), item => item.EffectiveState == WorkflowExternalRequestState.Pending);
            Assert.Equal(WorkflowExternalRequestKind.HumanInput, waiting.Kind);
        }
        if (change == FileReadChange.WorkspaceRebound) {
            replacementWorkspace = Path.Combine(environment.RootPath, "replacement-workspace");
            Directory.CreateDirectory(Path.Combine(replacementWorkspace, "sources"));
            await File.WriteAllTextAsync(Path.Combine(replacementWorkspace, "sources", "original.txt"), Content);
        }
        if (change == FileReadChange.RegistryReplaced) {
            registry = new ExternalTargetPathRegistry();
            Assert.NotEqual(ExternalTargetAliasResolutionKind.Resolved, registry.TryResolve(originalSelection, out _, out _));
        }
        await using var second = await TestApplication.CreateAsync(new() { TestEnvironment = environment, ActiveProfile = profile, ConfigureServices = Configure });
        await using var resumedScope = second.Services.CreateAsyncScope();
        var resumedServices = resumedScope.ServiceProvider;
        var resumedStore = resumedServices.GetRequiredService<IWorkflowRunStore>();
        if (!legacy) {
            var reopened = Assert.Single((await resumedStore.ReadProviderDisclosureAsync(started.RunId)).Completions,
                item => item.Proof.NodeId.Value == "read");
            Assert.Equal(savedRead!.Proof, reopened.Proof);
            var originalDefinition = Assert.IsType<WorkflowDefinitionDetail>(await resumedServices.GetRequiredService<IWorkflowCatalogService>()
                .GetDefinitionAsync(started.WorkflowId, started.VersionId));
            var originalNode = Assert.Single(originalDefinition.Definition.Graph.Nodes, item => item.Id.Value == "read");
            Assert.Equal(originalSettings, originalNode.Settings.ExecutorSettingsJson);
            Assert.Equal(reopened.Proof.SettingsHash, WorkflowProviderDisclosureContent.Settings(originalNode));
            Assert.Equal(savedRead.Manifest, reopened.Manifest);
            Assert.Equal(savedRead.Evidence.Select(item => item.PayloadJson), reopened.Evidence.Select(item => item.PayloadJson));
        }
        using var answer = JsonDocument.Parse("{\"reviewed\":true}");
        var resumed = resumedServices.GetRequiredService<IWorkflowExternalResponseService>().SubmitAsync(new(
            resumedServices.GetRequiredService<IWorkflowExternalResponseActorContextFactory>().CreateLocalOperator(), waiting!.Id, waiting.Version,
            answer.RootElement, new("file-after-restart"), new("file-after-restart")));
        try {
            await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(45));
            await ApplyChangeAsync(change, api, originalFile, decoyFile);
        } finally {
            pause.Release.TrySetResult();
        }
        try {
            var result = await resumed.WaitAsync(TimeSpan.FromSeconds(45));
            AssertFinished(change, legacy, port, pause, result.Outcome == WorkflowExternalResponseServiceOutcome.Completed);
            if (!legacy) {
                var retained = Assert.Single((await resumedStore.ReadProviderDisclosureAsync(started.RunId)).Completions,
                    item => item.Proof.NodeId.Value == "read");
                Assert.Equal(savedRead!.Proof.CompletionId, retained.Proof.CompletionId);
                Assert.Equal(savedRead.Evidence.Select(item => item.PayloadJson), retained.Evidence.Select(item => item.PayloadJson));
            }
            Assert.DoesNotContain(await resumedStore.ListEventsAsync(started.RunId), item => item.Kind == WorkflowEventKind.ProviderReadEvidence);
        } finally {
            if (change == FileReadChange.ReparseReplacement && File.GetAttributes(originalFile).HasFlag(FileAttributes.ReparsePoint)) {
                File.Delete(originalFile);
            }
        }
    }

    private static void AssertFinished(FileReadChange change, bool legacy, FileDisclosurePort port, FileDisclosurePause pause, bool completed) {
        var allowed = !legacy && change is FileReadChange.None or FileReadChange.RegistryReplaced;
        Assert.Equal(allowed ? 1 : 0, port.Calls);
        Assert.Equal(allowed, completed);
        if (allowed) {
            Assert.Null(pause.Failure);
        } else {
            Assert.NotNull(pause.Failure);
            Assert.Contains(legacy ? "retained legacy Workflow" : change switch {
                FileReadChange.SourceRevoked => "original live source",
                FileReadChange.WorkspaceRebound => "original workspace",
                FileReadChange.ContentReplaced => "changed",
                FileReadChange.ReparseReplacement => "reparse",
                FileReadChange.OriginalRemoved => "does not exist",
                _ => throw new ArgumentOutOfRangeException(nameof(change))
            }, pause.Failure.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static async Task ApplyChangeAsync(FileReadChange change, FileApiOptions options, string originalFile, string? decoyFile) {
        switch (change) {
            case FileReadChange.SourceRevoked:
                options.CurrentValue.Enabled = false;
                break;
            case FileReadChange.ContentReplaced:
                var stamp = File.GetLastWriteTimeUtc(originalFile);
                var length = new FileInfo(originalFile).Length;
                await File.WriteAllTextAsync(originalFile, "fixture-file-replace");
                File.SetLastWriteTimeUtc(originalFile, stamp);
                Assert.Equal(length, new FileInfo(originalFile).Length);
                Assert.Equal(stamp, File.GetLastWriteTimeUtc(originalFile));
                break;
            case FileReadChange.ReparseReplacement:
                var replacement = Path.Combine(Path.GetDirectoryName(originalFile)!, "replacement.txt");
                await File.WriteAllTextAsync(replacement, "replacement data");
                File.Delete(originalFile);
                File.CreateSymbolicLink(originalFile, replacement);
                break;
            case FileReadChange.OriginalRemoved:
                File.Delete(originalFile);
                break;
        }
        if (decoyFile is not null) {
            Assert.True(File.Exists(decoyFile));
        }
    }

    private static async Task<(WorkflowDefinition Definition, LlmCallComponent Component)> CreateDefinitionAsync(IServiceProvider services,
        FileReadKind kind, string selectedPath, bool wait) {
        var shape = new WorkflowValueShape(WorkflowValueShapeKind.Object, "{}", "Original file data");
        var component = await services.GetRequiredService<IWorkflowComponentLibraryService>().SaveComponentAsync(new(null,
            "File summary", null, "file-disclosure", WorkflowModality.Text, new(0, 100, false, ""), "Summarize the selected file.",
            shape, shape, AgentPermissionsPolicy.Default));
        var start = new WorkflowNode(new("start"), WorkflowNodeKind.Start, "Start", [], new(null, null, null, null, "", shape, shape));
        var read = new WorkflowNode(new("read"), WorkflowNodeKind.Executor, "Read selected file", [], new(null, null, null, null, "", shape, shape) {
            ExecutorId = IsWorkspaceRead(kind) ? WorkflowExecutorIds.StorageFile : WorkflowExecutorIds.SourceIngestion,
            ExecutorSettingsJson = IsWorkspaceRead(kind) ? WorkflowExecutorJson.Serialize(new WorkflowStorageFileExecutorSettings {
                Operation = kind switch {
                    FileReadKind.WorkspaceList or FileReadKind.WorkspaceListShorthand => WorkflowStorageFileOperation.List,
                    FileReadKind.WorkspaceDirectory => WorkflowStorageFileOperation.ListDirectory,
                    FileReadKind.WorkspaceTree or FileReadKind.WorkspaceTreeShorthand => WorkflowStorageFileOperation.Tree,
                    FileReadKind.WorkspaceSearch => WorkflowStorageFileOperation.SearchText,
                    _ => WorkflowStorageFileOperation.ReadText
                },
                Path = selectedPath, Query = "fixture", IncludeGlobs = IsCollectionRead(kind) ? ["**/*name.txt"] : []
            }) : WorkflowExecutorJson.Serialize(new WorkflowSourceIngestionExecutorSettings {
                AllowedExtensions = [".txt"], AllowAbsoluteInputPaths = kind == FileReadKind.AbsoluteSource
            })
        });
        var review = new WorkflowNode(new("review"), WorkflowNodeKind.HumanInput, "Review file read", [],
            new(null, null, null, WorkflowExternalRequestKind.HumanInput, "Continue the original file read.", shape, shape));
        var llm = new WorkflowNode(new("provider"), WorkflowNodeKind.LlmCall, "Provider", [], new(component.Id, null, null, null, "", shape, shape));
        var end = new WorkflowNode(new("end"), WorkflowNodeKind.End, "End", [], new(null, null, null, null, "", shape, shape));
        var nodes = wait ? new[] { start, read, review, llm, end } : new[] { start, read, llm, end };
        var edges = nodes.Zip(nodes.Skip(1), (left, right) => new WorkflowEdge(new($"{left.Id.Value}-{right.Id.Value}"), left.Id, null,
            right.Id, null, WorkflowEdgeKind.Direct, "")).ToArray();
        var definition = await services.GetRequiredService<IWorkflowCatalogService>().SaveDefinitionAsync(new(null, null,
            "Native file disclosure", "Original selected source", WorkflowLifecycleStatus.Draft, new(start.Id, nodes, edges),
            new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false)));
        return (definition, component);
    }

    private static async Task<WorkflowRunSnapshot> StartLegacyAsync(IServiceProvider services, WorkflowDefinition definition,
        LlmCallComponent component, WorkflowRunStartRequest request) {
        var runId = WorkflowRunId.New();
        var store = services.GetRequiredService<IWorkflowRunStore>();
        var now = DateTimeOffset.UtcNow;
        await store.CreateRunWithStartedEventAsync(new(runId, definition.Id, definition.VersionId, WorkflowRunState.Running,
            WorkflowRuntimeBackendKind.InProcess, runId.ToString(), "Original v1 file run", now, now) { Origin = request.Origin },
            new(Guid.NewGuid(), runId, WorkflowEventKind.Started, null, "Original v1 producer", "{}", now));
        var compiler = services.GetRequiredService<IWorkflowMafCompiler>();
        var build = compiler.Compile(definition, [component], WorkflowPreviewSimulationPlan.Empty,
            WorkflowExecutorInvocationContext.Empty with { CompilerContractVersion = WorkflowProviderDisclosureProtocol.Legacy });
        Assert.True(build.Compilation.Succeeded, build.Compilation.ErrorMessage);
        var clock = TimeProvider.System;
        var payloads = services.GetRequiredService<IWorkflowBackendCheckpointPayloadStore>();
        var mapper = new MafWorkflowTurnResultMapper(payloads, new MafWorkflowExternalRequestMapper(clock), new MafWorkflowEventNormalizer(),
            new WorkflowCheckpointFactory(), new WorkflowPayloadPolicyService(), clock);
        var driver = new MafWorkflowNativeStartDriver(payloads, new MafWorkflowStreamingRunDriver(), mapper, clock);
        var started = await driver.StartAsync(definition, request, runId, build, CancellationToken.None);
        await store.SaveRunAsync(started.Run);
        foreach (var item in started.Events) {
            Assert.Null(item.CompletionProof);
            await store.SaveEventAsync(item);
        }
        foreach (var item in started.ExternalRequests) {
            await store.SaveExternalRequestAsync(item);
            Assert.True(WorkflowExternalRequestBoundaryRecord.TryCreate(item, out var boundary));
            Assert.True((await services.GetRequiredService<IWorkflowExternalRequestBoundaryStore>().UpsertAsync(boundary!)).Succeeded);
        }
        foreach (var item in started.Checkpoints) {
            await store.SaveCheckpointAsync(item);
        }
        return started.Run;
    }

    private static WorkspaceScopeDescriptor Organization(IServiceProvider services)
        => WorkspaceScopeDescriptor.Organization(services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile.Id.ToString("N"));

    private const string Content = "fixture-file-content";
    private static bool IsWorkspaceRead(FileReadKind kind) => kind == FileReadKind.WorkspaceText || IsCollectionRead(kind);
    private static bool IsCollectionRead(FileReadKind kind) => kind is FileReadKind.WorkspaceList or FileReadKind.WorkspaceDirectory or
        FileReadKind.WorkspaceTree or FileReadKind.WorkspaceSearch or FileReadKind.WorkspaceListShorthand or FileReadKind.WorkspaceTreeShorthand;
    public enum FileReadKind { WorkspaceText, SourceIngestion, AbsoluteSource, AliasedSource, WorkspaceList, WorkspaceDirectory, WorkspaceTree, WorkspaceSearch, WorkspaceListShorthand, WorkspaceTreeShorthand }
    public enum FileReadChange { None, SourceRevoked, WorkspaceRebound, ContentReplaced, ReparseReplacement, RegistryReplaced, OriginalRemoved }

    private sealed class FileDisclosurePause {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Exception? Failure { get; set; }
    }

    private sealed class FileDisclosureGate(IWorkflowProviderInputAdmission inner, FileDisclosurePause pause) : IWorkflowProviderInputAdmission {
        public async ValueTask RequireAsync(WorkflowDefinition definition, WorkflowNode node, WorkflowNodeInput input, CancellationToken cancellationToken = default) {
            pause.Entered.TrySetResult();
            await pause.Release.Task.WaitAsync(TimeSpan.FromSeconds(45), cancellationToken);
            try {
                await inner.RequireAsync(definition, node, input, cancellationToken);
            } catch (Exception exception) {
                pause.Failure = exception;
                throw;
            }
        }
    }

    private sealed class FileDisclosurePort : ILlmInvocationPort {
        public int Calls { get; private set; }
        public Task<LlmInvocationResult> InvokeAsync(LlmInvocationRequest request, CancellationToken cancellationToken = default) {
            Calls++;
            return Task.FromResult(new LlmInvocationResult(request.Model, "File summary", new(1, 1, 0)));
        }
    }

    private sealed class FileDisclosureProvider : IProviderRuntimeProfileSource {
        private readonly ProviderProfile provider = new(Guid.NewGuid(), "File disclosure synthetic provider", ProviderKind.OpenAi,
            "https://example.invalid/v1", "FILE_DISCLOSURE_TEST_KEY", "file-disclosure", ProviderTransportKind.ChatCompletions,
            true, false, false, true, false, "{}", "", "Not checked", null, ["file-disclosure"]);
        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProviderProfile>>([provider]);
        public Task<ProviderProfile?> GetProviderAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<ProviderProfile?>(id == provider.Id ? provider : null);
    }

    private sealed class FileApiOptions : IOptionsMonitor<ApiAccessOptions> {
        public ApiAccessOptions CurrentValue { get; } = new() { Enabled = true };
        public ApiAccessOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<ApiAccessOptions, string?> listener) => null;
    }
}
