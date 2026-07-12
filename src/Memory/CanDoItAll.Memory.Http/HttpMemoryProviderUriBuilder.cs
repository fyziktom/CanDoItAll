namespace CanDoItAll.Memory.Http;

internal static class HttpMemoryProviderUriBuilder
{
    public static Uri Build(Uri baseUrl, string path)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        ValidatePath(path);

        var endpoint = new Uri(baseUrl, path);
        if (!HasSameAuthority(baseUrl, endpoint) || !string.IsNullOrEmpty(endpoint.UserInfo))
        {
            throw new InvalidOperationException(
                "HTTP memory provider path must resolve to the configured service authority without user information.");
        }

        return endpoint;
    }

    private static void ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("HTTP memory provider path must not be empty.", nameof(path));
        }

        if (path[0] != '/' ||
            path.Contains("//", StringComparison.Ordinal) ||
            path.Contains('\\') ||
            path.Contains('?') ||
            path.Contains('#') ||
            path.Any(char.IsControl) ||
            !Uri.TryCreate(path, UriKind.Relative, out _))
        {
            throw new ArgumentException(
                "HTTP memory provider path must be a rooted relative path beginning with exactly one '/'.",
                nameof(path));
        }
    }

    private static bool HasSameAuthority(Uri baseUrl, Uri endpoint)
    {
        return string.Equals(baseUrl.Scheme, endpoint.Scheme, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(baseUrl.Host, endpoint.Host, StringComparison.OrdinalIgnoreCase) &&
            baseUrl.Port == endpoint.Port;
    }
}
