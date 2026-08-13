using System.Text.Json;

namespace CanDoItAll.Processes.Persistence;

internal enum ProcessPlanPayloadShape
{
    LegacyV1,
    HostCapabilitiesV2,
    Unknown
}

internal static class ProcessPlanPayloadShapeClassifier
{
    private static readonly HashSet<string> HostCapabilityPropertyNames = new(StringComparer.Ordinal)
    {
        "hostProfileId",
        "hostCapabilities",
        "requiredHostCapabilities",
        "requiredRuntimeToolNames"
    };

    public static ProcessPlanPayloadShape Classify(string payloadJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return ProcessPlanPayloadShape.Unknown;
            }

            if (!ContainsHostCapabilityProperty(root))
            {
                return ProcessPlanPayloadShape.LegacyV1;
            }

            return HasCompleteHostCapabilityShape(root)
                ? ProcessPlanPayloadShape.HostCapabilitiesV2
                : ProcessPlanPayloadShape.Unknown;
        }
        catch (JsonException)
        {
            return ProcessPlanPayloadShape.Unknown;
        }
    }

    private static bool HasCompleteHostCapabilityShape(JsonElement root)
    {
        if (!TryGetObject(root, "driverStack", out var driverStack) ||
            !HasStringValueObject(driverStack, "hostProfileId") ||
            !TryGetArray(driverStack, "hostCapabilities", out _) ||
            !TryGetArray(driverStack, "drivers", out var drivers) ||
            !AllObjectsHaveArray(drivers, "requiredHostCapabilities") ||
            !TryGetArray(root, "steps", out var steps) ||
            !AllStepsAreComplete(steps) ||
            !TryGetObject(root, "strategies", out var strategies) ||
            !HasCompleteBindingArray(strategies, "executionBindings") ||
            !HasCompleteBindingArray(strategies, "managerBindings") ||
            !HasCompleteBindingArray(strategies, "recoveryBindings") ||
            !HasCompleteBindingArray(strategies, "resupplyBindings") ||
            !TryGetObject(root, "manager", out var manager) ||
            !HasCompleteOptionalBinding(manager, "managerStrategyBinding") ||
            !HasCompleteBindingArray(manager, "recoveryBindings") ||
            !HasCompleteBindingArray(manager, "resupplyBindings"))
        {
            return false;
        }

        return true;
    }

    private static bool AllStepsAreComplete(JsonElement steps)
    {
        foreach (var step in steps.EnumerateArray())
        {
            if (step.ValueKind != JsonValueKind.Object ||
                !TryGetArray(step, "requiredHostCapabilities", out _) ||
                !TryGetArray(step, "requiredRuntimeToolNames", out _) ||
                !HasCompleteOptionalBinding(step, "executionStrategyBinding"))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AllObjectsHaveArray(JsonElement items, string propertyName)
    {
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !TryGetArray(item, propertyName, out _))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasCompleteBindingArray(JsonElement parent, string propertyName)
    {
        if (!TryGetArray(parent, propertyName, out var bindings))
        {
            return false;
        }

        foreach (var binding in bindings.EnumerateArray())
        {
            if (!IsCompleteBinding(binding))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasCompleteOptionalBinding(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var binding))
        {
            return false;
        }

        return binding.ValueKind == JsonValueKind.Null || IsCompleteBinding(binding);
    }

    private static bool IsCompleteBinding(JsonElement binding)
        => binding.ValueKind == JsonValueKind.Object &&
           HasStringValueObject(binding, "hostProfileId") &&
           TryGetArray(binding, "hostCapabilities", out _);

    private static bool HasStringValueObject(JsonElement parent, string propertyName)
        => TryGetObject(parent, propertyName, out var valueObject) &&
           valueObject.TryGetProperty("value", out var value) &&
           value.ValueKind == JsonValueKind.String &&
           !string.IsNullOrWhiteSpace(value.GetString());

    private static bool TryGetObject(
        JsonElement parent,
        string propertyName,
        out JsonElement value)
        => parent.TryGetProperty(propertyName, out value) &&
           value.ValueKind == JsonValueKind.Object;

    private static bool TryGetArray(
        JsonElement parent,
        string propertyName,
        out JsonElement value)
        => parent.TryGetProperty(propertyName, out value) &&
           value.ValueKind == JsonValueKind.Array;

    private static bool ContainsHostCapabilityProperty(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (HostCapabilityPropertyNames.Contains(property.Name) ||
                    ContainsHostCapabilityProperty(property.Value))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (ContainsHostCapabilityProperty(item))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
