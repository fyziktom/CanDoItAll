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
            Description = RequestBodyDescription,
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
            Description = IdempotencyKeyDescription,
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

    // The handler reads the body and the header itself, so the XML comment transformer cannot describe them; these
    // descriptions are the reviewed client text for the two hand-built parts of the operation.
    private const string RequestBodyDescription =
        "The expected request version and the response value, as a JSON object with exactly these two members. " +
        "Take `expectedRequestVersion` from `version` of the pending request and build `response` from its " +
        "`responseContract`.";

    private const string IdempotencyKeyDescription =
        "Client-chosen key for this answer attempt, required: exactly one value, not blank, without commas and at " +
        "most 256 characters after trimming, for example `approval-7f3c-attempt-1`. It is scoped to the external " +
        "request. Resend the same key with the same body to retry safely; a different body under the same key is " +
        "rejected with HTTP 409.";
}
