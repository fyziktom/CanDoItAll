using Bunit;
using CanDoItAll.Editing;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

internal static class AgentDecisionPolicyFixture {
    public static void AddPolicyOpeningFixture(this BunitContext context) {
        context.Services.AddAgentConfigurationFixture();
        context.Services.AddSingleton(provider => new AgentConfigurationOpening(provider.GetRequiredService<AgentConfigurationChanges>(),
            EditingOwner.AgentDefinition, null, Guid.NewGuid(), () => true));
    }

    public static void OpenPolicyScope(this BunitContext context) => context.Services.GetRequiredService<AgentConfigurationOpening>()
        .StartAsync(_ => Task.CompletedTask, () => Task.CompletedTask, catalog: true).GetAwaiter().GetResult();

    public static IRenderedComponent<AgentDecisionPolicyDialog> RenderPolicy(this BunitContext context,
        Action<ComponentParameterCollectionBuilder<AgentDecisionPolicyDialog>> configure)
        => context.Render<AgentDecisionPolicyDialog>(parameters => {
            parameters.Add(component => component.Opening, context.Services.GetRequiredService<AgentConfigurationOpening>());
            configure(parameters);
        });

    public static IRenderedComponent<AgentDecisionPolicyRecoveryDialog> RenderPolicyRecovery(this BunitContext context,
        Action<ComponentParameterCollectionBuilder<AgentDecisionPolicyRecoveryDialog>>? configure = null)
        => context.Render<AgentDecisionPolicyRecoveryDialog>(parameters => {
            parameters.Add(component => component.Opening, context.Services.GetRequiredService<AgentConfigurationOpening>());
            configure?.Invoke(parameters);
        });
}
