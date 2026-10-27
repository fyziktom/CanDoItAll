using System.Text.Json.Nodes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CanDoItAll.Web.Api;

internal static class ProjectDefinitionOperationOpenApiSchemas {
    private const string NonemptyUuidPattern = "^(?!00000000-0000-0000-0000-000000000000)[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}(?![\\s\\S])";

    internal static async Task AddAccessDeniedResponseAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken) {
        var response = operation.Responses!["403"].Content!["application/json"];
        response.Schema = new OpenApiSchema {
            AnyOf = [response.Schema!, await context.GetOrCreateSchemaAsync(typeof(ApiErrorResponse), null, cancellationToken)]
        };
    }

    internal static async Task AddUnavailableWriteResponseAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken) {
        operation.Responses!["503"].Content!["application/json"].Schema = new OpenApiSchema {
            OneOf = [await context.GetOrCreateSchemaAsync(typeof(ProjectDefinitionRejectedApiResponse), null, cancellationToken),
                await context.GetOrCreateSchemaAsync(typeof(ProjectDefinitionUnconfirmedApiResponse), null, cancellationToken)],
            Discriminator = new OpenApiDiscriminator { PropertyName = "status" },
            Description = "Rejected means this attempt was refused before commit because current write authority was unavailable. Unconfirmed means commit acknowledgement is unresolved. Neither clears earlier uncertainty at the same operation address."
        };
    }

    internal static async Task TransformSchemaAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken) {
        var type = context.JsonTypeInfo.Type;
        if (type == typeof(DatasetNamespaceId)) {
            ApplyScalar(schema, Identifier("Original dataset namespace. Preserve it through every retry and lookup."));
        } else if (type == typeof(ProjectCallerBinding)) {
            ApplyScalar(schema, CallerBinding());
        } else if (type == typeof(ProjectWriteAdmission)) {
            ApplyObject(schema, new() {
                ["databaseProfileId"] = Identifier("Original database profile returned by the owner."),
                ["projectId"] = Identifier("Original project identifier returned by the owner."),
                ["lifetimeId"] = Identifier("Original project lifetime returned by the owner. Same-ID recreation does not preserve it.")
            });
        } else if (type == typeof(ProjectDefinitionExpectation)) {
            ApplyObject(schema, new() {
                ["project"] = await Model<ProjectWriteAdmission>(),
                ["revision"] = Identifier("Definition revision from the original read. Never substitute a newer revision into an older draft.")
            });
        } else if (type == typeof(ProjectDefinitionRequestVersion)) {
            ApplyObject(schema, new() {
                ["schema"] = PositiveVersion("Original request schema version. Supported version: 1."),
                ["normalization"] = PositiveVersion("Original normalization version. Supported version: 1.")
            });
        } else if (type == typeof(ProjectDefinitionResultVersion)) {
            ApplyObject(schema, new() {
                ["value"] = PositiveVersion("Original accepted-result schema version. Supported version: 1.")
            });
        } else if (type == typeof(ProjectDefinitionWriteContext)) {
            schema.Properties!["requestVersions"] = new OpenApiSchema {
                Type = JsonSchemaType.Array,
                Items = await Model<ProjectDefinitionRequestVersion>()
            };
            schema.Properties["resultVersions"] = new OpenApiSchema {
                Type = JsonSchemaType.Array,
                Items = await Model<ProjectDefinitionResultVersion>()
            };
        } else if (type == typeof(ProjectDefinitionOperation)) {
            ApplyObject(schema, new() {
                ["namespace"] = await Model<DatasetNamespaceId>(),
                ["epoch"] = await Model<ProjectOperationEpoch>(),
                ["id"] = Identifier("Operation identifier chosen once before dispatch and retained with the frozen command."),
                ["callerBinding"] = await Model<ProjectCallerBinding>()
            });
            schema.Description = "The complete original operation address. Preserve all four fields exactly; never refresh individual fields after dispatch.";
        } else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ProjectDefinitionRequest<>)) {
            ApplyObject(schema, new() {
                ["operation"] = await Model<ProjectDefinitionOperation>(),
                ["version"] = await Model<ProjectDefinitionRequestVersion>(),
                ["command"] = await context.GetOrCreateSchemaAsync(type.GenericTypeArguments[0], null, cancellationToken)
            });
            schema.Description = "One frozen definition command, its original operation address and interpretation version. Replay the complete unchanged request.";
        } else if (type == typeof(CreateProjectDefinitionCommand)) {
            var properties = await FullDefinition();
            properties["externalIdentity"] = Nullable(await Model<ProjectDefinitionExternalIdentity>());
            ApplyObject(schema, properties, "scalars", "phases", "options");
        } else if (type == typeof(ReplaceProjectDefinitionCommand)) {
            var properties = await FullDefinition();
            properties["expected"] = await Model<ProjectDefinitionExpectation>();
            properties["assignExternalIdentity"] = Nullable(await Model<ProjectDefinitionExternalIdentity>());
            ApplyObject(schema, properties, "expected", "scalars", "phases", "options");
        } else if (type == typeof(UpdateProjectDefinitionScalarsCommand)) {
            ApplyObject(schema, new() {
                ["expected"] = await Model<ProjectDefinitionExpectation>(),
                ["scalars"] = await Model<ProjectDefinitionScalars>()
            });
        } else if (type == typeof(ProjectDefinitionRejectedApiResponse)) {
            ApplyObject(schema, new() {
                ["status"] = Status("rejected"),
                ["operation"] = Nullable(await Model<ProjectDefinitionOperation>()),
                ["errors"] = await Model<IReadOnlyList<Error>>()
            });
        } else if (type == typeof(ProjectDefinitionUnconfirmedApiResponse)) {
            ApplyObject(schema, new() {
                ["status"] = Status("unconfirmed"),
                ["operation"] = await Model<ProjectDefinitionOperation>(),
                ["reason"] = await Model<ProjectDefinitionRecoveryReason>()
            });
        } else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ProjectDefinitionAcceptedApiResponse<>)) {
            ApplyObject(schema, new() {
                ["status"] = Status("accepted"),
                ["receipt"] = await context.GetOrCreateSchemaAsync(
                    typeof(ProjectDefinitionAccepted<>).MakeGenericType(type.GenericTypeArguments[0]), null, cancellationToken),
                ["replayed"] = new OpenApiSchema { Type = JsonSchemaType.Boolean }
            });
        }

        if (type == typeof(ProjectStatus) || type == typeof(ProjectPhaseStatus) || type == typeof(ProjectOptionCategory) ||
            type == typeof(ProjectDefinitionCommandKind) || type == typeof(ProjectDefinitionRecoveryReason) ||
            type == typeof(ProjectOperationObservationState) || type == typeof(ProjectDefinitionDetailUnavailableReason) ||
            type == typeof(ProjectDefinitionInvalidContextReason) || type == typeof(ErrorSeverity)) {
            schema.Type = JsonSchemaType.Integer | (schema.Type?.HasFlag(JsonSchemaType.Null) == true ? JsonSchemaType.Null : 0);
            schema.Format = "int32";
            schema.Enum = Enum.GetValues(type).Cast<object>().Select(value => (JsonNode)JsonValue.Create(Convert.ToInt32(value))!).ToList();
        }


        async Task<IOpenApiSchema> Model<T>() => await context.GetOrCreateSchemaAsync(typeof(T), null, cancellationToken);

        async Task<Dictionary<string, IOpenApiSchema>> FullDefinition() => new() {
            ["scalars"] = await Model<ProjectDefinitionScalars>(),
            ["phases"] = new OpenApiSchema {
                Type = JsonSchemaType.Array,
                Items = await Model<ProjectDefinitionPhaseInput>(),
                Description = "Complete ordered phase replacement. Send an explicit empty array to remove all phases."
            },
            ["options"] = new OpenApiSchema {
                Type = JsonSchemaType.Array,
                Items = await Model<ProjectDefinitionOptionInput>(),
                Description = "Complete ordered option replacement. Send an explicit empty array to remove all options."
            }
        };
    }

    private static void ApplyObject(OpenApiSchema schema, Dictionary<string, IOpenApiSchema> properties, params string[] required) {
        var nullable = schema.Type?.HasFlag(JsonSchemaType.Null) == true;
        schema.Type = JsonSchemaType.Object | (nullable ? JsonSchemaType.Null : 0);
        schema.Properties = properties;
        schema.Required = (required.Length == 0 ? properties.Keys : required.AsEnumerable()).ToHashSet(StringComparer.Ordinal);
        schema.AdditionalPropertiesAllowed = false;
    }

    private static void ApplyScalar(OpenApiSchema schema, OpenApiSchema value) {
        var nullable = schema.Type?.HasFlag(JsonSchemaType.Null) == true;
        schema.Type = value.Type | (nullable ? JsonSchemaType.Null : 0);
        schema.Format = value.Format;
        schema.Pattern = value.Pattern;
        schema.MinLength = value.MinLength;
        schema.MaxLength = value.MaxLength;
        schema.Description = value.Description;
        schema.Properties?.Clear();
        schema.Required?.Clear();
        schema.AdditionalProperties = null;
    }

    private static OpenApiSchema CallerBinding() => new() {
        Type = JsonSchemaType.String, MinLength = 68, MaxLength = 68,
        Pattern = "^pc1:[0-9a-f]{64}(?![\\s\\S])",
        Description = "Original opaque caller binding returned by write context. It is not permission to read or write."
    };

    private static OpenApiSchema Nullable(IOpenApiSchema value) => new() {
        AnyOf = [value, new OpenApiSchema { Type = JsonSchemaType.Null }]
    };

    private static OpenApiSchema Status(string value) => new() {
        Type = JsonSchemaType.String, Enum = [JsonValue.Create(value)!]
    };

    private static OpenApiSchema Identifier(string description) => new() {
        Type = JsonSchemaType.String, Format = "uuid", MinLength = 36, MaxLength = 36,
        Pattern = NonemptyUuidPattern, Description = description
    };

    private static OpenApiSchema PositiveVersion(string description) => new() {
        Type = JsonSchemaType.Integer, Format = "int32", Minimum = "1", Description = description
    };
}
