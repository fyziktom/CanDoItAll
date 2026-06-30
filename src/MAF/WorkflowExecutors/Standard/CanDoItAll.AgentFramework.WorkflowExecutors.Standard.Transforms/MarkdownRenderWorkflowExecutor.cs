using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Transforms;

public sealed partial class MarkdownRenderWorkflowExecutor(IWorkspaceFileService files) : IWorkflowExecutor
{
    public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.MarkdownRender;

    public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = WorkflowExecutorJson.Deserialize<WorkflowMarkdownRenderExecutorSettings>(context.SettingsJson);
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(input.PayloadJson) ? "{}" : input.PayloadJson);
        var template = ResolveTemplate(files, settings);
        var values = ResolveBindings(document.RootElement, settings);
        var markdown = ReplacePlaceholders(template, values, settings.MissingPlaceholderBehavior);
        if (!string.IsNullOrWhiteSpace(settings.OutputPath))
        {
            var writeResult = settings.Append
                ? files.AppendTextFile(settings.OutputPath, markdown)
                : files.WriteTextFile(settings.OutputPath, markdown, settings.Overwrite);
            EnsureSucceeded(writeResult);
        }

        return ValueTask.FromResult(WorkflowExecutorJson.Result(context, new
        {
            markdown,
            outputPath = settings.OutputPath,
            characterCount = markdown.Length
        }));
    }

    private static string ResolveTemplate(
        IWorkspaceFileService files,
        WorkflowMarkdownRenderExecutorSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.TemplatePath))
        {
            var result = EnsureSucceeded(files.ReadTextFile(settings.TemplatePath, maxCharacters: 200000));
            return result.Content;
        }

        if (string.IsNullOrWhiteSpace(settings.Template))
        {
            throw new InvalidOperationException("Markdown render requires either 'template' or 'templatePath'.");
        }

        return settings.Template;
    }

    private static Dictionary<string, string> ResolveBindings(
        JsonElement root,
        WorkflowMarkdownRenderExecutorSettings settings)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var binding in settings.Bindings)
        {
            values[binding.Key] = ResolveJsonValue(root, binding.Value);
        }

        foreach (var table in settings.Tables)
        {
            var key = NormalizePlaceholderKey(table.Placeholder);
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException("Markdown table binding placeholder is required.");
            }

            values[key] = RenderTable(root, table);
        }

        return values;
    }

    private static string RenderTable(JsonElement root, WorkflowMarkdownTableBinding table)
    {
        var array = ResolveJsonElement(root, table.JsonPath);
        if (array.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Markdown table binding '{table.Placeholder}' did not resolve to a JSON array.");
        }

        var rows = array.EnumerateArray().ToArray();
        var columns = table.Columns.Count > 0
            ? table.Columns
            : rows
                .Where(row => row.ValueKind == JsonValueKind.Object)
                .SelectMany(row => row.EnumerateObject().Select(property => property.Name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        if (columns.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append("| ");
        builder.Append(string.Join(" | ", columns.Select(EscapeMarkdownCell)));
        builder.AppendLine(" |");
        builder.Append("| ");
        builder.Append(string.Join(" | ", columns.Select(_ => "---")));
        builder.AppendLine(" |");

        foreach (var row in rows)
        {
            builder.Append("| ");
            builder.Append(string.Join(" | ", columns.Select(column => EscapeMarkdownCell(ReadColumn(row, column)))));
            builder.AppendLine(" |");
        }

        return builder.ToString().TrimEnd();
    }

    private static string ReplacePlaceholders(
        string template,
        IReadOnlyDictionary<string, string> values,
        WorkflowMarkdownMissingPlaceholderBehavior missingPlaceholderBehavior)
    {
        return PlaceholderRegex().Replace(template, match =>
        {
            var key = NormalizePlaceholderKey(match.Groups["key"].Value);
            if (values.TryGetValue(key, out var value))
            {
                return value;
            }

            if (missingPlaceholderBehavior == WorkflowMarkdownMissingPlaceholderBehavior.Empty)
            {
                return string.Empty;
            }

            throw new InvalidOperationException($"Markdown render placeholder '{key}' does not have a binding.");
        });
    }

    private static string ResolveJsonValue(JsonElement root, string jsonPath)
    {
        var value = ResolveJsonElement(root, jsonPath);
        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : value.GetRawText();
    }

    private static JsonElement ResolveJsonElement(JsonElement root, string jsonPath)
    {
        if (!WorkflowRoutingValidation.TryParseJsonPath(jsonPath, out var path, out var pathError))
        {
            throw new InvalidOperationException($"Markdown render JSON path '{jsonPath}' is invalid: {pathError}.");
        }

        var value = root;
        foreach (var segment in path)
        {
            if (segment.PropertyName is not null)
            {
                if (value.ValueKind != JsonValueKind.Object ||
                    !value.TryGetProperty(segment.PropertyName, out value))
                {
                    throw new InvalidOperationException($"Markdown render JSON path '{jsonPath}' was not found.");
                }

                continue;
            }

            if (segment.Index is not { } targetIndex ||
                value.ValueKind != JsonValueKind.Array ||
                targetIndex < 0 ||
                targetIndex >= value.GetArrayLength())
            {
                throw new InvalidOperationException($"Markdown render JSON path '{jsonPath}' was not found.");
            }

            value = value.EnumerateArray().ElementAt(targetIndex);
        }

        return value;
    }

    private static string ReadColumn(JsonElement row, string column)
    {
        if (row.ValueKind != JsonValueKind.Object ||
            !row.TryGetProperty(column, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : value.GetRawText();
    }

    private static T EnsureSucceeded<T>(T result)
    {
        var succeededProperty = typeof(T).GetProperty("Succeeded");
        var messageProperty = typeof(T).GetProperty("Message");
        if (succeededProperty?.GetValue(result) is false)
        {
            var message = messageProperty?.GetValue(result)?.ToString() ?? "Markdown render workspace operation failed.";
            throw new InvalidOperationException(message);
        }

        return result;
    }

    private static string EscapeMarkdownCell(string value)
        => (value ?? string.Empty)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Trim();

    private static string NormalizePlaceholderKey(string value)
        => value.Trim().Trim('{', '}').Trim();

    [GeneratedRegex(@"\{\{\s*(?<key>[A-Za-z0-9_.-]+)\s*\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();
}
