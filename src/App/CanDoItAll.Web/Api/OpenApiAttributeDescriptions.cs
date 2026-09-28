using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CanDoItAll.Web.Api;

internal static class OpenApiAttributeDescriptions {
    private const string TypeInfoMetadataKey = "candoitall-attribute-type-info";

    public static Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken) {
        var type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
        var description = type.GetCustomAttribute<DescriptionAttribute>()?.Description;
        if (description is not null) {
            schema.Description ??= description;
            if (type.IsEnum) {
                schema.Description = WithEnumValues(schema.Description, type);
            }
        }
        var typeInfo = context.JsonTypeInfo.Options.GetTypeInfo(type);
        schema.Metadata ??= new Dictionary<string, object>();
        schema.Metadata[TypeInfoMetadataKey] = typeInfo;
        return Task.CompletedTask;
    }

    public static Task TransformDocumentAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken) {
        foreach (var schema in (document.Components?.Schemas?.Values ?? []).OfType<OpenApiSchema>()) {
            if (schema.Metadata?.TryGetValue(TypeInfoMetadataKey, out var metadata) != true || metadata is not JsonTypeInfo typeInfo || schema.Properties is not { } properties) {
                continue;
            }
            foreach (var property in typeInfo.Properties) {
                var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                var enumType = propertyType.GenericTypeArguments is [var itemType] ? itemType : propertyType;
                if (enumType?.IsEnum != true || enumType.GetCustomAttribute<DescriptionAttribute>() is null ||
                    !properties.TryGetValue(property.Name, out var propertySchema)) {
                    continue;
                }
                propertySchema.Description = WithEnumValues(propertySchema.Description, enumType);
            }
        }
        return Task.CompletedTask;
    }

    private static string WithEnumValues(string? description, Type type) => description + " Values: " +
        string.Join(", ", Enum.GetValues(type).Cast<object>().Select(value => $"{Convert.ToInt64(value)} {value}")) + ".";
}
