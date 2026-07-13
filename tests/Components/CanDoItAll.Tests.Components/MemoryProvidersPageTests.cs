using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Memory;
using CanDoItAll.Modules.Memory.Pages;
using CanDoItAll.Modules.Memory.Services;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Web.Composition;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class MemoryProvidersPageTests
{
    [Fact]
    public void MemoryProvidersPage_RendersLoadingStateWhileProviderSnapshotIsPending()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton(new MemoryProvidersPageController(
            new PendingSnapshotMemoryProviderManagementUiService()));

        var cut = context.RenderComponent<MemoryProvidersPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Loading memory providers", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void MemoryProvidersPage_RendersZeroProviderStateWithoutNativeServices()
    {
        var setup = CreateContext();
        using var context = setup.Context;

        var cut = context.RenderComponent<MemoryProvidersPage>();

        cut.WaitForElement("[data-testid='memory-ui-zero-provider']");
        Assert.Contains("No memory providers are configured", cut.Markup);
        cut.Find("[data-testid='memory-ui-tab-query']").Click();
        cut.WaitForElement("[data-testid='memory-ui-query-submit']");
        Assert.Contains("Query unavailable", cut.Markup);
        Assert.True(cut.Find("[data-testid='memory-ui-query-submit']").HasAttribute("disabled"));
        Assert.DoesNotContain("Cognitive Memory", cut.Markup);
        Assert.Empty(setup.Store.Profiles);
    }

    [Fact]
    public void MemoryProvidersPage_RendersTwoMockProvidersAndCapabilityHealthDetails()
    {
        var setup = CreateContext(
            CreateMockProvider(
                "provider.business",
                "Business memory",
                MemoryProviderHealthState.Healthy,
                MemoryCapabilityIds.ContextQuerySync,
                MemoryCapabilityIds.FeedbackImmediate),
            CreateMockProvider(
                "provider.programming",
                "Programming memory",
                MemoryProviderHealthState.Degraded,
                MemoryCapabilityIds.ContextQuerySync,
                MemoryCapabilityIds.ContextQueryAsync,
                MemoryCapabilityIds.OperationStatus));
        using var context = setup.Context;

        var cut = context.RenderComponent<MemoryProvidersPage>();

        cut.WaitForElement("[data-testid='memory-ui-provider-list']");
        Assert.Contains("Business memory", cut.Markup);
        Assert.Contains("Programming memory", cut.Markup);

        cut.Find("[data-testid='memory-provider-provider-programming']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Programming memory", cut.Markup);
            Assert.Contains("Degraded", cut.Markup);
            Assert.Contains("context.query.async", cut.Markup);
            Assert.Contains("operations.status", cut.Markup);
        });
    }

    [Fact]
    public void MemoryProvidersPage_CreatesDemoProvidersOnlyAfterExplicitAction()
    {
        var setup = CreateContext();
        using var context = setup.Context;
        var cut = context.RenderComponent<MemoryProvidersPage>();

        cut.WaitForElement("[data-testid='memory-ui-zero-provider']");
        Assert.Empty(setup.Store.Profiles);

        cut.Find("[data-testid='memory-ui-add-demo-providers']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, setup.Store.Profiles.Count);
            Assert.Contains("provider.business-demo", setup.Store.Profiles.Select(profile => profile.InstanceId.Value));
            Assert.Contains("provider.programming-demo", setup.Store.Profiles.Select(profile => profile.InstanceId.Value));
            Assert.All(setup.Store.Profiles, profile =>
                Assert.Equal(
                    [MemoryCapabilityIds.ContextQuerySync],
                    profile.Manifest.Capabilities
                        .Where(capability => capability.Supported)
                        .Select(capability => capability.Id)));
            Assert.Contains("Business demo memory", cut.Markup);
            Assert.Contains("Programming demo memory", cut.Markup);
        });
    }

    [Fact]
    public async Task Imported_stale_manifest_cannot_enable_unimplemented_mutation_actions()
    {
        var setup = CreateContext(CreateMockProvider(
            "provider.stale",
            "Stale imported memory",
            MemoryProviderHealthState.Healthy,
            MemoryCapabilityIds.ContextQuerySync,
            MemoryCapabilityIds.IngestionSnapshot,
            MemoryCapabilityIds.FeedbackImmediate,
            MemoryCapabilityIds.EventsProviderPush,
            MemoryCapabilityIds.OperationStatus));
        using var context = setup.Context;
        var cut = context.RenderComponent<MemoryProvidersPage>();

        cut.WaitForElement("[data-testid='memory-ui-tab-ingestion']").Click();
        cut.WaitForAssertion(() =>
            Assert.Empty(cut.FindAll("[data-testid='memory-ui-ingestion-submit']")));

        var guard = context.Services.GetRequiredService<MemoryProviderExecutableActionGuard>();
        foreach (var capability in new[]
                 {
                     MemoryCapabilityIds.IngestionSnapshot,
                     MemoryCapabilityIds.FeedbackImmediate,
                     MemoryCapabilityIds.EventsProviderPush
                 })
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                guard.EnsureProviderCanExecuteAsync("provider.stale", capability, CancellationToken.None));
        }

        Assert.False(MemoryProviderCapabilityPolicy.CanCancelOperation(MemoryProviderDriverKind.Mock));
    }

    [Fact]
    public void MemoryShellNavigationContributor_AddsMemoryProvidersRouteAfterLiveProcesses()
    {
        var items = ShellNavigation.GetItems(
            0,
            [
                new AgentFrameworkShellNavigationContributor(),
                new ProcessesShellNavigationContributor(),
                new MemoryShellNavigationContributor()
            ]);
        var memoryIndex = items.ToList().FindIndex(item => item.Route == "/memory");
        var liveProcessesIndex = items.ToList().FindIndex(item => item.Route == "/processes/live");

        Assert.True(liveProcessesIndex > 0);
        Assert.Equal(liveProcessesIndex + 1, memoryIndex);
        Assert.Equal("Memory Providers", items[memoryIndex].Title);
        Assert.Equal("psychology", items[memoryIndex].Icon);
        Assert.DoesNotContain(items, item => item.Route == "/cognitive-memory");

        var matched = ShellNavigation.MatchRoute("memory", [new MemoryShellNavigationContributor()]);

        Assert.Equal("/memory", matched.Route);
        Assert.Equal("Memory Providers", matched.Title);
    }

    [Fact]
    public void MemoryUiModule_DoesNotReferenceNativeCognitiveMemoryOrQdrant()
    {
        var root = FindRepositoryRoot();
        var moduleRoot = Path.Combine(root, "src", "Modules", "CanDoItAll.Modules.Memory");
        var sourceText = string.Join(
            Environment.NewLine,
            Directory.GetFiles(moduleRoot, "*.*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                               path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) ||
                               path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));

        Assert.DoesNotContain("CanDoItAll.Modules.CognitiveMemory", sourceText);
        Assert.DoesNotContain("CognitiveMemory", sourceText);
        Assert.DoesNotContain("Qdrant", sourceText);
        Assert.DoesNotContain("CanDoItAll.AgentFramework.Rag", sourceText);
    }

    [Fact]
    public void MemoryUiModule_ProviderManagementRemainsResponsibilityBased()
    {
        var root = FindRepositoryRoot();
        var moduleRoot = Path.Combine(root, "src", "Modules", "CanDoItAll.Modules.Memory");
        var sourceFiles = Directory.GetFiles(moduleRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                           !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var oversizedFiles = sourceFiles
            .Select(path => new { Path = path, Lines = File.ReadLines(path).Count() })
            .Where(file => file.Lines > 220)
            .ToArray();
        var sourceText = string.Join(Environment.NewLine, sourceFiles.Select(File.ReadAllText));
        var facade = File.ReadAllText(Path.Combine(moduleRoot, "Services", "MemoryProviderManagementUiService.cs"));

        Assert.Empty(oversizedFiles);
        Assert.DoesNotContain("partial class", sourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildCapabilities", facade, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectRclSurface", facade, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer", facade, StringComparison.Ordinal);
    }

    private static (TestContext Context, InMemoryMemoryProviderProfileStore Store) CreateContext(
        params MemoryProviderProfile[] profiles)
    {
        var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddDbContextFactory<AppDbContext>(options =>
            options.UseInMemoryDatabase($"memory-ui-shell-{Guid.NewGuid():N}"));
        context.Services.AddGenericMemoryModule();

        var store = new InMemoryMemoryProviderProfileStore(profiles);
        context.Services.AddSingleton<IMemoryProviderProfileStore>(store);
        context.Services.AddMemoryUiModule();

        return (context, store);
    }

    private static MemoryProviderProfile CreateMockProvider(
        string instanceId,
        string displayName,
        MemoryProviderHealthState healthState,
        params MemoryCapabilityId[] capabilities)
    {
        return new MemoryProviderProfile(
            MemoryProviderInstanceId.Parse(instanceId),
            displayName,
            MemoryProviderDriverKind.Mock,
            IsEnabled: true,
            healthState,
            MemoryProviderWorkspaceScope.AllWorkspaces,
            SelectionTags: ["component-test"],
            MemoryProviderProfilePolicy.Default,
            new MemoryProviderManifest(
                MemoryProviderKind.Parse("memory.mock"),
                MemoryProtocolVersion.Current,
                capabilities
                    .Select(capability => new MemoryCapabilityDescriptor(capability, Version: "1", Supported: true))
                    .ToArray(),
                new MemoryProviderInteractionSupport(
                    SupportsSynchronousQueries: capabilities.Contains(MemoryCapabilityIds.ContextQuerySync),
                    SupportsAsynchronousOperations: capabilities.Contains(MemoryCapabilityIds.ContextQueryAsync),
                    SupportsSourceRequests: capabilities.Contains(MemoryCapabilityIds.IngestionProviderRequestedSource),
                    SupportsFeedback: capabilities.Contains(MemoryCapabilityIds.FeedbackImmediate),
                    SupportsProviderEvents: capabilities.Contains(MemoryCapabilityIds.EventsProviderPush)),
                UiSurfaces: [],
                MemoryProviderLimits.Default,
                MemoryExtensionData.Empty));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed class InMemoryMemoryProviderProfileStore(
        IEnumerable<MemoryProviderProfile> seed) : IMemoryProviderProfileStore
    {
        public List<MemoryProviderProfile> Profiles { get; } = seed.ToList();

        public Task UpsertAsync(
            MemoryProviderProfile profile,
            DateTimeOffset updatedAtUtc,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(profile);
            var existingIndex = Profiles.FindIndex(existing =>
                string.Equals(existing.InstanceId.Value, profile.InstanceId.Value, StringComparison.Ordinal));
            if (existingIndex >= 0)
            {
                Profiles[existingIndex] = profile;
            }
            else
            {
                Profiles.Add(profile);
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MemoryProviderProfile>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<MemoryProviderProfile>>(
                Profiles
                    .OrderBy(profile => profile.InstanceId.Value, StringComparer.Ordinal)
                    .ToArray());
        }
    }

    private sealed class PendingSnapshotMemoryProviderManagementUiService : IMemoryProviderManagementUiService
    {
        private readonly TaskCompletionSource<MemoryProviderManagementSnapshot> snapshotSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<MemoryProviderManagementSnapshot> GetSnapshotAsync(
            string? selectedProviderInstanceId = null,
            CancellationToken cancellationToken = default)
            => snapshotSource.Task;

        public Task<MemoryProviderProfile> SaveProviderAsync(
            MemoryProviderProfileEditorModel editor,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<MemoryProviderProfile>> CreateDemoProvidersAsync(
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MemoryProviderQueryUiResult> RunQueryAsync(
            string? selectedProviderInstanceId,
            MemoryQueryEditorModel editor,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MemoryProviderOperationUiResult> RefreshOperationAsync(
            string operationId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MemoryProviderOperationUiResult> CancelOperationAsync(
            string operationId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MemoryProviderFeedbackUiResult> SubmitFeedbackAsync(
            string? selectedProviderInstanceId,
            MemoryFeedbackEditorModel editor,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MemoryProviderManualIngestionUiResult> EnqueueManualIngestionAsync(
            string? selectedProviderInstanceId,
            MemoryManualIngestionEditorModel editor,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MemoryProviderEventAcknowledgeUiResult> AcknowledgeEventAsync(
            string? selectedProviderInstanceId,
            string providerEventId,
            bool accepted,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
