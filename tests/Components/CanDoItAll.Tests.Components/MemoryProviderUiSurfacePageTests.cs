using System.Text.Json;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Persistence;
using CanDoItAll.Modules.Memory;
using CanDoItAll.Modules.Memory.Pages;
using CanDoItAll.Modules.Memory.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class MemoryProviderUiSurfacePageTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-05T21:30:00Z");

    [Fact]
    public async Task MemoryProvidersPage_RendersRegisteredRclSurface()
    {
        var setup = await CreateRuntimeContextAsync(
            CreateProviderProfile(
                "provider.rcl",
                "RCL memory",
                capabilities: [MemoryCapabilityIds.UiRcl],
                surfaces:
                [
                    new MemoryProviderUiSurface(
                        MemoryProviderUiSurfaceKind.RazorComponentLibrary,
                        "Mock RCL panel",
                        ComponentKey: "memory.test.rcl",
                        UrlSettingKey: null,
                        MemoryCapabilityIds.UiRcl)
                ],
                extensions: MemoryExtensionData.Empty),
            services => services.AddSingleton(
                new MemoryProviderUiSurfaceComponentRegistration(
                    "memory.test.rcl",
                    typeof(MockMemoryProviderRclSurface))));
        using var context = setup.Context;
        var cut = context.RenderComponent<MemoryProvidersPage>();

        cut.WaitForElement("[data-testid='memory-ui-provider-list']");
        cut.Find("[data-testid='memory-ui-tab-provider-ui']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Mock RCL panel", cut.Markup);
            Assert.Contains("RCL memory", cut.Markup);
            Assert.Contains("memory.test.rcl", cut.Markup);
            Assert.Contains("Mock RCL surface rendered", cut.Markup);
        });
    }

    [Fact]
    public async Task MemoryProvidersPage_RendersPolicyControlledIframeSurface()
    {
        var setup = await CreateRuntimeContextAsync(
            CreateProviderProfile(
                "provider.iframe",
                "Iframe memory",
                capabilities: [MemoryCapabilityIds.UiIframe],
                surfaces:
                [
                    new MemoryProviderUiSurface(
                        MemoryProviderUiSurfaceKind.Iframe,
                        "Provider console",
                        ComponentKey: null,
                        UrlSettingKey: "provider.vendor.uiUrl",
                        MemoryCapabilityIds.UiIframe)
                ],
                extensions: StringExtension("provider.vendor.uiUrl", "https://memory.example.test/console")));
        using var context = setup.Context;
        var cut = context.RenderComponent<MemoryProvidersPage>();

        cut.WaitForElement("[data-testid='memory-ui-provider-list']");
        cut.Find("[data-testid='memory-ui-tab-provider-ui']").Click();

        cut.WaitForAssertion(() =>
        {
            var frame = cut.Find("[data-testid='memory-ui-provider-iframe']");
            Assert.Equal("https://memory.example.test/console", frame.GetAttribute("src"));
            Assert.Contains("allow-scripts", frame.GetAttribute("sandbox"));
            Assert.Contains("Provider console", cut.Markup);
        });
    }

    [Fact]
    public async Task MemoryProvidersPage_RendersSafeFallbacksForMissingCapabilityAndUnsafeUrl()
    {
        var setup = await CreateRuntimeContextAsync(
            CreateProviderProfile(
                "provider.fallback",
                "Fallback memory",
                capabilities: [MemoryCapabilityIds.UiIframe],
                surfaces:
                [
                    new MemoryProviderUiSurface(
                        MemoryProviderUiSurfaceKind.RazorComponentLibrary,
                        "Native placeholder",
                        ComponentKey: "native.cognitiveMemory.placeholder",
                        UrlSettingKey: null,
                        MemoryCapabilityIds.UiRcl),
                    new MemoryProviderUiSurface(
                        MemoryProviderUiSurfaceKind.Iframe,
                        "Unsafe console",
                        ComponentKey: null,
                        UrlSettingKey: "provider.vendor.uiUrl",
                        MemoryCapabilityIds.UiIframe)
                ],
                extensions: StringExtension("provider.vendor.uiUrl", "javascript:alert(1)")));
        using var context = setup.Context;
        var cut = context.RenderComponent<MemoryProvidersPage>();

        cut.WaitForElement("[data-testid='memory-ui-provider-list']");
        cut.Find("[data-testid='memory-ui-tab-provider-ui']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Native placeholder", cut.Markup);
            Assert.Contains("Required capability 'ui.rcl' is not declared by the selected provider.", cut.Markup);
            Assert.Contains("Unsafe console", cut.Markup);
            Assert.Contains("Provider UI URL must use HTTPS or loopback HTTP.", cut.Markup);
            Assert.DoesNotContain("javascript:alert", cut.Markup);
        });
    }

    [Fact]
    public async Task MemoryProvidersPage_BlocksProviderUiWhenProviderIsDisabled()
    {
        var setup = await CreateRuntimeContextAsync(
            CreateProviderProfile(
                "provider.disabled",
                "Disabled memory",
                capabilities: [MemoryCapabilityIds.UiRcl],
                surfaces:
                [
                    new MemoryProviderUiSurface(
                        MemoryProviderUiSurfaceKind.RazorComponentLibrary,
                        "Disabled RCL panel",
                        ComponentKey: "memory.test.rcl",
                        UrlSettingKey: null,
                        MemoryCapabilityIds.UiRcl)
                ],
                extensions: MemoryExtensionData.Empty,
                isEnabled: false),
            services => services.AddSingleton(
                new MemoryProviderUiSurfaceComponentRegistration(
                    "memory.test.rcl",
                    typeof(MockMemoryProviderRclSurface))));
        using var context = setup.Context;
        var cut = context.RenderComponent<MemoryProvidersPage>();

        cut.WaitForElement("[data-testid='memory-ui-provider-list']");
        cut.Find("[data-testid='memory-ui-tab-provider-ui']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Disabled RCL panel", cut.Markup);
            Assert.Contains("Selected provider must be enabled and healthy before provider UI can render.", cut.Markup);
            Assert.DoesNotContain("Mock RCL surface rendered", cut.Markup);
        });
    }

    private static async Task<ComponentSetup> CreateRuntimeContextAsync(
        MemoryProviderProfile profile,
        Action<IServiceCollection>? configureServices = null)
    {
        var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddDbContextFactory<AppDbContext>(options =>
            options.UseInMemoryDatabase($"memory-ui-surfaces-{Guid.NewGuid():N}"));
        context.Services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        context.Services.AddGenericMemoryModule();
        configureServices?.Invoke(context.Services);
        context.Services.AddMemoryUiModule();

        using var scope = context.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var profileStore = scope.ServiceProvider.GetRequiredService<IMemoryProviderProfileStore>();
        await profileStore.UpsertAsync(profile, Now);

        return new ComponentSetup(context);
    }

    private static MemoryProviderProfile CreateProviderProfile(
        string instanceId,
        string displayName,
        IReadOnlyList<MemoryCapabilityId> capabilities,
        IReadOnlyList<MemoryProviderUiSurface> surfaces,
        MemoryExtensionData extensions,
        bool isEnabled = true,
        MemoryProviderHealthState healthState = MemoryProviderHealthState.Healthy)
    {
        return new MemoryProviderProfile(
            MemoryProviderInstanceId.Parse(instanceId),
            displayName,
            MemoryProviderDriverKind.Mock,
            isEnabled,
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
                MemoryProviderInteractionSupport.SyncQueryOnly,
                surfaces,
                MemoryProviderLimits.Default,
                extensions));
    }

    private static MemoryExtensionData StringExtension(string key, string value)
    {
        return MemoryExtensionData.From((key, JsonDocument.Parse(JsonSerializer.Serialize(value)).RootElement.Clone()));
    }

    private sealed class MockMemoryProviderRclSurface : ComponentBase
    {
        [Parameter]
        public MemoryProviderManagementProfile? Provider { get; set; }

        [Parameter]
        public MemoryProviderUiSurfaceProjection? Surface { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "section");
            builder.AddAttribute(1, "data-testid", "memory-provider-rcl-surface");
            builder.OpenElement(2, "strong");
            builder.AddContent(3, "Mock RCL surface rendered");
            builder.CloseElement();
            builder.OpenElement(4, "span");
            builder.AddContent(5, Provider?.DisplayName);
            builder.CloseElement();
            builder.OpenElement(6, "span");
            builder.AddContent(7, Surface?.ComponentKey);
            builder.CloseElement();
            builder.CloseElement();
        }
    }

    private sealed record ComponentSetup(TestContext Context);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
