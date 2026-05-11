using System.Globalization;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IWorkflowRoutingCompiler
{
    WorkflowCompiledRoute CompilePredicate(WorkflowDefinition definition, WorkflowEdge edge);

    WorkflowCompiledFanOutRoute CompileFanOut(
        WorkflowDefinition definition,
        WorkflowNodeId sourceNodeId,
        IReadOnlyList<WorkflowEdge> fanOutEdges);
}

public sealed record WorkflowCompiledRoute(
    WorkflowEdgeId EdgeId,
    string Label,
    Func<WorkflowNodeInput?, bool> Predicate);

public sealed record WorkflowCompiledFanOutRoute(
    WorkflowNodeId SourceNodeId,
    IReadOnlyList<WorkflowEdgeId> OrderedEdgeIds,
    IReadOnlyList<WorkflowNodeId> OrderedTargetNodeIds,
    Func<WorkflowNodeInput?, int, IEnumerable<int>> TargetSelector);

public sealed class BuiltInJsonWorkflowRoutingCompiler : IWorkflowRoutingCompiler
{
    public WorkflowCompiledRoute CompilePredicate(WorkflowDefinition definition, WorkflowEdge edge)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(edge);

        var predicate = CompileBuiltInPredicate(edge);
        return new WorkflowCompiledRoute(
            edge.Id,
            WorkflowRoutingValidation.GetRouteLabel(edge),
            predicate);
    }

    public WorkflowCompiledFanOutRoute CompileFanOut(
        WorkflowDefinition definition,
        WorkflowNodeId sourceNodeId,
        IReadOnlyList<WorkflowEdge> fanOutEdges)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(fanOutEdges);

        if (fanOutEdges.Count == 0)
        {
            throw new InvalidOperationException($"Workflow node '{sourceNodeId}' does not define fan-out routes.");
        }

        var orderedEdges = fanOutEdges
            .Select((edge, originalIndex) => new
            {
                Edge = edge,
                OriginalIndex = originalIndex,
                TargetIndex = edge.Routing.FanOutTargetIndex ?? originalIndex
            })
            .OrderBy(item => item.TargetIndex)
            .ThenBy(item => item.OriginalIndex)
            .Select(item => item.Edge)
            .ToArray();
        var predicates = orderedEdges
            .Select(edge => edge.Routing.Kind == WorkflowRouteKind.Always
                ? static (WorkflowNodeInput? _) => true
                : CompileBuiltInPredicate(edge))
            .ToArray();

        IEnumerable<int> SelectTargets(WorkflowNodeInput? input, int targetCount)
        {
            for (var targetIndex = 0; targetIndex < predicates.Length && targetIndex < targetCount; targetIndex++)
            {
                if (predicates[targetIndex](input))
                {
                    yield return targetIndex;
                }
            }
        }

        return new WorkflowCompiledFanOutRoute(
            sourceNodeId,
            orderedEdges.Select(edge => edge.Id).ToArray(),
            orderedEdges.Select(edge => edge.TargetNodeId).ToArray(),
            SelectTargets);
    }

    private static Func<WorkflowNodeInput?, bool> CompileBuiltInPredicate(WorkflowEdge edge)
    {
        var route = edge.Routing;
        if (!WorkflowRoutingValidation.IsBuiltInRoute(route))
        {
            throw new InvalidOperationException(
                $"Workflow edge '{edge.Id}' uses unsupported routing language '{route.RoutingLanguage}'.");
        }

        if (!WorkflowRoutingValidation.TryParseJsonPath(route.JsonPath, out var path, out var pathError))
        {
            throw new InvalidOperationException($"Workflow edge '{edge.Id}' has invalid route JSON path: {pathError}");
        }

        var expected = WorkflowRoutingValidation.RequiresExpectedValue(route.Operator)
            ? ParseExpectedValue(edge)
            : null;

        return input => BuiltInJsonRouteEvaluator.Evaluate(input, path, route, expected);
    }

    private static JsonElement? ParseExpectedValue(WorkflowEdge edge)
    {
        try
        {
            using var document = JsonDocument.Parse(edge.Routing.ExpectedValueJson);
            return document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Workflow edge '{edge.Id}' has invalid route expected value JSON: {exception.Message}",
                exception);
        }
    }
}

