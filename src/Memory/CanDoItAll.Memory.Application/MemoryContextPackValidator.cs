using System.Text;
using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

internal static class MemoryContextPackValidator
{
    public static string? GetFailure(
        MemoryContextPack contextPack,
        MemoryBudget budget,
        MemoryProviderLimits limits)
    {
        ArgumentNullException.ThrowIfNull(contextPack);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(limits);
        if (contextPack.ContextPackId.Value == Guid.Empty ||
            string.IsNullOrWhiteSpace(contextPack.Summary) ||
            contextPack.Sections is null ||
            contextPack.Warnings is null)
        {
            return "Memory provider returned a malformed context pack.";
        }

        var maxSections = Math.Min(budget.MaxContextItems, limits.MaxContextSections);
        if (contextPack.Sections.Count > maxSections)
        {
            return $"Memory provider context pack exceeds the section limit of {maxSections}.";
        }

        if (!IsConfidence(contextPack.ProviderConfidence) ||
            contextPack.Sections.Any(IsMalformedSection) ||
            contextPack.Warnings.Any(warning =>
                warning is null || string.IsNullOrWhiteSpace(warning.Message)))
        {
            return "Memory provider returned malformed context content or confidence values.";
        }

        var citationCount = contextPack.Sections.Sum(section => section.Citations.Count);
        if (citationCount > limits.MaxSourceItems)
        {
            return $"Memory provider context pack exceeds the citation limit of {limits.MaxSourceItems}.";
        }

        if (CountUtf8Bytes(contextPack) > budget.MaxSourceBytes)
        {
            return $"Memory provider context pack exceeds the UTF-8 byte budget of {budget.MaxSourceBytes}.";
        }

        return null;
    }

    private static bool IsMalformedSection(MemoryContextSection? section)
    {
        return section is null ||
               string.IsNullOrWhiteSpace(section.Title) ||
               string.IsNullOrWhiteSpace(section.Text) ||
               !IsConfidence(section.Confidence) ||
               section.Citations is null ||
               section.Citations.Any(citation =>
                   citation is null || string.IsNullOrWhiteSpace(citation.SourceRef));
    }

    private static bool IsConfidence(decimal value) => value is >= 0m and <= 1m;

    private static long CountUtf8Bytes(MemoryContextPack contextPack)
    {
        long total = Encoding.UTF8.GetByteCount(contextPack.Summary);
        foreach (var section in contextPack.Sections)
        {
            total += Encoding.UTF8.GetByteCount(section.Title);
            total += Encoding.UTF8.GetByteCount(section.Text);
            foreach (var citation in section.Citations)
            {
                total += Encoding.UTF8.GetByteCount(citation.SourceRef);
                total += Encoding.UTF8.GetByteCount(citation.Label ?? string.Empty);
            }
        }

        foreach (var warning in contextPack.Warnings)
        {
            total += Encoding.UTF8.GetByteCount(warning.Message);
        }

        return total;
    }
}
