using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectStructureNodeFact(string Label, string Value);

internal sealed record ProjectStructureCompactPathPresentation(
    string Label,
    string DisplayText,
    string FullPath,
    string PromotedText);

internal sealed record ProjectStructureNodeLeadPresentation(
    string LeadText,
    ProjectStructureCompactPathPresentation? CompactPath);

internal static class ProjectStructureNodeDescriptor
{
    private static readonly HashSet<string> KnownFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bat",
        ".cmd",
        ".config",
        ".cs",
        ".csproj",
        ".csv",
        ".docx",
        ".gif",
        ".jpeg",
        ".jpg",
        ".json",
        ".log",
        ".md",
        ".mp3",
        ".mp4",
        ".pdf",
        ".png",
        ".ps1",
        ".py",
        ".razor",
        ".sh",
        ".sln",
        ".slnx",
        ".sql",
        ".svg",
        ".ts",
        ".tsx",
        ".txt",
        ".xlsx",
        ".xml",
        ".yaml",
        ".yml",
        ".zip"
    };

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
                AddIfValue(facts, "Parties", metadata.Meeting?.RelatedPartySummary);
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
                AddIfValue(facts, "Directory", metadata.Participant?.LinkedPartyDisplayName);
                break;
            case ProjectObjectType.WorkItem:
                AddIfValue(facts, "Kind", HumanizeEnum(metadata.WorkItem?.WorkItemKind));
                AddIfValue(facts, "Due", metadata.WorkItem?.DueUtc?.ToLocalTime().ToString("g"));
                AddIfValue(facts, "Channel", HumanizeEnum(metadata.WorkItem?.DeliveryChannel));
                AddIfValue(facts, "Send", HumanizeEnum(metadata.WorkItem?.SendKind));
                AddIfValue(facts, "Amount", metadata.WorkItem?.Amount.HasValue == true
                    ? $"{metadata.WorkItem.Amount:0.##} {metadata.WorkItem.CurrencyCode}".Trim()
                    : string.Empty);
                AddIfValue(facts, "Party", metadata.WorkItem?.AssigneePartyDisplayName);
                break;
            case ProjectObjectType.Repository:
                AddIfValue(facts, "Mode", HumanizeToken(node.ObjectSubtype));
                AddIfValue(facts, "Path", metadata.Repository?.LocalPath);
                AddIfValue(facts, "URL", metadata.Repository?.RepositoryUrl);
                AddIfValue(facts, "Host", ProjectStructureExternalLinkClassifier.DescribeGitHost(metadata.Repository?.RepositoryUrl));
                AddIfValue(facts, "Branch", metadata.Repository?.DefaultBranch);
                break;
            case ProjectObjectType.File:
                AddIfValue(facts, "Type", HumanizeToken(node.ObjectSubtype));
                if (string.Equals(node.ObjectSubtype, "mermaid", StringComparison.OrdinalIgnoreCase))
                {
                    AddIfValue(facts, "Diagram", HumanizeEnum(metadata.File?.MermaidDiagramKind));
                }

                AddIfValue(facts, "Source", metadata.File?.SourceHint);
                AddIfValue(facts, "Path", metadata.File?.ExternalPath);
                if (StorageJson.TryParseReference(node.StorageObjectReferenceJson, out var storageReference) &&
                    storageReference is not null)
                {
                    AddIfValue(facts, "Storage", StoragePresentation.DescribeProvider(storageReference.ProviderKind));
                    AddIfValue(facts, "Locator", StoragePresentation.DescribeLocator(storageReference.LocatorKind));
                    AddIfValue(facts, "Path", storageReference.Locator);
                }
                break;
            case ProjectObjectType.Script:
                AddIfValue(facts, "Command", metadata.Script?.Command);
                AddIfValue(facts, "Args", metadata.Script?.Arguments);
                AddIfValue(facts, "Path", metadata.Script?.ScriptPath);
                AddIfValue(facts, "Work dir", metadata.Script?.WorkingDirectory);
                break;
            case ProjectObjectType.Environment:
                AddIfValue(facts, "Kind", HumanizeEnum(metadata.Environment?.EnvironmentKind));
                AddIfValue(facts, "Provider", HumanizeEnum(metadata.Environment?.PythonProvider));
                AddIfValue(facts, "Name", metadata.Environment?.EnvironmentName);
                AddIfValue(facts, "Project", metadata.Environment?.ProjectPath);
                AddIfValue(facts, "Entry point", metadata.Environment?.EntryPoint);
                AddIfValue(facts, "Args", metadata.Environment?.Arguments);
                AddIfValue(facts, "Profile", metadata.Environment?.LaunchProfileName);
                AddIfValue(facts, "URL", metadata.Environment?.LocalhostUrl);
                break;
            case ProjectObjectType.Infrastructure:
                AddIfValue(facts, "Kind", HumanizeEnum(metadata.Infrastructure?.InfrastructureKind));
                AddIfValue(facts, "Host", metadata.Infrastructure?.Host);
                AddIfValue(facts, "Provider", metadata.Infrastructure?.ProviderName);
                AddIfValue(facts, "Domain", metadata.Infrastructure?.DomainName);
                AddIfValue(facts, "DB", metadata.Infrastructure?.DatabaseType);
                AddIfValue(facts, "Purpose", ResolveStoragePurposeLabel(metadata.Infrastructure?.StoragePurpose));
                AddIfValue(facts, "Path", metadata.Infrastructure?.StoragePathPrefix);
                AddIfValue(facts, "Folder", metadata.Infrastructure?.FolderPath);
                AddIfValue(facts, "Command", metadata.Infrastructure?.RuntimeCommand);
                AddIfValue(facts, "Args", metadata.Infrastructure?.RuntimeArguments);
                AddIfValue(facts, "Work dir", metadata.Infrastructure?.WorkingDirectory);
                AddIfValue(facts, "AI", HumanizeEnum(metadata.Infrastructure?.AiReferenceKind));
                break;
            case ProjectObjectType.SecretReference:
                AddIfValue(facts, "Secret", metadata.SecretReference?.SecretNameSnapshot);
                AddIfValue(facts, "Purpose", metadata.SecretReference?.Purpose);
                AddIfValue(facts, "Reference", metadata.SecretReference?.ExternalReference);
                break;
            case ProjectObjectType.Link:
                AddIfValue(facts, "Host", ProjectStructureExternalLinkClassifier.DescribeGitHost(metadata.Link?.Url));
                AddIfValue(facts, "URL", metadata.Link?.Url);
                AddIfValue(facts, "Channel", HumanizeEnum(metadata.Link?.Channel));
                break;
        }

        return FilterFacts(node, facts);
    }

    public static string BuildLeadText(ProjectStructureNode node)
        => BuildLeadPresentation(node).LeadText;

    public static ProjectStructureNodeLeadPresentation BuildLeadPresentation(ProjectStructureNode node)
    {
        if (node.ObjectType == ProjectObjectType.Note)
        {
            return new(string.IsNullOrWhiteSpace(node.Notes) ? node.Title : node.Notes, null);
        }

        if (node.ObjectType is ProjectObjectType.ImageAsset or ProjectObjectType.VideoAsset &&
            !string.IsNullOrWhiteSpace(node.MediaOriginalFileName))
        {
            return new(node.MediaOriginalFileName, null);
        }

        var facts = BuildFacts(node);
        var compactPath = TryBuildCompactPath(facts);
        var leadText = BuildFactLeadText(facts, compactPath);
        if (!string.IsNullOrWhiteSpace(leadText))
        {
            return new(leadText, compactPath);
        }

        if (!string.IsNullOrWhiteSpace(node.Notes))
        {
            return new(node.Notes, compactPath);
        }

        return new(
            string.IsNullOrWhiteSpace(node.Subtitle)
                ? $"Status: {node.Status}"
                : node.Subtitle,
            compactPath);
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

    private static IReadOnlyList<ProjectStructureNodeFact> FilterFacts(ProjectStructureNode node, IReadOnlyList<ProjectStructureNodeFact> facts)
    {
        if (facts.Count == 0)
        {
            return facts;
        }

        var surfacedLabels = BuildSurfacedLabels(node);
        return facts
            .Where(fact => !ShouldSuppressFact(fact, surfacedLabels))
            .ToList();
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

    private static string ResolveStoragePurposeLabel(string? storagePurpose)
    {
        return Enum.TryParse<StorageUsagePurpose>(storagePurpose, true, out var parsedPurpose)
            ? StoragePresentation.DescribeUsagePurpose(parsedPurpose)
            : string.Empty;
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

    private static HashSet<string> BuildSurfacedLabels(ProjectStructureNode node)
    {
        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ProjectStructureCanvasCatalog.ResolveNodeLabel(node),
            node.VisualProfile.AccentBadge
        };

        foreach (var badge in node.Badges)
        {
            labels.Add(badge);
        }

        return labels;
    }

    private static bool ShouldSuppressFact(ProjectStructureNodeFact fact, IReadOnlySet<string> surfacedLabels)
    {
        if (fact.Label is not ("Type" or "Kind" or "Mode"))
        {
            return false;
        }

        var normalizedValue = NormalizeFactValue(fact.Value);
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            return false;
        }

        foreach (var surfacedLabel in surfacedLabels)
        {
            var normalizedLabel = NormalizeFactValue(surfacedLabel);
            if (string.IsNullOrWhiteSpace(normalizedLabel))
            {
                continue;
            }

            if (normalizedLabel.Contains(normalizedValue, StringComparison.OrdinalIgnoreCase) ||
                normalizedValue.Contains(normalizedLabel, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeFactValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Concat(value.Where(char.IsLetterOrDigit));
    }

    private static string BuildFactLeadText(
        IReadOnlyList<ProjectStructureNodeFact> facts,
        ProjectStructureCompactPathPresentation? compactPath)
    {
        var leadFacts = facts
            .Take(2)
            .Where(fact => compactPath is null || !string.Equals(fact.Value, compactPath.FullPath, StringComparison.Ordinal))
            .Select(fact => $"{fact.Label}: {fact.Value}")
            .ToList();

        return leadFacts.Count > 0
            ? string.Join(" | ", leadFacts)
            : string.Empty;
    }

    private static ProjectStructureCompactPathPresentation? TryBuildCompactPath(IReadOnlyList<ProjectStructureNodeFact> facts)
    {
        foreach (var fact in facts)
        {
            if (!LooksLikePathFact(fact))
            {
                continue;
            }

            var fullPath = fact.Value.Trim();
            var promotedText = TryResolveFileName(fullPath);
            return new(
                fact.Label,
                BuildCompactPathText(fullPath, !string.IsNullOrWhiteSpace(promotedText)),
                fullPath,
                promotedText);
        }

        return null;
    }

    private static bool LooksLikePathFact(ProjectStructureNodeFact fact)
    {
        if (!string.IsNullOrWhiteSpace(fact.Value) &&
            fact.Value.Contains("://", StringComparison.Ordinal))
        {
            return false;
        }

        return fact.Label is "Path" or "Project" or "Work dir" or "Source" or "Folder" &&
            LooksLikePathValue(fact.Value);
    }

    private static bool LooksLikePathValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        return candidate.Contains('\\') ||
            candidate.Contains('/');
    }

    private static string BuildCompactPathText(string fullPath, bool omitLeaf)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return string.Empty;
        }

        var separator = fullPath.Contains('\\', StringComparison.Ordinal) ? '\\' : '/';
        var segments = fullPath
            .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (omitLeaf && segments.Count > 0)
        {
            segments.RemoveAt(segments.Count - 1);
        }

        if (segments.Count == 0 || fullPath.Length <= 36)
        {
            return fullPath;
        }

        var root = TryResolvePathRoot(fullPath, separator);
        if (segments.Count == 1)
        {
            return $"{root}{segments[0]}";
        }

        return $"{root}{segments[0]}{separator}...{separator}{segments[^1]}";
    }

    private static string TryResolveFileName(string fullPath)
    {
        var leaf = fullPath
            .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault()?
            .Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(leaf) || leaf[0] == '.')
        {
            return string.Empty;
        }

        var extensionIndex = leaf.LastIndexOf('.');
        if (extensionIndex <= 0 || extensionIndex == leaf.Length - 1)
        {
            return string.Empty;
        }

        var extension = leaf[extensionIndex..];
        return KnownFileExtensions.Contains(extension)
            ? leaf
            : string.Empty;
    }

    private static string TryResolvePathRoot(string fullPath, char separator)
    {
        if (fullPath.Length >= 3 &&
            char.IsLetter(fullPath[0]) &&
            fullPath[1] == ':' &&
            (fullPath[2] == '\\' || fullPath[2] == '/'))
        {
            return $"{fullPath[..2]}{separator}";
        }

        if (fullPath.StartsWith(@"\\", StringComparison.Ordinal))
        {
            var segments = fullPath
                .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries)
                .Take(2)
                .ToList();
            if (segments.Count == 2)
            {
                return $"{separator}{separator}{segments[0]}{separator}{segments[1]}{separator}";
            }
        }

        return fullPath.Length > 0 && fullPath[0] == separator
            ? separator.ToString()
            : string.Empty;
    }
}
