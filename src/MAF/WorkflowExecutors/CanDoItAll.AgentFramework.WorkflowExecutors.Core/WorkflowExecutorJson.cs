using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkflowExecutorJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json)
        => JsonSerializer.Deserialize<T>(json, Options)
           ?? throw new InvalidOperationException($"Workflow executor settings could not be deserialized as {typeof(T).Name}.");

    public static WorkflowNodeExecutionResult Result(
        WorkflowExecutorExecutionContext context,
        object payload)
        => new(
            context.Node.Id,
            Serialize(payload),
            context.Descriptor.ResultShape);

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
