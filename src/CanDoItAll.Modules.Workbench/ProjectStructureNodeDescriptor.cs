using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal sealed record ProjectStructureNodeFact(string Label, string Value);

internal static class ProjectStructureNodeDescriptor
{
    public static IReadOnlyList<ProjectStructureNodeFact> BuildFacts(ProjectStructureNode node)
    {
        var facts = new List<ProjectStructureNodeFact>();
        var metadata = ProjectObjectMetadataSerializer.Parse(node.MetadataJson);

        switch (node.ObjectType)
        {
            case ProjectObjectType.Meeting:
                facts.Add(Fact("Mode", node.ObjectSubtype.Equals("onsite", StringComparison.OrdinalIgnoreCase) ? "Onsite" : "Online"));
                AddScheduledFacts(facts, node);
                AddIfValue(facts, "Channel", metadata.Meeting?.Channel switch
                {
                    ProjectMeetingChannel.Unknown => string.Empty,
                    ProjectMeetingChannel.MsTeams => "MS Teams",
                    _ => metadata.Meeting?.Channel.ToString()
                });
                AddIfValue(facts, "Repeat", HumanizeEnum(metadata.Meeting?.RepeatCadence));
                AddIfValue(facts, "Join", metadata.Meeting?.MeetingUrl);
                AddIfValue(facts, "Address", metadata.Meeting?.Address);
                break;
            case ProjectObjectType.Recording:
                AddIfValue(facts, "Source", metadata.Recording?.RecordingSource);
                AddIfValue(facts, "Storage", metadata.Recording?.StorageReference);
                AddIfValue(facts, "Length", metadata.Recording?.DurationMinutes > 0 ? $"{metadata.Recording.DurationMinutes} min" : string.Empty);
                break;
            case ProjectObjectType.Transcript:
                AddIfValue(facts, "Action", HumanizeEnum(metadata.Transcript?.LastActionKind));
                AddIfValue(facts, "Provider", metadata.Transcript?.LastProviderName);
                AddIfValue(facts, "Summary", Shorten(metadata.Transcript?.SummaryText, 72));
                AddIfValue(facts, "My tasks", Shorten(metadata.Transcript?.MyTasksText, 72));
                AddIfValue(facts, "Others to me", Shorten(metadata.Transcript?.OthersDeliveriesText, 72));
                break;
            case ProjectObjectType.Participant:
                AddIfValue(facts, "Role", metadata.Participant?.Role);
                AddIfValue(facts, "Org", metadata.Participant?.Organization);
                AddIfValue(facts, "Email", metadata.Participant?.Email);
                AddIfValue(facts, "Phone", metadata.Participant?.Phone);
                break;
            case ProjectObjectType.WorkItem:
                AddIfValue(facts, "Kind", HumanizeEnum(metadata.WorkItem?.WorkItemKind));
                AddIfValue(facts, "Due", metadata.WorkItem?.DueUtc?.ToLocalTime().ToString("g"));
                AddIfValue(facts, "Channel", HumanizeEnum(metadata.WorkItem?.DeliveryChannel));
                AddIfValue(facts, "Send", HumanizeEnum(metadata.WorkItem?.SendKind));
                AddIfValue(facts, "Amount", metadata.WorkItem?.Amount.HasValue == true
                    ? $"{metadata.WorkItem.Amount:0.##} {metadata.WorkItem.CurrencyCode}".Trim()
                    : string.Empty);
                break;
            case ProjectObjectType.Repository:
                AddIfValue(facts, "Mode", HumanizeToken(node.ObjectSubtype));
                AddIfValue(facts, "Path", metadata.Repository?.LocalPath);
                AddIfValue(facts, "URL", metadata.Repository?.RepositoryUrl);
                AddIfValue(facts, "Branch", metadata.Repository?.DefaultBranch);
                break;
            case ProjectObjectType.File:
                AddIfValue(facts, "Type", HumanizeToken(node.ObjectSubtype));
                if (string.Equals(node.ObjectSubtype, "mermaid", StringComparison.OrdinalIgnoreCase))
                {
                    AddIfValue(facts, "Diagram", HumanizeEnum<MermaidDiagramKind>(ProjectObjectMetadataSerializer.DetectMermaidDiagramKind(node.Notes)));
                }

                AddIfValue(facts, "Source", metadata.File?.SourceHint);
                break;
            case ProjectObjectType.Script:
                AddIfValue(facts, "Command", metadata.Script?.Command);
                AddIfValue(facts, "Path", metadata.Script?.ScriptPath);
                AddIfValue(facts, "Work dir", metadata.Script?.WorkingDirectory);
                break;
            case ProjectObjectType.Environment:
                AddIfValue(facts, "Kind", HumanizeEnum(metadata.Environment?.EnvironmentKind));
                AddIfValue(facts, "Provider", HumanizeEnum(metadata.Environment?.PythonProvider));
                AddIfValue(facts, "Name", metadata.Environment?.EnvironmentName);
                AddIfValue(facts, "URL", metadata.Environment?.LocalhostUrl);
                break;
            case ProjectObjectType.Infrastructure:
                AddIfValue(facts, "Kind", HumanizeEnum(metadata.Infrastructure?.InfrastructureKind));
                AddIfValue(facts, "Host", metadata.Infrastructure?.Host);
                AddIfValue(facts, "Provider", metadata.Infrastructure?.ProviderName);
                AddIfValue(facts, "Domain", metadata.Infrastructure?.DomainName);
                AddIfValue(facts, "DB", metadata.Infrastructure?.DatabaseType);
                AddIfValue(facts, "AI", HumanizeEnum(metadata.Infrastructure?.AiReferenceKind));
                break;
            case ProjectObjectType.Link:
                AddIfValue(facts, "URL", metadata.Link?.Url);
                AddIfValue(facts, "Channel", HumanizeEnum(metadata.Link?.Channel));
                break;
        }

        return facts;
    }

