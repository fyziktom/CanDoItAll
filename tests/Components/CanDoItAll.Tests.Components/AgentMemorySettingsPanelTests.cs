using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Components.Memory;

public sealed class AgentMemorySettingsPanelTests : AgentMemorySettingsPanelTestBase
{
    [Fact]
    public void Enabled_providers_are_listed_and_disabled_providers_are_excluded()
    {
        using var context = CreateContext(new TestProfileStore(
            CreateProvider("provider.zulu", "Zulu memory", isEnabled: true),
            CreateProvider("provider.disabled", "Disabled memory", isEnabled: false),
            CreateProvider("provider.alpha", "Alpha memory", isEnabled: true)));

        var cut = Render(context, new AgentMemoryAccessSettings());

        cut.WaitForElement("[data-testid='agents-catalog-memory-new-provider'] option[value='provider.alpha']");
        var selectMarkup = cut.Find("[data-testid='agents-catalog-memory-new-provider']").InnerHtml;
        var alphaIndex = selectMarkup.IndexOf("Alpha memory (provider.alpha)", StringComparison.Ordinal);
        var zuluIndex = selectMarkup.IndexOf("Zulu memory (provider.zulu)", StringComparison.Ordinal);
        Assert.True(alphaIndex >= 0 && alphaIndex < zuluIndex);
        Assert.DoesNotContain("provider.disabled", selectMarkup, StringComparison.Ordinal);
    }

    [Fact]
    public void No_provider_state_disables_binding_actions()
    {
        using var context = CreateContext(new TestProfileStore());

        var cut = Render(context, new AgentMemoryAccessSettings());

        cut.WaitForElement("[data-testid='agents-catalog-memory-no-providers']");
        Assert.True(cut.Find("[data-testid='agents-catalog-memory-new-provider']").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid='agents-catalog-memory-add-binding']").HasAttribute("disabled"));
    }

