using System.Buffers;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CanDoItAll.Web;

internal static class ProjectStructureHttpJsonContract
{
    public const string InvalidObjectTypeErrorCode = "ProjectStructureObjectTypeInvalid";
    public const string UnsupportedContentTypeErrorCode = "ProjectStructureContentTypeUnsupported";
    public const string RuntimeDispatchContentType = "*/*";

    public static JsonSerializerOptions SerializerOptions { get; } = CreateSerializerOptions();

    public static Task TransformOpenApiOperationAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var endpointMetadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (endpointMetadata.OfType<ProjectStructureHttpResponseContract>().Any())
        {
            TransformResponseSchemas(operation);
        }

        var bodyContract = endpointMetadata
            .OfType<ProjectStructureHttpBodyContract>()
            .SingleOrDefault();
        var requestBody = operation.RequestBody;
        if (bodyContract is null || requestBody is null)
        {
            return Task.CompletedTask;
        }

        var requestContent = requestBody.Content ??
            throw new InvalidOperationException(
                $"The OpenAPI request body for '{context.Description.RelativePath}' has no content.");
        var mediaType = requestContent.Values.SingleOrDefault(candidate => candidate?.Schema is not null) ??
            throw new InvalidOperationException(
                $"The OpenAPI request body for '{context.Description.RelativePath}' has no schema.");
        requestContent.Clear();
        requestContent["application/json"] = mediaType;
        requestContent["application/*+json"] = mediaType;

        if (mediaType.Schema is { } mediaTypeSchema)
        {
            var requestSchema = ResolveSchema(mediaTypeSchema);
            var properties = requestSchema.Properties ??
                throw new InvalidOperationException(
                    $"The OpenAPI request schema for '{context.Description.RelativePath}' has no properties.");
            if (!properties.ContainsKey(bodyContract.PropertyName))
            {
                throw new InvalidOperationException(
                    $"The OpenAPI request schema for '{context.Description.RelativePath}' does not contain '{bodyContract.PropertyName}'.");
            }

            properties[bodyContract.PropertyName] =
                bodyContract.Shape == ProjectStructureObjectTypeBodyShape.OptionalArray
                    ? CreateObjectTypeArrayInputSchema(bodyContract)
                    : CreateObjectTypeInputSchema(bodyContract);

            // These routes read the body themselves, so no handler parameter carries a request-body description.
            if (requestBody is OpenApiRequestBody concreteRequestBody &&
                string.IsNullOrWhiteSpace(concreteRequestBody.Description))
            {
                concreteRequestBody.Description = DescribeRequestBody(requestSchema, bodyContract);
            }
        }

