using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web.Api;
using Microsoft.Extensions.DependencyInjection;
using Editor = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;
using RuntimeProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;
using Kind = CanDoItAll.AgentFramework.Models.ProviderKind;

namespace CanDoItAll.Tests.Integration.Api;

public sealed class ProviderModelMaintenanceApiTests {
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task Model_maintenance_requires_both_scopes_and_preserves_known_remote_completion(bool execute, bool write, bool peer) {
        var diagnostics = DispatchProxy.Create<IProviderDiagnosticsService, DiagnosticsProxy>();
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: true, useInMemoryDatabase: false,
            configureServices: services => services.AddSingleton(diagnostics));
        var completed = new ProviderModelMaintenanceEditorResult("created-model", "base-model", "Owned instruction", 4096, "Owned modelfile", "Created remotely");
        var proxy = (DiagnosticsProxy)(object)diagnostics;
        proxy.Maintenance = async provider => {
            if (peer) {
                await using var peerScope = host.App.Services.CreateAsyncScope();
                var registry = peerScope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
                var draft = await registry.GetProviderEditorAsync(provider.Id);
                draft.DefaultModel = "peer-model";
                draft.SuggestedModels = ["peer-model"];
                await registry.SaveProviderAsync(draft);
            }
            return completed;
        };
        Guid id;
        await using (var scope = host.App.Services.CreateAsyncScope()) {
            id = await scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>().SaveProviderAsync(new Editor {
                Name = $"Owned provider API maintenance {Guid.NewGuid():N}", Kind = Kind.Ollama,
                BaseUrl = "http://127.0.0.1:11434", DefaultModel = "base-model", SuggestedModels = ["base-model"],
                Transport = ProviderTransportKind.ChatCompletions, IsEnabled = true
            });
        }
        List<string> scopes = [];
        if (execute) {
            scopes.Add(ApiAccessScopeNames.ExecuteAgents);
        }
        if (write) {
            scopes.Add(ApiAccessScopeNames.WriteAgents);
        }
        var token = host.App.Services.GetRequiredService<IApiTokenService>().IssueToken(new ApiTokenIssueRequest {
            Subject = "owned-provider-maintenance-api", DisplayName = "Owned provider maintenance API", Scopes = scopes
        });
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(token.TokenType, token.Token);
        using var response = await host.Client.PostAsJsonAsync($"/api/agents/providers/{id:D}/ollama-modelfile",
            new ProviderModelMaintenanceEditorRequest("base-model", "created-model", "Owned instruction", 4096));
        if (!execute || !write) {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(0, proxy.Calls);
            return;
        }
        Assert.Equal(1, proxy.Calls);
        Assert.Equal(peer ? HttpStatusCode.Conflict : HttpStatusCode.OK, response.StatusCode);
        if (peer) {
            var outcome = await response.Content.ReadFromJsonAsync<ProviderModelMaintenancePersistenceApiResponse>();
            Assert.NotNull(outcome);
            Assert.True(outcome.RemoteOperationCompleted);
            Assert.False(outcome.AutomaticReplaySafe);
            Assert.Equal(ProviderModelMaintenancePersistenceState.Conflict, outcome.ConfigurationState);
            Assert.Equal("created-model", outcome.Result.ModelName);
            Assert.Equal(id, outcome.ProviderId);
            Assert.True(response.Headers.CacheControl!.NoStore);
        }
        await using var readScope = host.App.Services.CreateAsyncScope();
        var current = await readScope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>().GetProviderEditorAsync(id);
        Assert.Equal(peer ? "peer-model" : "created-model", current.DefaultModel);
    }

    public class DiagnosticsProxy : DispatchProxy {
        public int Calls { get; private set; }
        public Func<RuntimeProfile, Task<ProviderModelMaintenanceEditorResult>> Maintenance { get; set; } = _ => throw new InvalidOperationException();
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            if (targetMethod?.Name != nameof(IProviderDiagnosticsService.CreateOrUpdateProviderModelAsync)) {
                throw new InvalidOperationException("This fixture dispatches only the requested remote model operation.");
            }
            Calls++;
            return Maintenance((RuntimeProfile)args![0]!);
        }
    }
}
