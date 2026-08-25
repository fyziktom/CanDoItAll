namespace CanDoItAll.SharedProviders.Abstractions;

public enum SharedProviderSourceNetworkPolicy
{
    PublicOnly,
    AllowPrivateNetwork
}

public interface ISharedProviderSourceUriPolicy
{
    Uri Normalize(
        Uri sourceBaseUri,
        SharedProviderSourceNetworkPolicy networkPolicy);
}

public sealed class SharedProviderCatalogAccessToken
{
    public const int MaximumLength = 16 * 1024;

    private readonly string value;

    public SharedProviderCatalogAccessToken(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException("The shared-provider catalog access token is invalid.", nameof(value));
        }

        this.value = value;
    }

    public TResult UseValue<TResult>(Func<string, TResult> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action(value);
    }

    public override string ToString()
        => "[REDACTED]";

    private static bool IsValid(string? candidate)
        => candidate is { Length: > 0 and <= MaximumLength } &&
            candidate.All(character =>
                char.IsAsciiLetterOrDigit(character) ||
                character is '-' or '.' or '_' or '~' or '+' or '/' or '=');
}

public readonly record struct SharedProviderCatalogEntityTag
{
    public SharedProviderCatalogEntityTag(string value)
    {
        if (value is null ||
            value.Length != SharedProviderPublicRevision.Prefix.Length + SharedProviderPublicRevision.HashLength + 2 ||
            value[0] != '"' ||
            value[^1] != '"' ||
            !SharedProviderPublicRevision.TryParse(value[1..^1], out _))
        {
            throw new ArgumentException("A shared-provider catalog ETag is invalid.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public static SharedProviderCatalogEntityTag FromRevision(SharedProviderPublicRevision revision)
        => new($"\"{revision.Value}\"");

    public override string ToString()
        => !string.IsNullOrEmpty(Value)
            ? Value
            : throw new InvalidOperationException("The shared-provider catalog ETag is invalid.");
}

public sealed record SharedProviderCatalogFetchRequest
{
    public SharedProviderCatalogFetchRequest(
        Uri sourceBaseUri,
        SharedProviderSourceNetworkPolicy networkPolicy,
        SharedProviderCatalogAccessToken accessToken,
        SharedProviderCatalogEntityTag? ifNoneMatch = null,
        SharedProviderSourceInstanceId? expectedSourceInstanceId = null)
    {
        ArgumentNullException.ThrowIfNull(sourceBaseUri);
        if (!sourceBaseUri.IsAbsoluteUri)
        {
            throw new ArgumentException("The source base URI must be absolute.", nameof(sourceBaseUri));
        }

        if (!Enum.IsDefined(networkPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(networkPolicy));
        }

        ArgumentNullException.ThrowIfNull(accessToken);
        if (ifNoneMatch.HasValue && string.IsNullOrEmpty(ifNoneMatch.Value.Value))
        {
            throw new ArgumentException("The conditional ETag is invalid.", nameof(ifNoneMatch));
        }

        if (expectedSourceInstanceId.HasValue && expectedSourceInstanceId.Value.Value == Guid.Empty)
        {
            throw new ArgumentException("The expected source instance id is invalid.", nameof(expectedSourceInstanceId));
        }

        SourceBaseUri = new Uri(sourceBaseUri.OriginalString, UriKind.Absolute);
        NetworkPolicy = networkPolicy;
        AccessToken = accessToken;
        IfNoneMatch = ifNoneMatch;
        ExpectedSourceInstanceId = expectedSourceInstanceId;
    }

    public Uri SourceBaseUri { get; }

    public SharedProviderSourceNetworkPolicy NetworkPolicy { get; }

    public SharedProviderCatalogAccessToken AccessToken { get; }

    public SharedProviderCatalogEntityTag? IfNoneMatch { get; }

    public SharedProviderSourceInstanceId? ExpectedSourceInstanceId { get; }

    public override string ToString()
        => nameof(SharedProviderCatalogFetchRequest);
}

public abstract record SharedProviderCatalogFetchResult
{
    private SharedProviderCatalogFetchResult()
    {
    }

    public sealed record Succeeded : SharedProviderCatalogFetchResult
    {
        public Succeeded(
            SharedProviderCatalogDocument catalog,
            SharedProviderCatalogEntityTag entityTag)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            SharedProviderProtocolJson.ValidateCatalog(catalog);
            var normalizedCatalog = SharedProviderProtocolJson.NormalizeCatalog(catalog);
            if (string.IsNullOrEmpty(entityTag.Value))
            {
                throw new ArgumentException("The catalog ETag is invalid.", nameof(entityTag));
            }

            var expectedEntityTag = SharedProviderCatalogEntityTag.FromRevision(normalizedCatalog.CatalogRevision);
            if (entityTag != expectedEntityTag)
            {
                throw new ArgumentException(
                    "The catalog ETag must match the catalog revision.",
                    nameof(entityTag));
            }

            Catalog = normalizedCatalog;
            EntityTag = entityTag;
        }

        public SharedProviderCatalogDocument Catalog { get; }

        public SharedProviderCatalogEntityTag EntityTag { get; }
    }

    public sealed record NotModified : SharedProviderCatalogFetchResult
    {
        public NotModified(SharedProviderCatalogEntityTag entityTag)
        {
            if (string.IsNullOrEmpty(entityTag.Value))
            {
                throw new ArgumentException("The catalog ETag is invalid.", nameof(entityTag));
            }

            EntityTag = entityTag;
        }

        public SharedProviderCatalogEntityTag EntityTag { get; }
    }

    public sealed record Failed : SharedProviderCatalogFetchResult
    {
        public Failed(SharedProviderFailure failure)
        {
            ArgumentNullException.ThrowIfNull(failure);
            Failure = failure;
        }

        public SharedProviderFailure Failure { get; }
    }
}

public interface ISharedProviderCatalogClient
{
    ValueTask<SharedProviderCatalogFetchResult> FetchAsync(
        SharedProviderCatalogFetchRequest request,
        CancellationToken cancellationToken = default);
}

public static class SharedProviderCatalogFailureCodes
{
    public static SharedProviderFailureCode SourceUriInvalid { get; } = new("shared_provider_source_uri_invalid");

    public static SharedProviderFailureCode Unauthorized { get; } = new("shared_provider_catalog_unauthorized");

    public static SharedProviderFailureCode InsufficientScope { get; } = new("shared_provider_catalog_scope_denied");

    public static SharedProviderFailureCode NotFound { get; } = new("shared_provider_catalog_not_found");

    public static SharedProviderFailureCode Timeout { get; } = new("shared_provider_catalog_timeout");

    public static SharedProviderFailureCode RateLimited { get; } = new("shared_provider_catalog_rate_limited");

    public static SharedProviderFailureCode Unavailable { get; } = new("shared_provider_source_unavailable");

    public static SharedProviderFailureCode RequestRejected { get; } = new("shared_provider_catalog_request_rejected");

    public static SharedProviderFailureCode ContractInvalid { get; } = new("shared_provider_catalog_contract_invalid");

    public static SharedProviderFailureCode SourceIdentityMismatch { get; } = new("shared_provider_source_identity_mismatch");
}
