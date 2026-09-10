using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Agents.SimpleChats;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class AgentRecoveryApiIntegrationTests {
    public enum RecoveryOperation {
        Recover,
        ReconcileCancellation
    }

    [Theory]
    [InlineData(RecoveryOperation.Recover)]
    [InlineData(RecoveryOperation.ReconcileCancellation)]
    public async Task Recovery_routes_require_authorized_API_identity_before_owner_dispatch(RecoveryOperation operation) {
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, RecoveryWorkspace>();
        var probe = (RecoveryWorkspace)(object)workspace;
        await using var host = await CreateHostAsync(workspace, jwtEnabled: true);
        var route = Route(operation, Guid.NewGuid());

        using var anonymous = await host.Client.PostAsJsonAsync(route, new AgentExecutionRecoveryApiRequest());
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var token = host.App.Services.GetRequiredService<IApiTokenService>().IssueToken(new ApiTokenIssueRequest {
            Subject = "recovery-read-only-fixture",
            Scopes = [ApiAccessScopeNames.ReadLlmChats]
        });
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(token.TokenType, token.Token);
        using var unrelatedScope = await host.Client.PostAsJsonAsync(route, new AgentExecutionRecoveryApiRequest());

        Assert.Equal(HttpStatusCode.Forbidden, unrelatedScope.StatusCode);
        Assert.Equal(0, probe.Invocations);
        var allowedToken = host.App.Services.GetRequiredService<IApiTokenService>().IssueToken(new ApiTokenIssueRequest {
            Subject = "recovery-authorized-fixture",
            Scopes = [ApiAccessScopeNames.Api]
        });
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(allowedToken.TokenType, allowedToken.Token);
        using var authorized = await host.Client.PostAsJsonAsync(route, new AgentExecutionRecoveryApiRequest());
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
        Assert.Equal(1, probe.Invocations);
    }

    [Theory]
    [InlineData(RecoveryOperation.Recover)]
    [InlineData(RecoveryOperation.ReconcileCancellation)]
    public async Task Recovery_routes_retain_original_identity_activity_and_safe_outcome(RecoveryOperation operation) {
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, RecoveryWorkspace>();
        var probe = (RecoveryWorkspace)(object)workspace;
        await using var host = await CreateHostAsync(workspace);
        var runId = Guid.NewGuid();
        var activity = AgentExecutionOperationId.New();

        using var response = await host.Client.PostAsJsonAsync(Route(operation, runId), new AgentExecutionRecoveryApiRequest(activity));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(runId, probe.RunId);
        Assert.Equal(activity, probe.Activity);
        Assert.Equal(1, probe.Invocations);
        Assert.Equal(activity.Value.ToString("D"), Assert.Single(response.Headers.GetValues(AgentApiHeaderNames.ActivityOperationId)));
        var raw = await response.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(raw);
        Assert.Equal(runId, body.RootElement.GetProperty("executionRunId").GetGuid());
        Assert.Equal(probe.ChatSessionId, body.RootElement.GetProperty("chatSessionId").GetGuid());
        Assert.DoesNotContain(RecoveryWorkspace.PrivateReceipt, raw, StringComparison.Ordinal);
        Assert.DoesNotContain("payloadJson", raw, StringComparison.Ordinal);
        if (operation == RecoveryOperation.ReconcileCancellation) {
            Assert.True(body.RootElement.GetProperty("hasUnknownEffects").GetBoolean());
            var outcomes = body.RootElement.GetProperty("outcomes");
            Assert.Equal(2, outcomes.GetArrayLength());
            Assert.Equal(probe.EffectId, outcomes[0].GetProperty("committedEffect").GetProperty("sourceId").GetString());
            Assert.Equal((int)AgentToolEffectState.Unknown, outcomes[1].GetProperty("effectState").GetInt32());
            Assert.False(outcomes[0].TryGetProperty("receipt", out _));
        }
    }

    [Theory]
    [InlineData(RecoveryOperation.Recover)]
    [InlineData(RecoveryOperation.ReconcileCancellation)]
    public async Task Recovery_routes_preserve_admission_failure_and_run_identity(RecoveryOperation operation) {
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, RecoveryWorkspace>();
        var probe = (RecoveryWorkspace)(object)workspace;
        probe.RejectAdmission = true;
        await using var host = await CreateHostAsync(workspace);
        var runId = Guid.NewGuid();

        using var response = await host.Client.PostAsJsonAsync(Route(operation, runId), new AgentExecutionRecoveryApiRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(runId, body.RootElement.GetProperty("executionRunId").GetGuid());
        Assert.Contains(RecoveryWorkspace.RejectionCode, body.RootElement.GetRawText(), StringComparison.Ordinal);
        Assert.Equal(1, probe.Invocations);
        Assert.NotEqual(Guid.Empty, probe.Activity.Value);
    }

    private static Task<ApiTestHost> CreateHostAsync(IAgentFrameworkWorkspaceService workspace, bool jwtEnabled = false)
        => ApiTestHost.CreateAsync(jwtEnabled, useInMemoryDatabase: true,
            configureServices: services => services.Replace(ServiceDescriptor.Singleton(workspace)));

    private static string Route(RecoveryOperation operation, Guid runId) => operation switch {
        RecoveryOperation.Recover => $"/api/agents/execution-runs/{runId:D}/recover",
        RecoveryOperation.ReconcileCancellation => $"/api/agents/execution-runs/{runId:D}/reconcile-cancellation",
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    public class RecoveryWorkspace : DispatchProxy {
        public const string PrivateReceipt = "private-owner-protocol-fixture";
        public const string RejectionCode = "tool-admission.recovery-test-rejected";
        public int Invocations { get; private set; }
        public Guid RunId { get; private set; }
        public AgentExecutionOperationId Activity { get; private set; }
        public Guid ChatSessionId { get; } = Guid.NewGuid();
        public string EffectId { get; } = Guid.NewGuid().ToString("D");
        public bool RejectAdmission { get; set; }

        protected override object? Invoke(MethodInfo? method, object?[]? args) {
            if (method?.Name is not (nameof(IAgentFrameworkWorkspaceService.RecoverExecutionRunAsync)
                or nameof(IAgentFrameworkWorkspaceService.ReconcileCancelledExecutionRunAsync))) {
                throw new InvalidOperationException("Unexpected recovery API fixture dispatch.");
            }

            Invocations++;
            RunId = (Guid)args![0]!;
            Activity = (AgentExecutionOperationId)args[1]!;
            if (RejectAdmission) {
                throw new AgentToolAdmissionException(RejectionCode, "The original admission is unavailable.");
            }

            if (method.Name == nameof(IAgentFrameworkWorkspaceService.RecoverExecutionRunAsync)) {
                return Task.FromResult(new ExecutionRunResult(RunId, ChatSessionId, "Recovered original run", null,
                    new AgentRunMetric(Guid.NewGuid(), Guid.NewGuid(), ChatSessionId, DateTimeOffset.UtcNow,
                        RunOutcome.Succeeded, "Fixture", "fixture-model", 1, 0, 0, 0)) { State = ExecutionState.Completed });
            }

            return Task.FromResult(new AgentToolRunCancellationReconciliation(RunId, ChatSessionId, [
                new(new(Guid.NewGuid()), HrSimpleChatToolPolicy.Get(HrSimpleChatOperation.Create).ToolName, AgentToolEffectState.Committed,
                    new(AgentToolCancellationDisposition.ReceiptCommitted, AgentToolCancellationReason.ReceiptFound,
                        new("simple-chat-definition", EffectId),
                        AgentToolProtocolEnvelope.Create("fixture", 1, JsonSerializer.Serialize(new { value = PrivateReceipt })))),
                new(new(Guid.NewGuid()), HrSimpleChatToolPolicy.Get(HrSimpleChatOperation.Create).ToolName, AgentToolEffectState.Unknown,
                    new(AgentToolCancellationDisposition.CancelledUnreconciled, AgentToolCancellationReason.ReceiptNotObserved))
            ]));
        }
    }
}
