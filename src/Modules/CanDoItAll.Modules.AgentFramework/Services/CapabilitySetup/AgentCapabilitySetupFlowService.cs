using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Capabilities.Templates;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tools.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.AgentFramework;

public sealed partial class AgentCapabilitySetupFlowService(
    IAgentFrameworkWorkspaceService workspaceService,
    IToolSetupTestService toolSetupTestService,
    ICapabilityAccessPolicyEvaluator accessPolicyEvaluator,
    IServiceProvider serviceProvider) : IAgentCapabilitySetupFlowService
{
    private const int DefaultTimeoutSeconds = 30;
    private const int DefaultMaxPayloadBytes = 4096;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<CapabilitySetupTestResult> TestToolSetupAsync(
        CapabilityToolSetupTestRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var correlationId = ResolveCorrelationId(request.CorrelationId, "tool-setup");
        if (!TryBuildExternalToolDescriptor(request.Capability, correlationId, out var descriptor, out var diagnostics))
        {
            return ToolSetupFailure(correlationId, diagnostics);
        }

        if (!IsValidJsonInput(request.JsonInput, correlationId, descriptor.Identity, out var inputDiagnostic))
        {
            return new CapabilitySetupTestResult(false, descriptor.Identity, correlationId, [inputDiagnostic]);
        }

        return descriptor switch
        {
            ExternalProcessToolDescriptor processDescriptor => await toolSetupTestService.TestProcessToolAsync(
                processDescriptor,
                request.JsonInput,
                correlationId,
                cancellationToken),
            ExternalHttpToolDescriptor httpDescriptor => await toolSetupTestService.TestHttpToolAsync(
                httpDescriptor,
                request.JsonInput,
                correlationId,
                cancellationToken),
            _ => new CapabilitySetupTestResult(
                false,
                descriptor.Identity,
                correlationId,
                [Diagnostic(
                    CapabilityDiagnosticCategory.ImplementationMissing,
                    descriptor.Identity,
                    "$.toolKind",
                    "Only external process and external HTTP tools support setup test calls.",
                    "Switch the tool setup kind to externalProcess or externalHttp before running a setup test.",
                    correlationId,
                    implementationKey: descriptor.ImplementationKey)])
        };
    }

    public async Task<McpSetupTestResult> TestMcpSetupAsync(
        CapabilityMcpSetupTestRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var correlationId = ResolveCorrelationId(request.CorrelationId, "mcp-setup");
        var descriptor = BuildMcpDescriptor(request.Capability, correlationId, out var diagnostics);
        if (diagnostics.Count > 0)
        {
            return McpSetupTestResult.Failure(descriptor, correlationId, diagnostics);
        }

        var mcpSetupTestService = serviceProvider.GetService<IMcpSetupTestService>();
        if (mcpSetupTestService is null)
        {
            return McpSetupTestResult.Failure(
                descriptor,
                correlationId,
                [Diagnostic(
                    CapabilityDiagnosticCategory.ImplementationMissing,
                    descriptor.Identity,
                    "$.mcpSetupService",
                    "No MCP setup-test runtime client factory is registered for this application host.",
                    "Register IMcpSetupTestService with an IMcpClientFactory before running live MCP start/list-tools tests.",
                    correlationId,
                    mcpServerKey: descriptor.ServerKey,
                    transport: ResolveMcpTransport(descriptor))]);
        }

        return await mcpSetupTestService.TestAsync(descriptor, correlationId, cancellationToken);
    }

    public async Task<CapabilityAccessPreviewResult> PreviewAccessAsync(
        CapabilityAccessPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var correlationId = ResolveCorrelationId(request.CorrelationId, "access-preview");
        var capabilities = await ResolvePreviewCapabilitiesAsync(request, cancellationToken);
        var validationIssues = new List<CapabilityValidationIssue>();
        var candidates = new List<CapabilityExposureDescriptor>();

        foreach (var capability in capabilities)
        {
            candidates.AddRange(BuildExposureDescriptors(capability, validationIssues));
        }

        var required = ReadRequiredCapabilities(request.RequiredCapabilities, validationIssues);
        var policyCompilation = new CapabilityAccessPolicyTemplateCompiler()
            .Compile(request.Policy, TemplatePath.Create("ui/capability-access-preview.json"));
        validationIssues.AddRange(policyCompilation.ValidationResult.Issues);
        var validationResult = new CapabilityValidationResult(validationIssues);

        if (!validationResult.IsValid || policyCompilation.Policy is null)
        {
            return new CapabilityAccessPreviewResult(
                validationResult,
                new EffectiveCapabilitySet([], []),
                candidates
                    .Select(candidate => new CapabilityAccessPreviewCapabilityRow(candidate.Identity, candidate.DisplayName, false, []))
                    .ToList());
        }

        var evaluation = accessPolicyEvaluator.Evaluate(new CapabilityAccessEvaluationContext(
            candidates,
            required,
            [policyCompilation.Policy],
            correlationId));
        var allowedIdentities = evaluation.AllowedCapabilities
            .Select(capability => capability.Identity)
            .ToHashSet();
        var diagnosticLookup = evaluation.Diagnostics
            .GroupBy(diagnostic => diagnostic.Identity)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<SuppressedCapabilityDiagnostic>)group.ToList());

        var rows = candidates
            .Select(candidate => new CapabilityAccessPreviewCapabilityRow(
                candidate.Identity,
                candidate.DisplayName,
                allowedIdentities.Contains(candidate.Identity),
                diagnosticLookup.TryGetValue(candidate.Identity, out var diagnosticsForCapability)
                    ? diagnosticsForCapability
                    : []))
            .ToList();

        return new CapabilityAccessPreviewResult(validationResult, evaluation.ToEffectiveSet(), rows);
    }
}