        return Task.CompletedTask;
    }

    private static string DescribeRequestBody(
        IOpenApiSchema requestSchema,
        ProjectStructureHttpBodyContract bodyContract)
    {
        var transport =
            "Send one JSON object as `application/json` or `application/*+json`. The Project Structure reader " +
            "rejects other content types with HTTP 415 `ProjectStructureContentTypeUnsupported`, a body larger than " +
            $"{bodyContract.MaximumBodyBytes.ToString("N0", CultureInfo.InvariantCulture)} bytes with HTTP 413 " +
            "`ProjectStructureRequestBodyTooLarge`, and malformed JSON or members of the wrong JSON type with " +
            "HTTP 400 `ProjectStructureRequestInvalid`.";
        return string.IsNullOrWhiteSpace(requestSchema.Description)
            ? transport
            : $"{requestSchema.Description.TrimEnd()} {transport}";
    }

    public static async Task<TRequest> ReadRequestAsync<TRequest>(
        HttpRequest request,
        ProjectStructureHttpBodyContract bodyContract,
        CancellationToken cancellationToken)
    {
        if (!request.HasJsonContentType())
        {
            throw UnsupportedContentType();
        }

        var document = await ReadBoundedDocumentAsync(
            request,
            bodyContract.MaximumBodyBytes,
            cancellationToken);

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw InvalidRequest();
            }

            ValidateObjectType<TRequest>(document.RootElement, bodyContract);

            try
            {
                return document.RootElement.Deserialize<TRequest>(SerializerOptions)
                    ?? throw InvalidRequest();
            }
            catch (ProjectStructureObjectTypeJsonException)
            {
                throw InvalidObjectType();
            }
            catch (JsonException)
            {
                throw InvalidRequest();
            }
        }
    }

    private static async Task<JsonDocument> ReadBoundedDocumentAsync(
        HttpRequest request,
        long maximumBodyBytes,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength is > 0 &&
            request.ContentLength.Value > maximumBodyBytes)
        {
            throw RequestBodyTooLarge(maximumBodyBytes);
        }

        await using var buffer = new MemoryStream();
        var rentedBuffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            while (true)
            {
                var bytesRead = await request.Body.ReadAsync(
                    rentedBuffer.AsMemory(),
                    cancellationToken);
                if (bytesRead == 0)
                {
                    break;
                }

                if (buffer.Length > maximumBodyBytes - bytesRead)
                {
                    throw RequestBodyTooLarge(maximumBodyBytes);
                }

                await buffer.WriteAsync(
                    rentedBuffer.AsMemory(0, bytesRead),
                    cancellationToken);
            }

            buffer.Position = 0;
            try
            {
                return await JsonDocument.ParseAsync(
                    buffer,
                    cancellationToken: cancellationToken);
            }
            catch (JsonException)
            {
                throw InvalidRequest();
            }
        }
        catch (IOException)
        {
            throw InvalidRequest();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    private static void ValidateObjectType<TRequest>(
        JsonElement root,
        ProjectStructureHttpBodyContract bodyContract)
    {
        if (!TryGetProperty(root, bodyContract.PropertyName, out var propertyValue))
        {
            if (bodyContract.Shape == ProjectStructureObjectTypeBodyShape.RequiredSingle)
            {
                throw InvalidObjectType();
            }

            return;
        }

        if (bodyContract.Shape == ProjectStructureObjectTypeBodyShape.OptionalArray)
        {
            if (propertyValue.ValueKind == JsonValueKind.Null)
            {
                return;
            }

            ValidateObjectTypeArray(propertyValue);
            return;
        }

        if (bodyContract.Shape == ProjectStructureObjectTypeBodyShape.OptionalSingle &&
            propertyValue.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        ValidateObjectTypeValue<TRequest>(root, propertyValue, bodyContract);
    }

    private static void ValidateObjectTypeArray(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw InvalidObjectType();
        }

        foreach (var item in value.EnumerateArray())
        {
            ValidateStrictObjectTypeValue(item);
        }
    }

    private static void ValidateObjectTypeValue<TRequest>(
        JsonElement root,
        JsonElement value,
        ProjectStructureHttpBodyContract bodyContract)
    {
        if (!bodyContract.AllowNodeKindAliases || value.ValueKind != JsonValueKind.String)
        {
            ValidateStrictObjectTypeValue(value);
            return;
        }

        try
        {
            ValidateStrictObjectTypeValue(value);
            return;
        }
        catch (ProjectStructureAgentException exception)
            when (exception.ErrorCode == InvalidObjectTypeErrorCode)
        {
        }

        var aliasProbe = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            [ProjectStructureHttpBodyContracts.ObjectTypePropertyName] = value.Clone()
        };
        if (TryGetProperty(
                root,
                ProjectStructureHttpBodyContracts.ObjectSubtypePropertyName,
                out var objectSubtype))
        {
            aliasProbe[ProjectStructureHttpBodyContracts.ObjectSubtypePropertyName] =
                objectSubtype.Clone();
        }

        try
        {
            var probeJson = JsonSerializer.SerializeToElement(aliasProbe, SerializerOptions);
            _ = probeJson.Deserialize<TRequest>(SerializerOptions)
                ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw InvalidObjectType();
        }
    }

    private static void ValidateStrictObjectTypeValue(JsonElement value)
    {
        if (value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number))
        {
            throw InvalidObjectType();
        }

        try
        {
            _ = value.Deserialize<ProjectObjectType>(SerializerOptions);
        }
        catch (ProjectStructureObjectTypeJsonException)
        {
            throw InvalidObjectType();
        }
        catch (JsonException)
        {
            throw InvalidObjectType();
        }
    }

    private static bool TryGetProperty(
        JsonElement root,
        string propertyName,
        out JsonElement value)
    {
        value = default;
        var found = false;
        foreach (var property in root.EnumerateObject())
        {
            if (!string.Equals(
                    property.Name,
                    propertyName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (found)
            {
                throw InvalidRequest();
            }

            value = property.Value;
            found = true;
        }

        return found;
    }

    private static ProjectStructureAgentException InvalidObjectType()
        => new(
            StatusCodes.Status400BadRequest,
            InvalidObjectTypeErrorCode,
            "ProjectObjectType values must use a supported symbol or defined numeric value.",
            new ProjectStructureHttpRejectionDetails(
                "objectType",
                "Case-insensitive canonical ProjectObjectType symbol or defined numeric value"));

    private static ProjectStructureAgentException UnsupportedContentType()
        => new(
            StatusCodes.Status415UnsupportedMediaType,
            UnsupportedContentTypeErrorCode,
            "Content-Type must identify a JSON request body.",
            new ProjectStructureHttpRejectionDetails(
                "Content-Type",
                "application/json or application/*+json"));

    private static ProjectStructureAgentException RequestBodyTooLarge(
        long maximumBodyBytes)
        => new(
            StatusCodes.Status413PayloadTooLarge,
            "ProjectStructureRequestBodyTooLarge",
            "The Project Structure request body exceeds the limit for this operation.",
            new ProjectStructureBodySizeRejectionDetails(
                "body",
                maximumBodyBytes));

    private static ProjectStructureAgentException InvalidRequest()
        => new(
            StatusCodes.Status400BadRequest,
            "ProjectStructureRequestInvalid",
            "The Project Structure request body is not valid JSON for this operation.");

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new ProjectStructureProjectObjectTypeJsonConverter());
        return options;
    }

    private static IList<JsonNode> CreateSymbolEnum()
        => Enum.GetNames<ProjectObjectType>()
            .Select(name => JsonValue.Create(name)!)
            .Cast<JsonNode>()
            .ToList();

    private static IList<JsonNode> CreateNumericEnum()
        => Enum.GetValues<ProjectObjectType>()
            .Select(value => JsonValue.Create((int)value)!)
            .Cast<JsonNode>()
            .ToList();

    private static OpenApiSchema CreateObjectTypeInputSchema(
        ProjectStructureHttpBodyContract bodyContract)
    {
        var alternatives = new List<IOpenApiSchema>
        {
            CreateObjectTypeStringInputSchema(bodyContract),
            new OpenApiSchema
            {
                Type = JsonSchemaType.Integer,
                Format = "int32",
                Enum = CreateNumericEnum()
            }
        };
        if (bodyContract.Shape == ProjectStructureObjectTypeBodyShape.OptionalSingle)
        {
            alternatives.Add(new OpenApiSchema
            {
                Type = JsonSchemaType.Null
            });
        }

        return new OpenApiSchema
        {
            Description = bodyContract.AllowNodeKindAliases
                ? bodyContract.Shape == ProjectStructureObjectTypeBodyShape.OptionalSingle
                    ? "Object type to give the node: its name in any casing (for example `WorkItem`), a node-kind " +
                      "alias that also sets the subtype (for example `FeatureBlock` or `Folder`) or its integer from " +
                      "0 through 30; null or omitted keeps the current type."
                    : "Object type of the new node: its name in any casing (for example `WorkItem`), a node-kind " +
                      "alias that also sets the subtype (for example `FeatureBlock` or `Folder`) or its integer from " +
                      "0 through 30."
                : "Object type as its name in any casing (for example `WorkItem`) or its integer from 0 through 30.",
            OneOf = alternatives
        };
    }

    private static OpenApiSchema CreateObjectTypeStringInputSchema(
        ProjectStructureHttpBodyContract bodyContract)
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String
        };
        if (!bodyContract.AllowNodeKindAliases)
        {
            schema.Enum = CreateSymbolEnum();
        }

        return schema;
    }

    private static OpenApiSchema CreateObjectTypeArrayInputSchema(
        ProjectStructureHttpBodyContract bodyContract)
        => new()
        {
            Description = "Object types to keep, each as its name in any casing (for example `WorkItem`) or its " +
                "integer from 0 through 30; null or an empty array keeps every type. Null items are rejected.",
            OneOf =
            [
                new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = CreateObjectTypeInputSchema(bodyContract)
                },
                new OpenApiSchema
                {
                    Type = JsonSchemaType.Null
                }
            ]
        };

    private static void TransformResponseSchemas(OpenApiOperation operation)
    {
        if (operation.Responses is not { } responses)
        {
            return;
        }

        foreach (var response in responses.Values)
        {
            if (response?.Content is null)
            {
                continue;
            }

            foreach (var mediaType in response.Content.Values)
            {
                if (mediaType?.Schema is { } schema)
                {
                    mediaType.Schema = CloneProjectStructureResponseSchema(
                        schema,
                        new HashSet<IOpenApiSchema>(ReferenceEqualityComparer.Instance));
                }
            }
        }
    }

    private static IOpenApiSchema CloneProjectStructureResponseSchema(
        IOpenApiSchema schema,
        ISet<IOpenApiSchema> recursionPath)
    {
        if (schema is OpenApiSchemaReference reference)
        {
            // A description on the reference describes the role of this use (for example a property); the inlined
            // copy keeps it instead of falling back to the referenced type's description.
            var useDescription = reference.Reference.Description;
            if (string.Equals(
                    reference.Reference.Id,
                    nameof(ProjectObjectType),
                    StringComparison.Ordinal))
            {
                return CreateObjectTypeResponseSchema(useDescription);
            }

            var target = reference.Target ??
                throw new InvalidOperationException(
                    $"OpenAPI response schema reference '{reference.Reference.Id}' is unresolved.");
            var inlined = CloneProjectStructureResponseSchema(target, recursionPath);
            if (!string.IsNullOrWhiteSpace(useDescription) && inlined is OpenApiSchema inlinedSchema)
            {
                inlinedSchema.Description = useDescription;
            }

            return inlined;
        }

        if (schema is not OpenApiSchema concreteSchema)
        {
            throw new InvalidOperationException(
                $"Unsupported OpenAPI response schema type '{schema.GetType().FullName}'.");
        }

        if (!recursionPath.Add(schema))
        {
            throw new InvalidOperationException(
                "The Project Structure OpenAPI response schema contains a recursive reference that cannot be safely inlined.");
        }

        try
        {
            var clone = concreteSchema.CreateShallowCopy() as OpenApiSchema ??
                throw new InvalidOperationException(
                    "The OpenAPI response schema could not be cloned.");
            clone.Definitions = CloneSchemaDictionary(
                concreteSchema.Definitions,
                recursionPath);
            clone.Properties = CloneSchemaDictionary(
                concreteSchema.Properties,
                recursionPath);
            clone.PatternProperties = CloneSchemaDictionary(
                concreteSchema.PatternProperties,
                recursionPath);
            clone.DependentSchemas = CloneSchemaDictionary(
                concreteSchema.DependentSchemas,
                recursionPath);
            clone.AllOf = CloneSchemaList(concreteSchema.AllOf, recursionPath);
            clone.OneOf = CloneSchemaList(concreteSchema.OneOf, recursionPath);
            clone.AnyOf = CloneSchemaList(concreteSchema.AnyOf, recursionPath);
            clone.Items = CloneOptionalSchema(concreteSchema.Items, recursionPath);
            clone.Not = CloneOptionalSchema(concreteSchema.Not, recursionPath);
            clone.Contains = CloneOptionalSchema(concreteSchema.Contains, recursionPath);
            clone.AdditionalProperties = CloneOptionalSchema(
                concreteSchema.AdditionalProperties,
                recursionPath);
            clone.UnevaluatedPropertiesSchema = CloneOptionalSchema(
                concreteSchema.UnevaluatedPropertiesSchema,
                recursionPath);
            clone.ContentSchema = CloneOptionalSchema(
                concreteSchema.ContentSchema,
                recursionPath);
            clone.PropertyNames = CloneOptionalSchema(
                concreteSchema.PropertyNames,
                recursionPath);
            clone.If = CloneOptionalSchema(concreteSchema.If, recursionPath);
            clone.Then = CloneOptionalSchema(concreteSchema.Then, recursionPath);
            clone.Else = CloneOptionalSchema(concreteSchema.Else, recursionPath);
            return clone;
        }
        finally
        {
            recursionPath.Remove(schema);
        }
    }

    private static IDictionary<string, IOpenApiSchema>? CloneSchemaDictionary(
        IDictionary<string, IOpenApiSchema>? schemas,
        ISet<IOpenApiSchema> recursionPath)
    {
        if (schemas is null)
        {
            return null;
        }

        return schemas.ToDictionary(
            entry => entry.Key,
            entry => CloneProjectStructureResponseSchema(
                entry.Value,
                recursionPath),
            StringComparer.Ordinal);
    }

    private static IList<IOpenApiSchema>? CloneSchemaList(
        IList<IOpenApiSchema>? schemas,
        ISet<IOpenApiSchema> recursionPath)
    {
        if (schemas is null)
        {
            return null;
        }

        return schemas
            .Select(schema => CloneProjectStructureResponseSchema(
                schema,
                recursionPath))
            .ToList();
    }

    private static IOpenApiSchema? CloneOptionalSchema(
        IOpenApiSchema? schema,
        ISet<IOpenApiSchema> recursionPath)
        => schema is null
            ? null
            : CloneProjectStructureResponseSchema(schema, recursionPath);

    private static OpenApiSchema CreateObjectTypeResponseSchema(string? useDescription)
        => new()
        {
            Type = JsonSchemaType.String,
            Description = string.IsNullOrWhiteSpace(useDescription)
                ? "Object type name, for example `WorkItem`."
                : $"{useDescription.TrimEnd()} Returned as the object type name, for example `WorkItem`.",
            Enum = CreateSymbolEnum()
        };

    private static IOpenApiSchema ResolveSchema(IOpenApiSchema schema)
    {
        while (schema is OpenApiSchemaReference reference)
        {
            schema = reference.Target ??
                throw new InvalidOperationException(
                    $"OpenAPI schema reference '{reference.Reference.Id}' is unresolved.");
        }

        return schema;
    }

    private sealed record ProjectStructureHttpRejectionDetails(
        string Field,
        string SupportedRepresentation);

    private sealed record ProjectStructureBodySizeRejectionDetails(
        string Field,
        long MaximumBytes);
}

