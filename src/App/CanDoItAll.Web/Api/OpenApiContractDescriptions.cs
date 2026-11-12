using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CanDoItAll.Web.Api;

internal static partial class OpenApiContractDescriptions {
    private const string DescriptionMetadataKey = "candoitall-contract-description";
    private sealed record Description(string Text, params (string Name, string Text)[] Properties);

    private static readonly IReadOnlyDictionary<Type, Description> Descriptions = CreateDefinitionDescriptions()
        .Concat(CreateLifecycleDescriptions()).Concat(CreateAgentPlanningDescriptions()).Concat(CreateProcessPlanningDescriptions())
        .ToDictionary(pair => pair.Key, pair => new Description(pair.Value.Text,
            pair.Value.Properties.Select(property => (property.Name,
                DescribeEnumProperty(pair.Key, property.Name, property.Text))).ToArray()));

    internal static Task TransformSchemaAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken) {
        var type = Key(context.JsonTypeInfo.Type);
        if (Descriptions.TryGetValue(type, out var description)) {
            schema.Metadata ??= new Dictionary<string, object>();
            schema.Metadata[DescriptionMetadataKey] = description;
            schema.Description = description.Text;
            ApplyProperties(schema, description, wrapReferences: false);
        }
        if (!type.IsEnum && context.JsonPropertyInfo is { } jsonProperty &&
            Descriptions.TryGetValue(Key(jsonProperty.DeclaringType), out var owner)) {
            foreach (var (name, text) in owner.Properties) {
                if (JsonSerializerOptions.Web.PropertyNamingPolicy!.ConvertName(name) == jsonProperty.Name) {
                    schema.Description = text;
                    break;
                }
            }
        }
        return Task.CompletedTask;
    }

    internal static Task TransformDocumentAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken) {
        HashSet<OpenApiSchema> visited = new(ReferenceEqualityComparer.Instance);
        foreach (var schema in document.Components?.Schemas?.Values ?? []) {
            Visit(schema);
        }
        foreach (var path in document.Paths?.Values.AsEnumerable() ?? []) {
            foreach (var operation in path.Operations?.Values.AsEnumerable() ?? []) {
                foreach (var parameter in operation.Parameters ?? []) {
                    Visit(parameter.Schema);
                }
                foreach (var media in operation.RequestBody?.Content?.Values.AsEnumerable() ?? []) {
                    Visit(media.Schema);
                }
                foreach (var response in operation.Responses?.Values.AsEnumerable() ?? []) {
                    foreach (var media in response.Content?.Values.AsEnumerable() ?? []) {
                        Visit(media.Schema);
                    }
                }
            }
        }
        return Task.CompletedTask;

        void Visit(IOpenApiSchema? value) {
            cancellationToken.ThrowIfCancellationRequested();
            if (value is not OpenApiSchema schema || !visited.Add(schema)) {
                return;
            }
            if (schema.Metadata?.TryGetValue(DescriptionMetadataKey, out var metadata) == true && metadata is Description description) {
                ApplyProperties(schema, description, wrapReferences: true);
            }
            foreach (var property in schema.Properties?.Values ?? []) {
                Visit(property);
            }
            foreach (var branch in (schema.AllOf ?? []).Concat(schema.AnyOf ?? []).Concat(schema.OneOf ?? [])) {
                Visit(branch);
            }
            Visit(schema.Items);
            Visit(schema.AdditionalProperties);
            Visit(schema.Not);
        }
    }

    private static void ApplyProperties(OpenApiSchema schema, Description description, bool wrapReferences) {
        foreach (var (name, text) in description.Properties) {
            var propertyName = JsonSerializerOptions.Web.PropertyNamingPolicy!.ConvertName(name);
            if (schema.Properties is not { } properties || !properties.TryGetValue(propertyName, out var property) || property is null) {
                continue;
            }
            if (wrapReferences && property is OpenApiSchemaReference reference) {
                properties[propertyName] = new OpenApiSchema {
                    Description = text,
                    AllOf = [reference]
                };
            } else {
                property.Description = text;
            }
        }
    }

    private static Type Key(Type type) {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsGenericType ? type.GetGenericTypeDefinition() : type;
    }

    private static string DescribeEnumProperty(Type owner, string name, string description) {
        var type = owner.GetProperty(name)?.PropertyType;
        if (type is null) {
            return description;
        }
        type = Nullable.GetUnderlyingType(type) ?? type;
        type = type.IsArray ? type.GetElementType()! : type.GenericTypeArguments is [var item] ? item : type;
        return type.IsEnum ? EnumText(type, description) : description;
    }

    private static string EnumText<T>(string description) where T : struct, Enum => EnumText(typeof(T), description);

    private static string EnumText(Type type, string description) => description + " Values: " +
        string.Join(", ", Enum.GetValues(type).Cast<object>().Select(value => $"{Convert.ToInt64(value, CultureInfo.InvariantCulture)} {value}")) + ".";
}
