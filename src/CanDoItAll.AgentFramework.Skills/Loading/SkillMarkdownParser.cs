namespace CanDoItAll.AgentFramework.Skills;

internal static class SkillMarkdownParser
{
    public static SkillMarkdownMetadata Parse(string content)
    {
        using var reader = new StringReader(content);
        var firstLine = reader.ReadLine();
        if (!string.Equals(firstLine?.Trim(), "---", StringComparison.Ordinal))
        {
            return new SkillMarkdownMetadata(null, null, content);
        }

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.Equals(line.Trim(), "---", StringComparison.Ordinal))
            {
                break;
            }

            var separator = line.IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim().Trim('"');
            metadata[key] = value;
        }

        var instructionLines = new List<string>();
        while ((line = reader.ReadLine()) is not null)
        {
            instructionLines.Add(line);
        }

        var instructions = string.Join(Environment.NewLine, instructionLines).Trim();
        return new SkillMarkdownMetadata(
            metadata.GetValueOrDefault("name"),
            metadata.GetValueOrDefault("description"),
            instructions);
    }
}

internal sealed record SkillMarkdownMetadata(
    string? Name,
    string? Description,
    string Instructions);
