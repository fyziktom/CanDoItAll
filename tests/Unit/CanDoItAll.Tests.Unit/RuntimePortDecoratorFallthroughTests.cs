using static CanDoItAll.Tests.Support.ProductToolPolicyTestRegistration;
using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.Modules.AgentFramework.Hosting;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Unit.AgentFramework;

/// <summary>
/// SB18 negative-path proof for the port-level mock/scenario decorators: the deterministic
/// interception cores no longer implement any runtime interface or hold an inner fallback
/// themselves (deleted with <c>IAgentRuntime</c>), so the decorators alone own the
/// provider-matching branch. These tests fail if a decorator ever falls through to the inner port
/// for its own matched provider, or if it stops delegating for every other provider.
/// </summary>
public sealed class RuntimePortDecoratorFallthroughTests
{
    [Theory]
    [InlineData(AgentSavedEffectDispatchSupport.Unsupported, false)]
    [InlineData(AgentSavedEffectDispatchSupport.OriginalPreparedOwnerEffect, false)]
    [InlineData(AgentSavedEffectDispatchSupport.OriginalPreparedOwnerEffect, true)]
    public async Task Production_decorator_chain_preserves_saved_dispatch_capability_identity_and_cancellation(
        AgentSavedEffectDispatchSupport support, bool cancelled) {
        var binding = new AgentToolApprovalBinding(new(Guid.NewGuid()), new(Guid.NewGuid()), 1,
            AgentToolProtocolEnvelope.Create("saved-decorator-fixture", 1, "{}").Digest);
        var request = new AgentSavedEffectDispatchRequest(new(Guid.NewGuid(), Guid.NewGuid(), AgentExecutionAuthorityId.Create()), binding);
        var result = new AgentSavedEffectDispatchResult(binding, AgentToolProposalState.Completed,
            AgentToolEffectState.Committed, 7, 6);
        var inner = new SavedDispatchProbe(support, result);
        var execution = DispatchProxy.Create<IAgentExecutionRuntime, ThrowingProxy>();
        var scenario = new ScenarioHarnessExecutionDecorator(execution, inner, null!);
        var decorated = new ProcessMockExecutionDecorator(scenario, scenario, null!);
        using var cancellation = new CancellationTokenSource();
        if (cancelled) {
            cancellation.Cancel();
        }
        Assert.Equal(support, decorated.SavedEffectDispatchSupport);
        if (support == AgentSavedEffectDispatchSupport.Unsupported) {
            await Assert.ThrowsAsync<NotSupportedException>(() => decorated.DispatchSavedEffectAsync(request, cancellation.Token));
        } else if (cancelled) {
            var failure = await Assert.ThrowsAsync<OperationCanceledException>(() => decorated.DispatchSavedEffectAsync(request, cancellation.Token));
            Assert.Equal(cancellation.Token, failure.CancellationToken);
        } else {
            Assert.Same(result, await decorated.DispatchSavedEffectAsync(request, cancellation.Token));
        }
        Assert.Equal(1, inner.Calls);
        Assert.Same(request, inner.Request);
        Assert.Equal(cancellation.Token, inner.Token);
    }

    private sealed class SavedDispatchProbe(AgentSavedEffectDispatchSupport support, AgentSavedEffectDispatchResult result)
        : IAgentContinuationRuntime {
        public AgentSavedEffectDispatchSupport SavedEffectDispatchSupport => support;
        internal int Calls { get; private set; }
        internal AgentSavedEffectDispatchRequest? Request { get; private set; }
        internal CancellationToken Token { get; private set; }

        public Task<AgentSavedEffectDispatchResult> DispatchSavedEffectAsync(AgentSavedEffectDispatchRequest request,
            CancellationToken cancellationToken = default) {
            Calls++;
            Request = request;
            Token = cancellationToken;
            if (support == AgentSavedEffectDispatchSupport.Unsupported) {
                throw new NotSupportedException("Saved publication is unavailable.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result);
        }

        public Task<AgentRuntimeResponse> ContinueAsync(AgentRuntimeContinuationRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Saved dispatch cannot enter ordinary model continuation.");
    }

    [Fact]
    public async Task ProcessMock_diagnostics_decorator_never_reaches_inner_for_the_process_mock_provider()
    {
        var processMock = new ProcessMockAgentRuntime(
            DispatchProxy.Create<IWorkspaceFileService, ThrowingProxy>(),
            "unused-root",
            Options.Create(new ProcessMockAgentOptions { Enabled = true }),
            ProductToolPolicies);
        var decorator = new ProcessMockDiagnosticsDecorator(
            DispatchProxy.Create<IProviderDiagnosticsRuntime, ThrowingProxy>(),
            DispatchProxy.Create<IProviderModelAdministrationRuntime, ThrowingProxy>(),
            processMock);

        var health = await decorator.TestHealthAsync(CreateProvider(ProcessMockAgentCatalog.ProviderBaseUrl));

        Assert.True(health.Success);
    }

    [Fact]
    public async Task ProcessMock_diagnostics_decorator_delegates_to_inner_for_every_other_provider()
    {
        var processMock = new ProcessMockAgentRuntime(
            DispatchProxy.Create<IWorkspaceFileService, ThrowingProxy>(),
            "unused-root",
            Options.Create(new ProcessMockAgentOptions { Enabled = true }),
            ProductToolPolicies);
        var decorator = new ProcessMockDiagnosticsDecorator(
            DispatchProxy.Create<IProviderDiagnosticsRuntime, ThrowingProxy>(),
            DispatchProxy.Create<IProviderModelAdministrationRuntime, ThrowingProxy>(),
            processMock);

        var exception = await Record.ExceptionAsync(
            () => decorator.TestHealthAsync(CreateProvider("https://provider.example")));

        Assert.NotNull(exception);
        Assert.Contains(nameof(IProviderDiagnosticsRuntime.TestHealthAsync), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScenarioHarness_diagnostics_decorator_never_reaches_inner_for_the_scenario_provider()
    {
        var decorator = new ScenarioHarnessDiagnosticsDecorator(
            DispatchProxy.Create<IProviderDiagnosticsRuntime, ThrowingProxy>(),
            DispatchProxy.Create<IProviderModelAdministrationRuntime, ThrowingProxy>());

        var health = await decorator.TestHealthAsync(CreateProvider(ScenarioHarnessCatalog.ProviderBaseUrl));

        Assert.True(health.Success);
    }

    [Fact]
    public async Task ScenarioHarness_diagnostics_decorator_delegates_to_inner_for_every_other_provider()
    {
        var decorator = new ScenarioHarnessDiagnosticsDecorator(
            DispatchProxy.Create<IProviderDiagnosticsRuntime, ThrowingProxy>(),
            DispatchProxy.Create<IProviderModelAdministrationRuntime, ThrowingProxy>());

        var exception = await Record.ExceptionAsync(
            () => decorator.TestHealthAsync(CreateProvider("https://provider.example")));

        Assert.NotNull(exception);
        Assert.Contains(nameof(IProviderDiagnosticsRuntime.TestHealthAsync), exception.Message, StringComparison.Ordinal);
    }

    private static ProviderProfile CreateProvider(string baseUrl)
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            "Decorator fallthrough provider",
            ProviderKind.OpenAi,
            baseUrl,
            ApiKeyEnvironmentVariable: string.Empty,
            DefaultModel: "decorator-fallthrough-model",
            ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels: []);
    }

    // DispatchProxy generates a derived type at runtime, so the base proxy type must not be sealed.
    public class ThrowingProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => throw new InvalidOperationException(
                $"{targetMethod?.Name} must not be called for a provider the decorator itself owns.");
    }
}
