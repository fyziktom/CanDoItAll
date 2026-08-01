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

            string? frameBlockedProvider = ResolveKnownFrameBlockedProvider(uri);
            bool canEmbed = frameBlockedProvider is null;
            link = new ProjectStructureWebLink(
                uri,
                sourceLabel,
                canEmbed,
                canEmbed
                    ? string.Empty
                    : $"{frameBlockedProvider} prevents its pages from being displayed inside another site's embedded browser. Open the link in a new browser tab instead.");
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

    private static string? ResolveKnownFrameBlockedProvider(Uri uri)
    {
        string host = NormalizeHost(uri);
        if (MatchesDomain(host, "github.com"))
        {
            return "GitHub";
        }

        if (MatchesDomain(host, "gitlab.com"))
        {
            return "GitLab";
        }

        return IsGoogleDomain(host) && !IsKnownGoogleEmbed(uri, host)
            ? "Google"
            : null;
    }

    private static bool IsKnownGoogleEmbed(Uri uri, string host)
    {
        string path = uri.AbsolutePath.TrimEnd('/');
        if (host.Equals("www.google.com", StringComparison.OrdinalIgnoreCase) &&
            (path.Equals("/maps/embed", StringComparison.Ordinal) ||
             path.StartsWith("/maps/embed/", StringComparison.Ordinal)))
        {
            return true;
        }

        if (host.Equals("docs.google.com", StringComparison.OrdinalIgnoreCase) &&
            path.EndsWith("/preview", StringComparison.Ordinal))
        {
            return true;
        }

        return IsRecaptchaEmbedPath(path);
    }

    private static bool IsRecaptchaEmbedPath(string path)
        => path.Equals("/recaptcha/api2/anchor", StringComparison.Ordinal) ||
           path.Equals("/recaptcha/api2/bframe", StringComparison.Ordinal) ||
           path.Equals("/recaptcha/enterprise/anchor", StringComparison.Ordinal) ||
           path.Equals("/recaptcha/enterprise/bframe", StringComparison.Ordinal);

    private static bool IsGoogleDomain(string host)
    {
        string[] labels = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        int googleLabelIndex = Array.FindLastIndex(
            labels,
            static label => label.Equals("google", StringComparison.OrdinalIgnoreCase));
        int suffixLabelCount = labels.Length - googleLabelIndex - 1;
        if (googleLabelIndex < 0 || suffixLabelCount is < 1 or > 2)
        {
            return false;
        }

        if (suffixLabelCount == 1)
        {
            string suffix = labels[^1];
            return suffix.Equals("com", StringComparison.OrdinalIgnoreCase) ||
                   suffix.Equals("cat", StringComparison.OrdinalIgnoreCase) ||
                   IsCountryCode(suffix);
        }

        return IsCountryCode(labels[^1]) &&
               (labels[^2].Equals("co", StringComparison.OrdinalIgnoreCase) ||
                labels[^2].Equals("com", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCountryCode(string value)
        => value.Length == 2 && value.All(char.IsAsciiLetter);

    private static string NormalizeHost(Uri uri)
        => uri.IdnHost.TrimEnd('.');

    private static bool MatchesDomain(string host, string domain)
        => host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
           host.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase);
}