internal enum ProjectStructureObjectTypeBodyShape
{
    RequiredSingle,
    OptionalSingle,
    OptionalArray
}

internal sealed record ProjectStructureHttpBodyContract(
    string PropertyName,
    ProjectStructureObjectTypeBodyShape Shape,
    long MaximumBodyBytes,
    bool AllowNodeKindAliases = false);

internal sealed class ProjectStructureHttpResponseContract
{
    public static ProjectStructureHttpResponseContract Instance { get; } = new();

    private ProjectStructureHttpResponseContract()
    {
    }
}

internal static class ProjectStructureHttpBodyContracts
{
    public const string ObjectTypePropertyName = "objectType";
    public const string ObjectTypesPropertyName = "objectTypes";
    public const string ObjectSubtypePropertyName = "objectSubtype";

    private const long QueryBodyBytes = 256L * 1024L;
    private const long NodeMutationBodyBytes = 1024L * 1024L;
    private const long AssetMutationEnvelopeBytes = 1024L * 1024L;
    private const long AssetMutationBodyBytes =
        ProjectStructureAssetUploadLimits.MaximumBase64Characters +
        AssetMutationEnvelopeBytes;

    public static ProjectStructureHttpBodyContract StructureRead { get; } =
        new(
            ObjectTypesPropertyName,
            ProjectStructureObjectTypeBodyShape.OptionalArray,
            QueryBodyBytes);

