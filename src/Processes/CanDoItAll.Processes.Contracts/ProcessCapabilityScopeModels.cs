using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Processes.Contracts;

public static class ProcessProductToolReceiptRequirements
{
    public const string BrowserInteractionProof = "browser interaction proof";
}

public sealed class ProcessCapabilityScope
{
    private const int MaximumDirectives = 128;
    private const int MaximumInstructionFragments = 128;
    private const int MaximumIdentifierLength = 128;
    private const int MaximumNarrativeLength = ProcessPublicReceiptTextPolicy.MaximumPublicMessageLength;
    private const int MaximumRequiredReceiptCount = 1_024;

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

        if (!HasValidShape(scope))
        {
            return CreateInvalidContractScope();
        }

        return new ProcessCapabilityScope
        {
            Directives = scope.Directives
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

    internal static bool HasValidShape(ProcessCapabilityScope? scope)
    {
        if (scope is null)
        {
            return true;
        }

        return scope.Directives is not null &&
               scope.Directives.Count <= MaximumDirectives &&
               scope.InstructionFragments is not null &&
               scope.InstructionFragments.Count <= MaximumInstructionFragments &&
               ProcessRequiredRuntimeToolNames.HasValidRequiredReceiptShapes(scope) &&
               scope.Directives.All(IsValidDirectiveShape) &&
               scope.InstructionFragments.All(IsValidInstructionFragmentShape) &&
               scope.RequiredReceipts.All(IsValidRequiredReceiptShape);
    }

    private static ProcessCapabilityScope CreateInvalidContractScope()
    {
        return new ProcessCapabilityScope
        {
            RequiredReceipts =
            [
                new ProcessRequiredToolReceipt
                {
                    Key = "invalid-runtime-tool-contract",
                    ToolName = ProcessRequiredRuntimeToolNames.InvalidRuntimeToolContractMarker
                }
            ]
        };
    }

    private static bool IsValidDirectiveShape(ProcessCapabilityScopeDirective? directive)
    {
        if (directive is null ||
            !Enum.IsDefined(directive.Kind) ||
            directive.Target is null ||
            !Enum.IsDefined(directive.Target.Kind) ||
            directive.Target.Kind == ProcessCapabilityScopeTargetKind.Unspecified ||
            directive.Target.Value is null ||
            directive.Target.SecondaryValue is null ||
            directive.Reason is null ||
            !IsBoundedNarrative(directive.Reason) ||
            !IsBoundedSelector(directive.Target.Value, allowEmpty: directive.Target.Kind == ProcessCapabilityScopeTargetKind.All) ||
            !IsBoundedSelector(directive.Target.SecondaryValue, allowEmpty: true))
        {
            return false;
        }

        return directive.Target.Kind switch
        {
            ProcessCapabilityScopeTargetKind.All => true,
            ProcessCapabilityScopeTargetKind.CapabilityIdentity or ProcessCapabilityScopeTargetKind.McpToolName =>
                !string.IsNullOrWhiteSpace(directive.Target.Value) &&
                !string.IsNullOrWhiteSpace(directive.Target.SecondaryValue),
            _ => !string.IsNullOrWhiteSpace(directive.Target.Value)
        };
    }

    private static bool IsValidInstructionFragmentShape(ProcessScopedInstructionFragment? fragment)
    {
        return fragment is not null &&
               fragment.Key is not null &&
               fragment.Title is not null &&
               fragment.Content is not null &&
               IsBoundedSelector(fragment.Key, allowEmpty: false) &&
               IsBoundedNarrative(fragment.Title) &&
               IsBoundedNarrative(fragment.Content) &&
               Enum.IsDefined(fragment.Placement);
    }

    private static bool IsValidRequiredReceiptShape(ProcessRequiredToolReceipt receipt)
    {
        if (!IsBoundedNarrative(receipt.Reason) ||
            receipt.MinimumCount > MaximumRequiredReceiptCount ||
            !receipt.ApplicableBranchOutcomeKeys.All(value => IsBoundedSelector(value, allowEmpty: false)))
        {
            return false;
        }

        var normalized = NormalizeRequiredReceipt(receipt);
        if (!IsValidReceiptKey(normalized.Key))
        {
            return false;
        }

        return normalized.Kind switch
        {
            ProcessRequiredToolReceiptKind.RuntimeToolName =>
                ProcessRequiredRuntimeToolNames.IsCanonicalRuntimeToolName(normalized.ToolName),
            ProcessRequiredToolReceiptKind.RuntimeToolProviderKey =>
                IsBoundedSelector(normalized.RuntimeToolProviderKey, allowEmpty: false),
            ProcessRequiredToolReceiptKind.RuntimeToolNameWithProvider =>
                ProcessRequiredRuntimeToolNames.IsCanonicalRuntimeToolName(normalized.ToolName) &&
                IsBoundedSelector(normalized.RuntimeToolProviderKey, allowEmpty: false),
            ProcessRequiredToolReceiptKind.McpToolName =>
                IsBoundedSelector(normalized.ToolName, allowEmpty: false) &&
                IsBoundedSelector(normalized.McpServerKey, allowEmpty: true),
            _ => false
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

    private static bool IsBoundedNarrative(string value)
        => value.Length <= MaximumNarrativeLength;

    private static bool IsBoundedSelector(string value, bool allowEmpty)
    {
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
            value.Length > MaximumIdentifierLength)
        {
            return false;
        }

        if (value.Length == 0)
        {
            return allowEmpty;
        }

        return value.All(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or ':' or '@');
    }

    private static bool IsValidReceiptKey(string value)
        => value.Length <= MaximumIdentifierLength &&
           value.Length > 0 &&
           value.All(character =>
               char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or ':' or '@' or ',');
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
    public const string InvalidRuntimeToolContractMarker = "$invalid-runtime-tool-contract";
    public const int MaximumCount = 64;
    public const int MaximumNameLength = 128;
    public const int MaximumBranchOutcomeCount = 32;
    public const int MaximumSerializedReceiptContractLength = 65_536;

    public static bool HasInvalidRuntimeToolContract(IEnumerable<string>? runtimeToolNames)
        => runtimeToolNames?.Contains(InvalidRuntimeToolContractMarker, StringComparer.Ordinal) == true;

    public static IReadOnlyList<string> FromCapabilityScope(ProcessCapabilityScope? capabilityScope)
        => FromCapabilityScope(capabilityScope, activeLaunchContextToolNames: null);

    public static IReadOnlyList<string> FromCapabilityScope(
        ProcessCapabilityScope? capabilityScope,
        IReadOnlySet<string>? activeLaunchContextToolNames)
        => FromCapabilityScope(
            capabilityScope,
            activeLaunchContextToolNames,
            includeBranchScopedReceipts: true);

    public static IReadOnlyList<string> FromUnconditionalCapabilityScope(ProcessCapabilityScope? capabilityScope)
        => FromUnconditionalCapabilityScope(capabilityScope, activeLaunchContextToolNames: null);

    public static IReadOnlyList<string> FromUnconditionalCapabilityScope(
        ProcessCapabilityScope? capabilityScope,
        IReadOnlySet<string>? activeLaunchContextToolNames)
        => FromCapabilityScope(
            capabilityScope,
            activeLaunchContextToolNames,
            includeBranchScopedReceipts: false);

    private static IReadOnlyList<string> FromCapabilityScope(
        ProcessCapabilityScope? capabilityScope,
        IReadOnlySet<string>? activeLaunchContextToolNames,
        bool includeBranchScopedReceipts)
    {
        if (!HasValidRequiredReceiptShapes(capabilityScope))
        {
            return [InvalidRuntimeToolContractMarker];
        }

        var normalized = ProcessCapabilityScope.Normalize(capabilityScope);
        var candidates = normalized.RequiredReceipts
            .Where(receipt => IsActive(receipt, activeLaunchContextToolNames))
            .Where(receipt => includeBranchScopedReceipts || receipt.ApplicableBranchOutcomeKeys.Count == 0)
            .Select(ResolveRuntimeToolName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();
        var resolved = candidates
            .Select(ResolveRuntimeToolNameCandidate)
            .ToArray();
        if (resolved.Any(string.IsNullOrWhiteSpace))
        {
            return [InvalidRuntimeToolContractMarker];
        }

        return resolved
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static bool HasValidRequiredReceiptShapes(ProcessCapabilityScope? capabilityScope)
    {
        if (capabilityScope is null)
        {
            return true;
        }

        if (capabilityScope.RequiredReceipts is null ||
            capabilityScope.RequiredReceipts.Count > MaximumCount)
        {
            return false;
        }

        return capabilityScope.RequiredReceipts.All(receipt =>
            receipt is not null &&
            Enum.IsDefined(receipt.Kind) &&
            Enum.IsDefined(receipt.Activation) &&
            Enum.IsDefined(receipt.Purpose) &&
            receipt.Key is not null &&
            receipt.ToolName is not null &&
            receipt.RuntimeToolProviderKey is not null &&
            receipt.McpServerKey is not null &&
            receipt.Reason is not null &&
            IsBoundedReceiptValue(receipt.Key) &&
            IsBoundedReceiptValue(receipt.ToolName) &&
            IsBoundedReceiptValue(receipt.RuntimeToolProviderKey) &&
            IsBoundedReceiptValue(receipt.McpServerKey) &&
            receipt.MinimumCount > 0 &&
            receipt.ApplicableBranchOutcomeKeys is not null &&
            receipt.ApplicableBranchOutcomeKeys.Count <= MaximumBranchOutcomeCount &&
            receipt.ApplicableBranchOutcomeKeys.All(value =>
                value is not null && value.Length <= MaximumNameLength) &&
            receipt.Kind switch
            {
                ProcessRequiredToolReceiptKind.RuntimeToolName =>
                    !string.IsNullOrWhiteSpace(receipt.ToolName),
                ProcessRequiredToolReceiptKind.RuntimeToolProviderKey =>
                    !string.IsNullOrWhiteSpace(receipt.RuntimeToolProviderKey),
                ProcessRequiredToolReceiptKind.RuntimeToolNameWithProvider =>
                    !string.IsNullOrWhiteSpace(receipt.ToolName) &&
                    !string.IsNullOrWhiteSpace(receipt.RuntimeToolProviderKey),
                ProcessRequiredToolReceiptKind.McpToolName =>
                    !string.IsNullOrWhiteSpace(receipt.ToolName),
                _ => false
            });
    }

    public static IReadOnlyList<string> FromProductCompletionRequiredToolReceipts(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        if (value.Length > MaximumSerializedReceiptContractLength)
        {
            return [InvalidRuntimeToolContractMarker];
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return FromProductCompletionRequiredToolReceipts(document.RootElement);
        }
        catch (JsonException)
        {
            return NormalizeProductCompletionRuntimeToolNameCandidates(
                value.Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
    }

    public static IReadOnlyList<string> FromUnconditionalProductCompletionRequiredToolReceipts(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        if (value.Length > MaximumSerializedReceiptContractLength)
        {
            return [InvalidRuntimeToolContractMarker];
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return FromUnconditionalProductCompletionRequiredToolReceipts(document.RootElement);
        }
        catch (JsonException)
        {
            return NormalizeProductCompletionRuntimeToolNameCandidates(
                value.Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
    }

    public static IReadOnlyList<string> FromProductCompletionRequiredToolReceipts(JsonElement element)
    {
        if (!IsBoundedProductCompletionReceiptElement(element))
        {
            return [InvalidRuntimeToolContractMarker];
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return FromProductCompletionRequiredToolReceipts(element.GetString());
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            return NormalizeProductCompletionRuntimeToolNameCandidates(
                [ReadProductCompletionRequiredToolReceiptCandidate(element)]);
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return [InvalidRuntimeToolContractMarker];
        }

        return NormalizeProductCompletionRuntimeToolNameCandidates(element.EnumerateArray()
            .Select(ReadProductCompletionRequiredToolReceiptCandidate));
    }

    public static IReadOnlyList<string> FromUnconditionalProductCompletionRequiredToolReceipts(JsonElement element)
    {
        if (!IsBoundedProductCompletionReceiptElement(element))
        {
            return [InvalidRuntimeToolContractMarker];
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return FromUnconditionalProductCompletionRequiredToolReceipts(element.GetString());
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            var candidate = ReadProductCompletionRequiredToolReceiptCandidate(element);
            if (!IsValidProductCompletionRuntimeToolNameCandidate(candidate))
            {
                return [InvalidRuntimeToolContractMarker];
            }

            return HasBranchCondition(element)
                ? []
                : NormalizeProductCompletionRuntimeToolNameCandidates([candidate]);
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return [InvalidRuntimeToolContractMarker];
        }

        var candidates = element
            .EnumerateArray()
            .Select(item => new
            {
                Value = ReadProductCompletionRequiredToolReceiptCandidate(item),
                IsBranchScoped = item.ValueKind == JsonValueKind.Object && HasBranchCondition(item)
            })
            .ToArray();
        if (candidates.Any(candidate => !IsValidProductCompletionRuntimeToolNameCandidate(candidate.Value)))
        {
            return [InvalidRuntimeToolContractMarker];
        }

        return NormalizeProductCompletionRuntimeToolNameCandidates(candidates
            .Where(candidate => !candidate.IsBranchScoped)
            .Select(candidate => candidate.Value));
    }

    public static IReadOnlyList<string> FromProductCompletionRequiredToolReceipts(IEnumerable<string>? requiredToolReceipts)
        => NormalizeProductCompletionRuntimeToolNameCandidates(requiredToolReceipts);

    private static IReadOnlyList<string> NormalizeProductCompletionRuntimeToolNameCandidates(
        IEnumerable<string>? candidates)
    {
        if (candidates is null)
        {
            return [];
        }

        var boundedCandidates = candidates.Take(MaximumCount + 1).ToArray();
        if (boundedCandidates.Length > MaximumCount ||
            boundedCandidates.Any(value => value is null || value.Length > MaximumNameLength))
        {
            return [InvalidRuntimeToolContractMarker];
        }

        var normalized = new List<string>();
        foreach (var value in boundedCandidates)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var toolName = ResolveRuntimeToolNameCandidate(value);
            if (!string.IsNullOrWhiteSpace(toolName))
            {
                normalized.Add(toolName);
                continue;
            }

            if (!IsStandaloneProductCompletionPredicate(value))
            {
                return [InvalidRuntimeToolContractMarker];
            }
        }

        return normalized
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsStandaloneProductCompletionPredicate(string value)
    {
        var trimmed = value.Trim();
        if (string.Equals(
                trimmed,
                ProcessProductToolReceiptRequirements.BrowserInteractionProof,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var separatorIndex = trimmed.IndexOf('=');
        if (separatorIndex <= 0 || separatorIndex == trimmed.Length - 1 || trimmed.Contains('|'))
        {
            return false;
        }

        var key = trimmed[..separatorIndex];
        return key.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("template", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidProductCompletionRuntimeToolNameCandidate(string value)
        => !string.IsNullOrWhiteSpace(ResolveRuntimeToolNameCandidate(value)) ||
           IsStandaloneProductCompletionPredicate(value);

    public static IReadOnlyList<string> NormalizeRuntimeToolNameCandidates(IEnumerable<string>? candidates)
    {
        if (candidates is null)
        {
            return [];
        }

        var boundedCandidates = candidates.Take(MaximumCount + 1).ToArray();
        if (boundedCandidates.Length > MaximumCount ||
            boundedCandidates.Any(value => value is null || value.Length > MaximumNameLength))
        {
            return [InvalidRuntimeToolContractMarker];
        }

        return boundedCandidates
            .Select(ResolveRuntimeToolNameCandidate)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<string> NormalizeDeclaredRuntimeToolNames(IEnumerable<string>? candidates)
    {
        if (candidates is null)
        {
            return [];
        }

        var values = candidates.ToArray();
        if (values.Length > MaximumCount ||
            values.Any(value =>
                value is null ||
                value.Length > MaximumNameLength ||
                !IsCanonicalRuntimeToolName(value)))
        {
            return [InvalidRuntimeToolContractMarker];
        }

        var normalized = values
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return normalized.Length <= MaximumCount
            ? normalized
            : [InvalidRuntimeToolContractMarker];
    }

    public static bool IsValidBoundedContract(IReadOnlyCollection<string>? runtimeToolNames)
        => runtimeToolNames is not null &&
           runtimeToolNames.Count <= MaximumCount &&
           runtimeToolNames.All(toolName =>
               toolName is not null &&
               toolName.Length <= MaximumNameLength &&
               IsCanonicalRuntimeToolName(toolName));

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

    public static bool IsCanonicalRuntimeToolName(string? value)
        => value is not null &&
           string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
           !string.Equals(value, InvalidRuntimeToolContractMarker, StringComparison.Ordinal) &&
           LooksLikeConcreteRuntimeToolName(value);

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
            return InvalidRuntimeToolContractMarker;
        }

        foreach (var propertyName in new[] { "toolName", "tool", "toolReceipt", "receipt", "requiredToolReceipt", "name", "selector" })
        {
            if (TryGetPropertyCaseInsensitive(element, propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String)
            {
                return property.GetString() ?? string.Empty;
            }
        }

        return InvalidRuntimeToolContractMarker;
    }

    private static bool IsBoundedReceiptValue(string value)
        => value.Length <= MaximumNameLength;

    private static bool IsBoundedProductCompletionReceiptElement(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return (element.GetString()?.Length ?? 0) <= MaximumNameLength;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            return element.GetArrayLength() <= MaximumCount &&
                   element.EnumerateArray().All(IsBoundedProductCompletionReceiptElement);
        }

        if (element.ValueKind != JsonValueKind.Object ||
            element.EnumerateObject().Take(33).Count() > 32)
        {
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String &&
                (property.Value.GetString()?.Length ?? 0) > MaximumNameLength)
            {
                return false;
            }

            if (property.Name.Contains("BranchOutcome", StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.Array &&
                (property.Value.GetArrayLength() > MaximumBranchOutcomeCount ||
                 property.Value.EnumerateArray().Any(item =>
                     item.ValueKind != JsonValueKind.String ||
                     (item.GetString()?.Length ?? 0) > MaximumNameLength)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasBranchCondition(JsonElement element)
    {
        foreach (var propertyName in new[]
                 {
                     "applicableBranchOutcomeKeys",
                     "appliesToBranchOutcomeKeys",
                     "branchOutcomeKeys",
                     "whenBranchOutcomeKeys",
                     "requiredForBranchOutcomeKeys",
                     "enforceBranchOutcomeKeys",
                     "skipBranchOutcomeKeys",
                     "skippedBranchOutcomeKeys",
                     "excludedBranchOutcomeKeys"
                 })
        {
            if (!TryGetPropertyCaseInsensitive(element, propertyName, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Array &&
                property.EnumerateArray().Any(item =>
                    item.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(item.GetString())))
            {
                return true;
            }

            if (property.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(property.GetString()))
            {
                return true;
            }
        }

        return false;
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
