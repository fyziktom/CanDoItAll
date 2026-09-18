using System.Collections.Immutable;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed partial class WorkspaceToolResultDisclosureTests {
    [Theory]
    [InlineData(ToolContractCatalog.WorkspaceListDirectory, CollectionChange.None)]
    [InlineData(ToolContractCatalog.WorkspaceListDirectory, CollectionChange.Remove)]
    [InlineData(ToolContractCatalog.WorkspaceListDirectory, CollectionChange.Reparse)]
    [InlineData(ToolContractCatalog.WorkspaceListFiles, CollectionChange.None)]
    [InlineData(ToolContractCatalog.WorkspaceListFiles, CollectionChange.Remove)]
    [InlineData(ToolContractCatalog.WorkspaceListFiles, CollectionChange.Reparse)]
    [InlineData(ToolContractCatalog.WorkspaceSearch, CollectionChange.None)]
    [InlineData(ToolContractCatalog.WorkspaceSearch, CollectionChange.Remove)]
    [InlineData(ToolContractCatalog.WorkspaceSearch, CollectionChange.Reparse)]
    public async Task Registered_collection_functions_retain_exact_native_targets_across_SDK_marshalling(string toolName, CollectionChange change) {
        await using var fixture = await CollectionFixture.CreateAsync();
        var original = fixture.Write(Path.Combine("sources", "nested", NativeCollectionName), "original selected needle");
        var decoy = fixture.Write(Path.Combine("sources", "nested", "literal", "name.txt"), "decoy");
        var run = await fixture.InvokeAsync(toolName, "sources/nested");
        var evidence = WorkspaceToolResultEvidence.Read(run.Evidence);
        Assert.Equal(2, run.Evidence.Version);
        Assert.Equal(WorkspaceToolResultEvidenceState.Complete, evidence.State);
        Assert.Contains(original, evidence.Selection!.FullPaths);
        Assert.DoesNotContain("readSelection", run.Result.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(fixture.Root, run.Result.GetRawText(), StringComparison.Ordinal);
        if (!OperatingSystem.IsWindows()) {
            Assert.Contains("sources/nested/literal/name.txt", run.Result.GetRawText(), StringComparison.Ordinal);
            Assert.Contains('\\', Path.GetFileName(original));
        }
        await using (var allowed = await run.AuthorizeAsync()) {
            Assert.NotNull(allowed);
        }
        if (change == CollectionChange.None) {
            return;
        }
        File.Delete(original);
        if (change == CollectionChange.Reparse) {
            File.CreateSymbolicLink(original, decoy);
        }
        try {
            Assert.True(File.Exists(decoy));
            var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => run.AuthorizeAsync().AsTask());
            Assert.Equal("workspace.result-disclosure-denied", denied.Code);
        } finally {
            if (change == CollectionChange.Reparse) {
                File.Delete(original);
            }
        }
    }

    [Theory]
    [InlineData(ToolContractCatalog.WorkspaceListDirectory)]
    [InlineData(ToolContractCatalog.WorkspaceListFiles)]
    [InlineData(ToolContractCatalog.WorkspaceSearch)]
    public async Task Empty_success_and_missing_collection_results_retain_distinct_original_root_requirements(string toolName) {
        await using var fixture = await CollectionFixture.CreateAsync();
        Directory.CreateDirectory(Path.Combine(fixture.Root, "empty"));
        var empty = await fixture.InvokeAsync(toolName, "empty");
        var emptyEvidence = WorkspaceToolResultEvidence.Read(empty.Evidence);
        Assert.True(empty.Result.GetProperty("succeeded").GetBoolean());
        Assert.True(emptyEvidence.Selection!.RequiresExistingRoot);
        Assert.Empty(emptyEvidence.Selection.FullPaths);
        await using (var allowed = await empty.AuthorizeAsync()) {
            Assert.NotNull(allowed);
        }
        Directory.Delete(Path.Combine(fixture.Root, "empty"));
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => empty.AuthorizeAsync().AsTask());
        var missing = await fixture.InvokeAsync(toolName, "missing");
        Assert.False(missing.Result.GetProperty("succeeded").GetBoolean());
        Assert.False(WorkspaceToolResultEvidence.Read(missing.Evidence).Selection!.RequiresExistingRoot);
        await using var missingAllowed = await missing.AuthorizeAsync();
        Assert.NotNull(missingAllowed);
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "missing")));
    }

    [Fact]
    public async Task Incomplete_success_from_actual_query_owner_retains_its_exact_root_before_marshalling() {
        await using var fixture = await CollectionFixture.CreateAsync();
        var original = fixture.Write(NativeCollectionName, "original needle");
        var opened = new List<string>();
        var queries = new WorkspaceFileQueryService(TestWorkspaceServices.CreatePathPolicy(fixture.Root),
            new WorkspaceFileReceiptWriter(fixture.Root), new WorkspaceTextContentGuard(path => {
                opened.Add(path);
                throw new IOException("Private stream failure");
            }));
        var tool = WorkspaceToolResultDisclosure.CreateCollectionTool(queries.SearchText, ToolContractCatalog.WorkspaceSearch, "Search");
        var run = await fixture.InvokeOwnedAsync(tool, new() { ["relativePath"] = original, ["query"] = "needle" });
        Assert.Equal(original, Assert.Single(opened));
        Assert.True(run.Result.GetProperty("succeeded").GetBoolean());
        Assert.True(run.Result.GetProperty("isTruncated").GetBoolean());
        Assert.Empty(run.Result.GetProperty("matches").EnumerateArray());
        Assert.Contains("search is incomplete", run.Result.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Private stream failure", run.Result.GetRawText(), StringComparison.Ordinal);
        var evidence = WorkspaceToolResultEvidence.Read(run.Evidence);
        Assert.True(evidence.Selection!.RequiresExistingRoot);
        Assert.Equal(original, evidence.Selection.Root.FullPath);
        Assert.Empty(evidence.Selection.FullPaths);
        await using (var allowed = await run.AuthorizeAsync()) {
            Assert.NotNull(allowed);
        }
        File.Delete(original);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => run.AuthorizeAsync().AsTask());
        Assert.Single(opened);
    }

    [Fact]
    public async Task Concurrent_registered_invocations_never_share_a_selected_result_capture() {
        await using var fixture = await CollectionFixture.CreateAsync();
        var alpha = fixture.Write(Path.Combine("alpha", NativeCollectionName), "original alpha");
        var beta = fixture.Write(Path.Combine("beta", NativeCollectionName), "original beta");
        var both = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrived = 0;
        fixture.Source.BeforeCompletion = async () => {
            if (Interlocked.Increment(ref arrived) == 2) {
                both.TrySetResult();
            }
            await both.Task.WaitAsync(TimeSpan.FromSeconds(10));
        };
        var calls = await Task.WhenAll(fixture.InvokeAsync(ToolContractCatalog.WorkspaceSearch, "alpha"),
            fixture.InvokeAsync(ToolContractCatalog.WorkspaceSearch, "beta"));
        Assert.Equal(alpha, Assert.Single(WorkspaceToolResultEvidence.Read(calls[0].Evidence).Selection!.FullPaths));
        Assert.Equal(beta, Assert.Single(WorkspaceToolResultEvidence.Read(calls[1].Evidence).Selection!.FullPaths));
        Assert.Contains("original alpha", calls[0].Result.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("original beta", calls[0].Result.GetRawText(), StringComparison.Ordinal);
        Assert.Contains("original beta", calls[1].Result.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("original alpha", calls[1].Result.GetRawText(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CollectionFault.Missing)]
    [InlineData(CollectionFault.Count)]
    [InlineData(CollectionFault.Root)]
    [InlineData(CollectionFault.Origin)]
    [InlineData(CollectionFault.OutsideRoot)]
    public async Task Typed_results_without_consistent_owner_selection_cannot_attest_a_collection(CollectionFault fault) {
        await using var fixture = await CollectionFixture.CreateAsync();
        fixture.Write(Path.Combine("selected", "original.txt"), "original");
        var outside = fixture.Write("outside.txt", "unselected");
        var actual = TestWorkspaceServices.CreateFileService(fixture.Root).ListFiles("selected");
        var selection = actual.ReadSelection!;
        var malformed = actual with { ReadSelection = fault switch {
            CollectionFault.Missing => null,
            CollectionFault.Count => new(selection.GetRootPath(), true, []),
            CollectionFault.Root => new(fixture.Root, true, selection.GetSelectedPaths()),
            CollectionFault.Origin => new(selection.GetRootPath(), false, selection.GetSelectedPaths()),
            CollectionFault.OutsideRoot => new(selection.GetRootPath(), true, [outside]),
            _ => throw new ArgumentOutOfRangeException(nameof(fault))
        } };
        var tool = WorkspaceToolResultDisclosure.CreateCollectionTool(() => malformed, ToolContractCatalog.WorkspaceListFiles, "Malformed owner read");
        var run = await fixture.InvokeOwnedAsync(tool, new() { ["relativePath"] = "selected" });
        Assert.NotEqual(WorkspaceToolResultEvidenceState.Complete, WorkspaceToolResultEvidence.Read(run.Evidence).State);
        var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => run.AuthorizeAsync().AsTask());
        Assert.Equal("workspace.result-authority-unavailable", denied.Code);
    }

    [Theory]
    [InlineData(ToolContractCatalog.WorkspaceListDirectory)]
    [InlineData(ToolContractCatalog.WorkspaceListFiles)]
    [InlineData(ToolContractCatalog.WorkspaceSearch)]
    public async Task Owner_selection_does_not_change_the_SDK_public_result_bytes_or_function_schema(string toolName) {
        await using var fixture = await CollectionFixture.CreateAsync();
        fixture.Write(NativeCollectionName, "original selected needle");
        var files = TestWorkspaceServices.CreateFileService(fixture.Root);
        var listing = toolName == ToolContractCatalog.WorkspaceListDirectory ? files.ListDirectory() : files.ListFiles();
        var search = files.SearchText("needle");
        Delegate read = toolName == ToolContractCatalog.WorkspaceSearch
            ? (Func<WorkspaceTextSearchResult>)(() => search)
            : (Func<WorkspaceFileListResult>)(() => listing);
        var original = AIFunctionFactory.Create(read, toolName, "Original collection");
        var current = WorkspaceToolResultDisclosure.CreateCollectionTool(read, toolName, "Original collection");
        var expected = Assert.IsType<JsonElement>(await original.InvokeAsync(new()));
        var actual = Assert.IsType<JsonElement>(await current.InvokeAsync(new()));
        Assert.Equal(expected.GetRawText(), actual.GetRawText());
        Assert.Equal(original.JsonSchema.GetRawText(), current.JsonSchema.GetRawText());
        Assert.Equal(original.ReturnJsonSchema?.GetRawText(), current.ReturnJsonSchema?.GetRawText());
        Assert.DoesNotContain("readSelection", actual.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(fixture.Root, actual.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_marshaled_JSON_value_is_never_treated_as_a_live_owner_selection() {
        await using var fixture = await CollectionFixture.CreateAsync();
        fixture.Write("original.txt", "original");
        var actual = TestWorkspaceServices.CreateFileService(fixture.Root).ListFiles();
        var json = JsonSerializer.SerializeToElement(actual, AIJsonUtilities.DefaultOptions);
        var tool = WorkspaceToolResultDisclosure.CreateCollectionTool(() => json, ToolContractCatalog.WorkspaceListFiles, "JSON is not owner evidence");
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.InvokeOwnedAsync(tool, new()));
    }

    [Theory]
    [InlineData(ToolContractCatalog.WorkspaceListDirectory, 1)]
    [InlineData(ToolContractCatalog.WorkspaceListFiles, 1)]
    [InlineData(ToolContractCatalog.WorkspaceSearch, 1)]
    [InlineData(ToolContractCatalog.WorkspaceReadFile, 2)]
    public void Old_collection_and_mismatched_kind_versions_never_gain_exact_target_authority(string toolName, int version) {
        var root = Path.GetTempPath();
        var source = AgentToolProtocolEnvelope.Create("fixture-source", 1, "{}");
        var path = new WorkspaceToolResultPath(".", ".", root, true);
        var legacy = new WorkspaceToolResultEvidence(toolName, root, WorkspaceScopeDescriptor.Sandbox,
            source, WorkspaceToolResultEvidenceState.Complete, [path]);
        var envelope = AgentToolProtocolEnvelope.Create("configured-workspace-result-authority", version, JsonSerializer.Serialize(legacy));
        Assert.Throws<InvalidDataException>(() => WorkspaceToolResultEvidence.Read(envelope));
    }

    [Fact]
    public void Noncollection_version_one_retains_exact_old_payload_bytes_and_new_collection_readers_reject_old_versions() {
        var root = Path.GetTempPath();
        var evidence = new WorkspaceToolResultEvidence(ToolContractCatalog.WorkspaceReadFile, root, WorkspaceScopeDescriptor.Sandbox,
            AgentToolProtocolEnvelope.Create("fixture-source", 1, "{}"), WorkspaceToolResultEvidenceState.Complete,
            [new(".", ".", root, true)]);
        var oldBytes = JsonSerializer.Serialize(new { evidence.ToolName, evidence.WorkspaceRoot, evidence.Scope,
            evidence.Source, evidence.State, evidence.Paths, evidence.ExecutionWorkspaceScope });
        var saved = WorkspaceToolResultEvidence.Write(evidence);
        Assert.Equal(1, saved.Version);
        Assert.Equal(oldBytes, saved.PayloadJson);
        Assert.Equal(oldBytes, WorkspaceToolResultEvidence.Write(WorkspaceToolResultEvidence.Read(saved)).PayloadJson);
        var collection = evidence with { ToolName = ToolContractCatalog.WorkspaceListFiles,
            Selection = new(evidence.Paths[0], true, []) };
        var current = WorkspaceToolResultEvidence.Write(collection);
        Assert.Equal(2, current.Version);
        Assert.Equal(current.PayloadJson, WorkspaceToolResultEvidence.Write(WorkspaceToolResultEvidence.Read(current)).PayloadJson);
        Assert.Throws<InvalidDataException>(() => WorkspaceToolResultEvidence.Read(
            AgentToolProtocolEnvelope.Create(current.Format, 1, current.PayloadJson)));
    }

    [Fact]
    public void Exact_collection_evidence_is_bounded_within_the_existing_private_protocol_limit() {
        var root = Path.GetTempPath();
        var selected = Path.Combine(root, new string('\u00e9', WorkspaceToolResultEvidence.MaximumPathCharacters - root.Length));
        var rootPath = new WorkspaceToolResultPath(".", ".", root, true);
        var source = AgentToolProtocolEnvelope.Create("collection-source-fixture", 1, "{}");
        var evidence = new WorkspaceToolResultEvidence(ToolContractCatalog.WorkspaceListFiles, root, WorkspaceScopeDescriptor.Sandbox,
            source, WorkspaceToolResultEvidenceState.Complete, [rootPath], Selection: new(rootPath, true,
                Enumerable.Repeat(selected, WorkspaceToolResultEvidence.MaximumPaths - 1).ToImmutableArray()));
        var saved = WorkspaceToolResultEvidence.Write(evidence);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(saved.PayloadJson) < AgentToolProtocolEnvelope.MaximumUtf8Bytes);
        Assert.Equal(WorkspaceToolResultEvidence.MaximumPaths - 1, WorkspaceToolResultEvidence.Read(saved).Selection!.FullPaths.Length);
        Assert.Throws<InvalidDataException>(() => WorkspaceToolResultEvidence.Write(evidence with {
            Selection = evidence.Selection! with { FullPaths = evidence.Selection.FullPaths.Add(selected) }
        }));
    }

    private static string NativeCollectionName => OperatingSystem.IsWindows() ? "literal-name.txt" : "literal\\name.txt";
    public enum CollectionChange { None, Remove, Reparse }
    public enum CollectionFault { Missing, Count, Root, Origin, OutsideRoot }

    private sealed record CollectionRun(string ToolName, JsonElement Result, AgentToolProtocolEnvelope Evidence,
        Func<AgentToolResultDisclosure, CancellationToken, ValueTask<IAsyncDisposable?>> Authorize) {
        internal ValueTask<IAsyncDisposable?> AuthorizeAsync() => Authorize(new(new(Guid.NewGuid()),
            new(ToolName, 1, AgentToolProtocolEnvelope.ComputeDigest("{}"), "{}", AgentToolProposalEffect.Read,
                AgentToolProposalRecovery.RevalidateAndRead), AgentToolEffectState.Unknown, Result, Evidence), default);
    }

    private sealed class CollectionFixture(string root, ServiceProvider services, RuntimeCapabilityState state,
        CollectionSource source) : IAsyncDisposable {
        internal string Root => root;
        internal CollectionSource Source => source;

        internal static async Task<CollectionFixture> CreateAsync() {
            var root = TestFileSystem.CreateTemporaryRoot("collection-sdk-disclosure");
            var source = new CollectionSource();
            var services = MafRuntimeTestServices.CreateProviderRuntimeServiceCollection()
                .AddSingleton<IAgentWorkspaceToolResultSource>(source).BuildServiceProvider();
            var state = await ComposeAsync(services, explicitApproval: false, admitted: true, workspaceRoot: root);
            return new(root, services, state, source);
        }

        internal string Write(string relative, string content) {
            var path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        internal Task<CollectionRun> InvokeAsync(string toolName, string path)
            => InvokeAsync(toolName, new AIFunctionArguments { ["relativePath"] = path, ["query"] = "original" });

        internal Task<CollectionRun> InvokeAsync(string toolName, AIFunctionArguments arguments) {
            var function = Assert.IsAssignableFrom<AIFunction>(Assert.Single(state.Tools, item => item.Name == toolName));
            var metadata = Assert.Single(state.RuntimeToolMetadata, item => item.ToolName == toolName);
            return InvokeAsync(function, arguments, metadata.AuthorizeResultDisclosureAsync!);
        }

        internal async Task<CollectionRun> InvokeOwnedAsync(AIFunction function, AIFunctionArguments arguments) {
            var seed = SandboxWorkspaceSeedFactory.Create().ToCatalog();
            var actor = seed.Agents.First(item => item.ProviderProfileId.HasValue);
            var access = new AgentWorkspaceToolAccessSettings { Profile = AgentWorkspaceToolProfileKind.Custom, CanReadFiles = true };
            actor = actor with { ConfigurationJson = AgentWorkspaceToolAccessMetadata.Write("{}", access) };
            var context = new AgentRuntimeToolProviderContext(actor, seed.Providers.Single(item => item.Id == actor.ProviderProfileId), [], false,
                AgentRuntimeToolProviderPurpose.InteractiveChat, "collection-fixture", AgentRuntimeContextIntent.Empty,
                new Dictionary<string, string>()) {
                WorkspaceToolAccess = access, AdmittedToolSession = new(Guid.NewGuid(), Guid.NewGuid(), new(Guid.NewGuid())),
                ToolAdmissionSupport = AgentToolAdmissionSupport.Recoverable
            };
            var owner = new WorkspaceToolResultDisclosure(source, context, WorkspaceRuntimeServicesTestFactory.Create(root),
                TestWorkspaceServices.PhysicalPathPolicyFactory, (_, _) => access);
            return await InvokeAsync((AIFunction)owner.Wrap(function), arguments, owner.AuthorizeAsync);
        }

        private static async Task<CollectionRun> InvokeAsync(AIFunction function, AIFunctionArguments arguments,
            Func<AgentToolResultDisclosure, CancellationToken, ValueTask<IAsyncDisposable?>> authorize) {
            using var effect = AgentToolInvocationEffectScope.Begin();
            var result = Assert.IsType<JsonElement>(await function.InvokeAsync(arguments));
            var evidence = Assert.IsType<AgentToolProtocolEnvelope>(effect.DisclosureEvidence);
            var restored = JsonSerializer.Deserialize<AgentToolProtocolEnvelope>(JsonSerializer.Serialize(evidence))!;
            Assert.Equal(evidence, restored);
            return new(function.Name, result, restored, authorize);
        }

        public async ValueTask DisposeAsync() {
            Assert.Empty(await state.DisposeAcquiredResourcesAsync());
            await services.DisposeAsync();
            TestFileSystem.DeleteDirectoryWithRetry(root);
        }
    }

    private sealed class CollectionSource : IAgentWorkspaceToolResultSource {
        internal Func<Task>? BeforeCompletion { get; set; }
        public ValueTask<AgentToolProtocolEnvelope> CaptureAsync(AgentRuntimeToolProviderContext context,
            WorkspaceScopeDescriptor workspaceScope, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(AgentToolProtocolEnvelope.Create("collection-source-fixture", 1, "{}"));
        public async ValueTask<AgentToolProtocolEnvelope> CompleteAsync(AgentToolProtocolEnvelope original, CancellationToken cancellationToken = default) {
            if (BeforeCompletion is not null) {
                await BeforeCompletion();
            }
            return original;
        }
        public ValueTask<IAgentWorkspaceToolResultReadLease> AcquireReadAsync(AgentRuntimeToolProviderContext context,
            WorkspaceScopeDescriptor workspaceScope, AgentToolProtocolEnvelope original, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IAgentWorkspaceToolResultReadLease>(new CollectionLease(context.Agent, context.Capabilities.ToImmutableArray()));
    }

    private sealed class CollectionLease(AgentDefinition agent, ImmutableArray<CapabilityCatalogItem> capabilities) : IAgentWorkspaceToolResultReadLease {
        public AgentDefinition Agent => agent;
        public ImmutableArray<CapabilityCatalogItem> Capabilities => capabilities;
        public void RequireCurrent() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
