using CanDoItAll.AgentFramework.Capabilities.Abstractions;

namespace CanDoItAll.AgentFramework.Capabilities.Templates;

public sealed class CapabilityTemplateValidator
{
    public CapabilityValidationResult Validate(CapabilityTemplateDescriptorDto descriptor, TemplatePath templatePath)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var issues = new List<CapabilityValidationIssue>();
        var kind = TryReadEnum<CapabilityKind>(descriptor.Kind, "$.kind", templatePath, issues);
        var key = TryReadCapabilityKey(descriptor.Key, "$.key", templatePath, issues);

        if (string.IsNullOrWhiteSpace(descriptor.DisplayName))
        {
            issues.Add(Issue(
                CapabilityDiagnosticCategory.TemplateValidation,
                kind,
                key,
                templatePath,
                "$.displayName",
                "Capability display name is required.",
                "Add a short displayName for UI and diagnostics."));
        }

        if (!string.IsNullOrWhiteSpace(descriptor.StableId) &&
            !CapabilityStableId.TryCreate(descriptor.StableId, out _))
        {
            issues.Add(Issue(
                CapabilityDiagnosticCategory.TemplateValidation,
                kind,
                key,
                templatePath,
                "$.stableId",
                "Stable id is invalid.",
                "Use an explicit ASCII stable id such as 'tool:workspace-read-file:v1'."));
        }

        if (!string.IsNullOrWhiteSpace(descriptor.RuntimeToolName) &&
            !RuntimeToolName.TryCreate(descriptor.RuntimeToolName, out _))
        {
            issues.Add(Issue(
                CapabilityDiagnosticCategory.TemplateValidation,
                kind,
                key,
                templatePath,
                "$.runtimeToolName",
                "Runtime tool name is invalid.",
                "Use lower snake_case runtime tool names, for example 'workspace_read_file'."));
        }

        if (!string.IsNullOrWhiteSpace(descriptor.ImplementationKey) &&
            !ImplementationKey.TryCreate(descriptor.ImplementationKey, out _))
        {
            issues.Add(Issue(
                CapabilityDiagnosticCategory.TemplateValidation,
                kind,
                key,
                templatePath,
                "$.implementationKey",
                "Implementation key is invalid.",
                "Use lower ASCII implementation key segments separated by '.', '_' or '-'."));
        }

        if (!string.IsNullOrWhiteSpace(descriptor.McpServerKey) &&
            !McpServerKey.TryCreate(descriptor.McpServerKey, out _))
        {
            issues.Add(Issue(
                CapabilityDiagnosticCategory.TemplateValidation,
                kind,
                key,
                templatePath,
                "$.mcpServerKey",
                "MCP server key is invalid.",
                "Use lower kebab-case MCP server keys."));
        }

        ValidateTags(descriptor.Tags, kind, key, templatePath, issues);
        ValidateOperationClassifications(descriptor.OperationClassifications, kind, key, templatePath, issues);
        ValidateSideEffects(descriptor.SideEffects, kind, key, templatePath, issues);
        ValidateExternalProcess(descriptor.ExternalProcess, kind, key, templatePath, issues);
        ValidateExternalHttp(descriptor.ExternalHttp, kind, key, templatePath, issues);
        ValidateMcpTransport(descriptor.McpTransport, kind, key, templatePath, issues);

        if (descriptor.CapabilityAccessPolicy is not null)
        {
            var policyResult = new CapabilityAccessPolicyTemplateCompiler()
                .Compile(descriptor.CapabilityAccessPolicy, templatePath);
            issues.AddRange(policyResult.ValidationResult.Issues);
        }

