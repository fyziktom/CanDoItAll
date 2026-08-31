using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public static class SharedProviderSourceTransitions
{
    private const int MaximumNameLength = 200;
    private const int MaximumAddressLength = 2_048;
    private const int MaximumStatusMessageLength = 400;

    public static SharedProviderSource Create(
        string name,
        string baseUri,
        Guid apiTokenSecretId,
        bool allowInsecurePrivateNetwork,
        bool isEnabled,
        DateTimeOffset timestampUtc)
    {
        SharedProviderStateGuard.NonEmpty(apiTokenSecretId, nameof(apiTokenSecretId));
        SharedProviderStateGuard.Utc(timestampUtc, nameof(timestampUtc));

        return new SharedProviderSource
        {
            Name = SharedProviderStateGuard.NormalizeText(name, MaximumNameLength, nameof(name)),
            BaseUri = CanonicalizeBaseUri(
                baseUri,
                allowInsecurePrivateNetwork,
                nameof(baseUri)),
            ApiTokenSecretId = apiTokenSecretId,
            IsEnabled = isEnabled,
            AllowInsecurePrivateNetwork = allowInsecurePrivateNetwork,
            Status = SharedProviderSourceStatus.NeverSynchronized,
            LastStatusMessage = string.Empty,
            CreatedAtUtc = timestampUtc,
            UpdatedAtUtc = timestampUtc
        };
    }

    public static void UpdateConfiguration(
        SharedProviderSource source,
        string name,
        string baseUri,
        Guid apiTokenSecretId,
        bool allowInsecurePrivateNetwork,
        bool isEnabled,
        DateTimeOffset timestampUtc)
    {
        Validate(source);
        SharedProviderStateGuard.NonEmpty(apiTokenSecretId, nameof(apiTokenSecretId));
        SharedProviderStateGuard.TransitionTimestamp(
            timestampUtc,
            source.UpdatedAtUtc,
            nameof(timestampUtc));

        source.Name = SharedProviderStateGuard.NormalizeText(name, MaximumNameLength, nameof(name));
        source.BaseUri = CanonicalizeBaseUri(
            baseUri,
            allowInsecurePrivateNetwork,
            nameof(baseUri));
        source.ApiTokenSecretId = apiTokenSecretId;
        source.IsEnabled = isEnabled;
        source.AllowInsecurePrivateNetwork = allowInsecurePrivateNetwork;
        source.Status = SharedProviderSourceStatus.NeverSynchronized;
        source.LastCatalogETag = null;
        source.LastSyncAtUtc = null;
        source.LastStatusCode = null;
        source.LastStatusMessage = string.Empty;
        source.UpdatedAtUtc = timestampUtc;
    }

    public static void SetEnabled(
        SharedProviderSource source,
        bool isEnabled,
        DateTimeOffset timestampUtc)
    {
        Validate(source);
        SharedProviderStateGuard.TransitionTimestamp(
            timestampUtc,
            source.UpdatedAtUtc,
            nameof(timestampUtc));
        if (source.IsEnabled == isEnabled)
        {
            return;
        }

        source.IsEnabled = isEnabled;
        source.UpdatedAtUtc = timestampUtc;
    }

    public static void ResetTrustedIdentity(
        SharedProviderSource source,
        DateTimeOffset timestampUtc)
    {
        Validate(source);
        SharedProviderStateGuard.TransitionTimestamp(
            timestampUtc,
            source.UpdatedAtUtc,
            nameof(timestampUtc));
        source.RemoteInstanceId = null;
        source.LastCatalogETag = null;
        source.Status = SharedProviderSourceStatus.NeverSynchronized;
        source.LastSyncAtUtc = null;
        source.LastStatusCode = null;
        source.LastStatusMessage = string.Empty;
        source.UpdatedAtUtc = timestampUtc;
    }

    public static SharedProviderCatalogIdentityAcceptance ApplySuccessfulCatalog(
        SharedProviderSource source,
        SharedProviderSourceInstanceId remoteInstanceId,
        SharedProviderCatalogEntityTag entityTag,
        DateTimeOffset timestampUtc)
    {
        Validate(source);
        SharedProviderStateGuard.SourceInstanceId(remoteInstanceId, nameof(remoteInstanceId));
        SharedProviderStateGuard.EntityTag(entityTag, nameof(entityTag));
        SharedProviderStateGuard.TransitionTimestamp(
            timestampUtc,
            source.UpdatedAtUtc,
            nameof(timestampUtc));

        if (TryApplyIdentityMismatch(source, remoteInstanceId, timestampUtc))
        {
            return SharedProviderCatalogIdentityAcceptance.IdentityMismatch;
        }

        source.RemoteInstanceId = remoteInstanceId;
        source.LastCatalogETag = entityTag;
        source.Status = SharedProviderSourceStatus.Available;
        source.LastSyncAtUtc = timestampUtc;
        source.LastStatusCode = 200;
        source.LastStatusMessage = "Catalog synchronized.";
        source.UpdatedAtUtc = timestampUtc;
        return SharedProviderCatalogIdentityAcceptance.Accepted;
    }

    public static SharedProviderCatalogIdentityAcceptance ApplySuccessfulConnectionTest(
        SharedProviderSource source,
        SharedProviderSourceInstanceId remoteInstanceId,
        SharedProviderCatalogEntityTag entityTag,
        DateTimeOffset timestampUtc)
    {
        Validate(source);
        SharedProviderStateGuard.SourceInstanceId(remoteInstanceId, nameof(remoteInstanceId));
        SharedProviderStateGuard.EntityTag(entityTag, nameof(entityTag));
        SharedProviderStateGuard.TransitionTimestamp(
            timestampUtc,
            source.UpdatedAtUtc,
            nameof(timestampUtc));
        if (TryApplyIdentityMismatch(source, remoteInstanceId, timestampUtc))
        {
            return SharedProviderCatalogIdentityAcceptance.IdentityMismatch;
        }

        source.RemoteInstanceId = remoteInstanceId;
        source.Status = SharedProviderSourceStatus.Available;
        source.LastSyncAtUtc = timestampUtc;
        source.LastStatusCode = 200;
        source.LastStatusMessage = "Catalog connection verified.";
        source.UpdatedAtUtc = timestampUtc;
        return SharedProviderCatalogIdentityAcceptance.Accepted;
    }

    public static void ApplyFailure(
        SharedProviderSource source,
        SharedProviderSourceStatus status,
        int? statusCode,
        string sanitizedMessage,
        DateTimeOffset timestampUtc)
    {
        Validate(source);
        if (status is SharedProviderSourceStatus.NeverSynchronized or
            SharedProviderSourceStatus.Available)
        {
            throw new ArgumentException("A source failure requires a failure status.", nameof(status));
        }

        if (statusCode is < 100 or > 599)
        {
            throw new ArgumentOutOfRangeException(nameof(statusCode));
        }

        SharedProviderStateGuard.TransitionTimestamp(
            timestampUtc,
            source.UpdatedAtUtc,
            nameof(timestampUtc));
        source.Status = status;
        source.LastSyncAtUtc = timestampUtc;
        source.LastStatusCode = statusCode;
        source.LastStatusMessage = SharedProviderStateGuard.ExactText(
            sanitizedMessage,
            MaximumStatusMessageLength,
            nameof(sanitizedMessage));
        source.UpdatedAtUtc = timestampUtc;
    }

    private static void Validate(SharedProviderSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        SharedProviderStateGuard.NonEmpty(source.Id, nameof(source));
        _ = SharedProviderStateGuard.NormalizeText(source.Name, MaximumNameLength, nameof(source));
        _ = CanonicalizeBaseUri(
            source.BaseUri,
            source.AllowInsecurePrivateNetwork,
            nameof(source));
        SharedProviderStateGuard.NonEmpty(source.ApiTokenSecretId, nameof(source));
        SharedProviderStateGuard.Utc(source.CreatedAtUtc, nameof(source));
        SharedProviderStateGuard.Utc(source.UpdatedAtUtc, nameof(source));
        if (!Enum.IsDefined(source.Status))
        {
            throw new ArgumentOutOfRangeException(nameof(source));
        }
    }

    private static bool TryApplyIdentityMismatch(
        SharedProviderSource source,
        SharedProviderSourceInstanceId remoteInstanceId,
        DateTimeOffset timestampUtc)
    {
        if (source.RemoteInstanceId is not { } trustedIdentity ||
            trustedIdentity == remoteInstanceId)
        {
            return false;
        }

        source.Status = SharedProviderSourceStatus.SourceIdentityMismatch;
        source.LastSyncAtUtc = timestampUtc;
        source.LastStatusCode = 409;
        source.LastStatusMessage = "The source identity differs from the trusted identity.";
        source.UpdatedAtUtc = timestampUtc;
        return true;
    }

    private static string CanonicalizeBaseUri(
        string value,
        bool allowInsecurePrivateNetwork,
        string parameterName)
    {
        var normalized = SharedProviderStateGuard.NormalizeText(
            value,
            MaximumAddressLength,
            parameterName);
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            string.IsNullOrEmpty(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException(
                "A source base URI must be an absolute HTTP or HTTPS URI without userinfo, query, or fragment.",
                parameterName);
        }

        if (uri.Scheme == Uri.UriSchemeHttp &&
            !allowInsecurePrivateNetwork &&
            !uri.IsLoopback)
        {
            throw new ArgumentException(
                "An HTTP source requires the explicit insecure private-network policy.",
                parameterName);
        }

        var builder = new UriBuilder(uri)
        {
            Host = uri.IdnHost.ToLowerInvariant(),
            Query = string.Empty,
            Fragment = string.Empty
        };
        if (uri.IsDefaultPort)
        {
            builder.Port = -1;
        }

        var canonical = builder.Uri.GetLeftPart(UriPartial.Path);
        if (!canonical.EndsWith("/", StringComparison.Ordinal))
        {
            canonical += '/';
        }

        if (canonical.Length > MaximumAddressLength)
        {
            throw new ArgumentException(
                "The canonical source base URI is too long.",
                parameterName);
        }

        return canonical;
    }
}
