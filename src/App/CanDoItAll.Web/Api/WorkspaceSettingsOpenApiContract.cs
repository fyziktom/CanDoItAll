using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi;

namespace CanDoItAll.Web.Api;

internal static class WorkspaceSettingsOpenApiContract {
    private const string OriginalExpectation = "Original dataset and current-head observation acquired with the draft. Preserve it unchanged; substituting a newer revision does not reconcile older values.";
    private const string ExactSnapshot = "Exact accepted six-field snapshot and resulting head revision. It describes this acceptance, not a later current read; preserve post-dispatch local edits.";
    private const string NonemptyUuidPattern = "^(?!00000000-0000-0000-0000-000000000000)[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}(?![\\s\\S])";

    internal static string? CreateSchemaReferenceId(JsonTypeInfo typeInfo) =>
        typeInfo.Type.DeclaringType == typeof(WorkspaceSettingsSaveOutcomeV2)
            ? $"{nameof(WorkspaceSettingsSaveOutcomeV2)}{typeInfo.Type.Name}"
            : OpenApiOptions.CreateDefaultSchemaReferenceId(typeInfo);

    internal static async Task TransformSchemaAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken) {
        var type = context.JsonTypeInfo.Type;
        if (type == typeof(WorkspaceSettingsExpectation)) {
            Reset(schema);
            schema.Description = OriginalExpectation;
            schema.OneOf = [await Expectation(existing: false), await Expectation(existing: true)];
            schema.Discriminator = new OpenApiDiscriminator { PropertyName = "state" };
        } else if (type == typeof(WorkspaceSettingsHead)) {
            ApplyObject(schema, "Identity and application-managed revision of the current Workspace defaults row.", new() {
                ["id"] = Identifier("Original current row identity. An existing row is never silently replaced with another head."),
                ["revision"] = Identifier("Original current row revision. Every accepted replacement, including a normalized no-op, advances this value.")
            });
        } else if (type == typeof(WorkspaceSettingsValues)) {
            ApplyObject(schema, "Complete immutable values of all six Workspace business defaults.", new() {
                ["workspaceName"] = Text("Workspace display name; required, trimmed and limited to 200 characters without control characters.", 200),
                ["defaultProviderProfileId"] = Nullable(Identifier("Selected provider profile, or explicit null for no preference. This field is preserved even by editors without a provider picker.")),
                ["defaultPromptOutputFormat"] = Text("Default prompt output format; required, trimmed and limited to 40 characters.", 40),
                ["currencyCode"] = new OpenApiSchema { Type = JsonSchemaType.String, MinLength = 3, MaxLength = 3, Pattern = "^[A-Za-z]{3}(?![\\s\\S])", Description = "Three ASCII letters identifying the currency; accepted values are normalized to uppercase." },
                ["currencyCultureName"] = Text("Valid nonempty .NET culture name used to display currency; limited to 40 characters.", 40),
                ["notes"] = Text("Workspace notes, including explicit empty text; trimmed on acceptance and limited to 8192 characters.", 8192)
            });
        } else if (type == typeof(WorkspaceSettingsSnapshot)) {
            ApplyObject(schema, "Coherent current Workspace defaults and their original dataset/head expectation. A current read does not prove an earlier attempt's outcome.", new() {
                ["expectation"] = await Model<WorkspaceSettingsExpectation>(OriginalExpectation),
                ["values"] = await Model<WorkspaceSettingsValues>("All six values observed with this exact head expectation.")
            });
        } else if (type == typeof(ReplaceWorkspaceSettingsCommand)) {
            ApplyObject(schema, "One conditional complete replacement of Workspace defaults. No operation ID or durable replay receipt is provided by this protocol.", new() {
                ["expected"] = await Model<WorkspaceSettingsExpectation>(OriginalExpectation),
                ["values"] = await Model<WorkspaceSettingsValues>("All six proposed values frozen before dispatch; omission never means preserve an old field.")
            });
        } else if (type == typeof(WorkspaceSettingsSaveOutcome.Accepted)) {
            ApplyObject(schema, "The owner acknowledged the commit. Secondary cleanup or publication problems do not erase this acceptance.", new() {
                ["status"] = Literal("accepted", "This response acknowledges the exact committed replacement."),
                ["snapshot"] = await Model<WorkspaceSettingsSnapshot>(ExactSnapshot),
                ["diagnostic"] = await Model<WorkspaceSettingsSecondaryDiagnostic>("Optional postcommit issue: 1 ActivityPending, 2 CurrencyPublicationPending, 3 LoggingPending, 4 CleanupPending. The accepted snapshot remains authoritative for this attempt.")
            }, "status", "snapshot");
        } else if (type == typeof(WorkspaceSettingsSaveOutcome.Rejected)) {
            ApplyObject(schema, "This attempt was refused with known no-commit knowledge. It cannot resolve a different earlier unconfirmed attempt.", new() {
                ["status"] = Literal("rejected", "This attempt did not commit; preserve the original draft when resolving a conflict."),
                ["reason"] = await Model<WorkspaceSettingsRejectionReason>("Refusal reason: 1 InvalidValues, 2 HeadChanged, 3 DatasetChanged, 4 TransactionConflict, 5 OwnerRetired. OwnerRetired describes a retired caller lifetime, not dataset replacement.")
            });
        } else if (type == typeof(WorkspaceSettingsSaveOutcome.Unconfirmed)) {
            ApplyObject(schema, "Commit acknowledgement is unresolved. Preserve the submitted draft and do not automatically retry; this protocol has no durable lookup receipt.", new() {
                ["status"] = Literal("unconfirmed", "The attempt may have committed. A later current read cannot establish acceptance or rollback.")
            });
        } else if (type == typeof(DecisionDataPolicy)) {
            ApplyObject(schema, "Complete explicit root policy. No inheritance or installed default is supplied. Producer admission separately checks its supported modes and ceilings.", new() {
                ["schemaVersion"] = Integer(1, 1, "Decision policy schema version one."),
                ["readBasisMode"] = Integer(0, 1, "0 RunSnapshot; 1 StepSnapshot."),
                ["publicationFreshness"] = Integer(0, 1, "0 PinnedObservation; 1 RequireCurrent."),
                ["onBasisChange"] = Integer(0, 2, "0 Fail; 1 PauseForReview; 2 Replan."),
                ["maximumReplans"] = Integer(0, int.MaxValue, "Positive only for Replan; zero for other responses."),
                ["maximumWorkDuration"] = Text("Positive invariant constant TimeSpan string; exact ticks are preserved.", 32),
                ["maximumBasisAge"] = Text("Positive invariant constant TimeSpan string; exact ticks are preserved.", 32)
            });
        } else if (type == typeof(WorkspaceSettingsSnapshotV2) || type == typeof(ReplaceWorkspaceSettingsV2Command)) {
            var replacement = type == typeof(ReplaceWorkspaceSettingsV2Command);
            var policy = await Model<DecisionDataPolicy>("Complete configured Workspace root policy.");
            ApplyObject(schema, replacement ? "Conditional version-two replacement; every field is mandatory." : "Version-two current observation; null policy means legacy absence, never inheritance.", new() {
                ["schemaVersion"] = Integer(2, 2, "Workspace contract version two."),
                [replacement ? "expected" : "expectation"] = await Model<WorkspaceSettingsExpectation>(OriginalExpectation),
                ["values"] = await Model<WorkspaceSettingsValues>("All six Workspace defaults."),
                ["decisionDataPolicy"] = replacement ? policy : new OpenApiSchema { AnyOf = [policy, new OpenApiSchema { Type = JsonSchemaType.Null }] }
            });
        } else if (type == typeof(WorkspaceSettingsSaveOutcomeV2.Accepted)) {
            ApplyObject(schema, "Acknowledged defaults and policy replacement; retain this exact receipt separately from V1 acceptance.", new() {
                ["status"] = Literal("accepted", "This exact replacement committed."),
                ["snapshot"] = await Model<WorkspaceSettingsSnapshotV2>("Exact accepted defaults, configured policy and resulting head."),
                ["diagnostic"] = await Model<WorkspaceSettingsSecondaryDiagnostic>("Optional postcommit diagnostic; acceptance remains authoritative.")
            }, "status", "snapshot");
        } else if (type == typeof(WorkspaceSettingsSaveOutcomeV2.Rejected)) {
            ApplyObject(schema, "Known refusal of this attempt; preserve the original draft.", new() {
                ["status"] = Literal("rejected", "This attempt did not commit."),
                ["reason"] = await Model<WorkspaceSettingsRejectionReason>("Exact refusal reason.")
            });
        } else if (type == typeof(WorkspaceSettingsSaveOutcomeV2.Unconfirmed)) {
            ApplyObject(schema, "Unresolved acknowledgement; a later read cannot establish this attempt's outcome.", new() {
                ["status"] = Literal("unconfirmed", "Do not automatically retry.")
            });
        } else if (type == typeof(WorkspaceSettingsRejectionReason) || type == typeof(WorkspaceSettingsSecondaryDiagnostic)) {
            Reset(schema);
            schema.Type = JsonSchemaType.Integer;
            schema.Format = "int32";
            schema.Enum = Enum.GetValues(type).Cast<object>().Select(value => (JsonNode)JsonValue.Create(Convert.ToInt32(value))!).ToList();
            schema.Description = type == typeof(WorkspaceSettingsRejectionReason)
                ? "Known refusal reason: 1 InvalidValues, 2 HeadChanged, 3 DatasetChanged, 4 TransactionConflict, 5 OwnerRetired."
                : "Secondary issue after acknowledged commit: 1 ActivityPending, 2 CurrencyPublicationPending, 3 LoggingPending, 4 CleanupPending.";
        }

        async Task<IOpenApiSchema> Model<T>(string description) {
            var value = await context.GetOrCreateSchemaAsync(typeof(T), null, cancellationToken);
            return new OpenApiSchema { Description = description, AllOf = [value] };
        }

        async Task<OpenApiSchema> Expectation(bool existing) {
            var result = new OpenApiSchema();
            ApplyObject(result, existing ? "Original existing head; row identity and revision are mandatory." : "Original empty head; the head member must be explicit null.", new() {
                ["namespace"] = await Model<DatasetNamespaceId>("Original dataset activation identifier, acquired with this head observation. Restore or replacement invalidates older expectations."),
                ["state"] = Literal(existing ? "existing" : "empty", "Whether the acquired observation identified an existing current head or an empty head."),
                ["head"] = existing
                    ? await Model<WorkspaceSettingsHead>("Original current row identity and revision, both required for an existing observation.")
                    : new OpenApiSchema { Type = JsonSchemaType.Null, Description = "Explicit null because the acquired current head was empty." }
            });
            return result;
        }
    }

    internal static Task TransformOperationAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken) {
        var name = context.Description.ActionDescriptor.EndpointMetadata.OfType<IEndpointNameMetadata>().LastOrDefault()?.EndpointName;
        if (name is not ("GetWorkspaceSettings" or "SaveWorkspaceSettings" or "GetWorkspaceSettingsV2" or "SaveWorkspaceSettingsV2")) {
            return Task.CompletedTask;
        }
        foreach (var (status, response) in operation.Responses ?? []) {
            response.Description = status switch {
                "200" => name is "GetWorkspaceSettings" or "GetWorkspaceSettingsV2" ? "Coherent current defaults and original expectation; this is not proof of an earlier unconfirmed replacement." : "Exact acknowledged snapshot, with an optional secondary postcommit diagnostic. Preserve later local edits when reconciling.",
                "400" => "This request attempt was rejected before commit because its shape, original expectation or proposed values were invalid.",
                "401" => "Authentication did not establish a currently valid credential; no defaults mutation was dispatched.",
                "403" => "The current credential lacks the required Workspace settings capability; no defaults mutation was dispatched.",
                "409" => "The original head, dataset or caller lifetime conflicts, or a known transaction conflict prevented this attempt from committing. Preserve the draft and inspect reason.",
                "413" => "The raw request exceeds 65536 bytes; this attempt was rejected before dispatch with the generic API error envelope.",
                "415" => "The media type is unsupported; request routing rejected it before the settings handler, using the generic API error envelope.",
                "503" => name is "SaveWorkspaceSettings" or "SaveWorkspaceSettingsV2" ? "The commit acknowledgement is unconfirmed. Do not automatically retry; a current GET does not resolve it." : "Current settings could not be acquired. Preserve any existing draft and unresolved attempt.",
                _ => response.Description
            };
        }
        return Task.CompletedTask;
    }

    private static void ApplyObject(OpenApiSchema schema, string description, Dictionary<string, IOpenApiSchema> properties,
        params string[] required) {
        Reset(schema);
        schema.Type = JsonSchemaType.Object;
        schema.Description = description;
        schema.Properties = properties;
        schema.Required = (required.Length == 0 ? properties.Keys : required.AsEnumerable()).ToHashSet(StringComparer.Ordinal);
        schema.AdditionalPropertiesAllowed = false;
    }

    private static void Reset(OpenApiSchema schema) {
        schema.Type = null;
        schema.Properties = null;
        schema.Required = null;
        schema.AllOf = null;
        schema.AnyOf = null;
        schema.OneOf = null;
        schema.AdditionalProperties = null;
        schema.Discriminator = null;
    }

    private static OpenApiSchema Integer(int minimum, int maximum, string description) => new() {
        Type = JsonSchemaType.Integer, Format = "int32", Minimum = minimum.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Maximum = maximum.ToString(System.Globalization.CultureInfo.InvariantCulture), Description = description
    };

    private static OpenApiSchema Identifier(string description) => new() {
        Type = JsonSchemaType.String, Format = "uuid", MinLength = 36, MaxLength = 36,
        Pattern = NonemptyUuidPattern, Description = description
    };

    private static OpenApiSchema Text(string description, int maximum) => new() {
        Type = JsonSchemaType.String, MaxLength = maximum, Description = description
    };

    private static OpenApiSchema Literal(string value, string description) => new() {
        Type = JsonSchemaType.String, Enum = [JsonValue.Create(value)!], Description = description
    };

    private static OpenApiSchema Nullable(OpenApiSchema value) => new() {
        AnyOf = [value, new OpenApiSchema { Type = JsonSchemaType.Null }], Description = value.Description
    };
}