    public static string BuildLeadText(ProjectStructureNode node)
    {
        if (node.ObjectType == ProjectObjectType.Note)
        {
            return string.IsNullOrWhiteSpace(node.Notes) ? node.Title : node.Notes;
        }

        if (node.ObjectType is ProjectObjectType.ImageAsset or ProjectObjectType.VideoAsset &&
            !string.IsNullOrWhiteSpace(node.MediaOriginalFileName))
        {
            return node.MediaOriginalFileName;
        }

        var facts = BuildFacts(node)
            .Take(2)
            .Select(fact => $"{fact.Label}: {fact.Value}")
            .ToList();
        if (facts.Count > 0)
        {
            return string.Join(" | ", facts);
        }

        if (!string.IsNullOrWhiteSpace(node.Notes))
        {
            return node.Notes;
        }

        return string.IsNullOrWhiteSpace(node.Subtitle)
            ? $"Status: {node.Status}"
            : node.Subtitle;
    }

    private static void AddScheduledFacts(List<ProjectStructureNodeFact> facts, ProjectStructureNode node)
    {
        AddIfValue(facts, "Start", node.StartUtc?.ToLocalTime().ToString("g"));
        AddIfValue(facts, "End", node.EndUtc?.ToLocalTime().ToString("g"));
    }

    private static ProjectStructureNodeFact Fact(string label, string value)
        => new(label, value);

    private static void AddIfValue(List<ProjectStructureNodeFact> facts, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            facts.Add(Fact(label, value.Trim()));
        }
    }

    private static string HumanizeEnum<TEnum>(TEnum? value)
        where TEnum : struct, Enum
        => value.HasValue ? HumanizeToken(value.Value.ToString()) : string.Empty;

    private static string HumanizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Replace("dotnet", ".NET", StringComparison.OrdinalIgnoreCase)
            .Replace("ai", "AI", StringComparison.OrdinalIgnoreCase)
            .Replace('-', ' ')
            .Replace('_', ' ');
    }

    private static string Shorten(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength].TrimEnd() + "...";
    }
}
