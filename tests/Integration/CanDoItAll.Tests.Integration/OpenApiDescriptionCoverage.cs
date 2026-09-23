using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CanDoItAll.Tests.Integration.Api;

// Walks a generated OpenAPI document and reports every operation, parameter, request body, response, component
// schema and schema property of a route family that lacks a usable description. A property whose schema is a nullable
// reference (oneOf null and $ref) is described by the description on its $ref branch. An integer enum schema carries no
// list of values and Swagger UI shows a property's own description instead of the enum type's, so the enum type and
// every property or parameter of that type must list the values themselves, for example "0 Hours, 1 ManDays".
internal static class OpenApiDescriptionCoverage
{
    private static readonly string[] OperationMethods = ["get", "put", "post", "delete", "patch", "head", "options"];

    private static readonly HashSet<string> ReasonPhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        "OK", "Created", "Accepted", "No Content", "Not Modified", "Moved Permanently", "Found", "See Other",
        "Temporary Redirect", "Bad Request", "Unauthorized", "Forbidden", "Not Found", "Method Not Allowed",
        "Request Timeout", "Conflict", "Gone", "Precondition Failed", "Payload Too Large", "Content Too Large",
        "Unsupported Media Type", "Unprocessable Entity", "Unprocessable Content", "Precondition Required",
        "Too Many Requests", "Internal Server Error", "Bad Gateway", "Service Unavailable", "Gateway Timeout"
    };

    private static readonly string[] FillerPrefixes = ["Gets or sets", "Gets ", "Sets "];

    private static readonly HashSet<string> FillerTexts = new(StringComparer.OrdinalIgnoreCase)
    {
        "The data.", "The model.", "The identifier.", "The ID.", "The id.", "The value.", "The name.", "The type."
    };

    private static readonly Regex EnumValueListing = new(
        @"(?<![\w.])-?\d+ [A-Z][A-Za-z]",
        RegexOptions.CultureInvariant);

    public static IReadOnlyList<string> FindGaps(JsonObject document, string pathPrefix)
    {
        var gaps = new List<string>();
        var components = new SortedSet<string>(StringComparer.Ordinal);
        var schemas = document["components"]?["schemas"]?.AsObject() ?? [];
        var paths = document["paths"]?.AsObject() ?? [];
        foreach (var (path, pathItemNode) in paths)
        {
            if (!BelongsToFamily(path, pathPrefix) || pathItemNode is not JsonObject pathItem)
            {
                continue;
            }

            foreach (var method in OperationMethods)
            {
                if (pathItem[method] is JsonObject operation)
                {
                    CheckOperation(operation, $"#/paths/{Escape(path)}/{method}", schemas, gaps, components);
                }
            }
        }

        var checkedComponents = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>(components);
        while (pending.Count > 0)
        {
            var name = pending.Dequeue();
            if (!checkedComponents.Add(name))
            {
                continue;
            }

            if (schemas[name] is not JsonObject schema)
            {
                gaps.Add($"#/components/schemas/{Escape(name)}: referenced schema is missing");
                continue;
            }

            var pointer = $"#/components/schemas/{Escape(name)}";
            RequireDescription(schema, pointer, "schema", gaps);
            if (IsIntegerEnum(schema) && !ListsEnumValues(schema["description"]?.GetValue<string>()))
            {
                gaps.Add($"{pointer}/description: enum schema does not list its values");
            }

            var references = new SortedSet<string>(StringComparer.Ordinal);
            CheckSchemaContent(schema, pointer, schemas, gaps, references);
            foreach (var reference in references)
            {
                pending.Enqueue(reference);
            }
        }

        return gaps;
    }

    private static bool BelongsToFamily(string path, string prefix)
        => path.Equals(prefix, StringComparison.Ordinal) ||
           path.StartsWith(prefix + "/", StringComparison.Ordinal);

    private static void CheckOperation(
        JsonObject operation,
        string pointer,
        JsonObject schemas,
        List<string> gaps,
        SortedSet<string> components)
    {
        RequireText(operation["summary"], $"{pointer}/summary", "operation summary", gaps);
        RequireText(operation["description"], $"{pointer}/description", "operation description", gaps);

        if (operation["parameters"] is JsonArray parameters)
        {
            for (var index = 0; index < parameters.Count; index++)
            {
                if (parameters[index] is not JsonObject parameter)
                {
                    continue;
                }

                var parameterPointer = $"{pointer}/parameters/{index}";
                RequireDescription(parameter, parameterPointer, $"parameter '{parameter["name"]}'", gaps);
                if (parameter["schema"] is JsonObject parameterSchema)
                {
                    if (UsesIntegerEnum(parameterSchema, schemas) &&
                        !ListsEnumValues(parameter["description"]?.GetValue<string>()))
                    {
                        gaps.Add(
                            $"{parameterPointer}/description: enum parameter '{parameter["name"]}' does not list its values");
                    }

                    CheckSchemaContent(parameterSchema, $"{parameterPointer}/schema", schemas, gaps, components);
                }
            }
        }

        if (operation["requestBody"] is JsonObject requestBody)
        {
            RequireDescription(requestBody, $"{pointer}/requestBody", "request body", gaps);
            CheckContent(requestBody["content"], $"{pointer}/requestBody/content", schemas, gaps, components);
        }

        if (operation["responses"] is JsonObject responses)
        {
            foreach (var (status, responseNode) in responses)
            {
                if (responseNode is not JsonObject response)
                {
                    continue;
                }

                var responsePointer = $"{pointer}/responses/{status}";
                var description = response["description"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(description) || ReasonPhrases.Contains(description.Trim()))
                {
                    gaps.Add($"{responsePointer}/description: response {status} has no specific description");
                }

                if (response["headers"] is JsonObject headers)
                {
                    foreach (var (name, headerNode) in headers)
                    {
                        if (headerNode is JsonObject header)
                        {
                            RequireDescription(
                                header,
                                $"{responsePointer}/headers/{Escape(name)}",
                                $"header '{name}'",
                                gaps);
                        }
                    }
                }

                CheckContent(response["content"], $"{responsePointer}/content", schemas, gaps, components);
            }
        }
    }

    private static void CheckContent(
        JsonNode? contentNode,
        string pointer,
        JsonObject schemas,
        List<string> gaps,
        SortedSet<string> components)
    {
        if (contentNode is not JsonObject content)
        {
            return;
        }

        foreach (var (mediaType, mediaNode) in content)
        {
            if (mediaNode?["schema"] is JsonObject schema)
            {
                CheckSchemaContent(schema, $"{pointer}/{Escape(mediaType)}/schema", schemas, gaps, components);
            }
        }
    }

    private static void CheckSchemaContent(
        JsonObject schema,
        string pointer,
        JsonObject schemas,
        List<string> gaps,
        SortedSet<string> components)
    {
        if (ReferenceName(schema) is { } referenced)
        {
            components.Add(referenced);
        }

        if (schema["properties"] is JsonObject properties)
        {
            foreach (var (name, propertyNode) in properties)
            {
                if (propertyNode is not JsonObject property)
                {
                    continue;
                }

                var propertyPointer = $"{pointer}/properties/{Escape(name)}";
                if (!IsDescribed(property))
                {
                    gaps.Add($"{propertyPointer}: property has no description");
                }
                else if (DescriptionOf(property) is { } text && IsFiller(text))
                {
                    gaps.Add($"{propertyPointer}: property description is filler");
                }
                else if (UsesIntegerEnum(property, schemas) && !ListsEnumValues(DescriptionOf(property)))
                {
                    gaps.Add($"{propertyPointer}: enum property description does not list its values");
                }

                CheckSchemaContent(property, propertyPointer, schemas, gaps, components);
            }
        }

        foreach (var keyword in new[] { "items", "additionalProperties", "not" })
        {
            if (schema[keyword] is JsonObject child)
            {
                CheckSchemaContent(child, $"{pointer}/{keyword}", schemas, gaps, components);
            }
        }

        foreach (var keyword in new[] { "oneOf", "anyOf", "allOf" })
        {
            if (schema[keyword] is not JsonArray branches)
            {
                continue;
            }

            for (var index = 0; index < branches.Count; index++)
            {
                if (branches[index] is JsonObject branch)
                {
                    CheckSchemaContent(branch, $"{pointer}/{keyword}/{index}", schemas, gaps, components);
                }
            }
        }
    }

    private static bool IsDescribed(JsonObject property)
        => DescriptionOf(property) is not null;

    private static string? DescriptionOf(JsonObject property)
    {
        if (property["description"]?.GetValue<string>() is { } own && !string.IsNullOrWhiteSpace(own))
        {
            return own;
        }

        foreach (var keyword in new[] { "oneOf", "anyOf" })
        {
            if (property[keyword] is not JsonArray branches ||
                !branches.Any(IsNullBranch))
            {
                continue;
            }

            var described = branches
                .OfType<JsonObject>()
                .Where(branch => !IsNullBranch(branch))
                .Select(branch => branch["description"]?.GetValue<string>())
                .ToList();
            if (described.Count > 0 && described.All(text => !string.IsNullOrWhiteSpace(text)))
            {
                return described[0];
            }
        }

        return null;
    }

    private static bool IsNullBranch(JsonNode? branch)
        => branch is JsonObject schema &&
           schema.Count == 1 &&
           schema["type"]?.GetValue<string>() == "null";

    private static string? ReferenceName(JsonObject schema)
        => schema["$ref"]?.GetValue<string>() is { } reference &&
           reference.StartsWith("#/components/schemas/", StringComparison.Ordinal)
            ? Unescape(reference["#/components/schemas/".Length..])
            : null;

    // The component used by a schema: its own reference, a nullable reference (oneOf null and $ref) or its items'.
    private static string? UseTarget(JsonObject schema)
    {
        if (ReferenceName(schema) is { } direct)
        {
            return direct;
        }

        foreach (var keyword in new[] { "oneOf", "anyOf" })
        {
            if (schema[keyword] is JsonArray branches && branches.Any(IsNullBranch))
            {
                var references = branches
                    .OfType<JsonObject>()
                    .Where(branch => !IsNullBranch(branch))
                    .Select(ReferenceName)
                    .ToList();
                if (references is [{ } nullable])
                {
                    return nullable;
                }
            }
        }

        return schema["items"] is JsonObject items ? UseTarget(items) : null;
    }

    private static bool UsesIntegerEnum(JsonObject schema, JsonObject schemas)
        => UseTarget(schema) is { } target &&
           schemas[target] is JsonObject component &&
           IsIntegerEnum(component);

    private static bool IsIntegerEnum(JsonObject schema)
        => schema["properties"] is null &&
           schema["enum"] is null &&
           schema["type"] switch
           {
               JsonValue value => value.GetValue<string>() == "integer",
               JsonArray types => types.Any(type => type?.GetValue<string>() == "integer"),
               _ => false
           };

    private static bool ListsEnumValues(string? description)
        => description is not null && EnumValueListing.IsMatch(description);

    private static void RequireDescription(JsonObject node, string pointer, string subject, List<string> gaps)
        => RequireText(node["description"], $"{pointer}/description", subject, gaps);

    private static void RequireText(JsonNode? value, string pointer, string subject, List<string> gaps)
    {
        var text = value?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            gaps.Add($"{pointer}: {subject} is missing");
        }
        else if (IsFiller(text))
        {
            gaps.Add($"{pointer}: {subject} is filler");
        }
    }

    private static bool IsFiller(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length < 10 ||
               FillerTexts.Contains(trimmed) ||
               FillerPrefixes.Any(prefix => trimmed.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static string Escape(string segment)
        => segment.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    private static string Unescape(string segment)
        => segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
}
