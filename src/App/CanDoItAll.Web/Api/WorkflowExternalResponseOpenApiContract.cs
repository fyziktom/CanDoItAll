using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CanDoItAll.Web.Api;

internal sealed class WorkflowExternalResponseOpenApiContract
{
    public static WorkflowExternalResponseOpenApiContract Instance { get; } = new();

    private WorkflowExternalResponseOpenApiContract()
    {
    }

    public static async Task TransformOperationAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (!context.Description.ActionDescriptor.EndpointMetadata
                .OfType<WorkflowExternalResponseOpenApiContract>()
                .Any())
        {
            return;
        }

        var schema = await context.GetOrCreateSchemaAsync(
            typeof(WorkflowExternalResponseApiRequest),
            parameterDescription: null,
            cancellationToken);
        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>(StringComparer.OrdinalIgnoreCase)
            {
                ["application/json"] = new OpenApiMediaType { Schema = schema },
                ["application/*+json"] = new OpenApiMediaType { Schema = schema }
            }
        };
        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = WorkflowExternalResponseIdempotencyKeyParser.HeaderName,
            In = ParameterLocation.Header,
            Required = true,
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                MinLength = 1,
                MaxLength = WorkflowExternalResponseIdempotencyKeyParser.MaximumLength
            }
        });
    }
}