        return new CapabilityValidationResult(issues);
    }

    private static void ValidateTags(
        IReadOnlyList<string> tags,
        CapabilityKind? kind,
        CapabilityKey? key,
        TemplatePath templatePath,
        List<CapabilityValidationIssue> issues)
    {
        for (var index = 0; index < tags.Count; index++)
        {
            if (CapabilityTag.TryCreate(tags[index], out _))
            {
                continue;
            }

            issues.Add(Issue(
                CapabilityDiagnosticCategory.TemplateValidation,
                kind,
                key,
                templatePath,
                $"$.tags[{index}]",
                "Capability tag is invalid.",
                "Use lower kebab-case tags."));
        }
    }

    private static void ValidateOperationClassifications(
        IReadOnlyList<string> classifications,
        CapabilityKind? kind,
        CapabilityKey? key,
        TemplatePath templatePath,
        List<CapabilityValidationIssue> issues)
    {
        for (var index = 0; index < classifications.Count; index++)
        {
            if (CapabilityText.TryParseEnum<CapabilityOperationClassification>(classifications[index], out _))
            {
                continue;
            }

            issues.Add(Issue(
                CapabilityDiagnosticCategory.TemplateValidation,
                kind,
                key,
                templatePath,
                $"$.operationClassifications[{index}]",
                "Operation classification is invalid.",
                "Use a known operation classification such as 'validation', 'mutation', or 'browserAccess'."));
        }
    }

    private static void ValidateSideEffects(
        CapabilitySideEffectTemplateDto? sideEffects,
        CapabilityKind? kind,
        CapabilityKey? key,
        TemplatePath templatePath,
        List<CapabilityValidationIssue> issues)
    {
        if (sideEffects is null ||
            string.IsNullOrWhiteSpace(sideEffects.Kind) ||
            CapabilityText.TryParseEnum<CapabilitySideEffectKind>(sideEffects.Kind, out _))
        {
            return;
        }

        issues.Add(Issue(
            CapabilityDiagnosticCategory.TemplateValidation,
            kind,
            key,
            templatePath,
            "$.sideEffects.kind",
            "Side-effect kind is invalid.",
            "Use a known side-effect kind such as 'workspaceRead', 'workspaceWrite', or 'localProcessExecution'."));
    }

    private static void ValidateExternalProcess(
        ExternalProcessToolTemplateDto? externalProcess,
        CapabilityKind? kind,
        CapabilityKey? key,
        TemplatePath templatePath,
        List<CapabilityValidationIssue> issues)
    {
        if (externalProcess is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(externalProcess.Command))
        {
            issues.Add(Issue(
                CapabilityDiagnosticCategory.TemplateValidation,
                kind,
                key,
                templatePath,
                "$.externalProcess.command",
                "External process command is required.",
                "Declare the executable/script command and validate it through command policy."));
        }

        foreach (var item in externalProcess.EnvironmentVariables)
        {
            issues.Add(Issue(
                CapabilityDiagnosticCategory.SecretBinding,
                kind,
                key,
                templatePath,
                $"$.externalProcess.environmentVariables.{item.Key}",
                "Raw environment variables are not allowed in capability templates.",
                "Use an environment variable secret binding instead of storing raw environment values."));
        }

        ValidateSecretBindings(
            externalProcess.EnvironmentVariableBindings,
            "$.externalProcess.environmentVariableBindings",
            kind,
            key,
            templatePath,
            issues);
    }

    private static void ValidateExternalHttp(
        ExternalHttpToolTemplateDto? externalHttp,
        CapabilityKind? kind,
        CapabilityKey? key,
        TemplatePath templatePath,
        List<CapabilityValidationIssue> issues)
    {
        if (externalHttp is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(externalHttp.Method))
        {
            issues.Add(Issue(
                CapabilityDiagnosticCategory.TemplateValidation,
                kind,
                key,
                templatePath,
                "$.externalHttp.method",
                "External HTTP method is required.",
                "Declare an HTTP method such as GET or POST."));
        }

        if (string.IsNullOrWhiteSpace(externalHttp.UrlTemplate))
        {
            issues.Add(Issue(
                CapabilityDiagnosticCategory.TemplateValidation,
                kind,
                key,
                templatePath,
                "$.externalHttp.urlTemplate",
                "External HTTP URL template is required.",
                "Declare a URL template without raw secret values."));
        }

        foreach (var item in externalHttp.Headers)
        {
            issues.Add(Issue(
                CapabilityDiagnosticCategory.SecretBinding,
                kind,
                key,
                templatePath,
                $"$.externalHttp.headers.{item.Key}",
                "Raw HTTP headers are not allowed in capability templates.",
                "Use a header secret binding instead of storing raw header values."));
        }

        ValidateSecretBindings(
            externalHttp.HeaderBindings,
            "$.externalHttp.headerBindings",
            kind,
            key,
            templatePath,
            issues);
    }

    private static void ValidateMcpTransport(
        McpTransportTemplateDto? transport,
        CapabilityKind? kind,
        CapabilityKey? key,
        TemplatePath templatePath,
        List<CapabilityValidationIssue> issues)
    {
        if (transport is null)
        {
            return;
        }

        if (!CapabilityText.TryParseEnum<CapabilityTransportKind>(transport.Transport, out _))
        {
            issues.Add(Issue(
                CapabilityDiagnosticCategory.TemplateValidation,
                kind,
                key,
                templatePath,
                "$.mcpTransport.transport",
                "MCP transport is invalid.",
                "Use internalHosted, localStdio, or remoteHttp."));
        }

        foreach (var item in transport.EnvironmentVariables)
        {
            issues.Add(Issue(
                CapabilityDiagnosticCategory.SecretBinding,
                kind,
                key,
                templatePath,
                $"$.mcpTransport.environmentVariables.{item.Key}",
                "Raw MCP environment variables are not allowed in capability templates.",
                "Use an MCP environment variable secret binding instead."));
        }

        for (var index = 0; index < transport.AllowedTools.Count; index++)
        {
            if (McpToolName.TryCreate(transport.AllowedTools[index], out _))
            {
                continue;
            }

            issues.Add(Issue(
                CapabilityDiagnosticCategory.TemplateValidation,
                kind,
                key,
                templatePath,
                $"$.mcpTransport.allowedTools[{index}]",
                "Allowed MCP tool name is invalid.",
                "Use the server-provided MCP tool name with ASCII letters, digits, '.', '_' or '-'."));
        }

        ValidateSecretBindings(
            transport.EnvironmentVariableBindings,
            "$.mcpTransport.environmentVariableBindings",
            kind,
            key,
            templatePath,
            issues);
    }

    private static void ValidateSecretBindings(
        IReadOnlyList<SecretBindingTemplateDto> bindings,
        string basePath,
        CapabilityKind? kind,
        CapabilityKey? key,
        TemplatePath templatePath,
        List<CapabilityValidationIssue> issues)
    {
        for (var index = 0; index < bindings.Count; index++)
        {
            var binding = bindings[index];
            if (string.IsNullOrWhiteSpace(binding.BindingKey))
            {
                issues.Add(Issue(
                    CapabilityDiagnosticCategory.SecretBinding,
                    kind,
                    key,
                    templatePath,
                    $"{basePath}[{index}].bindingKey",
                    "Secret binding key is required.",
                    "Reference a named secret binding key, not a raw secret value."));
            }

            if (string.IsNullOrWhiteSpace(binding.DestinationName))
            {
                issues.Add(Issue(
                    CapabilityDiagnosticCategory.SecretBinding,
                    kind,
                    key,
                    templatePath,
                    $"{basePath}[{index}].destinationName",
                    "Secret binding destination name is required.",
                    "Declare the environment variable or header name that receives the bound secret."));
            }
        }
    }

    private static CapabilityKind? TryReadEnum<TEnum>(
        string? value,
        string fieldPath,
        TemplatePath templatePath,
        List<CapabilityValidationIssue> issues)
        where TEnum : struct, Enum
    {
        if (CapabilityText.TryParseEnum<TEnum>(value, out var parsed) &&
            parsed is CapabilityKind kind)
        {
            return kind;
        }

        issues.Add(Issue(
            CapabilityDiagnosticCategory.TemplateValidation,
            null,
            null,
            templatePath,
            fieldPath,
            $"{typeof(TEnum).Name} value is invalid.",
            $"Use a known {typeof(TEnum).Name} value."));
        return null;
    }

    private static CapabilityKey? TryReadCapabilityKey(
        string? value,
        string fieldPath,
        TemplatePath templatePath,
        List<CapabilityValidationIssue> issues)
    {
        if (CapabilityKey.TryCreate(value, out var key))
        {
            return key;
        }

        issues.Add(Issue(
            CapabilityDiagnosticCategory.TemplateValidation,
            null,
            null,
            templatePath,
            fieldPath,
            "Capability key is invalid.",
            "Use lower kebab-case capability keys."));
        return null;
    }

    private static CapabilityValidationIssue Issue(
        CapabilityDiagnosticCategory category,
        CapabilityKind? kind,
        CapabilityKey? key,
        TemplatePath templatePath,
        string fieldPath,
        string message,
        string repairHint)
    {
        return new CapabilityValidationIssue(
            category,
            CapabilityValidationSeverity.Error,
            kind,
            key,
            templatePath,
            fieldPath,
            message,
            repairHint);
    }
}
