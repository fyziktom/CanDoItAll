using AngleSharp.Html.Dom;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace.Pages;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class SecretProviderSelectionTests
{
    [Fact]
    public async Task Stored_secret_is_copyable_by_id_and_selectable_in_agent_provider_editor()
    {
        await using var environment = CanDoItAllTestEnvironment.Create("secret-provider-selection");
        var profile = environment.CreateInMemoryProfile("primary");
        await using var harness = await ComponentTestHarness.CreateAsync(options: new TestHarnessOptions
        {
            TestEnvironment = environment,
            ActiveProfile = profile,
            ConfigurationOverrides = new Dictionary<string, string?>
            {
                ["ControlPlane:RootPath"] = environment.ControlPlaneRootPath
            }
        });
        var secretId = await SaveSecretAsync(harness);

        harness.Context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo("http://localhost/settings?tab=secrets");

        var settings = harness.Context.Render<SettingsPage>();
        var copyTestId = $"settings-secret-copy-id-{secretId:N}";
        settings.WaitForElement($"[data-testid='{copyTestId}']");
        var copyButton = settings.FindComponents<CopyButton>()
            .Single(component => component.Markup.Contains(copyTestId, StringComparison.Ordinal));

        Assert.Equal(secretId.ToString("D"), copyButton.Instance.Value);
        Assert.Contains("Copy ID", copyButton.Markup, StringComparison.Ordinal);

        var providers = harness.Context.Render<AgentProviderProfilesPanel>();
        providers.WaitForAssertion(() =>
        {
            var select = (IHtmlSelectElement)providers.Find("[data-testid='providers-api-key-input']");
            var expectedReference = $"secret:{secretId:D}";
            var secretOption = Assert.Single(
                select.Options,
                option => string.Equals(option.Value, expectedReference, StringComparison.Ordinal));

            Assert.Equal("OpenAI API key", secretOption.Text);
            Assert.DoesNotContain("Secret record reference", providers.Markup, StringComparison.Ordinal);
        });
    }

    private static async Task<Guid> SaveSecretAsync(ComponentTestHarness harness)
    {
        var secretService = harness.Context.Services.GetRequiredService<SecretService>();
        var result = await secretService.SaveAsync(new SecretEditorModel
        {
            Name = "OpenAI API key",
            Kind = SecretKind.ApiKey,
            SecretValue = "component-test-secret",
            Scope = "workspace"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
