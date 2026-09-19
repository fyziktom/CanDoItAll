using System.Text.Json;
using CanDoItAll.Components.Gantt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CanDoItAll.Web.Api;

// Framework types and value types from the sibling Components repository carry no XML documentation in this build, so
// the XML comment transformer cannot describe their schemas. These reviewed boundary descriptions cover only types the
// API exposes and only fill descriptions that are still empty.
internal static class OpenApiExternalSchemaDescriptions
{
    private static readonly Dictionary<Type, string> TypeDescriptions = new()
    {
        [typeof(ProblemDetails)] =
            "Problem details (RFC 9457) returned as `application/problem+json`. The LLM Chat operations add `code`, " +
            "a stable machine-readable reason to branch on, and, when an LLM Chat operation is concerned, " +
            "`operationId` (that operation) and `retryable` (true when the failure is transient, such as an " +
            "unavailable provider, so a later attempt can succeed). The file routes return only the standard " +
            "members, with the reason in `detail`.",
        [typeof(JsonElement)] =
            "Any JSON value. The member that uses it describes the expected shape.",
        [typeof(IFormFile)] =
            "Binary content of one file part in a `multipart/form-data` request.",
        [typeof(Stream)] =
            "Raw bytes of a file, sent as the whole response body. The response's `Content-Type` header gives the " +
            "actual media type.",
        [typeof(GanttScheduleGesture)] =
            "How a planned task interval is edited, as a JSON integer: 0 Move (shift the whole interval), 1 " +
            "ResizeStart (change only the start), 2 ResizeEnd (change only the end), 3 SetInterval (set an explicit " +
            "start and end).",
        [typeof(GanttTaskId)] =
            "Gantt task identifier object. Its `value` member holds the task's string node identifier.",
    };

    private static readonly Dictionary<(Type DeclaringType, string JsonName), string> PropertyDescriptions = new()
    {
        [(typeof(ProblemDetails), "type")] =
            "URI identifying the problem type. For LLM Chat problems it ends with the problem `code` and is not " +
            "dereferenceable.",
        [(typeof(ProblemDetails), "title")] =
            "Short summary of the problem type; the same for every problem with the same status.",
        [(typeof(ProblemDetails), "status")] =
            "HTTP status code of the response.",
        [(typeof(ProblemDetails), "detail")] =
            "Human-readable explanation of this occurrence. Its wording can change and must not be parsed.",
        [(typeof(ProblemDetails), "instance")] =
            "URI reference identifying this occurrence, when the server provides one.",
        [(typeof(GanttTaskId), "value")] =
            "String node identifier of the task, as returned in `nodes[].id` by the Project Structure read.",
    };

    public static Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(schema.Description))
        {
            return Task.CompletedTask;
        }

        var isComponent = schema.Metadata is not null &&
            schema.Metadata.TryGetValue("x-schema-id", out var schemaId) &&
            !string.IsNullOrEmpty(schemaId as string);
        var type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
        if (isComponent && TypeDescriptions.TryGetValue(type, out var typeDescription))
        {
            schema.Description = typeDescription;
        }
        else if (!isComponent &&
                 context.JsonPropertyInfo is { } property &&
                 PropertyDescriptions.TryGetValue((property.DeclaringType, property.Name), out var propertyDescription))
        {
            schema.Description = propertyDescription;
        }

        return Task.CompletedTask;
    }
}
