using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Modules.Projects;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi;

namespace CanDoItAll.Web.Api;

internal static class ProjectOperationEpochHttpContract {
    internal const string LookupEndpointName = "GetProjectDefinitionOperation";
    internal const string Maximum = "9223372036854775807";
    internal const string Description = "Original operation epoch as a canonical decimal string from 1 through 9223372036854775807. Preserve it exactly; numbers, signs, whitespace, leading zeros, fractions and exponents are invalid.";
    internal static readonly string Pattern = CreatePattern();

    internal static bool TryParse(string? text, out ProjectOperationEpoch? epoch) {
        epoch = null;
        if (text is not { Length: > 0 and <= 19 } || text[0] is < '1' or > '9' ||
            text.AsSpan().ContainsAnyExcept("0123456789") ||
            !long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value)) {
            return false;
        }
        epoch = new(value);
        return true;
    }

    internal static Task TransformSchemaAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken) {
        if (context.JsonTypeInfo.Type == typeof(ProjectOperationEpoch)) {
            ApplySchema(schema);
        }
        return Task.CompletedTask;
    }

    internal static Task TransformOperationAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken) {
        if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IEndpointNameMetadata>()
                .Any(metadata => metadata.EndpointName == LookupEndpointName) &&
            operation.Parameters?.SingleOrDefault(parameter => parameter.In == ParameterLocation.Path &&
                parameter.Name == "epoch") is OpenApiParameter epoch) {
            var schema = new OpenApiSchema();
            ApplySchema(schema);
            epoch.Schema = schema;
            epoch.Description = Description;
        }
        return Task.CompletedTask;
    }

    private static void ApplySchema(OpenApiSchema schema) {
        schema.Type = JsonSchemaType.String;
        schema.Format = null;
        schema.Pattern = Pattern;
        schema.MinLength = 1;
        schema.MaxLength = Maximum.Length;
        schema.Description = Description;
        schema.Minimum = null;
        schema.Maximum = null;
        schema.Properties?.Clear();
        schema.Required?.Clear();
        schema.AdditionalProperties = null;
    }

    private static string CreatePattern() {
        List<string> alternatives = ["[1-9][0-9]{0,17}"];
        for (var index = 0; index < Maximum.Length; index++) {
            var minimum = index == 0 ? '1' : '0';
            var exclusiveMaximum = Maximum[index];
            if (exclusiveMaximum <= minimum) {
                continue;
            }
            var lowerDigit = exclusiveMaximum - 1 == minimum ? minimum.ToString() : $"[{minimum}-{(char)(exclusiveMaximum - 1)}]";
            var remaining = Maximum.Length - index - 1;
            alternatives.Add(Maximum[..index] + lowerDigit + (remaining == 0 ? string.Empty : $"[0-9]{{{remaining}}}"));
        }
        alternatives.Add(Maximum);
        return "^(?:" + string.Join('|', alternatives) + ")(?![\\s\\S])";
    }
}

internal sealed class ProjectOperationEpochHttpJsonConverter : JsonConverter<ProjectOperationEpoch> {
    public override bool HandleNull => true;

    public override ProjectOperationEpoch Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType != JsonTokenType.String || !ProjectOperationEpochHttpContract.TryParse(reader.GetString(), out var epoch)) {
            throw new JsonException(ProjectOperationEpochHttpContract.Description);
        }
        return epoch!;
    }

    public override void Write(Utf8JsonWriter writer, ProjectOperationEpoch value, JsonSerializerOptions options) {
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStringValue(value.Value.ToString(CultureInfo.InvariantCulture));
    }
}