    public static ProjectStructureHttpBodyContract NodeCreate { get; } =
        new(
            ObjectTypePropertyName,
            ProjectStructureObjectTypeBodyShape.RequiredSingle,
            AssetMutationBodyBytes,
            AllowNodeKindAliases: true);

    public static ProjectStructureHttpBodyContract NodeEdit { get; } =
        new(
            ObjectTypePropertyName,
            ProjectStructureObjectTypeBodyShape.OptionalSingle,
            NodeMutationBodyBytes,
            AllowNodeKindAliases: true);

    public static ProjectStructureHttpBodyContract NodeType { get; } =
        new(
            ObjectTypePropertyName,
            ProjectStructureObjectTypeBodyShape.RequiredSingle,
            QueryBodyBytes);

    public static ProjectStructureHttpBodyContract ChecklistQuery { get; } =
        new(
            ObjectTypesPropertyName,
            ProjectStructureObjectTypeBodyShape.OptionalArray,
            QueryBodyBytes);

    public static ProjectStructureHttpBodyContract AssetCreate { get; } =
        new(
            ObjectTypePropertyName,
            ProjectStructureObjectTypeBodyShape.RequiredSingle,
            AssetMutationBodyBytes);
}

/// <summary>
/// Acknowledgement returned by Project Structure operations that have no other result, always
/// <c>{ "ok": true }</c>. It only confirms that the operation completed; read the affected project or structure again
/// to see the stored result.
/// </summary>
/// <param name="Ok">Always true. Failures return the Project Structure error envelope instead.</param>
internal sealed record ProjectStructureOkResponse(bool Ok);

