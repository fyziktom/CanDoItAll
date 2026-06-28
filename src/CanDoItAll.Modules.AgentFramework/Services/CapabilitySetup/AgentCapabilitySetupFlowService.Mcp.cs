using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.AgentFramework.Models;
using AccessCapabilityKind = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityKind;

namespace CanDoItAll.Modules.AgentFramework;

public sealed partial class AgentCapabilitySetupFlowService
{
    private static McpServerDescriptor BuildMcpDescriptor(
        CapabilityEditorModel capability,
        string correlationId,
        out IReadOnlyList<CapabilityDiagnostic> diagnostics)
    {
        var errors = new List<CapabilityDiagnostic>();
        var identity = ReadMcpIdentity(capability, correlationId, errors);
        var configuration = ReadMcpConfiguration(capability, correlationId, identity, errors);
        var serverKey = ReadMcpServerKey(configuration.ServerName, identity.Key.Value, correlationId, identity, errors);
        var approvalMode = CapabilityText.TryParseEnum<McpApprovalMode>(configuration.ApprovalMode, out var parsedApprovalMode)
            ? parsedApprovalMode
            : McpApprovalMode.NeverRequire;
        var allowedTools = (configuration.AllowedTools ?? [])
            .Where(tool => !string.IsNullOrWhiteSpace(tool))
            .Select(tool => McpToolName.TryCreate(tool, out var parsed) ? parsed : default)
            .Where(tool => tool != default)
            .ToHashSet();
        var timeout = TimeSpan.FromSeconds(configuration.TimeoutSeconds is > 0 ? configuration.TimeoutSeconds.Value : DefaultTimeoutSeconds);
        var classifications = ReadDiagnosticClassifications(
            configuration.OperationClassifications,
            [CapabilityOperationClassification.McpTool, CapabilityOperationClassification.ExternalAction]);
        var tags = ReadDiagnosticTags(capability.Tags)
            .Concat([CapabilityTag.Create("mcp")])
            .ToHashSet();
        var transport = NormalizeMcpTransport(configuration.Transport, configuration.Command, configuration.Endpoint);

        McpServerDescriptor descriptor;
        if (transport == "local-stdio")
        {
            if (string.IsNullOrWhiteSpace(configuration.Command))
            {
                errors.Add(Diagnostic(
                    CapabilityDiagnosticCategory.TemplateValidation,
                    identity,
                    "$.command",
                    "Local stdio MCP setup requires a command.",
                    "Set the MCP command before running setup discovery.",
                    correlationId,
                    mcpServerKey: serverKey,
                    transport: CapabilityTransportKind.LocalStdio));
            }

            descriptor = new LocalStdioMcpServerDescriptor(
                identity,
                serverKey,
                ResolveDisplayName(capability),
                ResolveDescription(capability),
                tags,
                classifications,
                new CapabilitySideEffectProfile(CapabilitySideEffectKind.LocalProcessExecution, approvalMode == McpApprovalMode.AlwaysRequire, true),
                CapabilityAvailabilityState.Available,
                allowedTools,
                approvalMode,
                timeout,
                string.IsNullOrWhiteSpace(configuration.Command) ? "missing-command" : configuration.Command.Trim(),
                configuration.Arguments ?? [],
                string.IsNullOrWhiteSpace(configuration.WorkingDirectory) ? "." : configuration.WorkingDirectory.Trim(),
                NormalizeStringSet(configuration.AllowedWorkingDirectories),
                configuration.EnvironmentVariableBindings ?? new Dictionary<string, string>(),
                configuration.EnvironmentVariables ?? new Dictionary<string, string>());
        }
        else if (transport == "remote-http")
        {
            if (!Uri.TryCreate(configuration.Endpoint, UriKind.Absolute, out var endpoint))
            {
                errors.Add(Diagnostic(
                    CapabilityDiagnosticCategory.TemplateValidation,
                    identity,
                    "$.endpoint",
                    "Remote MCP setup requires an absolute endpoint.",
                    "Use an absolute http or https endpoint before running setup discovery.",
                    correlationId,
                    mcpServerKey: serverKey,
                    transport: CapabilityTransportKind.RemoteHttp));
                endpoint = new Uri("https://invalid.local/");
            }

            descriptor = new RemoteHttpMcpServerDescriptor(
                identity,
                serverKey,
                ResolveDisplayName(capability),
                ResolveDescription(capability),
                tags,
                classifications,
                new CapabilitySideEffectProfile(CapabilitySideEffectKind.ExternalAction, approvalMode == McpApprovalMode.AlwaysRequire, true),
                CapabilityAvailabilityState.Available,
                allowedTools,
                approvalMode,
                timeout,
                endpoint,
                configuration.HeaderBindings ?? new Dictionary<string, string>(),
                configuration.Headers ?? new Dictionary<string, string>());
        }
        else
        {
            descriptor = new InternalHostedMcpServerDescriptor(
                identity,
                serverKey,
                ResolveDisplayName(capability),
                ResolveDescription(capability),
                tags,
                new HashSet<CapabilityOperationClassification> { CapabilityOperationClassification.McpTool },
                new CapabilitySideEffectProfile(CapabilitySideEffectKind.McpTool, approvalMode == McpApprovalMode.AlwaysRequire, false),
                CapabilityAvailabilityState.Available,
                allowedTools,
                approvalMode,
                timeout,
                ReadImplementationKey($"mcp.{identity.Key.Value}", $"mcp.{identity.Key.Value}", correlationId, identity, errors));
        }

        diagnostics = errors;
        return descriptor;
    }

