using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafToolInputSchema {
    internal static AgentToolPreparedPayload Bind(AgentToolPreparedPayload payload, JsonElement schema) =>
        new(payload.ToolName, 2, MafToolProtocolCodec.Digest(new { OriginalPayload = payload.Digest, Schema = schema }),
            payload.ArgumentsJson, payload.Effect, payload.Recovery, payload.SourcePreparation);

    internal static JsonElement Apply(JsonElement original, IReadOnlyList<ToolInputConstraint> constraints) {
        if (constraints.Count == 0) {
            return original;
        }
        var schema = JsonNode.Parse(original.GetRawText())!.AsObject();
        var properties = (schema["properties"] ??= new JsonObject()).AsObject();
        var required = (schema["required"] ??= new JsonArray()).AsArray();
        foreach (var constraint in constraints) {
            var property = (properties[constraint.Parameter] ??= new JsonObject()).AsObject();
            property["description"] = $"{property["description"]?.GetValue<string>()} {constraint.Description}".Trim();
            switch (constraint) {
                case ToolIntegerInputConstraint integer when integer.WhenTrue is null:
                    property["type"] = "integer";
                    property["maximum"] = Math.Min(property["maximum"]?.GetValue<double>() ?? double.MaxValue, integer.Maximum);
                    if (integer.Required && !required.Any(item => item?.GetValue<string>() == constraint.Parameter)) {
                        required.Add(constraint.Parameter);
                    }
                    break;
                case ToolIntegerInputConstraint integer:
                    var condition = (properties[integer.WhenTrue!] ??= new JsonObject { ["type"] = "boolean" }).AsObject();
                    condition["description"] = $"{condition["description"]?.GetValue<string>()} {integer.Description}".Trim();
                    break;
                case ToolBooleanInputConstraint boolean:
                    if (property["enum"] is JsonArray values && !values.Any(value => value?.GetValue<bool>() == boolean.AllowedValue)) {
                        throw new InvalidOperationException("The tool schema contradicts its owner's input constraint.");
                    }
                    property["type"] = "boolean";
                    property["enum"] = new JsonArray(JsonValue.Create(boolean.AllowedValue));
                    break;
                default:
                    throw new NotSupportedException($"Unsupported tool input constraint: {constraint.GetType().Name}.");
            }
        }
        return JsonSerializer.SerializeToElement(schema);
    }
}