/// <summary>
/// Node creation request: the kind of node, its display fields and an optional parent, schedule, file, metadata and
/// canvas position. Canonical tasks (WorkItem with subtype <c>task</c>) are created with the task operation, and File
/// nodes and <c>mermaid</c> subtypes with the asset operation. Every string member may be omitted; omitted strings are
/// stored as empty.
/// </summary>
/// <param name="ObjectType">
/// Object type of the new node: its name in any casing, a typed alias that also sets the subtype, or its integer.
/// </param>
/// <param name="Title">Title of the new node.</param>
/// <param name="Subtitle">Secondary display text.</param>
/// <param name="Notes">Free-text notes.</param>
/// <param name="ParentNodeKey">
/// Identifier of the parent node, as returned in <c>nodes[].id</c>; null or blank places the node under the project
/// root. An unknown parent is rejected with HTTP 404 <c>ParentNodeNotFound</c>.
/// </param>
/// <param name="X">
/// Preferred horizontal canvas position; used as a hint only when <c>y</c> is also sent. The node is placed near its
/// parent automatically.
/// </param>
/// <param name="Y">Preferred vertical canvas position; used as a hint only when <c>x</c> is also sent.</param>
/// <param name="StartUtc">Planned start as an instant with offset; null or omitted leaves the node unscheduled.</param>
/// <param name="EndUtc">
/// Planned end as an instant with offset. When omitted and <c>startUtc</c> is sent, the end is the start plus
/// <c>durationSeconds</c> (one hour when that is omitted or not positive).
/// </param>
/// <param name="ObjectSubtype">
/// Lower-case subtype, for example <c>feature</c> for a ProjectBlock; known synonyms are normalized. A typed alias in
/// <c>objectType</c> supplies it when omitted. See <c>objectTypes[].creatableSubtypes</c> in the node catalog.
/// </param>
/// <param name="Media">
/// Optional file content stored with the node, for example the picture of an ImageAsset. File nodes are created with
/// the asset operation instead.
/// </param>
/// <param name="MetadataJson">
/// Optional metadata as a JSON string that contains a JSON object, with the section for the node's type (see the node
/// catalog guidance). It takes precedence over <c>metadata</c> when not blank. Script, Environment and Infrastructure
/// metadata is validated.
/// </param>
/// <param name="Metadata">
/// Optional metadata as a JSON object instead of a string; used only when <c>metadataJson</c> is null or blank.
/// </param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on this project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the operation uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
/// <param name="DurationSeconds">
/// Planned duration in seconds; a value of 0 or less stores no duration. When omitted it is computed from the planned
/// start and end.
/// </param>
internal sealed record ProjectStructureNodeCreateOpenApiRequest(
    ProjectObjectType ObjectType,
    string Title,
    string Subtitle,
    string Notes,
    string? ParentNodeKey,
    double? X = null,
    double? Y = null,
    DateTimeOffset? StartUtc = null,
    DateTimeOffset? EndUtc = null,
    string? ObjectSubtype = null,
    ProjectObjectMediaPayload? Media = null,
    string? MetadataJson = null,
    JsonElement? Metadata = null,
    string? LeaseToken = null,
    int? DurationSeconds = null);

