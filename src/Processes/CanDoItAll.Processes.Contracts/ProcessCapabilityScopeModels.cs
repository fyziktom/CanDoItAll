using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Processes.Contracts;

public sealed class ProcessCapabilityScope
{
    public static ProcessCapabilityScope Empty => new();

    public List<ProcessCapabilityScopeDirective> Directives { get; set; } = [];

    public List<ProcessScopedInstructionFragment> InstructionFragments { get; set; } = [];

    public List<ProcessRequiredToolReceipt> RequiredReceipts { get; set; } = [];

    public bool IsEmpty => Directives.Count == 0 && InstructionFragments.Count == 0 && RequiredReceipts.Count == 0;

    public static ProcessCapabilityScope Normalize(ProcessCapabilityScope? scope)
    {
        if (scope is null)
        {
            return Empty;
        }

        return new ProcessCapabilityScope
        {
            Directives = scope.Directives
                .Where(directive => directive.Target.Kind != ProcessCapabilityScopeTargetKind.Unspecified)
                .Select(NormalizeDirective)
                .ToList(),
            InstructionFragments = scope.InstructionFragments
                .Select(NormalizeInstructionFragment)
                .Where(fragment => !string.IsNullOrWhiteSpace(fragment.Content))
                .ToList(),
            RequiredReceipts = scope.RequiredReceipts
                .Select(NormalizeRequiredReceipt)
                .Where(HasRequiredReceiptSelector)
                .GroupBy(receipt => receipt.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList()
        };
    }

    private static ProcessCapabilityScopeDirective NormalizeDirective(ProcessCapabilityScopeDirective directive)
    {
        return new ProcessCapabilityScopeDirective
        {
            Kind = directive.Kind,
            Target = new ProcessCapabilityScopeTarget
            {
                Kind = directive.Target.Kind,
                Value = directive.Target.Value.Trim(),
                SecondaryValue = directive.Target.SecondaryValue.Trim()
            },
            Reason = directive.Reason.Trim()
        };
    }

    private static ProcessScopedInstructionFragment NormalizeInstructionFragment(ProcessScopedInstructionFragment fragment)
    {
        return new ProcessScopedInstructionFragment
        {
            Key = fragment.Key.Trim(),
            Title = fragment.Title.Trim(),
            Content = fragment.Content.Trim(),
            Placement = fragment.Placement
        };
    }

    private static ProcessRequiredToolReceipt NormalizeRequiredReceipt(ProcessRequiredToolReceipt receipt)
    {
        var normalized = new ProcessRequiredToolReceipt
        {
            Kind = receipt.Kind,
            ToolName = receipt.ToolName.Trim(),
            RuntimeToolProviderKey = receipt.RuntimeToolProviderKey.Trim(),
            McpServerKey = receipt.McpServerKey.Trim(),
            MinimumCount = Math.Max(1, receipt.MinimumCount),
            RequireSuccessfulExit = receipt.RequireSuccessfulExit,
            RequireCurrentRun = receipt.RequireCurrentRun,
            Activation = receipt.Activation,
            Purpose = receipt.Purpose,
            ApplicableBranchOutcomeKeys = NormalizeStringList(receipt.ApplicableBranchOutcomeKeys).ToList(),
            Reason = receipt.Reason.Trim()
        };
        normalized.Key = string.IsNullOrWhiteSpace(receipt.Key)
            ? CreateRequiredReceiptKey(normalized)
            : receipt.Key.Trim();
        return normalized;
    }

    private static bool HasRequiredReceiptSelector(ProcessRequiredToolReceipt receipt)
    {
        return receipt.Kind switch
        {
            ProcessRequiredToolReceiptKind.RuntimeToolName => !string.IsNullOrWhiteSpace(receipt.ToolName),
            ProcessRequiredToolReceiptKind.RuntimeToolProviderKey => !string.IsNullOrWhiteSpace(receipt.RuntimeToolProviderKey),
            ProcessRequiredToolReceiptKind.RuntimeToolNameWithProvider => !string.IsNullOrWhiteSpace(receipt.ToolName) &&
                                                                         !string.IsNullOrWhiteSpace(receipt.RuntimeToolProviderKey),
            ProcessRequiredToolReceiptKind.McpToolName => !string.IsNullOrWhiteSpace(receipt.ToolName),
            _ => false
        };
    }

    private static string CreateRequiredReceiptKey(ProcessRequiredToolReceipt receipt)
    {
        var selector = receipt.Kind switch
        {
            ProcessRequiredToolReceiptKind.RuntimeToolName => $"runtime-tool:{receipt.ToolName}",
            ProcessRequiredToolReceiptKind.RuntimeToolProviderKey => $"runtime-provider:{receipt.RuntimeToolProviderKey}",
            ProcessRequiredToolReceiptKind.RuntimeToolNameWithProvider => $"runtime-tool-provider:{receipt.RuntimeToolProviderKey}:{receipt.ToolName}",
            ProcessRequiredToolReceiptKind.McpToolName => string.IsNullOrWhiteSpace(receipt.McpServerKey)
                ? $"mcp-tool:{receipt.ToolName}"
                : $"mcp-tool:{receipt.McpServerKey}:{receipt.ToolName}",
            _ => "runtime-tool:unknown"
        };
        var purpose = receipt.Purpose == ProcessRequiredToolReceiptPurpose.Unspecified
            ? string.Empty
            : $":purpose:{receipt.Purpose}";
        var branches = receipt.ApplicableBranchOutcomeKeys.Count == 0
            ? string.Empty
            : $":branches:{string.Join(",", NormalizeStringList(receipt.ApplicableBranchOutcomeKeys))}";
        return $"{selector}{purpose}{branches}";
    }

    private static IReadOnlyList<string> NormalizeStringList(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return [];
        }

        return values
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

public sealed class ProcessCapabilityScopeDirective
{
    public ProcessCapabilityScopeDirectiveKind Kind { get; set; } = ProcessCapabilityScopeDirectiveKind.Deny;

    public ProcessCapabilityScopeTarget Target { get; set; } = new();

    public string Reason { get; set; } = string.Empty;
}

public sealed class ProcessCapabilityScopeTarget
{
    public ProcessCapabilityScopeTargetKind Kind { get; set; } = ProcessCapabilityScopeTargetKind.Unspecified;

    public string Value { get; set; } = string.Empty;

    public string SecondaryValue { get; set; } = string.Empty;
}

public sealed class ProcessScopedInstructionFragment
{
    public string Key { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public ProcessScopedInstructionPlacement Placement { get; set; } = ProcessScopedInstructionPlacement.AppendToStepBrief;
}

public sealed class ProcessRequiredToolReceipt
{
    public string Key { get; set; } = string.Empty;

    public ProcessRequiredToolReceiptKind Kind { get; set; } = ProcessRequiredToolReceiptKind.RuntimeToolName;

    public string ToolName { get; set; } = string.Empty;

    public string RuntimeToolProviderKey { get; set; } = string.Empty;

    public string McpServerKey { get; set; } = string.Empty;

    public int MinimumCount { get; set; } = 1;

    public bool RequireSuccessfulExit { get; set; } = true;

    public bool RequireCurrentRun { get; set; } = true;

    public ProcessRequiredToolReceiptActivation Activation { get; set; } = ProcessRequiredToolReceiptActivation.Always;

    public ProcessRequiredToolReceiptPurpose Purpose { get; set; } = ProcessRequiredToolReceiptPurpose.Unspecified;

    public List<string> ApplicableBranchOutcomeKeys { get; set; } = [];

    public string Reason { get; set; } = string.Empty;
}

public static class ProcessRequiredRuntimeToolNames
{
    public static IReadOnlyList<string> FromCapabilityScope(ProcessCapabilityScope? capabilityScope)
        => FromCapabilityScope(capabilityScope, activeLaunchContextToolNames: null);

    public static IReadOnlyList<string> FromCapabilityScope(
        ProcessCapabilityScope? capabilityScope,
        IReadOnlySet<string>? activeLaunchContextToolNames)
    {
        var normalized = ProcessCapabilityScope.Normalize(capabilityScope);
        return normalized.RequiredReceipts
            .Where(receipt => IsActive(receipt, activeLaunchContextToolNames))
            .Select(ResolveRuntimeToolName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<string> FromProductCompletionRequiredToolReceipts(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return FromProductCompletionRequiredToolReceipts(document.RootElement);
        }
        catch (JsonException)
        {
            return NormalizeRuntimeToolNameCandidates(
                value.Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
    }

    public static IReadOnlyList<string> FromProductCompletionRequiredToolReceipts(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return FromProductCompletionRequiredToolReceipts(element.GetString());
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return NormalizeRuntimeToolNameCandidates(element.EnumerateArray()
            .Select(ReadProductCompletionRequiredToolReceiptCandidate));
    }

    public static IReadOnlyList<string> FromProductCompletionRequiredToolReceipts(IEnumerable<string>? requiredToolReceipts)
        => NormalizeRuntimeToolNameCandidates(requiredToolReceipts);

    public static IReadOnlyList<string> NormalizeRuntimeToolNameCandidates(IEnumerable<string>? candidates)
    {
        if (candidates is null)
        {
            return [];
        }

        return candidates
            .Select(ResolveRuntimeToolNameCandidate)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ResolveRuntimeToolName(ProcessRequiredToolReceipt receipt)
    {
        return receipt.Kind switch
        {
            ProcessRequiredToolReceiptKind.RuntimeToolName => receipt.ToolName,
            ProcessRequiredToolReceiptKind.RuntimeToolNameWithProvider => receipt.ToolName,
            ProcessRequiredToolReceiptKind.McpToolName => receipt.ToolName,
            _ => string.Empty
        };
    }

    private static string ResolveRuntimeToolNameCandidate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var candidate = value.Trim();
        var predicateIndex = candidate.IndexOf('|');
        if (predicateIndex >= 0)
        {
            candidate = candidate[..predicateIndex].Trim();
        }

        candidate = candidate.Replace('-', '_');
        if (candidate.Contains('=') ||
            !LooksLikeConcreteRuntimeToolName(candidate))
        {
            return string.Empty;
        }

        return candidate;
    }

    private static bool LooksLikeConcreteRuntimeToolName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !char.IsLetter(value[0]))
        {
            return false;
        }

        var hasSeparator = false;
        foreach (var character in value)
        {
            if (character == '_')
            {
                hasSeparator = true;
                continue;
            }

            if (!char.IsLetterOrDigit(character))
            {
                return false;
            }
        }

        return hasSeparator;
    }

    public static bool IsActive(
        ProcessRequiredToolReceipt receipt,
        IReadOnlySet<string>? activeLaunchContextToolNames)
    {
        return receipt.Activation switch
        {
            ProcessRequiredToolReceiptActivation.Always => true,
            ProcessRequiredToolReceiptActivation.WhenLaunchContextDeclaresTool => IsDeclaredByLaunchContext(
                receipt,
                activeLaunchContextToolNames),
            _ => false
        };
    }

    private static bool IsDeclaredByLaunchContext(
        ProcessRequiredToolReceipt receipt,
        IReadOnlySet<string>? activeLaunchContextToolNames)
    {
        if (activeLaunchContextToolNames is null || activeLaunchContextToolNames.Count == 0)
        {
            return false;
        }

        var toolName = ResolveRuntimeToolName(receipt);
        return !string.IsNullOrWhiteSpace(toolName) &&
               activeLaunchContextToolNames.Contains(toolName);
    }

    public static bool IsApplicableToBranchOutcome(
        ProcessRequiredToolReceipt receipt,
        string? branchOutcomeKey)
    {
        if (receipt.ApplicableBranchOutcomeKeys.Count == 0)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(branchOutcomeKey) &&
               receipt.ApplicableBranchOutcomeKeys.Contains(branchOutcomeKey.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private static string ReadProductCompletionRequiredToolReceiptCandidate(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString() ?? string.Empty;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        foreach (var propertyName in new[] { "toolName", "tool", "receipt", "requiredToolReceipt", "name", "selector" })
        {
            if (TryGetPropertyCaseInsensitive(element, propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String)
            {
                return property.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static bool TryGetPropertyCaseInsensitive(
        JsonElement element,
        string propertyName,
        out JsonElement property)
    {
        foreach (var candidate in element.EnumerateObject())
        {
            if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                property = candidate.Value;
                return true;
            }
        }

        property = default;
        return false;
    }
}

[JsonConverter(typeof(JsonStringEnumConverter<ProcessCapabilityScopeDirectiveKind>))]
public enum ProcessCapabilityScopeDirectiveKind
{
    Allow,
    AllowOnly,
    Deny,
    Require
}

[JsonConverter(typeof(JsonStringEnumConverter<ProcessCapabilityScopeTargetKind>))]
public enum ProcessCapabilityScopeTargetKind
{
    Unspecified,
    All,
    CapabilityKind,
    CapabilityKey,
    CapabilityIdentity,
    CapabilityTag,
    RuntimeToolName,
    RuntimeToolProviderKey,
    McpServerKey,
    McpToolName,
    ImplementationKey,
    OperationClassification
}

[JsonConverter(typeof(JsonStringEnumConverter<ProcessRequiredToolReceiptKind>))]
public enum ProcessRequiredToolReceiptKind
{
    RuntimeToolName,
    RuntimeToolProviderKey,
    RuntimeToolNameWithProvider,
    McpToolName
}

[JsonConverter(typeof(JsonStringEnumConverter<ProcessRequiredToolReceiptActivation>))]
public enum ProcessRequiredToolReceiptActivation
{
    Always,
    WhenLaunchContextDeclaresTool
}

[JsonConverter(typeof(JsonStringEnumConverter<ProcessRequiredToolReceiptPurpose>))]
public enum ProcessRequiredToolReceiptPurpose
{
    Unspecified,
    CompletionEvidence,
    AcceptanceProof,
    DefectEvidence,
    LifecycleProof
}

[JsonConverter(typeof(JsonStringEnumConverter<ProcessScopedInstructionPlacement>))]
public enum ProcessScopedInstructionPlacement
{
    AppendToStepBrief
}
