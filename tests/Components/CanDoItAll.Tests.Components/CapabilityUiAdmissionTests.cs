using Bunit;
using CanDoItAll.AgentFramework.CapabilityAuthoring.UI;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Editing;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class CapabilityUiAdmissionTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Capability_surfaces_read_no_catalog_during_prerender_or_without_original_access(bool interactive, bool authoring) {
        var reads = new CountingReads();
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(new EditingUiTestAccess { Allowed = false });
            services.AddSingleton<IAgentCapabilitiesReads>(reads);
        });
        harness.Context.SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo(interactive ? "Server" : "Static", interactive));
        if (authoring) {
            var cut = harness.Context.Render<NativeCapabilityAuthoringHost>(parameters => parameters
                .Add(component => component.Mode, CapabilityAuthoringMode.Details)
                .Add(component => component.CapabilityId, Guid.NewGuid()));
            Assert.Empty(cut.FindAll("form"));
        } else {
            harness.Context.Render<AgentCapabilitiesPanel>();
        }
        Assert.Equal(0, reads.Calls);
    }

    private sealed class CountingReads : IAgentCapabilitiesReads {
        public int Calls { get; private set; }
        public Task<AgentCapabilitiesCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default) {
            Calls++;
            return Task.FromResult(new AgentCapabilitiesCatalog([], []));
        }
        public Task<AgentEditorModel> ReadEditorAsync(Guid agentId, CancellationToken cancellationToken = default) {
            Calls++;
            throw new InvalidOperationException("No protected editor read is expected.");
        }
    }
}
