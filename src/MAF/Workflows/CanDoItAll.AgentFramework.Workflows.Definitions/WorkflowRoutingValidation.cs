using System.Globalization;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Definitions;

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
