using System.Text.Json;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.Security;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

[Trait("Category", "HostPlatform")]
public sealed class AgentEditorSecretReferenceTests {
    [Fact]
    public async Task Actual_editor_selects_and_saves_secret_metadata_without_resolving_a_value() {
        const string sentinel = "test-only-editor-secret-value-not-for-rendering";
        var vault = new ReadRejectingVault();
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton<ISecretVault>(vault));
        var services = harness.Context.Services;
        var created = await services.GetRequiredService<SecretService>().SaveAsync(new() {
            Name = "Private editor fixture reference", Kind = SecretKind.Token, SecretValue = sentinel
        });
        Assert.True(created.IsSuccess);
        var beforeReads = vault.Reads;
        var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var id = await workspace.SaveAgentAsync(new() { Name = "Secret reference editor" });
        var cut = harness.Context.Render<AgentDetailsDialog>(p => p.Add(x => x.AgentId, id)
            .Add(x => x.InitialProviders, Array.Empty<ProviderProfile>()).Add(x => x.Section, AgentEditorSection.Secrets));
        var selector = $"input[data-secret-id='{created.Value:D}']";
        cut.WaitForElement(selector).Change(true);
        Assert.Empty((await workspace.GetAgentEditorAsync(id)).AllowedSecretReferences);
        await cut.Find("form").SubmitAsync();
        var saved = await workspace.GetAgentEditorAsync(id);
        var reference = Assert.Single(saved.AllowedSecretReferences);
        Assert.Equal(created.Value, reference.SecretId);
        Assert.Equal(AgentSecretPurposes.GeneralAgentRequest, reference.Purpose);
        Assert.Equal(beforeReads, vault.Reads);
        Assert.DoesNotContain(sentinel, cut.Markup);
        Assert.DoesNotContain(sentinel, JsonSerializer.Serialize(saved));
    }

    private sealed class ReadRejectingVault : ISecretVault {
        public int Reads { get; private set; }
        public Task SetAsync(string key, string value, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string?> GetAsync(string key, CancellationToken ct = default) {
            Reads++;
            throw new InvalidOperationException("Secret values must not be read by the editor.");
        }
    }
}
