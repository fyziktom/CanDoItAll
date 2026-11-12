using System.Text.Json;

namespace CanDoItAll.Tests.Integration.Api;

internal static class OpenApiSchemaAssertions {
    internal static JsonElement Resolve(JsonElement root, JsonElement schema) {
        while (true) {
            if (schema.TryGetProperty("$ref", out var reference)) {
                schema = root;
                foreach (var segment in reference.GetString()![2..].Split('/')) {
                    schema = schema.GetProperty(segment.Replace("~1", "/").Replace("~0", "~"));
                }
                continue;
            }
            if (!schema.TryGetProperty("allOf", out var branches)) {
                return schema;
            }
            Assert.Equal(new[] { "allOf", "description" }, Names(schema));
            Assert.False(string.IsNullOrWhiteSpace(schema.GetProperty("description").GetString()));
            schema = Assert.Single(branches.EnumerateArray());
            Assert.Equal(new[] { "$ref" }, Names(schema));
        }
    }

    private static string[] Names(JsonElement value)
        => value.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();
}
