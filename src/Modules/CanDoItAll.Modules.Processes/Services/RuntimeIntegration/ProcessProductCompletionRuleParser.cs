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
using static CanDoItAll.Modules.Processes.ProcessCompletionRuleParser;
using static CanDoItAll.Modules.Processes.ProcessExecutionMetadataBuilder;
using static CanDoItAll.Modules.Processes.ProcessExecutionResultFactory;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessProductCompletionRuleParser
{
    internal static IReadOnlyList<string> ResolveProductCompletionRequiredPaths(
        IReadOnlyDictionary<string, string> launchVariables,
        string stepKey)
        => ResolveProductCompletionRequiredStringList(
            launchVariables,
            ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths,
            ProcessRuntimeLaunchVariables.ProductCompletionRequiredPathsByStep,
            stepKey);

    internal static IReadOnlyList<string> ResolveProductCompletionRequiredToolReceipts(
        IReadOnlyDictionary<string, string> launchVariables,
        string stepKey)
        => ResolveProductCompletionRequiredToolReceiptRules(launchVariables, stepKey)
            .Select(rule => rule.ToolReceipt)
            .Where(receipt => !string.IsNullOrWhiteSpace(receipt))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    internal static IReadOnlyList<string> ResolveProductMutationRequiredBranchOutcomeKeys(
        IReadOnlyDictionary<string, string> launchVariables,
        string stepKey)
        => ResolveProductCompletionRequiredStringList(
            launchVariables,
            ProcessRuntimeLaunchVariables.ProductMutationRequiredBranchOutcomeKeys,
            ProcessRuntimeLaunchVariables.ProductMutationRequiredBranchOutcomeKeysByStep,
            stepKey);

    internal static IReadOnlyList<string> ResolveRuntimeRoutedBranchOutcomeKeys(
        IReadOnlyDictionary<string, string> launchVariables,
        string stepKey)
        => ResolveProductCompletionRequiredStringList(
            launchVariables,
            "RuntimeRoutedBranchOutcomeKeys",
            ProcessRuntimeLaunchVariables.RuntimeRoutedBranchOutcomeKeysByStep,
            stepKey);

    internal static IReadOnlyList<ProductCompletionRequiredToolReceiptRule> ResolveProductCompletionRequiredToolReceiptRules(
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

    internal static IReadOnlyList<string> ResolveProductCompletionRequiredStringList(
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

    internal static IReadOnlyList<string> ParseProductCompletionRequiredStringListElement(JsonElement element)
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

    internal static IReadOnlyList<ProductCompletionRequiredToolReceiptRule> ParseProductCompletionRequiredToolReceiptRules(string value)
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

    internal static IReadOnlyList<ProductCompletionRequiredToolReceiptRule> ParseProductCompletionRequiredToolReceiptRuleElement(JsonElement element)
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

    internal static bool TryReadProductCompletionRequiredToolReceiptRule(
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

    internal static IReadOnlyList<string> ParseProductCompletionRequiredStringList(string value)
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
}