public static class WorkflowRoutingValidation
{
    public static bool IsBuiltInRoute(WorkflowEdgeRouting routing)
        => string.Equals(routing.RoutingLanguage, WorkflowRoutingLanguages.BuiltInJsonV1, StringComparison.Ordinal);

    public static string GetRouteLabel(WorkflowEdge edge)
    {
        if (!string.IsNullOrWhiteSpace(edge.Routing.Label))
        {
            return edge.Routing.Label.Trim();
        }

        if (!string.IsNullOrWhiteSpace(edge.ConditionExpression))
        {
            return edge.ConditionExpression.Trim();
        }

        return edge.Routing.Kind switch
        {
            WorkflowRouteKind.Predicate => $"{edge.Routing.JsonPath} {FormatOperator(edge.Routing.Operator)} {edge.Routing.ExpectedValueJson}",
            WorkflowRouteKind.SwitchCase => $"case {edge.Routing.ExpectedValueJson}",
            WorkflowRouteKind.SwitchDefault => "default",
            WorkflowRouteKind.FanOutSelector => $"fan-out {edge.Routing.JsonPath} {FormatOperator(edge.Routing.Operator)} {edge.Routing.ExpectedValueJson}",
            _ => string.Empty
        };
    }

    public static bool RequiresExpectedValue(WorkflowRouteOperator @operator)
        => @operator is not (
            WorkflowRouteOperator.Exists or
            WorkflowRouteOperator.DoesNotExist or
            WorkflowRouteOperator.IsTruthy or
            WorkflowRouteOperator.IsFalsy);

    public static bool RequiresJsonPath(WorkflowEdgeRouting routing)
        => routing.Kind is
               WorkflowRouteKind.Predicate or
               WorkflowRouteKind.SwitchCase or
               WorkflowRouteKind.FanOutSelector;

    public static bool TryParseJsonPath(
        string jsonPath,
        out IReadOnlyList<BuiltInJsonPathSegment> path,
        out string error)
        => BuiltInJsonRoutePath.TryParse(jsonPath, out path, out error);

    public static bool TryValidateExpectedValue(WorkflowEdgeRouting routing, out string error)
    {
        error = string.Empty;
        if (!RequiresExpectedValue(routing.Operator))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(routing.ExpectedValueJson))
        {
            error = "an expected JSON value is required";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(routing.ExpectedValueJson);
            var actualKind = document.RootElement.ValueKind;
            if (!ExpectedValueKindMatches(routing.ExpectedValueKind, actualKind))
            {
                error = $"expected value kind '{routing.ExpectedValueKind}' does not match JSON token '{actualKind}'";
                return false;
            }
        }
        catch (JsonException exception)
        {
            error = exception.Message;
            return false;
        }

        return true;
    }

    public static string FormatOperator(WorkflowRouteOperator @operator)
        => @operator switch
        {
            WorkflowRouteOperator.DoesNotExist => "does not exist",
            WorkflowRouteOperator.NotEquals => "!=",
            WorkflowRouteOperator.GreaterThan => ">",
            WorkflowRouteOperator.GreaterThanOrEqual => ">=",
            WorkflowRouteOperator.LessThan => "<",
            WorkflowRouteOperator.LessThanOrEqual => "<=",
            WorkflowRouteOperator.IsTruthy => "is truthy",
            WorkflowRouteOperator.IsFalsy => "is falsy",
            _ => @operator.ToString().ToLowerInvariant()
        };

    private static bool ExpectedValueKindMatches(WorkflowRouteValueKind expectedKind, JsonValueKind actualKind)
        => expectedKind switch
        {
            WorkflowRouteValueKind.String => actualKind == JsonValueKind.String,
            WorkflowRouteValueKind.Number => actualKind == JsonValueKind.Number,
            WorkflowRouteValueKind.Boolean => actualKind is JsonValueKind.True or JsonValueKind.False,
            WorkflowRouteValueKind.Null => actualKind == JsonValueKind.Null,
            WorkflowRouteValueKind.Json => true,
            _ => false
        };
}

