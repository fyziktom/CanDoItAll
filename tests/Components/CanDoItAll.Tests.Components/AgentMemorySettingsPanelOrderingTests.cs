using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Tests.Components.Memory;

public sealed class AgentMemorySettingsPanelOrderingTests : AgentMemorySettingsPanelTestBase
{
    [Fact]
    public void Existing_bindings_render_in_configured_order_and_can_move()
    {
        var settings = new AgentMemoryAccessSettings
        {
            ProviderBindings =
            [
                Binding("zeta", "provider.zeta"),
                Binding("alpha", "provider.alpha")
            ]
        };
        using var context = CreateContext(new TestProfileStore());
        var cut = Render(context, settings);

        cut.WaitForElement("[data-testid='agents-catalog-memory-bindings']");
        Assert.True(cut.Markup.IndexOf("/mem:zeta", StringComparison.Ordinal) <
                    cut.Markup.IndexOf("/mem:alpha", StringComparison.Ordinal));

        cut.Find("[data-testid='agents-catalog-memory-down-zeta']").Click();

        cut.WaitForAssertion(() =>
            Assert.Equal(["alpha", "zeta"], settings.ProviderBindings.Select(binding => binding.Alias.Value)));
        cut.WaitForAssertion(() =>
            Assert.True(cut.Markup.IndexOf("/mem:alpha", StringComparison.Ordinal) <
                        cut.Markup.IndexOf("/mem:zeta", StringComparison.Ordinal)));
    }

    [Fact]
    public void Required_behavior_roundtrips_through_add_and_edit_controls()
    {
        var settings = new AgentMemoryAccessSettings();
        using var context = CreateContext(new TestProfileStore(
            CreateProvider("provider.primary", "Primary", isEnabled: true)));
        var cut = Render(context, settings);
        cut.WaitForElement("[data-testid='agents-catalog-memory-new-provider'] option[value='provider.primary']");

        cut.Find("[data-testid='agents-catalog-memory-new-alias']").Change("primary");
        cut.Find("[data-testid='agents-catalog-memory-new-provider']").Change("provider.primary");
        cut.Find("[data-testid='agents-catalog-memory-new-requirement']").Change("Required");
        cut.Find("[data-testid='agents-catalog-memory-add-binding']").Click();

        cut.WaitForAssertion(() =>
            Assert.Equal(AgentMemoryProviderRequirement.Required, Assert.Single(settings.ProviderBindings).Requirement));
        cut.Find("[data-testid='agents-catalog-memory-requirement-primary']").Change("Optional");
        cut.WaitForAssertion(() =>
            Assert.Equal(AgentMemoryProviderRequirement.Optional, Assert.Single(settings.ProviderBindings).Requirement));
    }

    [Fact]
    public void Removing_binding_prunes_every_hidden_provider_selector()
    {
        var providerId = MemoryProviderInstanceId.Parse("provider.primary");
        var settings = new AgentMemoryAccessSettings
        {
            ProviderBindings = [Binding("primary", providerId.Value)],
            PreferredProviderInstanceId = providerId,
            DefaultProviderInstanceId = providerId,
            AllowedProviderInstanceIds = [providerId],
            ProviderAssignments =
            [
                new AgentMemoryProviderAssignmentSetting(
                    MemoryProviderAssignmentScope.Workflow,
                    "workflow-a",
                    providerId)
            ]
        };
        using var context = CreateContext(new TestProfileStore());
        var cut = Render(context, settings);

        cut.Find("[data-testid='agents-catalog-memory-remove-primary']").Click();

        cut.WaitForAssertion(() => Assert.Empty(settings.ProviderBindings));
        Assert.Null(settings.PreferredProviderInstanceId);
        Assert.Null(settings.DefaultProviderInstanceId);
        Assert.Empty(settings.AllowedProviderInstanceIds);
        Assert.Empty(settings.ProviderAssignments);
    }

    private static AgentMemoryProviderBindingSetting Binding(string alias, string providerId) =>
        new(
            AgentMemoryProviderAlias.Parse(alias),
            MemoryProviderInstanceId.Parse(providerId));
}
