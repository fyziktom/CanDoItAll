using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Definitions;

namespace CanDoItAll.AgentFramework.WorkflowAuthoring.UI;

public static class WorkflowRouteEditing {
    private static readonly JsonSerializerOptions ExecutorJsonOptions = new(JsonSerializerDefaults.Web);

    public static WorkflowEdgeRouting BuildRouteFromFields(
        WorkflowRouteKind routeKind,
        string routeLabel,
        string routeJsonPath,
        WorkflowRouteOperator routeOperator,
        WorkflowRouteValueKind routeValueKind,
        string routeExpectedValue,
        int? routeFanOutTargetIndex,
        bool routeCaseSensitive)
    {
        var label = routeLabel.Trim();
        var jsonPath = routeJsonPath.Trim();
        var expectedValueJson = ShowsExpectedValue(routeKind, routeOperator)
            ? NormalizeExpectedValueJson(routeExpectedValue, routeValueKind)
            : string.Empty;

        return routeKind switch
        {
            WorkflowRouteKind.Always => WorkflowEdgeRouting.Always with
            {
                Label = label
            },
            WorkflowRouteKind.SwitchCase => WorkflowEdgeRouting.SwitchCase(
                jsonPath,
                expectedValueJson,
                routeValueKind,
                label,
                routeCaseSensitive),
            WorkflowRouteKind.SwitchDefault => WorkflowEdgeRouting.SwitchDefault(label),
            WorkflowRouteKind.FanOutSelector => WorkflowEdgeRouting.FanOutSelector(
                jsonPath,
                routeOperator,
                expectedValueJson,
                routeValueKind,
                routeFanOutTargetIndex,
                label,
                routeCaseSensitive),
            _ => WorkflowEdgeRouting.Predicate(
                jsonPath,
                routeOperator,
                expectedValueJson,
                routeValueKind,
                label,
                routeCaseSensitive)
        };
    }

    public static bool TryValidateEdgeRouting(WorkflowEdgeRouting routing, out string error)
    {
        error = string.Empty;
        if (routing.FanOutTargetIndex is < 0)
        {
            error = "Fan-out target index must be zero or greater.";
            return false;
        }

        if (WorkflowRoutingValidation.RequiresJsonPath(routing) &&
            !WorkflowRoutingValidation.TryParseJsonPath(routing.JsonPath, out _, out var pathError))
        {
            error = $"Route JSON path is invalid: {pathError}.";
            return false;
        }

        if (!WorkflowRoutingValidation.TryValidateExpectedValue(routing, out var valueError))
        {
            error = $"Route expected value is invalid: {valueError}.";
            return false;
        }

        return true;
    }

    public static WorkflowEdgeKind ResolveEdgeKindForRoute(WorkflowRouteKind routeKind)
        => routeKind switch
        {
            WorkflowRouteKind.Predicate or WorkflowRouteKind.SwitchCase or WorkflowRouteKind.SwitchDefault => WorkflowEdgeKind.Conditional,
            WorkflowRouteKind.FanOutSelector => WorkflowEdgeKind.FanOut,
            _ => WorkflowEdgeKind.Direct
        };


    public static bool IsPredicateRoute(WorkflowRouteKind routeKind)
        => routeKind is WorkflowRouteKind.Predicate or WorkflowRouteKind.SwitchCase or WorkflowRouteKind.FanOutSelector;

    public static bool ShowsOperator(WorkflowRouteKind routeKind)
        => routeKind is WorkflowRouteKind.Predicate or WorkflowRouteKind.FanOutSelector;

    public static bool ShowsExpectedValue(WorkflowRouteKind routeKind, WorkflowRouteOperator routeOperator)
        => routeKind == WorkflowRouteKind.SwitchCase ||
           IsPredicateRoute(routeKind) && WorkflowRoutingValidation.RequiresExpectedValue(routeOperator);

    public static bool ShowsCaseSensitivity(WorkflowRouteKind routeKind, WorkflowRouteOperator routeOperator)
        => ShowsExpectedValue(routeKind, routeOperator) &&
           routeOperator is
               WorkflowRouteOperator.Equals or
               WorkflowRouteOperator.NotEquals or
               WorkflowRouteOperator.Contains or
               WorkflowRouteOperator.StartsWith or
               WorkflowRouteOperator.EndsWith;

    public static string NormalizeExpectedValueJson(string value, WorkflowRouteValueKind valueKind)
    {
        var trimmed = value.Trim();
        if (valueKind == WorkflowRouteValueKind.String)
        {
            if (TryDeserializeRouteJson<string>(trimmed, out _))
            {
                return trimmed;
            }

            return JsonSerializer.Serialize(trimmed, ExecutorJsonOptions);
        }

        return valueKind switch
        {
            WorkflowRouteValueKind.Null => "null",
            WorkflowRouteValueKind.Boolean when bool.TryParse(trimmed, out var boolean) => boolean ? "true" : "false",
            _ => trimmed
        };
    }

    public static string FormatExpectedValueForEditor(WorkflowEdgeRouting routing)
    {
        if (!WorkflowRoutingValidation.RequiresExpectedValue(routing.Operator))
        {
            return string.Empty;
        }

        if (routing.ExpectedValueKind == WorkflowRouteValueKind.String &&
            TryDeserializeRouteJson<string>(routing.ExpectedValueJson, out var value))
        {
            return value ?? string.Empty;
        }

        return routing.ExpectedValueJson;
    }

    public static bool TryDeserializeRouteJson<TValue>(string json, out TValue? value)
    {
        try
        {
            value = string.IsNullOrWhiteSpace(json)
                ? default
                : JsonSerializer.Deserialize<TValue>(json, ExecutorJsonOptions);
            return true;
        }
        catch (JsonException)
        {
            value = default;
            return false;
        }
    }
}
