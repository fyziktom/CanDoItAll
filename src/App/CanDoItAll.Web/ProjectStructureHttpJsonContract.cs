using System.Buffers;
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
        }

        return Task.CompletedTask;
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
                    ? "Case-insensitive canonical ProjectObjectType symbol, existing node-kind alias, defined numeric value, or null to preserve the current type."
                    : "Case-insensitive canonical ProjectObjectType symbol, existing node-kind alias, or defined numeric value."
                : "Case-insensitive canonical ProjectObjectType symbol or defined numeric value.",
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
            Description = "Optional ProjectObjectType filters. A null collection means no object-type filter; null array items are not supported.",
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
            if (string.Equals(
                    reference.Reference.Id,
                    nameof(ProjectObjectType),
                    StringComparison.Ordinal))
            {
                return CreateObjectTypeResponseSchema();
            }

            var target = reference.Target ??
                throw new InvalidOperationException(
                    $"OpenAPI response schema reference '{reference.Reference.Id}' is unresolved.");
            return CloneProjectStructureResponseSchema(target, recursionPath);
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

    private static OpenApiSchema CreateObjectTypeResponseSchema()
        => new()
        {
            Type = JsonSchemaType.String,
            Description = "Canonical ProjectObjectType response symbol.",
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