    private static McpCapabilityConfigurationModel ReadMcpConfiguration(
        CapabilityEditorModel capability,
        string correlationId,
        CapabilityIdentity identity,
        List<CapabilityDiagnostic> errors)
    {
        var configuration = ReadConfiguration<McpCapabilityConfigurationModel>(
            capability.ConfigurationJson,
            "$.configurationJson",
            identity.Kind,
            errors,
            issue => Diagnostic(
                issue.Category,
                identity,
                issue.FieldPath,
                issue.Message,
                issue.RepairHint,
                correlationId));
        return configuration ?? new McpCapabilityConfigurationModel();
    }

    private static CapabilityIdentity ReadMcpIdentity(
        CapabilityEditorModel capability,
        string correlationId,
        List<CapabilityDiagnostic> errors)
        => ReadDiagnosticIdentity(capability, AccessCapabilityKind.McpServer, correlationId, errors);

    private static McpServerKey ReadMcpServerKey(
        string? value,
        string fallback,
        string correlationId,
        CapabilityIdentity identity,
        List<CapabilityDiagnostic> errors)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        if (McpServerKey.TryCreate(candidate, out var key))
        {
            return key;
        }

        errors.Add(Diagnostic(
            CapabilityDiagnosticCategory.TemplateValidation,
            identity,
            "$.serverName",
            $"MCP server key '{candidate}' is invalid.",
            "Use lower kebab-case MCP server keys.",
            correlationId));
        return McpServerKey.Create("invalid-mcp");
    }

    private static string NormalizeMcpTransport(string? transport, string? command, string? endpoint)
    {
        var normalized = (transport ?? string.Empty).Trim();
        if (string.Equals(normalized, "stdio", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "local-stdio", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(command))
        {
            return "local-stdio";
        }

        if (string.Equals(normalized, "http", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "sse", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "remote-http", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(endpoint))
        {
            return "remote-http";
        }

        return "internal-hosted";
    }

    private static CapabilityTransportKind ResolveMcpTransport(McpServerDescriptor descriptor)
    {
        return descriptor.DescriptorKind switch
        {
            McpServerDescriptorKind.LocalStdio => CapabilityTransportKind.LocalStdio,
            McpServerDescriptorKind.RemoteHttp => CapabilityTransportKind.RemoteHttp,
            _ => CapabilityTransportKind.InternalHosted
        };
    }
}
