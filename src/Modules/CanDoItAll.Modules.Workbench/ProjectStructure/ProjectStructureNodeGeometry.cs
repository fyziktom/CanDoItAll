using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal readonly record struct ProjectStructureNodeSize(double Width, double Height);

internal readonly record struct ProjectStructureNodeBounds(double Left, double Top, double Right, double Bottom)
{
    public static ProjectStructureNodeBounds FromCenter(double x, double y, ProjectStructureNodeSize size)
        => new(
            x - (size.Width / 2d),
            y - (size.Height / 2d),
            x + (size.Width / 2d),
            y + (size.Height / 2d));

    public ProjectStructureNodeBounds Inflate(double horizontal, double vertical)
        => new(Left - horizontal, Top - vertical, Right + horizontal, Bottom + vertical);

    public bool Intersects(ProjectStructureNodeBounds other)
        => Left < other.Right - ProjectStructureNodeGeometry.PositionEpsilon &&
           Right > other.Left + ProjectStructureNodeGeometry.PositionEpsilon &&
           Top < other.Bottom - ProjectStructureNodeGeometry.PositionEpsilon &&
           Bottom > other.Top + ProjectStructureNodeGeometry.PositionEpsilon;
}

internal static class ProjectStructureNodeGeometry
{
    internal const double PositionEpsilon = 0.5d;

    public static ProjectStructureNodeSize Estimate(ProjectObjectRecord node)
        => Estimate(node.ObjectType, node.Title, node.Subtitle, node.Notes);

    public static ProjectStructureNodeSize Estimate(ProjectStructureNode node)
        => Estimate(node.ObjectType, node.Title, node.Subtitle, node.Notes);

    public static ProjectStructureNodeSize Estimate(
        ProjectObjectType? objectType,
        string? title,
        string? subtitle,
        string? notes)
    {
        if (objectType == ProjectObjectType.Note && string.IsNullOrWhiteSpace(subtitle))
        {
            var text = string.IsNullOrWhiteSpace(notes) ? title : notes;
            return EstimateInlineNoteSize(text);
        }

        return objectType switch
        {
            ProjectObjectType.ProjectRoot => new ProjectStructureNodeSize(288d, 210d),
            ProjectObjectType.Phase or
                ProjectObjectType.PromptSession or
                ProjectObjectType.PromptFlow or
                ProjectObjectType.ProjectBlock or
                ProjectObjectType.ProcessDefinition => new ProjectStructureNodeSize(272d, 196d),
            ProjectObjectType.ProcessRun or
                ProjectObjectType.ValidationRun or
                ProjectObjectType.TestPlan or
                ProjectObjectType.Decision or
                ProjectObjectType.SecretReference => new ProjectStructureNodeSize(248d, 178d),
            _ => new ProjectStructureNodeSize(256d, 190d)
        };
    }

    private static ProjectStructureNodeSize EstimateInlineNoteSize(string? text)
    {
        var noteText = string.IsNullOrWhiteSpace(text) ? "Write note" : text.Trim();
        var longestTokenLength = noteText
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Length)
            .DefaultIfEmpty(0)
            .Max();
        var widthBias = Math.Clamp((noteText.Length - 18) * 2.25d, 0d, 220d);
        var longWordBias = Math.Max(0d, longestTokenLength - 12d) * 4.5d;
        var width = Math.Clamp(Math.Ceiling(164d + widthBias + longWordBias), 148d, 420d);
        var lines = EstimateWrappedLineCount(noteText, Math.Max(1, (int)Math.Floor((width - 40d) / 7.2d)));
        var height = Math.Clamp(Math.Ceiling(30d + (lines * 20d) + 26d), 76d, 304d);
        return new ProjectStructureNodeSize(width, height);
    }

    private static int EstimateWrappedLineCount(string text, int charactersPerLine)
    {
        var paragraphs = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var lines = 0;
        foreach (var paragraph in paragraphs)
        {
            lines += Math.Max(1, (int)Math.Ceiling(paragraph.Length / (double)charactersPerLine));
        }

        return Math.Clamp(lines, 1, 12);
    }
}
