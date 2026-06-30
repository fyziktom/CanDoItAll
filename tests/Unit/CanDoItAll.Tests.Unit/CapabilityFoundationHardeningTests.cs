using System.Net;
using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Capabilities.Access;
using CanDoItAll.AgentFramework.Mcp;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.AgentFramework.Skills;
using CanDoItAll.AgentFramework.Skills.Abstractions;
using CanDoItAll.AgentFramework.Tools;
using CanDoItAll.AgentFramework.Tools.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class CapabilityFoundationHardeningTests
{
    [Fact]
    public async Task SB05_INV_DIAGNOSTICS_001_external_http_status_masks_bearer_assignment_and_preserves_typed_shape()
    {
        var descriptor = ToolDescriptorFactory.ExternalHttp(
            CapabilityKey.Create("external-http-audit"),
            RuntimeToolName.Create("external_http_audit"),
            ImplementationKey.Create("external.http-audit"),
            HttpMethod.Post,
            new Uri("https://example.test/audit"),
            new Dictionary<string, string> { ["Authorization"] = "Bearer raw-secret-value" },
            timeout: TimeSpan.FromSeconds(5),
            maxResponseBytes: 512,
            requiredOutputProperties: ["ok"]);
        var invoker = new ExternalHttpToolInvoker(new FakeHttpTransport(new ExternalHttpResponse(
            HttpStatusCode.BadGateway,
            """{"error":"upstream failed","token":"raw-secret-value","padding":"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz"}""")));

        var result = await invoker.InvokeAsync(
            descriptor,
            ToolInvocationRequest.Create(descriptor.Identity, descriptor.ImplementationKey, """{"input":true}""", "SB05_INV_DIAGNOSTICS_001"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(CapabilityDiagnosticCategory.HttpStatus, diagnostic.Category);
        Assert.Equal(CapabilityKind.Tool, diagnostic.CapabilityKind);
        Assert.Equal(descriptor.Identity.Key, diagnostic.CapabilityKey);
        Assert.Equal(CapabilityTransportKind.ExternalHttp, diagnostic.Transport);
        Assert.Equal((int)HttpStatusCode.BadGateway, diagnostic.HttpStatusCode);
        Assert.Equal("SB05_INV_DIAGNOSTICS_001", diagnostic.CorrelationId);
        Assert.Equal("$.statusCode", diagnostic.FieldPath);
        Assert.DoesNotContain("raw-secret-value", diagnostic.MaskedDetail, StringComparison.Ordinal);
        Assert.True(diagnostic.MaskedDetail.Length <= 200);
        Assert.Contains("Repair", diagnostic.RepairHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SB05_INV_DIAGNOSTICS_002_external_process_cancellation_returns_typed_diagnostic()
    {
        var descriptor = ProcessDescriptor();
        var invoker = new ExternalProcessToolInvoker(new FakeProcessRunner(new OperationCanceledException()));

        var result = await invoker.InvokeAsync(
            descriptor,
            ToolInvocationRequest.Create(descriptor.Identity, descriptor.ImplementationKey, """{"input":true}""", "SB05_INV_DIAGNOSTICS_002"),
            CancellationToken.None);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.False(result.IsSuccess);
        Assert.Equal(CapabilityDiagnosticCategory.Cancellation, diagnostic.Category);
        Assert.Equal(CapabilityKind.Tool, diagnostic.CapabilityKind);
        Assert.Equal(descriptor.Identity.Key, diagnostic.CapabilityKey);
        Assert.Equal(CapabilityTransportKind.ExternalProcess, diagnostic.Transport);
        Assert.Equal(TimeSpan.FromSeconds(5), diagnostic.Timeout);
        Assert.Equal("SB05_INV_DIAGNOSTICS_002", diagnostic.CorrelationId);
        Assert.Contains("cancelled", diagnostic.MaskedDetail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Retry", diagnostic.RepairHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SB05_INV_DIAGNOSTICS_003_external_http_cancellation_returns_typed_diagnostic()
    {
        var descriptor = HttpDescriptor();
        var invoker = new ExternalHttpToolInvoker(new FakeHttpTransport(new OperationCanceledException()));

        var result = await invoker.InvokeAsync(
            descriptor,
            ToolInvocationRequest.Create(descriptor.Identity, descriptor.ImplementationKey, """{"input":true}""", "SB05_INV_DIAGNOSTICS_003"),
            CancellationToken.None);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.False(result.IsSuccess);
        Assert.Equal(CapabilityDiagnosticCategory.Cancellation, diagnostic.Category);
        Assert.Equal(CapabilityKind.Tool, diagnostic.CapabilityKind);
        Assert.Equal(descriptor.Identity.Key, diagnostic.CapabilityKey);
        Assert.Equal(CapabilityTransportKind.ExternalHttp, diagnostic.Transport);
        Assert.Equal(TimeSpan.FromSeconds(5), diagnostic.Timeout);
        Assert.Equal("SB05_INV_DIAGNOSTICS_003", diagnostic.CorrelationId);
    }

    [Fact]
    public async Task SB05_INV_DIAGNOSTICS_004_external_http_exception_masks_authorization_bearer_assignment()
    {
        var descriptor = HttpDescriptor();
        var invoker = new ExternalHttpToolInvoker(new FakeHttpTransport(new InvalidOperationException(
            "remote setup failed Authorization=Bearer raw-secret-value")));

        var result = await invoker.InvokeAsync(
            descriptor,
            ToolInvocationRequest.Create(descriptor.Identity, descriptor.ImplementationKey, """{"input":true}""", "SB05_INV_DIAGNOSTICS_004"),
            CancellationToken.None);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.False(result.IsSuccess);
        Assert.Equal(CapabilityDiagnosticCategory.HttpStatus, diagnostic.Category);
        Assert.Equal(CapabilityTransportKind.ExternalHttp, diagnostic.Transport);
        Assert.DoesNotContain("raw-secret-value", diagnostic.MaskedDetail, StringComparison.Ordinal);
    }

    [Fact]
    public void SB05_INV_POLICY_001_deny_required_and_require_rule_failures_are_deterministic()
    {
        var candidate = ToolExposureDescriptorFactory.Create(ToolDescriptorFactory.Internal(
            CapabilityKey.Create("workspace-write-file"),
            RuntimeToolName.Create("workspace_write_file"),
            ImplementationKey.Create("workspace.write-file"),
            [CapabilityTag.Create("workspace"), CapabilityTag.Create("mutation")],
            [CapabilityOperationClassification.Mutation],
            new CapabilitySideEffectProfile(CapabilitySideEffectKind.WorkspaceWrite, true, true)));
        var policy = new CapabilityAccessPolicy(
        [
            new CapabilityAccessRule(
                CapabilityRuleId.Create("allow-mutation"),
                CapabilityAccessEffect.Allow,
                CapabilityAccessScope.ProcessStep,
                CapabilitySelector.ByTag(CapabilityTag.Create("mutation")),
                "Assigned mutation tools stay candidates."),
            new CapabilityAccessRule(
                CapabilityRuleId.Create("deny-mutation"),
                CapabilityAccessEffect.Deny,
                CapabilityAccessScope.ProcessStep,
                CapabilitySelector.ByOperationClassification(CapabilityOperationClassification.Mutation),
                "This step is read-only."),
            new CapabilityAccessRule(
                CapabilityRuleId.Create("require-future"),
                CapabilityAccessEffect.Require,
                CapabilityAccessScope.ProcessStep,
                CapabilitySelector.ByTag(CapabilityTag.Create("future-required")),
                "A future-required capability must be assigned.")
        ]);

        var result = new CapabilityAccessPolicyEvaluator().Evaluate(new CapabilityAccessEvaluationContext(
            [candidate],
            [candidate.Identity],
            [policy],
            "SB05_INV_POLICY_001"));

        Assert.Empty(result.AllowedCapabilities);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Identity == candidate.Identity &&
            diagnostic.RuleId == CapabilityRuleId.Create("deny-mutation") &&
            diagnostic.Category == CapabilityDiagnosticCategory.RequiredCapabilityDenied);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.RuleId == CapabilityRuleId.Create("require-future") &&
            diagnostic.Category == CapabilityDiagnosticCategory.RequiredCapabilityDenied &&
            diagnostic.SelectorKind == CapabilitySelectorKind.Tag);
    }

    [Fact]
    public void SB05_INV_POLICY_002_future_capability_kind_uses_tag_policy_without_evaluator_changes()
    {
        var futureCapability = new CapabilityExposureDescriptor(
            new CapabilityIdentity(CapabilityKind.Memory, CapabilityKey.Create("semantic-memory")),
            "Semantic Memory",
            "Future memory capability.",
            ImplementationKey.Create("memory.semantic"),
            null,
            null,
            null,
            new HashSet<CapabilityTag> { CapabilityTag.Create("external"), CapabilityTag.Create("memory") },
            new HashSet<CapabilityOperationClassification> { CapabilityOperationClassification.Read },
            new CapabilitySideEffectProfile(CapabilitySideEffectKind.WorkspaceRead, false, false),
            CapabilityAvailabilityState.Available,
            TemplatePath.Create("Templates/Capabilities/memory/semantic-memory.json"));
        var policy = new CapabilityAccessPolicy(
        [
            new CapabilityAccessRule(
                CapabilityRuleId.Create("deny-external"),
                CapabilityAccessEffect.Deny,
                CapabilityAccessScope.WorkflowNode,
                CapabilitySelector.ByTag(CapabilityTag.Create("external")),
                "Workflow node forbids external capabilities.")
        ]);

        var result = new CapabilityAccessPolicyEvaluator().Evaluate(new CapabilityAccessEvaluationContext(
            [futureCapability],
            [],
            [policy],
            "SB05_INV_POLICY_002"));

        Assert.Empty(result.AllowedCapabilities);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Identity == futureCapability.Identity &&
            diagnostic.SelectorKind == CapabilitySelectorKind.Tag);
    }

    [Fact]
    public void SB05_INV_EXPOSURE_001_tool_skill_mcp_server_and_mcp_tool_share_policy_metadata_shape()
    {
        var tool = ToolExposureDescriptorFactory.Create(ProcessDescriptor());
        var skill = SkillExposureDescriptorFactory.Create(SkillDescriptorFactory.Inline(
            CapabilityKey.Create("workspace-delivery-skill"),
            "Workspace Delivery Skill",
            "Skill for workspace delivery.",
            "workspace-delivery",
            "Deliver workspace changes.",
            [],
            tags: [CapabilityTag.Create("implementation")],
            operationClassifications: [CapabilityOperationClassification.Read]));
        var mcpServerDescriptor = McpDescriptorFactory.LocalStdio(
            CapabilityKey.Create("playwright-local-mcp"),
            McpServerKey.Create("playwright-local"),
            "Playwright Local MCP",
            "Local browser automation MCP.",
            command: "node",
            arguments: ["@playwright/mcp"],
            workingDirectory: ".",
            allowedWorkingDirectories: [],
            allowedTools: [McpToolName.Create("browser_snapshot")],
            environmentVariableBindings: new Dictionary<string, string>(),
            rawEnvironmentVariables: new Dictionary<string, string>(),
            approvalMode: McpApprovalMode.AlwaysRequire,
            timeout: TimeSpan.FromSeconds(5),
            operationClassifications: [CapabilityOperationClassification.McpTool, CapabilityOperationClassification.BrowserAccess]);
        var mcpServer = McpExposureDescriptorFactory.CreateServer(mcpServerDescriptor);
        var mcpTool = McpExposureDescriptorFactory.CreateTool(
            mcpServerDescriptor,
            new DiscoveredMcpTool(McpToolName.Create("browser_snapshot"), "Snapshot page state."));

        foreach (var descriptor in new[] { tool, skill, mcpServer, mcpTool })
        {
            Assert.NotEqual(default, descriptor.Identity);
            Assert.NotEmpty(descriptor.DisplayName);
            Assert.NotEmpty(descriptor.Tags);
            Assert.NotEmpty(descriptor.OperationClassifications);
            Assert.NotNull(descriptor.SideEffectProfile);
            Assert.NotNull(descriptor.SourcePath);
        }

        Assert.Equal(McpServerKey.Create("playwright-local"), mcpTool.McpServerKey);
        Assert.Equal(McpToolName.Create("browser_snapshot"), mcpTool.McpToolName);
    }

    private static ExternalProcessToolDescriptor ProcessDescriptor()
    {
        return ToolDescriptorFactory.ExternalProcess(
            CapabilityKey.Create("external-audit-tool"),
            RuntimeToolName.Create("external_audit"),
            ImplementationKey.Create("external.audit"),
            executablePath: "fake-audit.exe",
            arguments: ["--json"],
            workingDirectory: ".",
            timeout: TimeSpan.FromSeconds(5),
            maxOutputBytes: 128,
            allowedExecutableNames: ["fake-audit.exe"],
            requiredOutputProperties: ["ok"]);
    }

    private static ExternalHttpToolDescriptor HttpDescriptor()
    {
        return ToolDescriptorFactory.ExternalHttp(
            CapabilityKey.Create("external-http-audit"),
            RuntimeToolName.Create("external_http_audit"),
            ImplementationKey.Create("external.http-audit"),
            HttpMethod.Post,
            new Uri("https://example.test/audit"),
            new Dictionary<string, string>(),
            timeout: TimeSpan.FromSeconds(5),
            maxResponseBytes: 128,
            requiredOutputProperties: ["ok"]);
    }

    private sealed class FakeProcessRunner(Exception exception) : IExternalProcessRunner
    {
        public Task<ExternalProcessRunResult> RunAsync(
            ExternalProcessRunRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromException<ExternalProcessRunResult>(exception);
        }
    }

    private sealed class FakeHttpTransport : IExternalHttpTransport
    {
        private readonly ExternalHttpResponse? response;
        private readonly Exception? exception;

        public FakeHttpTransport(ExternalHttpResponse response)
        {
            this.response = response;
        }

        public FakeHttpTransport(Exception exception)
        {
            this.exception = exception;
        }

        public Task<ExternalHttpResponse> SendAsync(
            ExternalHttpRequest request,
            CancellationToken cancellationToken)
        {
            if (exception is not null)
            {
                return Task.FromException<ExternalHttpResponse>(exception);
            }

            return Task.FromResult(response ?? throw new InvalidOperationException("Fake HTTP response was not configured."));
        }
    }
}
