using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CanDoItAll.Web.Api;

internal static class AgentDecisionPolicyOpenApiContract {
    internal static Task TransformSchemaAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken) {
        var type = context.JsonTypeInfo.Type;
        if (type == typeof(AgentDecisionPolicyApiExpectation)) {
            schema.Description = "The original Agent policy read, bound to its exact workspace, profile activation and whole-catalog revision.";
            schema.Properties = new Dictionary<string, IOpenApiSchema> {
                ["databaseProfileId"] = Identifier("Original database profile identity."),
                ["scopeKind"] = Integer(0, 4, "Original workspace kind: 0 Sandbox, 1 Process, 2 Project, 3 Tenant, 4 Organization."),
                ["scopeKey"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Original canonical scope key; empty only for Sandbox. Do not normalize or replace it on save." },
                ["databaseProfileGeneration"] = Revision(true),
                ["agentId"] = Identifier("Original Agent identity, identical to the route identity."),
                ["catalogRevision"] = Revision(false)
            };
            schema.Required = schema.Properties.Keys.ToHashSet(StringComparer.Ordinal);
            schema.AdditionalPropertiesAllowed = false;
        } else if (type == typeof(AgentDecisionPolicyApiSnapshot) || type == typeof(AgentDecisionPolicyApiSubmission)) {
            schema.Description = type == typeof(AgentDecisionPolicyApiSnapshot)
                ? "Current configured Agent override and original expectation. A current read does not prove a previous uncertain write committed."
                : "One original conditional policy replacement. Retain this operation and payload on conflict or an uncertain response.";
            schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
            schema.Properties["policy"] = Policy(type == typeof(AgentDecisionPolicyApiSnapshot));
            schema.Properties["expectation"].Description = "The exact original read expectation, never replaced by an automatic refresh.";
            if (type == typeof(AgentDecisionPolicyApiSubmission)) {
                schema.Properties["operationId"] = Identifier("Nonempty client operation ID. This owner has no durable replay receipt; this ID is not permission to retry.");
            }
            schema.Required = schema.Properties.Keys.ToHashSet(StringComparer.Ordinal);
            schema.AdditionalPropertiesAllowed = false;
        } else if (type == typeof(AgentDecisionPolicyApiResult)) {
            schema.Description = "Exact accepted snapshot, definite rejection or unconfirmed outcome. Private diagnostics are not disclosed.";
            var descriptions = new Dictionary<string, string> {
                ["outcome"] = "Accepted, Rejected or Unconfirmed. An unconfirmed response must not be automatically retried.",
                ["operationId"] = "The original submitted operation identity.",
                ["snapshot"] = "Present only for acknowledged acceptance, containing that command's exact resulting policy and next catalog revision.",
                ["rejection"] = "Present only for known refusal: 0 ContextChanged, 1 Conflict, 2 NotFound, 3 InvalidMetadata, 4 Unsupported, 5 Cancelled, 6 Unavailable.",
                ["followupRequired"] = "True when the commit is acknowledged but secondary work failed. Do not repeat the write."
            };
            foreach (var (name, description) in descriptions) {
                if (schema.Properties?.TryGetValue(name, out var property) == true) {
                    property.Description = description;
                }
            }
        }
        return Task.CompletedTask;
    }

    private static OpenApiSchema Policy(bool nullable) => new() {
        Type = nullable ? JsonSchemaType.Object | JsonSchemaType.Null : JsonSchemaType.Object,
        Description = "Complete version-one override envelope. Every override field is mandatory; null inherits that field. Null policy in a read means no configured Agent override. Runtime admission still checks owner capabilities and limits.",
        Properties = new Dictionary<string, IOpenApiSchema> {
            ["schemaVersion"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Enum = [JsonValue.Create(1)!], Description = "Override encoding version one." },
            ["readBasisMode"] = Integer(0, 1, "0 RunSnapshot; 1 StepSnapshot; null inherits.", true),
            ["publicationFreshness"] = Integer(0, 1, "0 PinnedObservation; 1 RequireCurrent; null inherits. Owner mandates still apply.", true),
            ["onBasisChange"] = Integer(0, 2, "0 Fail; 1 PauseForReview; 2 Replan; null inherits.", true),
            ["maximumReplans"] = Integer(0, int.MaxValue, "Nonnegative semantic replan allowance; null inherits.", true),
            ["maximumWorkDuration"] = Duration("Positive work allowance; null inherits."),
            ["maximumBasisAge"] = Duration("Positive maximum basis age; null inherits.")
        },
        Required = new HashSet<string>(["schemaVersion", "readBasisMode", "publicationFreshness", "onBasisChange", "maximumReplans", "maximumWorkDuration", "maximumBasisAge"], StringComparer.Ordinal),
        AdditionalPropertiesAllowed = false
    };

    private static OpenApiSchema Revision(bool zero) => new() {
        Type = JsonSchemaType.String, MinLength = 1, MaxLength = 19,
        Pattern = AgentHistoricalReceiptOpenApiContract.CreateRevisionPattern(zero),
        Description = $"Original Int64 revision as a canonical decimal string, from {(zero ? "0" : "1")} through 9223372036854775807. Never convert to a JavaScript number."
    };
    private static OpenApiSchema Identifier(string description) => new() { Type = JsonSchemaType.String, Format = "uuid", Description = description };
    private static OpenApiSchema Duration(string description) => new() {
        Type = JsonSchemaType.String | JsonSchemaType.Null, MaxLength = 32,
        Description = description + " Use invariant TimeSpan constant format; fractional seconds preserve exact ticks."
    };
    private static OpenApiSchema Integer(int minimum, int maximum, string description, bool nullable = false) => new() {
        Type = nullable ? JsonSchemaType.Integer | JsonSchemaType.Null : JsonSchemaType.Integer,
        Format = "int32", Minimum = minimum.ToString(CultureInfo.InvariantCulture), Maximum = maximum.ToString(CultureInfo.InvariantCulture),
        Description = description
    };
}
