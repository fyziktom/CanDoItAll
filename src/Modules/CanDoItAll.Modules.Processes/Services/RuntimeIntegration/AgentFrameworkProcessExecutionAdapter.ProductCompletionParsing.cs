using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Drivers.Standard;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class AgentFrameworkProcessExecutionAdapter
{
    private static bool TryResolveInspectableProductRoot(
        IReadOnlyDictionary<string, string> launchVariables,
        out string productRoot)
    {
        productRoot = FirstNonEmpty(
            ResolveLaunchVariable(launchVariables, "OutputFolder"),
            ResolveLaunchVariable(launchVariables, "OutputRoot"),
            ResolveLaunchVariable(launchVariables, "ProductRoot"),
            ResolveLaunchVariable(launchVariables, "ExternalTargetRoot"));
        if (string.IsNullOrWhiteSpace(productRoot) ||
            productRoot.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase) ||
            !Path.IsPathFullyQualified(productRoot))
        {
            productRoot = string.Empty;
            return false;
        }

        productRoot = Path.GetFullPath(productRoot);
        return true;
    }

    private static IReadOnlyList<string> ResolveProductCompletionRequiredPaths(
        IReadOnlyDictionary<string, string> launchVariables,
        string stepKey)
        => ResolveProductCompletionRequiredStringList(
            launchVariables,
            ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths,
            ProcessRuntimeLaunchVariables.ProductCompletionRequiredPathsByStep,
            stepKey);

    private static IReadOnlyList<string> ResolveProductCompletionRequiredToolReceipts(
        IReadOnlyDictionary<string, string> launchVariables,
        string stepKey)
        => ResolveProductCompletionRequiredToolReceiptRules(launchVariables, stepKey)
            .Select(rule => rule.ToolReceipt)
            .Where(receipt => !string.IsNullOrWhiteSpace(receipt))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<ProductCompletionRequiredToolReceiptRule> ResolveProductCompletionRequiredToolReceiptRules(
        IReadOnlyDictionary<string, string> launchVariables,
        string stepKey)
    {
        var direct = ResolveLaunchVariable(launchVariables, ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts);
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return ParseProductCompletionRequiredToolReceiptRules(direct);
        }

        var byStep = ResolveLaunchVariable(launchVariables, ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep);
        if (string.IsNullOrWhiteSpace(byStep) ||
            string.IsNullOrWhiteSpace(stepKey))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(byStep);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!string.Equals(property.Name, stepKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return ParseProductCompletionRequiredToolReceiptRuleElement(property.Value);
            }
        }
        catch (JsonException)
        {
            return [];
        }

        return [];
    }

    private static IReadOnlyList<string> ResolveProductCompletionRequiredStringList(
        IReadOnlyDictionary<string, string> launchVariables,
        string directKey,
        string byStepKey,
        string stepKey)
    {
        var direct = ResolveLaunchVariable(launchVariables, directKey);
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return ParseProductCompletionRequiredStringList(direct);
        }

        var byStep = ResolveLaunchVariable(launchVariables, byStepKey);
        if (string.IsNullOrWhiteSpace(byStep) ||
            string.IsNullOrWhiteSpace(stepKey))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(byStep);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!string.Equals(property.Name, stepKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return ParseProductCompletionRequiredStringListElement(property.Value);
            }
        }
        catch (JsonException)
        {
            return [];
        }

        return [];
    }

    private static IReadOnlyList<string> ParseProductCompletionRequiredStringListElement(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return ParseProductCompletionRequiredStringList(element.GetString() ?? string.Empty);
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return element.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()?.Trim() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<ProductCompletionRequiredToolReceiptRule> ParseProductCompletionRequiredToolReceiptRules(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return ParseProductCompletionRequiredToolReceiptRuleElement(document.RootElement);
        }
        catch (JsonException)
        {
            return value
                .Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => new ProductCompletionRequiredToolReceiptRule(
                    item,
                    [],
                    [],
                    string.Empty,
                    string.Empty,
                    string.Empty))
                .DistinctBy(rule => rule.ToolReceipt, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    private static IReadOnlyList<ProductCompletionRequiredToolReceiptRule> ParseProductCompletionRequiredToolReceiptRuleElement(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return ParseProductCompletionRequiredToolReceiptRules(element.GetString() ?? string.Empty);
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            return TryReadProductCompletionRequiredToolReceiptRule(element, out var rule)
                ? [rule]
                : [];
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return element
            .EnumerateArray()
            .SelectMany(ParseProductCompletionRequiredToolReceiptRuleElement)
            .Where(rule => !string.IsNullOrWhiteSpace(rule.ToolReceipt))
            .GroupBy(rule => string.IsNullOrWhiteSpace(rule.Key) ? rule.ToolReceipt : rule.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static bool TryReadProductCompletionRequiredToolReceiptRule(
        JsonElement element,
        out ProductCompletionRequiredToolReceiptRule rule)
    {
        var toolReceipt = ReadFirstStringProperty(element, "toolName", "tool", "receipt", "requiredToolReceipt", "name", "selector");
        if (string.IsNullOrWhiteSpace(toolReceipt))
        {
            rule = new ProductCompletionRequiredToolReceiptRule(string.Empty, [], [], string.Empty, string.Empty, string.Empty);
            return false;
        }

        rule = new ProductCompletionRequiredToolReceiptRule(
            toolReceipt,
            ReadStringPropertyValues(element, "applicableBranchOutcomeKeys", "appliesToBranchOutcomeKeys", "branchOutcomeKeys", "whenBranchOutcomeKeys", "requiredForBranchOutcomeKeys", "enforceBranchOutcomeKeys"),
            ReadStringPropertyValues(element, "skipBranchOutcomeKeys", "skippedBranchOutcomeKeys", "excludedBranchOutcomeKeys"),
            ReadFirstStringProperty(element, "purpose", "receiptPurpose"),
            ReadFirstStringProperty(element, "key", "id"),
            ReadFirstStringProperty(element, "reason", "description"));
        return true;
    }

    private static IReadOnlyList<string> ParseProductCompletionRequiredStringList(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return ParseProductCompletionRequiredStringListElement(document.RootElement);
        }
        catch (JsonException)
        {
            return value
                .Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    private static ProductCompletionRequiredFileContentCheckResolution ResolveProductCompletionRequiredFileContentChecks(
        IReadOnlyDictionary<string, string> launchVariables,
        string stepKey)
    {
        var direct = ResolveLaunchVariable(launchVariables, ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks);
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return ParseProductCompletionRequiredFileContentChecks(direct);
        }

        var byStep = ResolveLaunchVariable(launchVariables, ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecksByStep);
        if (string.IsNullOrWhiteSpace(byStep) ||
            string.IsNullOrWhiteSpace(stepKey))
        {
            return ProductCompletionRequiredFileContentCheckResolution.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(byStep);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return ProductCompletionRequiredFileContentCheckResolution.Invalid("by-step value must be a JSON object.");
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, stepKey, StringComparison.OrdinalIgnoreCase))
                {
                    return ParseProductCompletionRequiredFileContentChecks(property.Value);
                }
            }
        }
        catch (JsonException exception)
        {
            return ProductCompletionRequiredFileContentCheckResolution.Invalid(exception.Message);
        }

        return ProductCompletionRequiredFileContentCheckResolution.Empty;
    }

    private static IReadOnlyList<ProcessCompletionIssueRoute> ResolveCompletionIssueRoutes(
        IReadOnlyDictionary<string, string> launchVariables,
        string stepKey)
    {
        var direct = ResolveLaunchVariable(launchVariables, ProcessRuntimeLaunchVariables.CompletionIssueRoutes);
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return ParseCompletionIssueRoutes(direct);
        }

        var byStep = ResolveLaunchVariable(launchVariables, ProcessRuntimeLaunchVariables.CompletionIssueRoutesByStep);
        if (string.IsNullOrWhiteSpace(byStep) ||
            string.IsNullOrWhiteSpace(stepKey))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(byStep);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, stepKey, StringComparison.OrdinalIgnoreCase))
                {
                    return ParseCompletionIssueRouteElement(property.Value);
                }
            }
        }
        catch (JsonException)
        {
            return [];
        }

        return [];
    }

    private static IReadOnlyList<ProcessCompletionIssueRoute> ParseCompletionIssueRoutes(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return ParseCompletionIssueRouteElement(document.RootElement);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<ProcessCompletionIssueRoute> ParseCompletionIssueRouteElement(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            return TryReadCompletionIssueRoute(element, out var route)
                ? [route]
                : [];
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return element
            .EnumerateArray()
            .SelectMany(ParseCompletionIssueRouteElement)
            .Where(route => !string.IsNullOrWhiteSpace(route.IssueCode) &&
                            !string.IsNullOrWhiteSpace(route.TargetBranchOutcomeKey))
            .ToArray();
    }

    private static bool TryReadCompletionIssueRoute(JsonElement element, out ProcessCompletionIssueRoute route)
    {
        var issueCode = ReadFirstStringProperty(element, "issueCode", "code", "completionIssueCode");
        var targetBranchOutcomeKey = ReadFirstStringProperty(element, "targetBranchOutcomeKey", "routeBranchOutcomeKey", "branchOutcomeKey");
        if (string.IsNullOrWhiteSpace(issueCode) ||
            string.IsNullOrWhiteSpace(targetBranchOutcomeKey))
        {
            route = new ProcessCompletionIssueRoute(string.Empty, [], string.Empty, string.Empty, false);
            return false;
        }

        route = new ProcessCompletionIssueRoute(
            issueCode,
            ReadStringPropertyValues(element, "sourceBranchOutcomeKeys", "fromBranchOutcomeKeys", "whenBranchOutcomeKeys"),
            targetBranchOutcomeKey,
            ReadFirstStringProperty(element, "targetBranchOutcomeTitle", "branchOutcomeTitle", "title"),
            ReadBooleanProperty(element, defaultValue: false, "requiresDefectEvidence", "requireDefectEvidence"));
        return true;
    }

    private static ProductCompletionRequiredFileContentCheckResolution ParseProductCompletionRequiredFileContentChecks(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return ParseProductCompletionRequiredFileContentChecks(document.RootElement);
        }
        catch (JsonException exception)
        {
            return ProductCompletionRequiredFileContentCheckResolution.Invalid(exception.Message);
        }
    }

    private static ProductCompletionRequiredFileContentCheckResolution ParseProductCompletionRequiredFileContentChecks(JsonElement element)
    {
        var checks = new List<ProductCompletionRequiredFileContentCheck>();
        var errors = new List<string>();

        if (element.ValueKind == JsonValueKind.Object)
        {
            AddProductCompletionRequiredFileContentCheck(element, checks, errors);
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                AddProductCompletionRequiredFileContentCheck(item, checks, errors);
            }
        }
        else
        {
            errors.Add("value must be a JSON object or array of objects.");
        }

        if (errors.Count > 0)
        {
            return ProductCompletionRequiredFileContentCheckResolution.Invalid(string.Join("; ", errors));
        }

        return new ProductCompletionRequiredFileContentCheckResolution(checks, string.Empty);
    }

    private static void AddProductCompletionRequiredFileContentCheck(
        JsonElement element,
        List<ProductCompletionRequiredFileContentCheck> checks,
        List<string> errors)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            errors.Add("each check must be a JSON object.");
            return;
        }

        var pathCandidates = ReadStringPropertyValues(element, "pathCandidates", "paths", "path");
        if (pathCandidates.Count == 0)
        {
            errors.Add("each check must declare at least one path candidate.");
            return;
        }

        var requiredTextAnyGroups = ReadRequiredTextAnyGroups(element);
        var forbiddenTextAnyGroups = ReadForbiddenTextAnyGroups(element);
        if (requiredTextAnyGroups.Count == 0 &&
            forbiddenTextAnyGroups.Count == 0)
        {
            errors.Add("each check must declare at least one required or forbidden text group.");
            return;
        }

        checks.Add(new ProductCompletionRequiredFileContentCheck(
            pathCandidates,
            requiredTextAnyGroups,
            forbiddenTextAnyGroups,
            ReadBooleanProperty(element, defaultValue: true, "mustExist", "requiresPath"),
            ReadStringPropertyValues(element, "enforceBranchOutcomeKeys", "branchOutcomeKeys", "whenBranchOutcomeKey"),
            ReadStringPropertyValues(element, "evidenceBranchOutcomeKeys", "defectEvidenceBranchOutcomeKeys", "satisfiesBranchOutcomeKeys")));
    }

    private static IReadOnlyList<IReadOnlyList<string>> ReadRequiredTextAnyGroups(JsonElement element)
    {
        if (TryGetPropertyCaseInsensitive(element, "requiredTextAnyGroups", out var groupsElement) ||
            TryGetPropertyCaseInsensitive(element, "containsAnyGroups", out groupsElement))
        {
            return ReadStringGroups(groupsElement);
        }

        if (TryGetPropertyCaseInsensitive(element, "requiredTextAny", out var anyElement) ||
            TryGetPropertyCaseInsensitive(element, "containsAny", out anyElement))
        {
            var values = ReadStringValues(anyElement);
            return values.Count == 0
                ? []
                : [values];
        }

        if (TryGetPropertyCaseInsensitive(element, "requiredText", out var allElement) ||
            TryGetPropertyCaseInsensitive(element, "containsAll", out allElement))
        {
            return ReadStringValues(allElement)
                .Select(value => (IReadOnlyList<string>)[value])
                .ToArray();
        }

        return [];
    }

    private static IReadOnlyList<IReadOnlyList<string>> ReadForbiddenTextAnyGroups(JsonElement element)
    {
        if (TryGetPropertyCaseInsensitive(element, "forbiddenTextAnyGroups", out var groupsElement) ||
            TryGetPropertyCaseInsensitive(element, "mustNotContainAnyGroups", out groupsElement) ||
            TryGetPropertyCaseInsensitive(element, "absentTextAnyGroups", out groupsElement))
        {
            return ReadStringGroups(groupsElement);
        }

        if (TryGetPropertyCaseInsensitive(element, "forbiddenTextAny", out var anyElement) ||
            TryGetPropertyCaseInsensitive(element, "mustNotContainAny", out anyElement) ||
            TryGetPropertyCaseInsensitive(element, "absentTextAny", out anyElement))
        {
            var values = ReadStringValues(anyElement);
            return values.Count == 0
                ? []
                : [values];
        }

        if (TryGetPropertyCaseInsensitive(element, "forbiddenText", out var allElement) ||
            TryGetPropertyCaseInsensitive(element, "mustNotContain", out allElement) ||
            TryGetPropertyCaseInsensitive(element, "absentText", out allElement))
        {
            return ReadStringValues(allElement)
                .Select(value => (IReadOnlyList<string>)[value])
                .ToArray();
        }

        return [];
    }

    private static IReadOnlyList<IReadOnlyList<string>> ReadStringGroups(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            var scalarValues = ReadStringValues(element);
            return scalarValues.Count == 0
                ? []
                : [scalarValues];
        }

        var groups = new List<IReadOnlyList<string>>();
        foreach (var item in element.EnumerateArray())
        {
            var values = ReadStringValues(item);
            if (values.Count > 0)
            {
                groups.Add(values);
            }
        }

        return groups;
    }

    private static IReadOnlyList<string> ReadStringPropertyValues(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryGetPropertyCaseInsensitive(element, propertyName, out var property))
            {
                return ReadStringValues(property);
            }
        }

        return [];
    }

    private static string ReadFirstStringProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryGetPropertyCaseInsensitive(element, propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String)
            {
                return property.GetString()?.Trim() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static bool ReadBooleanProperty(JsonElement element, bool defaultValue, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!TryGetPropertyCaseInsensitive(element, propertyName, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (property.ValueKind == JsonValueKind.False)
            {
                return false;
            }
        }

        if (TryGetPropertyCaseInsensitive(element, "allowMissing", out var allowMissingProperty))
        {
            if (allowMissingProperty.ValueKind == JsonValueKind.True)
            {
                return false;
            }

            if (allowMissingProperty.ValueKind == JsonValueKind.False)
            {
                return true;
            }
        }

        return defaultValue;
    }


    private static IReadOnlyList<string> ReadStringValues(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString()?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(value)
                ? []
                : [value];
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return element.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()?.Trim() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
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

    private static bool TryResolveRequiredProductPath(
        string productRoot,
        string requiredPath,
        out string resolvedPath,
        out string invalidReason)
    {
        resolvedPath = string.Empty;
        invalidReason = string.Empty;
        var candidate = requiredPath.Trim().Trim('"', '\'');
        if (string.IsNullOrWhiteSpace(candidate))
        {
            invalidReason = "empty path";
            return false;
        }

        if (TryConvertExternalTargetAliasToNativePath(candidate, out var nativePath))
        {
            candidate = nativePath;
        }

        try
        {
            resolvedPath = Path.GetFullPath(Path.IsPathFullyQualified(candidate)
                ? candidate
                : Path.Combine(productRoot, candidate));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            invalidReason = exception.Message;
            return false;
        }

        if (!IsSameOrChildPath(productRoot, resolvedPath))
        {
            invalidReason = "outside product root";
            return false;
        }

        return true;
    }

    private static bool TryConvertExternalTargetAliasToNativePath(string value, out string nativePath)
    {
        nativePath = string.Empty;
        var normalized = value.Trim().Replace('\\', '/');
        if (!normalized.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2 ||
            segments[1].Length != 1 ||
            !char.IsLetter(segments[1][0]))
        {
            return false;
        }

        var driveRoot = $"{char.ToUpperInvariant(segments[1][0])}:{Path.DirectorySeparatorChar}";
        nativePath = segments.Length == 2
            ? driveRoot
            : Path.Combine(new[] { driveRoot }.Concat(segments.Skip(2)).ToArray());
        return true;
    }

    private static bool IsSameOrChildPath(string root, string candidate)
    {
        var normalizedRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedCandidate = Path.GetFullPath(candidate)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(normalizedRoot, normalizedCandidate, StringComparison.OrdinalIgnoreCase) ||
               normalizedCandidate.StartsWith(
                   normalizedRoot + Path.DirectorySeparatorChar,
                   StringComparison.OrdinalIgnoreCase) ||
               normalizedCandidate.StartsWith(
                   normalizedRoot + Path.AltDirectorySeparatorChar,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static ProductRootInspection InspectProductRoot(string productRoot)
    {
        try
        {
            if (!Directory.Exists(productRoot))
            {
                return new ProductRootInspection(false, "the directory does not exist");
            }

            return Directory
                .EnumerateFiles(productRoot, "*", SearchOption.AllDirectories)
                .Any(file => IsProductFile(productRoot, file))
                ? new ProductRootInspection(true, string.Empty)
                : new ProductRootInspection(false, "no product files were found");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException or ArgumentException or NotSupportedException)
        {
            return new ProductRootInspection(false, exception.Message);
        }
    }

    private static bool IsProductFile(string productRoot, string file)
    {
        var relativePath = Path.GetRelativePath(productRoot, file);
        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Any(IsIgnoredProductPathSegment))
        {
            return false;
        }

        var fileName = Path.GetFileName(file);
        return !string.Equals(fileName, ".gitkeep", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(fileName, ".DS_Store", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(fileName, "Thumbs.db", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIgnoredProductPathSegment(string segment)
        => string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(segment, ".vs", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(segment, "node_modules", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(segment, "packages", StringComparison.OrdinalIgnoreCase);

}
