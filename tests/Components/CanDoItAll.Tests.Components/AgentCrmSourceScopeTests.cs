using System.Text.Json;
using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Tests.Components.Memory;

public sealed class AgentCrmSourceScopeTests : AgentMemorySettingsPanelTestBase {
    [Theory]
    [InlineData(AgentMemoryInvocationMode.Disabled)]
    [InlineData(AgentMemoryInvocationMode.ExplicitDirective)]
    [InlineData(AgentMemoryInvocationMode.Automatic)]
    public void CRM_source_access_is_explicit_and_independent_of_memory_invocation_mode(AgentMemoryInvocationMode mode) {
        var settings = new AgentMemoryAccessSettings { InvocationMode = mode };
        using var context = CreateContext(new TestProfileStore());
        var cut = Render(context, settings);
        var checkbox = cut.Find("[data-testid='agents-catalog-crm-source-read']");
        Assert.False(checkbox.HasAttribute("checked"));
        Assert.False(checkbox.HasAttribute("disabled"));
        Assert.Empty(settings.AllowedSourceScopes);
        checkbox.Change(true);
        Assert.Equal(MemorySourceScope.Crm, Assert.Single(settings.AllowedSourceScopes));
        Assert.Equal(mode, settings.InvocationMode);
        Assert.False(settings.CanUseMemoryTools);
        Assert.Empty(settings.ProviderBindings);
        Assert.Contains("also require the CRM Planning Search or CRM Planning Summary capability", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Toggling_CRM_preserves_other_source_permissions_and_saved_configuration_shape() {
        const string original = "{\"unrelated\":{\"retain\":true},\"memory\":{\"invocationMode\":\"Disabled\",\"allowedSourceScopes\":[\"Project\",\"Resource\"]}}";
        var settings = AgentMemoryAccessMetadata.Read(original);
        using var context = CreateContext(new TestProfileStore());
        var cut = Render(context, settings);
        cut.Find("[data-testid='agents-catalog-crm-source-read']").Change(true);
        cut.Find("[data-testid='agents-catalog-crm-source-read']").Change(true);
        Assert.Equal(3, settings.AllowedSourceScopes.Count);
        var saved = AgentMemoryAccessMetadata.Write(original, settings);
        var restored = AgentMemoryAccessMetadata.Read(saved);
        Assert.Equal(settings.AllowedSourceScopes, restored.AllowedSourceScopes);
        using var json = JsonDocument.Parse(saved);
        Assert.True(json.RootElement.GetProperty("unrelated").GetProperty("retain").GetBoolean());
        Assert.Contains(json.RootElement.GetProperty("memory").GetProperty("allowedSourceScopes").EnumerateArray(),
            value => value.GetString() == "Crm");
        cut.Find("[data-testid='agents-catalog-crm-source-read']").Change(false);
        Assert.Equal(new[] { MemorySourceScope.Project, MemorySourceScope.Resource }, settings.AllowedSourceScopes);
        Assert.DoesNotContain(MemorySourceScope.Crm, AgentMemoryAccessMetadata.Read(AgentMemoryAccessMetadata.Write(saved, settings)).AllowedSourceScopes);
    }

    [Fact]
    public void Parent_editor_replacement_does_not_mutate_the_previous_agents_source_permissions() {
        var previous = new AgentMemoryAccessSettings { AllowedSourceScopes = [MemorySourceScope.Crm] };
        var current = new AgentMemoryAccessSettings { AllowedSourceScopes = [MemorySourceScope.Resource] };
        using var context = CreateContext(new TestProfileStore());
        var cut = Render(context, previous);
        Assert.True(cut.Find("[data-testid='agents-catalog-crm-source-read']").HasAttribute("checked"));
        cut.Render(parameters => parameters.Add(component => component.Value, current));
        Assert.False(cut.Find("[data-testid='agents-catalog-crm-source-read']").HasAttribute("checked"));
        cut.Find("[data-testid='agents-catalog-crm-source-read']").Change(true);
        Assert.Equal(MemorySourceScope.Crm, Assert.Single(previous.AllowedSourceScopes));
        Assert.Equal(new[] { MemorySourceScope.Resource, MemorySourceScope.Crm }, current.AllowedSourceScopes);
    }

    [Fact]
    public void Memory_provider_load_failure_does_not_enable_CRM_or_disable_its_explicit_permission_control() {
        var settings = new AgentMemoryAccessSettings();
        using var context = CreateContext(new ThrowingProfileStore());
        var cut = Render(context, settings);
        cut.WaitForElement("[data-testid='agents-catalog-memory-provider-load-error']");
        var checkbox = cut.Find("[data-testid='agents-catalog-crm-source-read']");
        Assert.False(checkbox.HasAttribute("checked"));
        Assert.False(checkbox.HasAttribute("disabled"));
        Assert.Empty(settings.AllowedSourceScopes);
        checkbox.Change(true);
        Assert.Equal(MemorySourceScope.Crm, Assert.Single(settings.AllowedSourceScopes));
        Assert.Empty(settings.ProviderBindings);
    }
}
