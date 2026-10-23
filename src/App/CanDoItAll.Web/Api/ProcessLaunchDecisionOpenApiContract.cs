using CanDoItAll.Processes.Application;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CanDoItAll.Web.Api;

internal static class ProcessLaunchDecisionOpenApiContract {
    internal static async Task TransformSchemaAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken) {
        if (context.JsonTypeInfo.Type != typeof(ProcessLaunchDecisionDescriptor)) {
            return;
        }
        schema.Properties!["supportedPolicies"] = new OpenApiSchema {
            Type = JsonSchemaType.Array,
            Description = "Supported policy combinations with owner budget ceilings. Preparation resolves the actual policy using current eligibility and saved Workspace bounds.",
            Items = await context.GetOrCreateSchemaAsync(typeof(DecisionDataPolicy), null, cancellationToken)
        };
    }
}