    [Fact]
    public void Enabled_provider_without_sync_query_is_not_offered_for_context_binding()
    {
        using var context = CreateContext(new TestProfileStore(
            CreateProvider(
                "provider.async-only",
                "Async only",
                isEnabled: true,
                supportsSyncQuery: false)));

        var cut = Render(context, new AgentMemoryAccessSettings());

        cut.WaitForElement("[data-testid='agents-catalog-memory-no-providers']");
        Assert.DoesNotContain("provider.async-only", cut.Find("[data-testid='agents-catalog-memory-new-provider']").InnerHtml);
        Assert.Contains("synchronous context-query capability", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Picker_excludes_unhealthy_missing_driver_and_ambiguous_driver_profiles()
    {
        using var context = CreateContext(new TestProfileStore(
            CreateProvider(
                "provider.unhealthy",
                "Unhealthy",
                isEnabled: true,
                healthState: MemoryProviderHealthState.Degraded),
            CreateProvider(
                "provider.no-driver",
                "No driver",
                isEnabled: true,
                driverKind: MemoryProviderDriverKind.Http),
            CreateProvider("provider.ambiguous", "Ambiguous", isEnabled: true)));
        context.Services.AddSingleton<IMemoryProviderDriver>(
            new TestMemoryProviderDriver(MemoryProviderDriverKind.Mock));

        var cut = Render(context, new AgentMemoryAccessSettings());

        cut.WaitForElement("[data-testid='agents-catalog-memory-no-providers']");
        var picker = cut.Find("[data-testid='agents-catalog-memory-new-provider']").InnerHtml;
        Assert.DoesNotContain("provider.unhealthy", picker, StringComparison.Ordinal);
        Assert.DoesNotContain("provider.no-driver", picker, StringComparison.Ordinal);
        Assert.DoesNotContain("provider.ambiguous", picker, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AgentMemoryInvocationMode.Disabled)]
    [InlineData(AgentMemoryInvocationMode.ExplicitDirective)]
    public void Non_automatic_mode_clears_and_disables_memory_tools(AgentMemoryInvocationMode mode)
    {
        var settings = new AgentMemoryAccessSettings
        {
            InvocationMode = mode,
            CanUseMemoryTools = true
        };
        using var context = CreateContext(new TestProfileStore());

        var cut = Render(context, settings);

        var checkbox = cut.Find("[data-testid='agents-catalog-memory-tools']");
        Assert.True(checkbox.HasAttribute("disabled"));
        Assert.False(checkbox.HasAttribute("checked"));
        Assert.False(settings.CanUseMemoryTools);
    }

    [Fact]
    public void Provider_load_failure_is_displayed_and_logged_without_mutating_settings()
    {
        var settings = CreateSettingsWithBinding("existing", "provider.existing");
        var originalBindings = settings.ProviderBindings.ToArray();
        var logger = new RecordingLogger<AgentMemorySettingsPanel>();
        using var context = CreateContext(new ThrowingProfileStore(), logger);

        var cut = Render(context, settings);

        cut.WaitForElement("[data-testid='agents-catalog-memory-provider-load-error']");
        Assert.Contains("could not be loaded", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.True(cut.Find("[data-testid='agents-catalog-memory-add-binding']").HasAttribute("disabled"));
        Assert.Equal(originalBindings, settings.ProviderBindings);
        Assert.DoesNotContain("Profile store unavailable", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("secret=do-not-render", logger.Entries.Single().Message, StringComparison.Ordinal);
        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Error &&
            entry.Exception is null &&
            entry.Message.Contains("Failed to load enabled memory providers", StringComparison.Ordinal));
    }

    [Fact]
    public void Selected_configured_provider_is_added_as_alias_binding()
    {
        var settings = new AgentMemoryAccessSettings
        {
            AllowedProviderInstanceIds = [MemoryProviderInstanceId.Parse("provider.existing")]
        };
        using var context = CreateContext(new TestProfileStore(
            CreateProvider("provider.selected", "Selected memory", isEnabled: true)));
        var cut = Render(context, settings);
        cut.WaitForElement("[data-testid='agents-catalog-memory-new-provider'] option[value='provider.selected']");

        cut.Find("[data-testid='agents-catalog-memory-new-alias']").Change("team-memory");
        cut.Find("[data-testid='agents-catalog-memory-new-provider']").Change("provider.selected");
        cut.Find("[data-testid='agents-catalog-memory-add-binding']").Click();

        cut.WaitForAssertion(() =>
        {
            var binding = Assert.Single(settings.ProviderBindings);
            Assert.Equal("team-memory", binding.Alias.Value);
            Assert.Equal("provider.selected", binding.ProviderInstanceId.Value);
            Assert.True(binding.IncludeInAutomaticContext);
            Assert.Contains(settings.AllowedProviderInstanceIds, id => id.Value == "provider.selected");
        });
    }

    [Fact]
    public void Arbitrary_unconfigured_provider_id_is_rejected()
    {
        var settings = new AgentMemoryAccessSettings();
        using var context = CreateContext(new TestProfileStore(
            CreateProvider("provider.configured", "Configured memory", isEnabled: true)));
        var cut = Render(context, settings);
        cut.WaitForElement("[data-testid='agents-catalog-memory-new-provider'] option[value='provider.configured']");

        cut.Find("[data-testid='agents-catalog-memory-new-alias']").Change("untrusted");
        cut.Find("[data-testid='agents-catalog-memory-new-provider']").Change("provider.unconfigured");
        cut.Find("[data-testid='agents-catalog-memory-add-binding']").Click();

        cut.WaitForElement("[data-testid='agents-catalog-memory-validation']");
        Assert.Contains("enabled configured memory provider", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(settings.ProviderBindings);
    }

    [Fact]
    public void Existing_bindings_render_when_profiles_are_disabled_or_deleted()
    {
        var settings = new AgentMemoryAccessSettings
        {
            ProviderBindings =
            [
                new AgentMemoryProviderBindingSetting(
                    AgentMemoryProviderAlias.Parse("disabled"),
                    MemoryProviderInstanceId.Parse("provider.disabled")),
                new AgentMemoryProviderBindingSetting(
                    AgentMemoryProviderAlias.Parse("deleted"),
                    MemoryProviderInstanceId.Parse("provider.deleted"))
            ]
        };
        using var context = CreateContext(new TestProfileStore(
            CreateProvider("provider.disabled", "Disabled memory", isEnabled: false)));

        var cut = Render(context, settings);

        cut.WaitForElement("[data-testid='agents-catalog-memory-bindings']");
        Assert.Contains("/mem:disabled", cut.Markup);
        Assert.Contains("provider.disabled", cut.Markup);
        Assert.Contains("/mem:deleted", cut.Markup);
        Assert.Contains("provider.deleted", cut.Markup);
        Assert.Equal(2, settings.ProviderBindings.Count);
    }

}
