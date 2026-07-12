using System.Globalization;
using System.Text;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.AgentFramework.Memory.Context;

internal static class MemoryContextPackRenderer
{
    private const string DataPrefix = "MEMORY-DATA | ";

    public static string Render(
        AgentMemoryProviderBindingSetting binding,
        MemoryContextPack contextPack)
    {
        var builder = new StringBuilder();
        builder.Append("Memory provider '");
        builder.Append(binding.Alias.Value);
        builder.AppendLine("':");
        AppendUntrusted(builder, $"Provider instance id: {binding.ProviderInstanceId.Value}");
        if (!string.IsNullOrWhiteSpace(contextPack.Summary))
        {
            AppendUntrusted(builder, contextPack.Summary);
        }

        foreach (var section in contextPack.Sections.Where(section => !string.IsNullOrWhiteSpace(section.Text)))
        {
            builder.AppendLine();
            AppendUntrusted(builder, $"## {(string.IsNullOrWhiteSpace(section.Title) ? "Memory" : section.Title.Trim())}");
            if (section.Citations.Count > 0)
            {
                AppendUntrusted(builder, "Sources: " + string.Join("; ", section.Citations.Select(RenderCitation)));
            }

            AppendUntrusted(builder, section.Text);
        }

        if (contextPack.Warnings.Count > 0)
        {
            builder.AppendLine();
            AppendUntrusted(builder, "Warnings: " + string.Join("; ", contextPack.Warnings.Select(warning => warning.Message)));
        }

        builder.Append("Provider confidence: ");
        builder.Append(contextPack.ProviderConfidence.ToString(CultureInfo.InvariantCulture));
        return builder.ToString().Trim();
    }

    private static void AppendUntrusted(StringBuilder builder, string value)
    {
        foreach (var line in value.Trim().ReplaceLineEndings("\n").Split('\n'))
        {
            builder.Append(DataPrefix);
            builder.AppendLine(line);
        }
    }

    private static string RenderCitation(MemoryCitation citation)
    {
        return string.IsNullOrWhiteSpace(citation.Label)
            ? citation.SourceRef
            : $"{citation.Label} ({citation.SourceRef})";
    }
}