/// <summary>
/// Node update request for a generic node. It replaces the node's subtitle, notes and schedule with the sent values,
/// so send the current values of members you do not want to change: an omitted or null <c>subtitle</c> or
/// <c>notes</c> clears it, and omitted schedule members clear the planned interval unless the request also
/// reclassifies the node. A blank <c>title</c> keeps the
/// current title, and blank <c>metadataJson</c> and <c>metadata</c> keep the stored metadata. A new
/// <c>objectType</c> or <c>objectSubtype</c> reclassifies the node, which only some kind changes allow.
/// </summary>
/// <param name="Title">New title; blank or omitted keeps the current title.</param>
/// <param name="Subtitle">New subtitle; omitted or null clears it.</param>
/// <param name="Notes">New notes; omitted or null clears them.</param>
/// <param name="ObjectType">
/// New object type: its name in any casing, a typed alias or its integer; null or omitted keeps the current type.
/// </param>
/// <param name="ObjectSubtype">
/// New lower-case subtype; null or omitted keeps the current subtype, or clears it when the object type changes.
/// </param>
/// <param name="StartUtc">
/// Planned start as an instant with offset; omitted or null clears it when the type stays the same.
/// </param>
/// <param name="EndUtc">
/// Planned end as an instant with offset. When omitted and <c>startUtc</c> is sent, the end is the start plus
/// <c>durationSeconds</c> (one hour when that is omitted or not positive).
/// </param>
/// <param name="MetadataJson">
/// New metadata as a JSON string that contains a JSON object; it replaces the stored metadata and takes precedence
/// over <c>metadata</c>. Blank keeps the stored metadata.
/// </param>
/// <param name="Metadata">
/// New metadata as a JSON object instead of a string; used only when <c>metadataJson</c> is null or blank.
/// </param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on this project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the operation uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
/// <param name="DurationSeconds">
/// Planned duration in seconds; a value of 0 or less stores no duration. When omitted it is computed from the planned
/// start and end.
/// </param>
internal sealed record ProjectStructureNodeEditOpenApiRequest(
    string Title,
    string Subtitle,
    string Notes,
    ProjectObjectType? ObjectType = null,
    string? ObjectSubtype = null,
    DateTimeOffset? StartUtc = null,
    DateTimeOffset? EndUtc = null,
    string? MetadataJson = null,
    JsonElement? Metadata = null,
    string? LeaseToken = null,
    int? DurationSeconds = null);

