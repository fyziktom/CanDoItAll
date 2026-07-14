using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal sealed record ProjectStructureWebLink(
    Uri Uri,
    string SourceLabel,
    bool CanEmbed,
    string EmbedUnavailableReason);

internal static class ProjectStructureWebLinkResolver
{
    public static bool TryResolve(ProjectStructureNode node, out ProjectStructureWebLink? link)
    {
        ArgumentNullException.ThrowIfNull(node);
        var metadata = ProjectObjectMetadataSerializer.Parse(node.MetadataJson);
        foreach ((string sourceLabel, string value) in EnumerateCandidates(node, metadata))
        {
            if (!TryCreateSafeUri(value, out Uri? uri) || uri is null)
            {
                continue;
            }

            bool canEmbed = !IsKnownFrameBlockedHost(uri.Host);
            string hostLabel = ProjectStructureExternalLinkClassifier.DescribeGitHost(uri.ToString());
            link = new ProjectStructureWebLink(
                uri,
                sourceLabel,
                canEmbed,
                canEmbed
                    ? string.Empty
                    : $"{hostLabel} does not allow repository pages to be embedded. Open the link in your browser instead.");
            return true;
        }

        link = null;
        return false;
    }

    internal static bool TryCreateSafeUri(string? value, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? candidate) ||
            string.IsNullOrWhiteSpace(candidate.Host) ||
            !string.IsNullOrEmpty(candidate.UserInfo))
        {
            return false;
        }

        bool isAllowed = candidate.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                         candidate.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && candidate.IsLoopback;
        if (!isAllowed)
        {
            return false;
        }

        uri = candidate;
        return true;
    }

    private static IEnumerable<(string SourceLabel, string Value)> EnumerateCandidates(
        ProjectStructureNode node,
        ProjectObjectMetadataEnvelope metadata)
    {
        switch (node.ObjectType)
        {
            case ProjectObjectType.Link when metadata.Link is not null:
                yield return ("Web link", metadata.Link.Url);
                break;
            case ProjectObjectType.Repository when metadata.Repository is not null:
                yield return ("Repository", metadata.Repository.RepositoryUrl);
                break;
            case ProjectObjectType.Meeting when metadata.Meeting is not null:
                yield return ("Meeting", metadata.Meeting.MeetingUrl);
                yield return ("Map", metadata.Meeting.MapUrl);
                break;
            case ProjectObjectType.Environment when metadata.Environment is not null:
                yield return ("Environment", metadata.Environment.LocalhostUrl);
                break;
            case ProjectObjectType.Infrastructure when metadata.Infrastructure is not null:
                yield return ("Provider", metadata.Infrastructure.ProviderUrl);
                yield return ("Login", metadata.Infrastructure.LoginUrl);
                yield return ("AI reference", metadata.Infrastructure.AiReferenceUrl);
                break;
        }

        yield return ("Node route", node.Route);
    }

    private static bool IsKnownFrameBlockedHost(string host)
        => host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
           host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase) ||
           host.Equals("gitlab.com", StringComparison.OrdinalIgnoreCase) ||
           host.EndsWith(".gitlab.com", StringComparison.OrdinalIgnoreCase);
}
