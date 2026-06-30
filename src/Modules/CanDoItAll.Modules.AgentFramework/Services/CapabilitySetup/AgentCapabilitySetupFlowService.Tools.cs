using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tools.Abstractions;
using AccessCapabilityKind = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityKind;

namespace CanDoItAll.Modules.AgentFramework;

public sealed partial class AgentCapabilitySetupFlowService
{
    private static bool TryBuildExternalToolDescriptor(
        CapabilityEditorModel capability,
        string correlationId,
        out ToolDescriptor descriptor,
        out IReadOnlyList<CapabilityDiagnostic> diagnostics)
    {
        var errors = new List<CapabilityDiagnostic>();
        var identity = ReadToolIdentity(capability, correlationId, errors);
        var configuration = ReadToolConfiguration(capability, correlationId, identity, errors);
        var runtimeToolName = ReadRuntimeToolName(configuration.RuntimeToolName, ToSnake(identity.Key.Value), correlationId, identity, errors);
        var implementationKey = ReadImplementationKey(configuration.ImplementationKey, $"external.{identity.Key.Value}", correlationId, identity, errors);

        descriptor = new InternalToolDescriptor(
            identity,
            runtimeToolName,
            implementationKey,
            ReadDiagnosticTags(capability.Tags),
            new HashSet<CapabilityOperationClassification>(),
            new CapabilitySideEffectProfile(CapabilitySideEffectKind.None, false, false));

        if (errors.Count > 0)
        {
            diagnostics = errors;
            return false;
        }

        var toolKind = NormalizeSetupKind(configuration.ToolKind);
        if (toolKind == "external-http")
        {
            descriptor = BuildHttpToolDescriptor(capability, configuration, identity, runtimeToolName, implementationKey, correlationId, errors);
        }
        else if (toolKind == "external-process")
        {
            descriptor = BuildProcessToolDescriptor(capability, configuration, identity, runtimeToolName, implementationKey, correlationId, errors);
        }
        else
        {
            errors.Add(Diagnostic(
                CapabilityDiagnosticCategory.ImplementationMissing,
                identity,
                "$.toolKind",
                $"Tool setup kind '{configuration.ToolKind}' cannot be tested through the external setup endpoint.",
                "Use externalProcess or externalHttp for setup-testable tools.",
                correlationId,
                implementationKey: implementationKey));
        }

        diagnostics = errors;
        return errors.Count == 0;
    }

    private static ExternalProcessToolDescriptor BuildProcessToolDescriptor(
        CapabilityEditorModel capability,
        CapabilityToolConfigurationModel configuration,
        CapabilityIdentity identity,
        RuntimeToolName runtimeToolName,
        ImplementationKey implementationKey,
        string correlationId,
        List<CapabilityDiagnostic> errors)
    {
        var process = configuration.ExternalProcess ?? new ExternalProcessToolConfigurationModel();
        if (string.IsNullOrWhiteSpace(process.Command))
        {
            errors.Add(Diagnostic(
                CapabilityDiagnosticCategory.TemplateValidation,
                identity,
                "$.externalProcess.command",
                "External process tool setup requires a command.",
                "Set the executable or script command before running the setup test.",
                correlationId,
                implementationKey: implementationKey,
                transport: CapabilityTransportKind.ExternalProcess));
        }

        var command = process.Command?.Trim() ?? "missing-command";
        var allowedExecutableNames = (process.AllowedExecutableNames is { Count: > 0 }
                ? process.AllowedExecutableNames
                : [Path.GetFileName(command)])
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new ExternalProcessToolDescriptor(
            identity,
            runtimeToolName,
            implementationKey,
            ReadDiagnosticTags(capability.Tags).Concat([CapabilityTag.Create("external"), CapabilityTag.Create("process")]).ToHashSet(),
            ReadDiagnosticClassifications(configuration.OperationClassifications, [CapabilityOperationClassification.ExternalAction]),
            new CapabilitySideEffectProfile(CapabilitySideEffectKind.ExternalAction, true, true),
            command,
            process.Arguments ?? [],
            string.IsNullOrWhiteSpace(process.WorkingDirectory) ? "." : process.WorkingDirectory.Trim(),
            TimeSpan.FromSeconds(process.TimeoutSeconds is > 0 ? process.TimeoutSeconds.Value : DefaultTimeoutSeconds),
            Math.Max(64, process.MaxOutputBytes ?? DefaultMaxPayloadBytes),
            allowedExecutableNames,
            NormalizeStringSet(process.RequiredOutputProperties));
    }