public readonly record struct BuiltInJsonPathSegment(string? PropertyName, int? Index);

internal static class BuiltInJsonRoutePath
{
    public static bool TryParse(
        string jsonPath,
        out IReadOnlyList<BuiltInJsonPathSegment> path,
        out string error)
    {
        path = [];
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            error = "path is required";
            return false;
        }

        if (jsonPath[0] != '$')
        {
            error = "path must start with '$'";
            return false;
        }

        var segments = new List<BuiltInJsonPathSegment>();
        var index = 1;
        while (index < jsonPath.Length)
        {
            if (jsonPath[index] == '.')
            {
                index++;
                var propertyStart = index;
                while (index < jsonPath.Length && jsonPath[index] is not '.' and not '[' and not ']')
                {
                    index++;
                }

                if (propertyStart == index)
                {
                    error = "property segment cannot be empty";
                    return false;
                }

                segments.Add(new BuiltInJsonPathSegment(jsonPath[propertyStart..index], Index: null));
                continue;
            }

            if (jsonPath[index] == '[')
            {
                index++;
                var indexStart = index;
                while (index < jsonPath.Length && char.IsDigit(jsonPath[index]))
                {
                    index++;
                }

                if (indexStart == index || index >= jsonPath.Length || jsonPath[index] != ']')
                {
                    error = "array segment must use a non-negative integer index like '[0]'";
                    return false;
                }

                var value = int.Parse(jsonPath[indexStart..index], CultureInfo.InvariantCulture);
                segments.Add(new BuiltInJsonPathSegment(PropertyName: null, value));
                index++;
                continue;
            }

            error = $"unexpected character '{jsonPath[index]}' at position {index}";
            return false;
        }

        path = segments;
        return true;
    }
}

internal static class BuiltInJsonRouteEvaluator
{
    public static bool Evaluate(
        WorkflowNodeInput? input,
        IReadOnlyList<BuiltInJsonPathSegment> path,
        WorkflowEdgeRouting routing,
        JsonElement? expectedValue)
    {
        if (input is null || string.IsNullOrWhiteSpace(input.PayloadJson))
        {
            return routing.Operator == WorkflowRouteOperator.DoesNotExist;
        }

        try
        {
            using var document = JsonDocument.Parse(input.PayloadJson);
            var found = TryResolve(document.RootElement, path, out var actual);
            return EvaluateResolvedValue(found, actual, routing, expectedValue);
        }
        catch (JsonException)
        {
            return routing.Operator == WorkflowRouteOperator.DoesNotExist;
        }
    }

