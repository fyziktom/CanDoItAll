namespace CanDoItAll.SharedProviders.Abstractions;

public static class SharedProviderProtocol
{
    public const string CurrentSchemaVersion = "1.1";
}

public static class SharedProviderHeaders
{
    public const string AccessContextReference = "CanDoItAll-Access-Context-Ref";
    public const string AccessContextReferenceType = "CanDoItAll-Access-Context-Type";
    public const string RequestId = "CanDoItAll-Request-Id";
}

public static class SharedProviderRoutes
{
    public const string Catalog = "/api/shared-providers/v1/catalog";
    public const string OpenAiBase = "/api/shared-providers/openai/v1";
    public const string Models = $"{OpenAiBase}/models";
    public const string Responses = $"{OpenAiBase}/responses";
    public const string ChatCompletions = $"{OpenAiBase}/chat/completions";
    public const string ImageGenerations = $"{OpenAiBase}/images/generations";

    public static Uri ResolveCatalog(Uri sourceBaseUri) => Resolve(sourceBaseUri, Catalog);

    public static Uri ResolveOpenAiBase(Uri sourceBaseUri) => Resolve(sourceBaseUri, OpenAiBase);

    private static Uri Resolve(Uri sourceBaseUri, string absoluteRoute)
    {
        ArgumentNullException.ThrowIfNull(sourceBaseUri);

        if (!sourceBaseUri.IsAbsoluteUri)
        {
            throw new ArgumentException("The source base URI must be absolute.", nameof(sourceBaseUri));
        }

        if (string.IsNullOrEmpty(absoluteRoute) || absoluteRoute[0] != '/')
        {
            throw new ArgumentException("A shared-provider route must start with '/'.", nameof(absoluteRoute));
        }

        var basePath = sourceBaseUri.AbsolutePath.EndsWith("/", StringComparison.Ordinal)
            ? sourceBaseUri.AbsolutePath
            : $"{sourceBaseUri.AbsolutePath}/";
        var builder = new UriBuilder(sourceBaseUri)
        {
            Path = $"{basePath}{absoluteRoute[1..]}",
            Query = string.Empty,
            Fragment = string.Empty
        };

        return builder.Uri;
    }
}
