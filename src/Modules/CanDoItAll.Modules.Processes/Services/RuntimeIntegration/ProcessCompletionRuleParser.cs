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

using static CanDoItAll.Modules.Processes.ProcessAgentRightsDiagnosticPolicy;
using static CanDoItAll.Modules.Processes.ProcessCompletionText;
using static CanDoItAll.Modules.Processes.ProcessExecutionMetadataBuilder;
using static CanDoItAll.Modules.Processes.ProcessExecutionResultFactory;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessCompletionRuleParser
{
    internal static ProductCompletionRequiredFileContentCheckResolution ResolveProductCompletionRequiredFileContentChecks(
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

    internal static IReadOnlyList<ProcessCompletionIssueRoute> ResolveCompletionIssueRoutes(
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

    internal static IReadOnlyList<ProcessCompletionIssueRoute> ParseCompletionIssueRoutes(string value)
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

    internal static IReadOnlyList<ProcessCompletionIssueRoute> ParseCompletionIssueRouteElement(JsonElement element)
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

    internal static bool TryReadCompletionIssueRoute(JsonElement element, out ProcessCompletionIssueRoute route)
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

    internal static ProductCompletionRequiredFileContentCheckResolution ParseProductCompletionRequiredFileContentChecks(string value)
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

    internal static ProductCompletionRequiredFileContentCheckResolution ParseProductCompletionRequiredFileContentChecks(JsonElement element)
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

    internal static void AddProductCompletionRequiredFileContentCheck(
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

    internal static IReadOnlyList<IReadOnlyList<string>> ReadRequiredTextAnyGroups(JsonElement element)
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

    internal static IReadOnlyList<IReadOnlyList<string>> ReadForbiddenTextAnyGroups(JsonElement element)
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

    internal static IReadOnlyList<IReadOnlyList<string>> ReadStringGroups(JsonElement element)
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

    internal static IReadOnlyList<string> ReadStringPropertyValues(JsonElement element, params string[] propertyNames)
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

    internal static string ReadFirstStringProperty(JsonElement element, params string[] propertyNames)
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

    internal static bool ReadBooleanProperty(JsonElement element, bool defaultValue, params string[] propertyNames)
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


    internal static IReadOnlyList<string> ReadStringValues(JsonElement element)
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

    internal static bool TryGetPropertyCaseInsensitive(
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
