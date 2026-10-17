using CanDoItAll.AgentFramework.Models;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CanDoItAll.Web.Api;

internal static class AgentHistoricalReceiptOpenApiContract {
    private const string MaximumRevision = "9223372036854775807";
    private static readonly string RevisionPattern = CreateRevisionPattern();

    internal static Task TransformSchemaAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken) {
        var type = context.JsonTypeInfo.Type;
        if (type != typeof(AgentHistoricalReceiptCursor) && type != typeof(AgentHistoricalReceiptReview)) {
            return Task.CompletedTask;
        }
        schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
        foreach (var name in new[] { "runRevision", "journalRevision" }) {
            schema.Properties[name] = new OpenApiSchema {
                Type = type == typeof(AgentHistoricalReceiptReview) ? JsonSchemaType.String | JsonSchemaType.Null : JsonSchemaType.String,
                MinLength = 1,
                MaxLength = MaximumRevision.Length,
                Pattern = RevisionPattern,
                Description = "Original positive Int64 revision as an exact decimal string from 1 through 9223372036854775807. Never convert to a JavaScript number or replace a saved cursor revision. Null in a refused review discloses no saved witness."
            };
        }
        return Task.CompletedTask;
    }

    private static string CreateRevisionPattern() {
        List<string> alternatives = ["[1-9][0-9]{0,17}"];
        for (var index = 0; index < MaximumRevision.Length; index++) {
            var minimum = index == 0 ? '1' : '0';
            var upper = MaximumRevision[index];
            if (upper <= minimum) {
                continue;
            }
            var digit = upper - 1 == minimum ? minimum.ToString() : $"[{minimum}-{(char)(upper - 1)}]";
            var remainder = MaximumRevision.Length - index - 1;
            alternatives.Add(MaximumRevision[..index] + digit + (remainder == 0 ? string.Empty : $"[0-9]{{{remainder}}}"));
        }
        alternatives.Add(MaximumRevision);
        return "^(?:" + string.Join('|', alternatives) + ")(?![\\s\\S])";
    }
}
