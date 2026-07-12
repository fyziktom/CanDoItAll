using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

public sealed class WorkflowIdJsonConverter : JsonConverter<WorkflowId>
{
    public override WorkflowId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new WorkflowId(WorkflowIdJsonConverterHelpers.ReadGuid(ref reader, nameof(WorkflowId)));
    }

    public override void Write(Utf8JsonWriter writer, WorkflowId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

public sealed class WorkflowVersionIdJsonConverter : JsonConverter<WorkflowVersionId>
{
    public override WorkflowVersionId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new WorkflowVersionId(WorkflowIdJsonConverterHelpers.ReadGuid(ref reader, nameof(WorkflowVersionId)));
    }

    public override void Write(Utf8JsonWriter writer, WorkflowVersionId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

public sealed class WorkflowNodeIdJsonConverter : JsonConverter<WorkflowNodeId>
{
    public override WorkflowNodeId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new WorkflowNodeId(WorkflowIdJsonConverterHelpers.ReadString(ref reader, nameof(WorkflowNodeId)));
    }

    public override void Write(Utf8JsonWriter writer, WorkflowNodeId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

public sealed class WorkflowEdgeIdJsonConverter : JsonConverter<WorkflowEdgeId>
{
    public override WorkflowEdgeId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new WorkflowEdgeId(WorkflowIdJsonConverterHelpers.ReadString(ref reader, nameof(WorkflowEdgeId)));
    }

    public override void Write(Utf8JsonWriter writer, WorkflowEdgeId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

public sealed class WorkflowPortIdJsonConverter : JsonConverter<WorkflowPortId>
{
    public override WorkflowPortId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new WorkflowPortId(WorkflowIdJsonConverterHelpers.ReadString(ref reader, nameof(WorkflowPortId)));
    }

    public override void Write(Utf8JsonWriter writer, WorkflowPortId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

public sealed class WorkflowComponentIdJsonConverter : JsonConverter<WorkflowComponentId>
{
    public override WorkflowComponentId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new WorkflowComponentId(WorkflowIdJsonConverterHelpers.ReadGuid(ref reader, nameof(WorkflowComponentId)));
    }

    public override void Write(Utf8JsonWriter writer, WorkflowComponentId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

public sealed class WorkflowRunIdJsonConverter : JsonConverter<WorkflowRunId>
{
    public override WorkflowRunId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new WorkflowRunId(WorkflowIdJsonConverterHelpers.ReadGuid(ref reader, nameof(WorkflowRunId)));
    }

    public override void Write(Utf8JsonWriter writer, WorkflowRunId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

public sealed class WorkflowUsageObservationIdJsonConverter : JsonConverter<WorkflowUsageObservationId>
{
    public override WorkflowUsageObservationId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new WorkflowUsageObservationId(WorkflowIdJsonConverterHelpers.ReadGuid(ref reader, nameof(WorkflowUsageObservationId)));
    }

    public override void Write(Utf8JsonWriter writer, WorkflowUsageObservationId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

public sealed class WorkflowCheckpointIdJsonConverter : JsonConverter<WorkflowCheckpointId>
{
    public override WorkflowCheckpointId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new WorkflowCheckpointId(WorkflowIdJsonConverterHelpers.ReadGuid(ref reader, nameof(WorkflowCheckpointId)));
    }

    public override void Write(Utf8JsonWriter writer, WorkflowCheckpointId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

public sealed class WorkflowExternalRequestIdJsonConverter : JsonConverter<WorkflowExternalRequestId>
{
    public override WorkflowExternalRequestId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new WorkflowExternalRequestId(WorkflowIdJsonConverterHelpers.ReadGuid(ref reader, nameof(WorkflowExternalRequestId)));
    }

    public override void Write(Utf8JsonWriter writer, WorkflowExternalRequestId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

public sealed class WorkflowArtifactIdJsonConverter : JsonConverter<WorkflowArtifactId>
{
    public override WorkflowArtifactId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new WorkflowArtifactId(WorkflowIdJsonConverterHelpers.ReadGuid(ref reader, nameof(WorkflowArtifactId)));
    }

    public override void Write(Utf8JsonWriter writer, WorkflowArtifactId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

public sealed class WorkflowExecutorIdJsonConverter : JsonConverter<WorkflowExecutorId>
{
    public override WorkflowExecutorId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new WorkflowExecutorId(WorkflowIdJsonConverterHelpers.ReadString(ref reader, nameof(WorkflowExecutorId)));
    }

    public override void Write(Utf8JsonWriter writer, WorkflowExecutorId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

internal static class WorkflowIdJsonConverterHelpers
{
    public static Guid ReadGuid(ref Utf8JsonReader reader, string typeName)
    {
        if (reader.TokenType == JsonTokenType.String && reader.TryGetGuid(out var directGuid))
        {
            return directGuid;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"{typeName} must be a GUID string or an object with a value property.");
        }

        Guid? value = null;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException($"{typeName} JSON object is malformed.");
            }

            var propertyName = reader.GetString();
            reader.Read();
            if (string.Equals(propertyName, "value", StringComparison.OrdinalIgnoreCase))
            {
                if (!reader.TryGetGuid(out var objectGuid))
                {
                    throw new JsonException($"{typeName}.value must be a GUID.");
                }

                value = objectGuid;
            }
            else
            {
                reader.Skip();
            }
        }

        return value ?? throw new JsonException($"{typeName}.value is required.");
    }

    public static string ReadString(ref Utf8JsonReader reader, string typeName)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString() ?? throw new JsonException($"{typeName} cannot be null.");
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"{typeName} must be a string or an object with a value property.");
        }

        string? value = null;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException($"{typeName} JSON object is malformed.");
            }

            var propertyName = reader.GetString();
            reader.Read();
            if (string.Equals(propertyName, "value", StringComparison.OrdinalIgnoreCase))
            {
                value = reader.GetString();
            }
            else
            {
                reader.Skip();
            }
        }

        return value ?? throw new JsonException($"{typeName}.value is required.");
    }
}
