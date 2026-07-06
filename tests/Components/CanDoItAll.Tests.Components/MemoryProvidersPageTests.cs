using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Persistence;
using CanDoItAll.Modules.Memory;
using CanDoItAll.Modules.Memory.Pages;
using CanDoItAll.Web.Composition;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class MemoryProvidersPageTests
{
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
            Assert.Contains("Business demo memory", cut.Markup);
            Assert.Contains("Programming demo memory", cut.Markup);
        });
    }

    [Fact]
    public void MemoryShellNavigationContributor_AddsGenericMemoryRouteBeforeNativeCognitiveMemory()
    {
        var items = ShellNavigation.GetItems(0, [new MemoryShellNavigationContributor()]);
        var memoryIndex = items.ToList().FindIndex(item => item.Route == "/memory");
        var cognitiveMemoryIndex = items.ToList().FindIndex(item => item.Route == "/cognitive-memory");

        Assert.True(memoryIndex > 0);
        Assert.Equal("Memory", items[memoryIndex].Title);
        Assert.True(cognitiveMemoryIndex > memoryIndex);

        var matched = ShellNavigation.MatchRoute("memory", [new MemoryShellNavigationContributor()]);

        Assert.Equal("/memory", matched.Route);
        Assert.Equal("Memory", matched.Title);
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
}
