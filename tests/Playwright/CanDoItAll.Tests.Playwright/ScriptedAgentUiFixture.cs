using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workbench;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

using static CanDoItAll.Tests.Playwright.Smoke.AgentUiJourneySupport;

namespace CanDoItAll.Tests.Playwright.Smoke;

internal static class ScriptedAgentUiFixture {
    internal static Task<bool> ConfigureScriptedAgentAsync(LiveUiHost host, Guid agentId, string baseUrl) => host.SeedAsync(async services => {
                var secret = await services.GetRequiredService<SecretService>().SaveAsync(new SecretEditorModel {
                    Name = "Synthetic Agent fixture credential", Kind = SecretKind.ApiKey, SecretValue = "local-fixture-credential", Scope = "workspace"
                });
                Assert.True(secret.IsSuccess);
                var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
                var provider = await workspace.SaveProviderAsync(new ProviderProfileEditorModel {
                    Name = "Scripted external Agent provider", Kind = ProviderKind.OpenAi, Transport = ProviderTransportKind.Responses,
                    BaseUrl = baseUrl, ApiKeyEnvironmentVariable = $"secret:{secret.Value:D}",
                    DefaultModel = ManagedSeedProviderFallbacks.OpenAiDefaultModel, SuggestedModels = [ManagedSeedProviderFallbacks.OpenAiDefaultModel],
                    SupportsStreaming = true, SupportsTools = true,
                    ModelPrices = [new() { Model = ManagedSeedProviderFallbacks.OpenAiDefaultModel, TariffKind = ProviderTariffKind.ExplicitFree }]
                });
                var agent = await workspace.GetAgentEditorAsync(agentId);
                agent.ProviderProfileId = provider;
                agent.Model = ManagedSeedProviderFallbacks.OpenAiDefaultModel;
                await workspace.SaveAgentAsync(agent);
                return true;
            });

}
