using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CanDoItAll.Web.Api;

// The compiler writes XML documentation with platform line endings and the source line wrapping, and the XML comment
// generator keeps XML entities escaped. This final document transformer turns that text into stable Markdown: wrapped
// lines are joined, blank lines separate paragraphs, list items keep their own lines and entities are decoded, so the
// document is identical whichever platform built it.
internal static partial class OpenApiDescriptionText
{
    public static Task TransformDocumentAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        new DocumentWalker().Visit(document);
        return Task.CompletedTask;
    }

    internal static string? Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var blocks = new List<(bool IsListItem, StringBuilder Text)>();
        var separatedByBlankLine = new List<bool>();
        StringBuilder? current = null;
        var pendingBlankLine = false;
        foreach (var rawLine in text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                current = null;
                pendingBlankLine = blocks.Count > 0;
                continue;
            }

            var isListItem = ListItemPattern().IsMatch(line);
            if (current is null || isListItem || line.StartsWith("```", StringComparison.Ordinal))
            {
                current = new StringBuilder(line);
                blocks.Add((isListItem, current));
                separatedByBlankLine.Add(pendingBlankLine);
                pendingBlankLine = false;
                continue;
            }

            current.Append(' ').Append(line);
        }

        var result = new StringBuilder();
        for (var index = 0; index < blocks.Count; index++)
        {
            if (index > 0)
            {
                var consecutiveListItems = blocks[index].IsListItem &&
                    blocks[index - 1].IsListItem &&
                    !separatedByBlankLine[index];
                result.Append(consecutiveListItems ? "\n" : "\n\n");
            }

            result.Append(blocks[index].Text);
        }

        return WebUtility.HtmlDecode(result.ToString());
    }

    private static IEnumerable<T> Items<T>(IEnumerable<T>? source)
        => source ?? [];

    [GeneratedRegex(@"^(?:[-*+]|\d+\.)\s")]
    private static partial Regex ListItemPattern();

    private sealed class DocumentWalker
    {
        private readonly HashSet<object> visited = new(ReferenceEqualityComparer.Instance);

        public void Visit(OpenApiDocument document)
        {
            if (document.Info is { } info)
            {
                info.Description = Normalize(info.Description);
            }

            foreach (var tag in Items(document.Tags))
            {
                tag.Description = Normalize(tag.Description);
            }

            foreach (var pathItem in Items(document.Paths?.Values))
            {
                VisitPathItem(pathItem);
            }

            if (document.Components is not { } components)
            {
                return;
            }

            foreach (var schema in Items(components.Schemas?.Values))
            {
                VisitSchema(schema);
            }

            foreach (var parameter in Items(components.Parameters?.Values))
            {
                VisitParameter(parameter);
            }

            foreach (var requestBody in Items(components.RequestBodies?.Values))
            {
                VisitRequestBody(requestBody);
            }

            foreach (var response in Items(components.Responses?.Values))
            {
                VisitResponse(response);
            }

            foreach (var header in Items(components.Headers?.Values))
            {
                VisitHeader(header);
            }

            foreach (var securityScheme in Items(components.SecuritySchemes?.Values))
            {
                if (securityScheme is OpenApiSecurityScheme concrete)
                {
                    concrete.Description = Normalize(concrete.Description);
                }
            }
        }

        private void VisitPathItem(IOpenApiPathItem pathItem)
        {
            if (pathItem is not OpenApiPathItem concrete || !visited.Add(concrete))
            {
                return;
            }

            concrete.Summary = Normalize(concrete.Summary);
            concrete.Description = Normalize(concrete.Description);
            foreach (var parameter in Items(concrete.Parameters))
            {
                VisitParameter(parameter);
            }

            foreach (var operation in Items(concrete.Operations?.Values))
            {
                VisitOperation(operation);
            }
        }

        private void VisitOperation(OpenApiOperation operation)
        {
            if (!visited.Add(operation))
            {
                return;
            }

            operation.Summary = Normalize(operation.Summary);
            operation.Description = Normalize(operation.Description);
            foreach (var parameter in Items(operation.Parameters))
            {
                VisitParameter(parameter);
            }

            if (operation.RequestBody is { } requestBody)
            {
                VisitRequestBody(requestBody);
            }

            foreach (var response in Items(operation.Responses?.Values))
            {
                VisitResponse(response);
            }
        }

        private void VisitParameter(IOpenApiParameter parameter)
        {
            if (parameter is not OpenApiParameter concrete || !visited.Add(concrete))
            {
                return;
            }

            concrete.Description = Normalize(concrete.Description);
            if (concrete.Schema is { } schema)
            {
                VisitSchema(schema);
            }

            VisitContent(concrete.Content);
        }

        private void VisitRequestBody(IOpenApiRequestBody requestBody)
        {
            if (requestBody is not OpenApiRequestBody concrete || !visited.Add(concrete))
            {
                return;
            }

            concrete.Description = Normalize(concrete.Description);
            VisitContent(concrete.Content);
        }

        private void VisitResponse(IOpenApiResponse response)
        {
            if (response is not OpenApiResponse concrete || !visited.Add(concrete))
            {
                return;
            }

            concrete.Description = Normalize(concrete.Description);
            foreach (var header in Items(concrete.Headers?.Values))
            {
                VisitHeader(header);
            }

            VisitContent(concrete.Content);
        }

        private void VisitHeader(IOpenApiHeader header)
        {
            if (header is not OpenApiHeader concrete || !visited.Add(concrete))
            {
                return;
            }

            concrete.Description = Normalize(concrete.Description);
            if (concrete.Schema is { } schema)
            {
                VisitSchema(schema);
            }

            VisitContent(concrete.Content);
        }

        private void VisitContent(IDictionary<string, OpenApiMediaType>? content)
        {
            foreach (var mediaType in Items(content?.Values))
            {
                if (mediaType.Schema is { } schema)
                {
                    VisitSchema(schema);
                }
            }
        }

        private void VisitSchema(IOpenApiSchema schema)
        {
            if (!visited.Add(schema))
            {
                return;
            }

            if (schema is OpenApiSchemaReference reference)
            {
                reference.Reference.Description = Normalize(reference.Reference.Description);
                return;
            }

            if (schema is not OpenApiSchema concrete)
            {
                return;
            }

            concrete.Description = Normalize(concrete.Description);
            foreach (var child in Items(concrete.Properties?.Values))
            {
                VisitSchema(child);
            }

            foreach (var child in Items(concrete.PatternProperties?.Values))
            {
                VisitSchema(child);
            }

            foreach (var child in Items(concrete.Definitions?.Values))
            {
                VisitSchema(child);
            }

            foreach (var child in Items(concrete.AllOf).Concat(Items(concrete.OneOf)).Concat(Items(concrete.AnyOf)))
            {
                VisitSchema(child);
            }

            foreach (var child in new[] { concrete.Items, concrete.AdditionalProperties, concrete.Not })
            {
                if (child is not null)
                {
                    VisitSchema(child);
                }
            }
        }
    }
}
