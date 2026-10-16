using CanDoItAll.Modules.Projects;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CanDoItAll.Web.Api;

internal static class ProjectDefinitionOperationOpenApiSchemas {
    private const string NonemptyUuidPattern = "^(?!00000000-0000-0000-0000-000000000000)[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}(?![\\s\\S])";

    internal static async Task TransformSchemaAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken) {
        var type = context.JsonTypeInfo.Type;
        if (type == typeof(ProjectDefinitionOperation)) {
            ApplyObject(schema, new() {
                ["namespace"] = Identifier("Original dataset namespace returned by write context. Preserve it through every retry and lookup."),
                ["epoch"] = await context.GetOrCreateSchemaAsync(typeof(ProjectOperationEpoch), null, cancellationToken),
                ["id"] = Identifier("Operation identifier chosen once before dispatch and retained with the frozen command."),
                ["callerBinding"] = new OpenApiSchema {
                    Type = JsonSchemaType.String, MinLength = 68, MaxLength = 68,
                    Pattern = "^pc1:[0-9a-f]{64}(?![\\s\\S])",
                    Description = "Original opaque caller binding returned by write context. It is not permission to read or write."
                }
            });
            schema.Description = "The complete original operation address. Preserve all four fields exactly; never refresh individual fields after dispatch.";
        } else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ProjectDefinitionRequest<>)) {
            ApplyObject(schema, new() {
                ["operation"] = await context.GetOrCreateSchemaAsync(typeof(ProjectDefinitionOperation), null, cancellationToken),
                ["version"] = new OpenApiSchema {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema> {
                        ["schema"] = PositiveVersion("Original request schema version. Supported version: 1."),
                        ["normalization"] = PositiveVersion("Original normalization version. Supported version: 1.")
                    },
                    Required = new HashSet<string> { "schema", "normalization" },
                    AdditionalPropertiesAllowed = false
                },
                ["command"] = await context.GetOrCreateSchemaAsync(type.GenericTypeArguments[0], null, cancellationToken)
            });
            schema.Description = "One frozen definition command, its original operation address and interpretation version. Replay the complete unchanged request.";
        }
    }

    private static void ApplyObject(OpenApiSchema schema, Dictionary<string, IOpenApiSchema> properties) {
        var nullable = schema.Type?.HasFlag(JsonSchemaType.Null) == true;
        schema.Type = JsonSchemaType.Object | (nullable ? JsonSchemaType.Null : 0);
        schema.Properties = properties;
        schema.Required = properties.Keys.ToHashSet(StringComparer.Ordinal);
        schema.AdditionalPropertiesAllowed = false;
    }

    private static OpenApiSchema Identifier(string description) => new() {
        Type = JsonSchemaType.String, Format = "uuid", MinLength = 36, MaxLength = 36,
        Pattern = NonemptyUuidPattern, Description = description
    };

    private static OpenApiSchema PositiveVersion(string description) => new() {
        Type = JsonSchemaType.Integer, Format = "int32", Minimum = "1", Description = description
    };
}