    private static bool TryResolve(
        JsonElement root,
        IReadOnlyList<BuiltInJsonPathSegment> path,
        out JsonElement value)
    {
        value = root;
        foreach (var segment in path)
        {
            if (segment.PropertyName is not null)
            {
                if (value.ValueKind != JsonValueKind.Object ||
                    !value.TryGetProperty(segment.PropertyName, out value))
                {
                    return false;
                }

                continue;
            }

            if (segment.Index is not { } targetIndex || value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var currentIndex = 0;
            var matched = false;
            foreach (var item in value.EnumerateArray())
            {
                if (currentIndex == targetIndex)
                {
                    value = item;
                    matched = true;
                    break;
                }

                currentIndex++;
            }

            if (!matched)
            {
                return false;
            }
        }

        return true;
    }

    private static bool EvaluateResolvedValue(
        bool found,
        JsonElement actual,
        WorkflowEdgeRouting routing,
        JsonElement? expectedValue)
        => routing.Operator switch
        {
            WorkflowRouteOperator.Exists => found,
            WorkflowRouteOperator.DoesNotExist => !found,
            WorkflowRouteOperator.Equals => found && expectedValue is { } expected && JsonValuesEqual(actual, expected, routing.CaseSensitive),
            WorkflowRouteOperator.NotEquals => found && expectedValue is { } expected && !JsonValuesEqual(actual, expected, routing.CaseSensitive),
            WorkflowRouteOperator.Contains => found && expectedValue is { } expected && Contains(actual, expected, routing.CaseSensitive),
            WorkflowRouteOperator.StartsWith => found && expectedValue is { } expected && StringCompare(actual, expected, routing.CaseSensitive, static (actualText, expectedText, comparison) => actualText.StartsWith(expectedText, comparison)),
            WorkflowRouteOperator.EndsWith => found && expectedValue is { } expected && StringCompare(actual, expected, routing.CaseSensitive, static (actualText, expectedText, comparison) => actualText.EndsWith(expectedText, comparison)),
            WorkflowRouteOperator.GreaterThan => found && expectedValue is { } expected && CompareNumbers(actual, expected, static result => result > 0),
            WorkflowRouteOperator.GreaterThanOrEqual => found && expectedValue is { } expected && CompareNumbers(actual, expected, static result => result >= 0),
            WorkflowRouteOperator.LessThan => found && expectedValue is { } expected && CompareNumbers(actual, expected, static result => result < 0),
            WorkflowRouteOperator.LessThanOrEqual => found && expectedValue is { } expected && CompareNumbers(actual, expected, static result => result <= 0),
            WorkflowRouteOperator.IsTruthy => found && IsTruthy(actual),
            WorkflowRouteOperator.IsFalsy => !found || !IsTruthy(actual),
            _ => false
        };

    private static bool JsonValuesEqual(JsonElement actual, JsonElement expected, bool caseSensitive)
    {
        if (actual.ValueKind != expected.ValueKind)
        {
            return false;
        }

        return actual.ValueKind switch
        {
            JsonValueKind.String => string.Equals(
                actual.GetString(),
                expected.GetString(),
                caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Number => CompareNumbers(actual, expected, static result => result == 0),
            JsonValueKind.True or JsonValueKind.False => actual.GetBoolean() == expected.GetBoolean(),
            JsonValueKind.Null => true,
            _ => string.Equals(actual.GetRawText(), expected.GetRawText(), StringComparison.Ordinal)
        };
    }

    private static bool Contains(JsonElement actual, JsonElement expected, bool caseSensitive)
    {
        if (actual.ValueKind == JsonValueKind.String && expected.ValueKind == JsonValueKind.String)
        {
            return actual.GetString()?.Contains(
                expected.GetString() ?? string.Empty,
                caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) == true;
        }

        if (actual.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return actual.EnumerateArray().Any(item => JsonValuesEqual(item, expected, caseSensitive));
    }

    private static bool StringCompare(
        JsonElement actual,
        JsonElement expected,
        bool caseSensitive,
        Func<string, string, StringComparison, bool> compare)
    {
        if (actual.ValueKind != JsonValueKind.String || expected.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        return compare(
            actual.GetString() ?? string.Empty,
            expected.GetString() ?? string.Empty,
            caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
    }

    private static bool CompareNumbers(JsonElement actual, JsonElement expected, Func<int, bool> compare)
    {
        if (actual.ValueKind != JsonValueKind.Number || expected.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        if (actual.TryGetDecimal(out var actualDecimal) && expected.TryGetDecimal(out var expectedDecimal))
        {
            return compare(actualDecimal.CompareTo(expectedDecimal));
        }

        return compare(actual.GetDouble().CompareTo(expected.GetDouble()));
    }

    private static bool IsTruthy(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False or JsonValueKind.Null or JsonValueKind.Undefined => false,
            JsonValueKind.Number => !CompareNumbers(value, Zero, static result => result == 0),
            JsonValueKind.String => !string.IsNullOrEmpty(value.GetString()),
            JsonValueKind.Array => value.EnumerateArray().Any(),
            JsonValueKind.Object => value.EnumerateObject().Any(),
            _ => false
        };

    private static JsonElement Zero
    {
        get
        {
            using var document = JsonDocument.Parse("0");
            return document.RootElement.Clone();
        }
    }
}