    private static ExternalHttpToolDescriptor BuildHttpToolDescriptor(
        CapabilityEditorModel capability,
        CapabilityToolConfigurationModel configuration,
        CapabilityIdentity identity,
        RuntimeToolName runtimeToolName,
        ImplementationKey implementationKey,
        string correlationId,
        List<CapabilityDiagnostic> errors)
    {
        var http = configuration.ExternalHttp ?? new ExternalHttpToolConfigurationModel();
        if (!Uri.TryCreate(http.Endpoint, UriKind.Absolute, out var endpoint))
        {
            errors.Add(Diagnostic(
                CapabilityDiagnosticCategory.TemplateValidation,
                identity,
                "$.externalHttp.endpoint",
                "External HTTP tool setup requires an absolute endpoint.",
                "Use an absolute http or https URL before running the setup test.",
                correlationId,
                implementationKey: implementationKey,
                transport: CapabilityTransportKind.ExternalHttp));
            endpoint = new Uri("https://invalid.local/");
        }

        if ((http.HeaderBindings?.Count ?? 0) > 0)
        {
            errors.Add(Diagnostic(
                CapabilityDiagnosticCategory.SecretBinding,
                identity,
                "$.externalHttp.headerBindings",
                "External HTTP setup tests cannot resolve header secret bindings in this UI flow.",
                "Run setup with a host that resolves secret bindings or test an endpoint that does not require secrets.",
                correlationId,
                implementationKey: implementationKey,
                transport: CapabilityTransportKind.ExternalHttp));
        }

        var method = string.IsNullOrWhiteSpace(http.Method)
            ? HttpMethod.Post
            : new HttpMethod(http.Method.Trim().ToUpperInvariant());

        return new ExternalHttpToolDescriptor(
            identity,
            runtimeToolName,
            implementationKey,
            ReadDiagnosticTags(capability.Tags).Concat([CapabilityTag.Create("external"), CapabilityTag.Create("http")]).ToHashSet(),
            ReadDiagnosticClassifications(configuration.OperationClassifications, [CapabilityOperationClassification.ExternalAction]),
            new CapabilitySideEffectProfile(CapabilitySideEffectKind.ExternalAction, true, true),
            method,
            endpoint,
            http.Headers ?? new Dictionary<string, string>(),
            TimeSpan.FromSeconds(http.TimeoutSeconds is > 0 ? http.TimeoutSeconds.Value : DefaultTimeoutSeconds),
            Math.Max(64, http.MaxResponseBytes ?? DefaultMaxPayloadBytes),
            NormalizeStringSet(http.RequiredOutputProperties));
    }

    private static CapabilityToolConfigurationModel ReadToolConfiguration(
        CapabilityEditorModel capability,
        string correlationId,
        CapabilityIdentity identity,
        List<CapabilityDiagnostic> errors)
    {
        var configuration = ReadConfiguration<CapabilityToolConfigurationModel>(
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
        return configuration ?? new CapabilityToolConfigurationModel();
    }

    private static bool IsValidJsonInput(
        string jsonInput,
        string correlationId,
        CapabilityIdentity identity,
        out CapabilityDiagnostic diagnostic)
    {
        diagnostic = default!;
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(jsonInput) ? "{}" : jsonInput);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                return true;
            }

            diagnostic = Diagnostic(
                CapabilityDiagnosticCategory.JsonParse,
                identity,
                "$.jsonInput",
                "Setup test input must be a JSON object.",
                "Provide an object payload such as {\"input\":true}.",
                correlationId);
            return false;
        }
        catch (JsonException exception)
        {
            diagnostic = Diagnostic(
                CapabilityDiagnosticCategory.JsonParse,
                identity,
                "$.jsonInput",
                $"Setup test input is not valid JSON. {exception.Message}",
                "Provide a valid JSON object payload before running the setup test.",
                correlationId);
            return false;
        }
    }

    private static CapabilityIdentity ReadToolIdentity(
        CapabilityEditorModel capability,
        string correlationId,
        List<CapabilityDiagnostic> errors)
        => ReadDiagnosticIdentity(capability, AccessCapabilityKind.Tool, correlationId, errors);

    private static CapabilitySetupTestResult ToolSetupFailure(
        string correlationId,
        IReadOnlyList<CapabilityDiagnostic> diagnostics)
    {
        var identity = diagnostics.FirstOrDefault()?.CapabilityKey is { } key
            ? new CapabilityIdentity(AccessCapabilityKind.Tool, key)
            : new CapabilityIdentity(AccessCapabilityKind.Tool, CapabilityKey.Create("invalid-capability"));
        return new CapabilitySetupTestResult(false, identity, correlationId, diagnostics);
    }

    private static string NormalizeSetupKind(string? value)
    {
        var normalized = (value ?? "externalProcess").Trim();
        return normalized.Replace("_", "-", StringComparison.Ordinal).ToLowerInvariant() switch
        {
            "externalhttp" => "external-http",
            "external-http" => "external-http",
            "http" => "external-http",
            "externalprocess" => "external-process",
            "external-process" => "external-process",
            "process" => "external-process",
            _ => normalized.ToLowerInvariant()
        };
    }
}
