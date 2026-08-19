using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Models;
using AccessCapabilityKind = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityKind;
using ModelCapabilityKind = CanDoItAll.AgentFramework.Models.CapabilityKind;

namespace CanDoItAll.Modules.AgentFramework;

public sealed partial class AgentCapabilitySetupFlowService
{
    private static T? ReadConfiguration<T>(
        string json,
        string fieldPath,
        AccessCapabilityKind kind,
        List<CapabilityValidationIssue> validationIssues)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, SerializerOptions);
        }
        catch (JsonException exception)
        {
            validationIssues.Add(ValidationIssue(
                fieldPath,
                $"Capability configuration JSON is invalid. {exception.Message}",
                "Repair the configuration JSON before previewing access.",
                kind));
            return default;
        }
    }

    private static T? ReadConfiguration<T>(
        string json,
        string fieldPath,
        AccessCapabilityKind kind,
        List<CapabilityDiagnostic> diagnostics,
        Func<CapabilityValidationIssue, CapabilityDiagnostic> convertIssue)
    {
        var issues = new List<CapabilityValidationIssue>();
        var configuration = ReadConfiguration<T>(json, fieldPath, kind, issues);
        diagnostics.AddRange(issues.Select(convertIssue));
        return configuration;
    }

    private static CapabilityIdentity ReadDiagnosticIdentity(
        CapabilityEditorModel capability,
        AccessCapabilityKind kind,
        string correlationId,
        List<CapabilityDiagnostic> errors)
    {
        var keyText = string.IsNullOrWhiteSpace(capability.Key) ? ToKebab(capability.Name) : capability.Key.Trim();
        if (CapabilityKey.TryCreate(keyText, out var key))
        {
            return new CapabilityIdentity(kind, key);
        }

        errors.Add(Diagnostic(
            CapabilityDiagnosticCategory.TemplateValidation,
            new CapabilityIdentity(kind, CapabilityKey.Create("invalid-capability")),
            "$.key",
            $"Capability key '{keyText}' is invalid.",
            "Use lower kebab-case capability keys.",
            correlationId));
        return new CapabilityIdentity(kind, CapabilityKey.Create("invalid-capability"));
    }

    private static CapabilityIdentity ReadIdentity(
        CapabilityEditorModel capability,
        AccessCapabilityKind kind,
        List<CapabilityValidationIssue> validationIssues)
    {
        var keyText = string.IsNullOrWhiteSpace(capability.Key) ? ToKebab(capability.Name) : capability.Key.Trim();
        if (CapabilityKey.TryCreate(keyText, out var key))
        {
            return new CapabilityIdentity(kind, key);
        }

        validationIssues.Add(ValidationIssue(
            "$.key",
            $"Capability key '{keyText}' is invalid.",
            "Use lower kebab-case capability keys.",
            kind));
        return new CapabilityIdentity(kind, CapabilityKey.Create("invalid-capability"));
    }

    private static RuntimeToolName ReadRuntimeToolName(
        string? value,
        string fallback,
        string correlationId,
        CapabilityIdentity identity,
        List<CapabilityDiagnostic> errors)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        if (RuntimeToolName.TryCreate(candidate, out var name))
        {
            return name;
        }

        errors.Add(Diagnostic(
            CapabilityDiagnosticCategory.TemplateValidation,
            identity,
            "$.runtimeToolName",
            $"Runtime tool name '{candidate}' is invalid.",
            "Use lower snake_case runtime tool names.",
            correlationId));
        return RuntimeToolName.Create("invalid_tool");
    }

    private static RuntimeToolName? ReadRuntimeToolName(
        string? value,
        string fallback,
        string fieldPath,
        List<CapabilityValidationIssue> validationIssues)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        if (RuntimeToolName.TryCreate(candidate, out var name))
        {
            return name;
        }

        validationIssues.Add(ValidationIssue(
            fieldPath,
            $"Runtime tool name '{candidate}' is invalid.",
            "Use lower snake_case runtime tool names.",
            AccessCapabilityKind.Tool));
        return null;
    }

    private static ImplementationKey ReadImplementationKey(
        string? value,
        string fallback,
        string correlationId,
        CapabilityIdentity identity,
        List<CapabilityDiagnostic> errors)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        if (ImplementationKey.TryCreate(candidate, out var key))
        {
            return key;
        }

        errors.Add(Diagnostic(
            CapabilityDiagnosticCategory.TemplateValidation,
            identity,
            "$.implementationKey",
            $"Implementation key '{candidate}' is invalid.",
            "Use lower ASCII implementation key segments separated by '.', '_' or '-'.",
            correlationId));
        return ImplementationKey.Create("invalid.implementation");
    }

    private static ImplementationKey? ReadImplementationKey(
        string? value,
        string fallback,
        string fieldPath,
        List<CapabilityValidationIssue> validationIssues)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        if (ImplementationKey.TryCreate(candidate, out var key))
        {
            return key;
        }

        validationIssues.Add(ValidationIssue(
            fieldPath,
            $"Implementation key '{candidate}' is invalid.",
            "Use lower ASCII implementation key segments separated by '.', '_' or '-'.",
            AccessCapabilityKind.Tool));
        return null;
    }

    private static CapabilityDiagnostic Diagnostic(
        CapabilityDiagnosticCategory category,
        CapabilityIdentity identity,
        string fieldPath,
        string detail,
        string repairHint,
        string correlationId,
        ImplementationKey? implementationKey = null,
        McpServerKey? mcpServerKey = null,
        CapabilityTransportKind? transport = null,
        int? exitCode = null,
        int? httpStatusCode = null,
        TimeSpan? timeout = null)
    {
        _ = mcpServerKey;
        return new CapabilityDiagnostic(
            category,
            CapabilityValidationSeverity.Error,
            identity.Kind,
            identity.Key,
            null,
            fieldPath,
            implementationKey,
            transport,
            exitCode,
            httpStatusCode,
            timeout,
            correlationId,
            detail,
            repairHint);
    }

    private static CapabilityValidationIssue ValidationIssue(
        string fieldPath,
        string message,
        string repairHint,
        AccessCapabilityKind? kind)
    {
        return new CapabilityValidationIssue(
            CapabilityDiagnosticCategory.TemplateValidation,
            CapabilityValidationSeverity.Error,
            kind,
            null,
            null,
            fieldPath,
            message,
            repairHint);
    }

    private static CapabilityValidationIssue ToValidationIssue(CapabilityDiagnostic diagnostic)
    {
        return new CapabilityValidationIssue(
            diagnostic.Category,
            diagnostic.Severity,
            diagnostic.CapabilityKind,
            diagnostic.CapabilityKey,
            diagnostic.TemplatePath,
            diagnostic.FieldPath,
            diagnostic.MaskedDetail,
            diagnostic.RepairHint);
    }

    private static IReadOnlySet<CapabilityTag> ReadTags(
        IEnumerable<string> tags,
        List<CapabilityValidationIssue> validationIssues)
    {
        var parsed = new HashSet<CapabilityTag>();
        foreach (var tag in tags.Where(tag => !string.IsNullOrWhiteSpace(tag)))
        {
            if (CapabilityTag.TryCreate(tag.Trim(), out var parsedTag))
            {
                parsed.Add(parsedTag);
                continue;
            }

            validationIssues.Add(ValidationIssue(
                "$.tags",
                $"Capability tag '{tag}' is invalid.",
                "Use lower kebab-case tags.",
                null));
        }

        return parsed;
    }

    private static IReadOnlySet<CapabilityTag> ReadDiagnosticTags(IEnumerable<string> tags)
    {
        return tags
            .Where(tag => CapabilityTag.TryCreate(tag, out _))
            .Select(CapabilityTag.Create)
            .ToHashSet();
    }

    private static IReadOnlySet<CapabilityOperationClassification> ReadClassifications(
        IEnumerable<string>? classifications,
        IEnumerable<CapabilityOperationClassification> fallback,
        List<CapabilityValidationIssue> validationIssues)
    {
        var parsed = new HashSet<CapabilityOperationClassification>();
        foreach (var classification in classifications ?? [])
        {
            if (CapabilityText.TryParseEnum<CapabilityOperationClassification>(classification, out var parsedClassification))
            {
                parsed.Add(parsedClassification);
                continue;
            }

            validationIssues.Add(ValidationIssue(
                "$.operationClassifications",
                $"Operation classification '{classification}' is invalid.",
                "Use a known operation classification such as read, write, validation, or externalAction.",
                null));
        }

        return parsed.Count > 0 ? parsed : fallback.ToHashSet();
    }

    private static IReadOnlySet<CapabilityOperationClassification> ReadDiagnosticClassifications(
        IEnumerable<string>? classifications,
        IEnumerable<CapabilityOperationClassification> fallback)
    {
        var parsed = (classifications ?? [])
            .Where(classification => CapabilityText.TryParseEnum<CapabilityOperationClassification>(classification, out _))
            .Select(classification =>
            {
                CapabilityText.TryParseEnum<CapabilityOperationClassification>(classification, out var value);
                return value;
            })
            .ToHashSet();

        return parsed.Count > 0 ? parsed : fallback.ToHashSet();
    }

    private static CapabilitySideEffectProfile ReadSideEffects(
        CapabilitySideEffectConfigurationModel? sideEffects,
        CapabilitySideEffectProfile fallback)
    {
        if (sideEffects is null ||
            !CapabilityText.TryParseEnum<CapabilitySideEffectKind>(sideEffects.Kind, out var sideEffectKind))
        {
            return fallback;
        }

        return new CapabilitySideEffectProfile(
            sideEffectKind,
            sideEffects.RequiresApprovalByDefault ?? fallback.RequiresApprovalByDefault,
            sideEffects.IsStateChanging ?? fallback.IsStateChanging);
    }

    private static AccessCapabilityKind MapKind(ModelCapabilityKind kind)
    {
        return kind switch
        {
            ModelCapabilityKind.Skill => AccessCapabilityKind.Skill,
            ModelCapabilityKind.Tool => AccessCapabilityKind.Tool,
            ModelCapabilityKind.McpServer => AccessCapabilityKind.McpServer,
            ModelCapabilityKind.Plugin => AccessCapabilityKind.Plugin,
            ModelCapabilityKind.Rag => AccessCapabilityKind.Rag,
            ModelCapabilityKind.AiContext => AccessCapabilityKind.AiContext,
            ModelCapabilityKind.Memory => AccessCapabilityKind.Memory,
            _ => AccessCapabilityKind.Tool
        };
    }

    private static string ResolveCorrelationId(string value, string prefix)
        => string.IsNullOrWhiteSpace(value)
            ? $"{prefix}-{Guid.NewGuid():N}"
            : value.Trim();

    private static string ResolveDisplayName(CapabilityEditorModel capability)
        => string.IsNullOrWhiteSpace(capability.Name) ? capability.Key : capability.Name.Trim();

    private static string ResolveDescription(CapabilityEditorModel capability)
        => string.IsNullOrWhiteSpace(capability.Description) ? "Capability setup preview." : capability.Description.Trim();

    private static IReadOnlySet<string> NormalizeStringSet(IEnumerable<string>? values)
        => (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlySet<string> NormalizeAuthoritySet(IEnumerable<string>? values)
        => (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);

    private static string ToSnake(string value)
        => ToSeparatedIdentifier(value, '_');

    private static string ToKebab(string value)
        => ToSeparatedIdentifier(value, '-');

    private static string ToSeparatedIdentifier(string value, char separator)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "capability";
        }

        var builder = new List<char>();
        var pendingSeparator = false;
        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Count > 0)
                {
                    builder.Add(separator);
                }

                builder.Add(char.ToLowerInvariant(character));
                pendingSeparator = false;
            }
            else if (builder.Count > 0)
            {
                pendingSeparator = true;
            }
        }

        return builder.Count == 0 ? "capability" : new string(builder.ToArray());
    }
}