internal sealed class ProjectStructureProjectObjectTypeJsonConverter : JsonConverter<ProjectObjectType>
{
    public override ProjectObjectType Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var symbol = reader.GetString();
            if (!string.IsNullOrWhiteSpace(symbol) &&
                Enum.TryParse<ProjectObjectType>(symbol, ignoreCase: true, out var symbolicValue) &&
                Enum.IsDefined(symbolicValue) &&
                string.Equals(
                    Enum.GetName(symbolicValue),
                    symbol,
                    StringComparison.OrdinalIgnoreCase))
            {
                return symbolicValue;
            }
        }
        else if (reader.TokenType == JsonTokenType.Number &&
                 reader.TryGetInt32(out var numericValue) &&
                 Enum.IsDefined(typeof(ProjectObjectType), numericValue))
        {
            return (ProjectObjectType)numericValue;
        }

        throw new ProjectStructureObjectTypeJsonException();
    }

    public override void Write(
        Utf8JsonWriter writer,
        ProjectObjectType value,
        JsonSerializerOptions options)
    {
        if (!Enum.IsDefined(value))
        {
            throw new JsonException(
                "The Project Structure response contains an undefined ProjectObjectType value.");
        }

        writer.WriteStringValue(value.ToString());
    }
}

internal sealed class ProjectStructureObjectTypeJsonException : JsonException
{
}
