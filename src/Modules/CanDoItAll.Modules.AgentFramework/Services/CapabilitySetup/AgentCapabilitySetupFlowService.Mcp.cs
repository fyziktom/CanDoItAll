using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using AccessCapabilityKind = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityKind;

namespace CanDoItAll.Modules.AgentFramework;

public sealed partial class AgentCapabilitySetupFlowService
{
    internal static McpServerDescriptor BuildMcpDescriptor(
        CapabilityEditorModel capability,
        string correlationId,
        out IReadOnlyList<CapabilityDiagnostic> diagnostics)
    {
        var errors = new List<CapabilityDiagnostic>();
        var identity = ReadMcpIdentity(capability, correlationId, errors);
        var configuration = ReadMcpConfiguration(capability, correlationId, identity, errors);
        var serverKey = ReadMcpServerKey(configuration.ServerName, identity.Key.Value, correlationId, identity, errors);
        var transport = NormalizeMcpTransport(configuration.Transport, configuration.Command, configuration.Endpoint);
        var hasValidApprovalMode = !string.IsNullOrWhiteSpace(configuration.ApprovalMode) &&
                                   Enum.GetNames<McpApprovalMode>()
                                       .Contains(configuration.ApprovalMode, StringComparer.OrdinalIgnoreCase);
        var approvalMode = hasValidApprovalMode
            ? Enum.Parse<McpApprovalMode>(configuration.ApprovalMode!, ignoreCase: true)
            : McpApprovalMode.NeverRequire;
        if (transport == "local-stdio" && string.IsNullOrWhiteSpace(configuration.ApprovalMode))
        {
            errors.Add(Diagnostic(
                CapabilityDiagnosticCategory.TemplateValidation,
                identity,
                "$.approvalMode",
                "Local MCP setup requires an explicit approval mode.",
                "Use NeverRequire or AlwaysRequire.",
                correlationId,
                mcpServerKey: serverKey));
        }
        else if (!string.IsNullOrWhiteSpace(configuration.ApprovalMode) && !hasValidApprovalMode)
        {
            errors.Add(Diagnostic(
                CapabilityDiagnosticCategory.TemplateValidation,
                identity,
                "$.approvalMode",
                "MCP setup requires a supported approval mode.",
                "Use NeverRequire or AlwaysRequire.",
                correlationId,
                mcpServerKey: serverKey));
        }
        var allowedTools = new HashSet<McpToolName>();
        foreach (var tool in configuration.AllowedTools ?? [])
        {
            if (McpToolName.TryCreate(tool, out var parsed))
            {
                allowedTools.Add(parsed);
                continue;
            }

            errors.Add(Diagnostic(
                CapabilityDiagnosticCategory.TemplateValidation,
                identity,
                "$.allowedTools",
                "MCP setup contains an invalid allowed-tool name.",
                "Use a non-empty MCP tool name without whitespace.",
                correlationId,
                mcpServerKey: serverKey));
        }
        var timeout = TimeSpan.FromSeconds(configuration.TimeoutSeconds is > 0 ? configuration.TimeoutSeconds.Value : DefaultTimeoutSeconds);
        var classifications = ReadDiagnosticClassifications(
            configuration.OperationClassifications,
            [CapabilityOperationClassification.McpTool, CapabilityOperationClassification.ExternalAction]);
        var tags = ReadDiagnosticTags(capability.Tags)
            .Concat([CapabilityTag.Create("mcp")])
            .ToHashSet();
        var messageFraming = ReadMcpMessageFraming(configuration.MessageFraming, identity, serverKey, correlationId, errors);

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

            if (SensitiveTextRedactor.ContainsSecretBearingArguments(configuration.Arguments ?? []))
            {
                errors.Add(Diagnostic(
                    CapabilityDiagnosticCategory.SecretBinding,
                    identity,
                    "$.arguments",
                    "Local MCP setup cannot launch a persisted secret-bearing argument.",
                    "Use an environment-variable or stored-secret binding instead.",
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
                string.IsNullOrWhiteSpace(configuration.WorkingDirectory) ? "." : configuration.WorkingDirectory,
                messageFraming,
                NormalizeAuthoritySet(configuration.AllowedWorkingDirectories),
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

    private static McpStdioMessageFraming ReadMcpMessageFraming(
        string? value,
        CapabilityIdentity identity,
        McpServerKey serverKey,
        string correlationId,
        List<CapabilityDiagnostic> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return McpStdioMessageFraming.ContentLength;
        }

        var normalized = NormalizeMcpMessageFramingToken(value);
        if (normalized == "contentlength")
        {
            return McpStdioMessageFraming.ContentLength;
        }

        if (normalized is "newlinedelimitedjson" or "newlinejson" or "newline" or "ndjson")
        {
            return McpStdioMessageFraming.NewlineDelimitedJson;
        }

        errors.Add(Diagnostic(
            CapabilityDiagnosticCategory.TemplateValidation,
            identity,
            "$.messageFraming",
            $"MCP stdio message framing '{value.Trim()}' is invalid.",
            "Use contentLength or newlineDelimitedJson.",
            correlationId,
            mcpServerKey: serverKey,
            transport: CapabilityTransportKind.LocalStdio));
        return McpStdioMessageFraming.ContentLength;
    }

    private static string NormalizeMcpMessageFramingToken(string value)
        => value
            .Trim()
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

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
