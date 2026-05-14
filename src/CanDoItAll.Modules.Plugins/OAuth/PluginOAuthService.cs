using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedKernel.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Plugins;

public sealed class PluginOAuthService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    PluginCatalogService catalogService,
    PluginConnectionStore connectionStore,
    PluginGrantEvaluator grantEvaluator,
    ISecretVault secretVault,
    IHttpClientFactory httpClientFactory,
    IClock clock,
    ILogger<PluginOAuthService> logger)
{
    private const int StateBytes = 32;
    private const int PkceVerifierBytes = 64;
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan AccessTokenClockSkew = TimeSpan.FromMinutes(1);
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> RefreshLocks = new();
    private static readonly EnvironmentVariableTarget[] ClientSecretEnvironmentTargets =
    [
        EnvironmentVariableTarget.Process,
        EnvironmentVariableTarget.User,
        EnvironmentVariableTarget.Machine
    ];

    public async Task<Result<PluginOAuthStartResponse>> StartAsync(
        PluginId pluginId,
        PluginOAuthStartRequest request,
        Uri requestBaseUri,
        string actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(requestBaseUri);

        var descriptorResult = await ResolveOAuthDescriptorAsync(pluginId, request.ConnectionKey, cancellationToken);
        if (descriptorResult.IsFailure)
        {
            return Result<PluginOAuthStartResponse>.Failure(descriptorResult.Errors);
        }

        var grant = await grantEvaluator.EvaluateAsync(pluginId, PluginCapabilityKind.OAuth2, cancellationToken: cancellationToken);
        if (!grant.Allowed)
        {
            return Result<PluginOAuthStartResponse>.Failure(Error.Failure(grant.Message, "plugins.oauth-grant-denied"));
        }

        var oauth = descriptorResult.Value!;
        var connectionResult = await ResolveOrCreateConnectionAsync(pluginId, request, actor, cancellationToken);
        if (connectionResult.IsFailure)
        {
            return Result<PluginOAuthStartResponse>.Failure(connectionResult.Errors);
        }

        var connection = connectionResult.Value!;
        var connectionSettings = ConfigurationState.FromJson(connection.SettingsJson);
        var clientId = ResolveClientId(oauth, connectionSettings);
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return Result<PluginOAuthStartResponse>.Failure(Error.Validation(
                $"OAuth client id is missing for plugin '{pluginId}' connection '{request.ConnectionKey}'.",
                "plugins.oauth-client-id-missing"));
        }

        var redirectUri = ResolveRedirectUri(oauth, request, connectionSettings, requestBaseUri);
        var requestedScopes = NormalizeScopes(request.Scopes is { Count: > 0 } ? request.Scopes : oauth.Scopes);
        var state = CreateRandomBase64Url(StateBytes);
        var stateHash = HashBase64Url(state);
        var codeVerifier = oauth.UsesPkce ? CreateRandomBase64Url(PkceVerifierBytes) : string.Empty;
        var codeVerifierVaultKey = $"plugins/oauth/sessions/{Guid.NewGuid():N}/pkce";
        var timestamp = clock.GetUtcNow();

        if (!string.IsNullOrWhiteSpace(codeVerifier))
        {
            await secretVault.SetAsync(codeVerifierVaultKey, codeVerifier, cancellationToken);
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var session = new PluginOAuthSessionRecord
        {
            StateHash = stateHash,
            PluginId = pluginId.Value,
            ConnectionId = connection.Id.Value,
            ConnectionKey = request.ConnectionKey.Value,
            ProviderKey = ResolveProviderKey(pluginId, request.ConnectionKey),
            CodeVerifierVaultKey = codeVerifierVaultKey,
            RedirectUri = redirectUri,
            ReturnPath = NormalizeReturnPath(request.ReturnPath),
            RequestedScopesJson = JsonSerializer.Serialize(requestedScopes, JsonOptions),
            CreatedAtUtc = timestamp,
            ExpiresAtUtc = timestamp.Add(SessionLifetime),
            Status = "Pending"
        };
        dbContext.Set<PluginOAuthSessionRecord>().Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        var authorizationUrl = BuildAuthorizationUrl(oauth, clientId, redirectUri, requestedScopes, state, codeVerifier);
        logger.LogInformation(
            "Started OAuth session for plugin {PluginId} connection {ConnectionId}. Scopes={ScopeCount}.",
            pluginId.Value,
            connection.Id.Value,
            requestedScopes.Count);

        return Result<PluginOAuthStartResponse>.Success(new PluginOAuthStartResponse(
            connection.Id,
            authorizationUrl,
            redirectUri,
            requestedScopes));
    }

    public async Task<Uri> CompleteCallbackAsync(
        string? state,
        string? code,
        string? providerError,
        string? providerErrorDescription,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return new Uri("/plugins?oauth=failed&reason=missing-state", UriKind.Relative);
        }

        var stateHash = HashBase64Url(state.Trim());
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var session = await dbContext.Set<PluginOAuthSessionRecord>()
            .SingleOrDefaultAsync(item => item.StateHash == stateHash, cancellationToken);
        if (session is null)
        {
            return new Uri("/plugins?oauth=failed&reason=invalid-state", UriKind.Relative);
        }

        var returnPath = NormalizeReturnPath(session.ReturnPath);
        if (!string.Equals(session.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            return BuildReturnUri(returnPath, "failed", "state-used", session.ConnectionId);
        }

        var timestamp = clock.GetUtcNow();
        if (session.ExpiresAtUtc <= timestamp)
        {
            await MarkSessionFailedAsync(dbContext, session, "expired", "OAuth login session expired.", timestamp, cancellationToken);
            await DeleteSessionSecretAsync(session, cancellationToken);
            return BuildReturnUri(returnPath, "failed", "expired", session.ConnectionId);
        }

        if (!string.IsNullOrWhiteSpace(providerError))
        {
            await MarkSessionFailedAsync(
                dbContext,
                session,
                providerError,
                providerErrorDescription ?? string.Empty,
                timestamp,
                cancellationToken);
            await UpsertOAuthConnectionErrorAsync(
                dbContext,
                session,
                PluginOAuthConnectionStatusKind.Error,
                providerError,
                providerErrorDescription ?? string.Empty,
                timestamp,
                cancellationToken);
            await DeleteSessionSecretAsync(session, cancellationToken);
            return BuildReturnUri(returnPath, "failed", providerError, session.ConnectionId);
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            await MarkSessionFailedAsync(dbContext, session, "missing-code", "OAuth callback did not contain an authorization code.", timestamp, cancellationToken);
            await DeleteSessionSecretAsync(session, cancellationToken);
            return BuildReturnUri(returnPath, "failed", "missing-code", session.ConnectionId);
        }

        var descriptorResult = await ResolveOAuthDescriptorAsync(
            new PluginId(session.PluginId),
            new PluginConnectionKey(session.ConnectionKey),
            cancellationToken);
        if (descriptorResult.IsFailure)
        {
            await MarkSessionFailedAsync(dbContext, session, "descriptor-missing", descriptorResult.Errors[0].Message, timestamp, cancellationToken);
            await DeleteSessionSecretAsync(session, cancellationToken);
            return BuildReturnUri(returnPath, "failed", "descriptor-missing", session.ConnectionId);
        }

        var connection = await connectionStore.FindAsync(
            new PluginId(session.PluginId),
            new PluginConnectionId(session.ConnectionId),
            cancellationToken);
        if (connection is null)
        {
            await MarkSessionFailedAsync(dbContext, session, "connection-missing", "OAuth connection no longer exists.", timestamp, cancellationToken);
            await DeleteSessionSecretAsync(session, cancellationToken);
            return BuildReturnUri(returnPath, "failed", "connection-missing", session.ConnectionId);
        }

        var codeVerifier = await secretVault.GetAsync(session.CodeVerifierVaultKey, cancellationToken) ?? string.Empty;
        var exchangeResult = await ExchangeAuthorizationCodeAsync(
            descriptorResult.Value!,
            ConfigurationState.FromJson(connection.SettingsJson),
            code,
            codeVerifier,
            session.RedirectUri,
            DeserializeScopes(session.RequestedScopesJson),
            cancellationToken);
        if (exchangeResult.IsFailure)
        {
            await MarkSessionFailedAsync(dbContext, session, exchangeResult.Errors[0].Code, exchangeResult.Errors[0].Message, timestamp, cancellationToken);
            await UpsertOAuthConnectionErrorAsync(
                dbContext,
                session,
                PluginOAuthConnectionStatusKind.Error,
                exchangeResult.Errors[0].Code,
                exchangeResult.Errors[0].Message,
                timestamp,
                cancellationToken);
            await DeleteSessionSecretAsync(session, cancellationToken);
            return BuildReturnUri(returnPath, "failed", exchangeResult.Errors[0].Code, session.ConnectionId);
        }

        await StoreTokenEnvelopeAsync(dbContext, session, exchangeResult.Value!, timestamp, cancellationToken);
        session.Status = "Completed";
        session.CompletedAtUtc = timestamp;
        await dbContext.SaveChangesAsync(cancellationToken);
        await DeleteSessionSecretAsync(session, cancellationToken);

        logger.LogInformation(
            "Completed OAuth session for plugin {PluginId} connection {ConnectionId}.",
            session.PluginId,
            session.ConnectionId);

        return BuildReturnUri(returnPath, "connected", string.Empty, session.ConnectionId);
    }

    public async Task<IReadOnlyList<PluginOAuthConnectionStatusItem>> ListStatusesAsync(
        PluginId pluginId,
        CancellationToken cancellationToken = default)
    {
        var requiredScopesByConnectionKey = await LoadRequiredOAuthScopesByConnectionKeyAsync(pluginId, cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var records = await dbContext.Set<PluginOAuthConnectionRecord>()
            .AsNoTracking()
            .Where(item => item.PluginId == pluginId.Value)
            .OrderBy(item => item.ConnectionKey)
            .ThenBy(item => item.AccountDisplay)
            .ToArrayAsync(cancellationToken);

        return records
            .Select(item => ToStatusItem(item, requiredScopesByConnectionKey))
            .ToArray();
    }

    public async Task<Result<PluginOAuthDisconnectResponse>> DisconnectAsync(
        PluginId pluginId,
        PluginConnectionId connectionId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<PluginOAuthConnectionRecord>()
            .SingleOrDefaultAsync(item => item.PluginId == pluginId.Value && item.ConnectionId == connectionId.Value, cancellationToken);
        if (record is null)
        {
            return Result<PluginOAuthDisconnectResponse>.Failure(Error.Failure(
                $"OAuth connection '{connectionId}' was not found for plugin '{pluginId}'.",
                "plugins.oauth-connection-not-found"));
        }

        if (!string.IsNullOrWhiteSpace(record.TokenVaultKey))
        {
            await secretVault.DeleteAsync(record.TokenVaultKey, cancellationToken);
        }

        record.TokenVaultKey = string.Empty;
        record.Status = nameof(PluginOAuthConnectionStatusKind.NotConnected);
        record.AccountDisplay = string.Empty;
        record.GrantedScopesJson = "[]";
        record.AccessTokenExpiresAtUtc = null;
        record.RefreshTokenExpiresAtUtc = null;
        record.LastErrorCode = string.Empty;
        record.LastErrorDescription = string.Empty;
        record.UpdatedAtUtc = clock.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<PluginOAuthDisconnectResponse>.Success(new PluginOAuthDisconnectResponse(
            connectionId,
            PluginOAuthConnectionStatusKind.NotConnected));
    }

    public async ValueTask<PluginOAuth2TokenSnapshot> GetAccessTokenAsync(
        PluginId pluginId,
        PluginConnectionId connectionId,
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken = default)
    {
        var grant = await grantEvaluator.EvaluateAsync(pluginId, PluginCapabilityKind.OAuth2, cancellationToken: cancellationToken);
        if (!grant.Allowed)
        {
            throw new InvalidOperationException(grant.Message);
        }

        var refreshLock = RefreshLocks.GetOrAdd(connectionId.Value, _ => new SemaphoreSlim(1, 1));
        await refreshLock.WaitAsync(cancellationToken);
        try
        {
            return await GetAccessTokenCoreAsync(pluginId, connectionId, NormalizeScopes(scopes), cancellationToken);
        }
        finally
        {
            refreshLock.Release();
        }
    }

    private async ValueTask<PluginOAuth2TokenSnapshot> GetAccessTokenCoreAsync(
        PluginId pluginId,
        PluginConnectionId connectionId,
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var connection = await dbContext.Set<PluginConnectionRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == connectionId.Value && item.PluginId == pluginId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Plugin connection '{connectionId}' was not found for plugin '{pluginId}'.");
        if (!connection.IsEnabled)
        {
            throw new InvalidOperationException($"Plugin connection '{connectionId}' is disabled.");
        }

        var oauthRecord = await dbContext.Set<PluginOAuthConnectionRecord>()
            .SingleOrDefaultAsync(item => item.ConnectionId == connectionId.Value && item.PluginId == pluginId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"OAuth connection '{connectionId}' is not connected.");
        if (!string.Equals(oauthRecord.Status, nameof(PluginOAuthConnectionStatusKind.Connected), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"OAuth connection '{connectionId}' requires reconnect. Status={oauthRecord.Status}.");
        }

        var envelope = await ReadTokenEnvelopeAsync(oauthRecord.TokenVaultKey, cancellationToken);
        EnsureScopesGranted(scopes, envelope.Scopes);
        var timestamp = clock.GetUtcNow();
        if (envelope.AccessTokenExpiresAtUtc > timestamp.Add(AccessTokenClockSkew))
        {
            return new PluginOAuth2TokenSnapshot(envelope.AccessToken, envelope.AccessTokenExpiresAtUtc, envelope.Scopes);
        }

        if (string.IsNullOrWhiteSpace(envelope.RefreshToken))
        {
            await MarkReconnectRequiredAsync(dbContext, oauthRecord, "refresh-token-missing", "OAuth refresh token is missing.", timestamp, cancellationToken);
            throw new InvalidOperationException($"OAuth connection '{connectionId}' does not have a refresh token. Reconnect the plugin connection.");
        }

        var descriptorResult = await ResolveOAuthDescriptorAsync(pluginId, new PluginConnectionKey(connection.ConnectionKey), cancellationToken);
        if (descriptorResult.IsFailure)
        {
            throw new InvalidOperationException(descriptorResult.Errors[0].Message);
        }

        var refreshResult = await RefreshTokenAsync(
            descriptorResult.Value!,
            ConfigurationState.FromJson(connection.SettingsJson),
            envelope,
            cancellationToken);
        if (refreshResult.IsFailure)
        {
            await MarkReconnectRequiredAsync(dbContext, oauthRecord, refreshResult.Errors[0].Code, refreshResult.Errors[0].Message, timestamp, cancellationToken);
            throw new InvalidOperationException($"OAuth refresh failed for connection '{connectionId}': {refreshResult.Errors[0].Message}");
        }

        var refreshed = refreshResult.Value!;
        await secretVault.SetAsync(oauthRecord.TokenVaultKey, JsonSerializer.Serialize(refreshed, JsonOptions), cancellationToken);
        oauthRecord.AccessTokenExpiresAtUtc = refreshed.AccessTokenExpiresAtUtc;
        oauthRecord.RefreshTokenExpiresAtUtc = refreshed.RefreshTokenExpiresAtUtc;
        oauthRecord.GrantedScopesJson = JsonSerializer.Serialize(refreshed.Scopes, JsonOptions);
        oauthRecord.LastErrorCode = string.Empty;
        oauthRecord.LastErrorDescription = string.Empty;
        oauthRecord.UpdatedAtUtc = timestamp;
        await dbContext.SaveChangesAsync(cancellationToken);

        EnsureScopesGranted(scopes, refreshed.Scopes);
        return new PluginOAuth2TokenSnapshot(refreshed.AccessToken, refreshed.AccessTokenExpiresAtUtc, refreshed.Scopes);
    }

    private async Task<Result<PluginConnectionItem>> ResolveOrCreateConnectionAsync(
        PluginId pluginId,
        PluginOAuthStartRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (request.ConnectionId is { } connectionId)
        {
            var existing = await connectionStore.FindAsync(pluginId, connectionId, cancellationToken);
            return existing is null
                ? Result<PluginConnectionItem>.Failure(Error.Failure(
                    $"Plugin connection '{connectionId}' was not found for plugin '{pluginId}'.",
                    "plugins.connection-not-found"))
                : Result<PluginConnectionItem>.Success(existing);
        }

        var byKey = await connectionStore.FindFirstByKeyAsync(pluginId, request.ConnectionKey, cancellationToken);
        if (byKey is not null)
        {
            return Result<PluginConnectionItem>.Success(byKey);
        }

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? request.ConnectionKey.Value
            : request.DisplayName.Trim();
        return await connectionStore.SaveAsync(
            pluginId,
            new PluginConnectionSaveRequest(
                Id: null,
                request.ConnectionKey,
                displayName,
                "{}",
                IsEnabled: true),
            actor,
            cancellationToken);
    }

    private async Task<Result<PluginOAuth2Descriptor>> ResolveOAuthDescriptorAsync(
        PluginId pluginId,
        PluginConnectionKey connectionKey,
        CancellationToken cancellationToken)
    {
        var catalog = await catalogService.ListCatalogAsync(cancellationToken);
        var item = catalog.SingleOrDefault(candidate => candidate.PluginId == pluginId);
        if (item is null)
        {
            return Result<PluginOAuth2Descriptor>.Failure(Error.Failure($"Plugin '{pluginId}' was not found.", "plugins.not-found"));
        }

        var descriptor = item.Descriptor.OAuth2;
        if (descriptor is null || descriptor.ConnectionKey != connectionKey)
        {
            return Result<PluginOAuth2Descriptor>.Failure(Error.Failure(
                $"Plugin '{pluginId}' does not declare OAuth2 for connection '{connectionKey}'.",
                "plugins.oauth-descriptor-missing"));
        }

        return Result<PluginOAuth2Descriptor>.Success(descriptor);
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> LoadRequiredOAuthScopesByConnectionKeyAsync(
        PluginId pluginId,
        CancellationToken cancellationToken)
    {
        var plugin = (await catalogService.ListCatalogAsync(cancellationToken))
            .SingleOrDefault(item => item.PluginId == pluginId);
        if (plugin?.Descriptor.OAuth2 is null)
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        }

        return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [plugin.Descriptor.OAuth2.ConnectionKey.Value] = NormalizeScopes(plugin.Descriptor.OAuth2.Scopes)
        };
    }

    private async Task<Result<PluginOAuthTokenEnvelope>> ExchangeAuthorizationCodeAsync(
        PluginOAuth2Descriptor descriptor,
        ConfigurationState settings,
        string code,
        string codeVerifier,
        string redirectUri,
        IReadOnlyList<string> requestedScopes,
        CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["client_id"] = ResolveClientId(descriptor, settings),
            ["code"] = code.Trim(),
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = redirectUri
        };
        if (descriptor.UsesPkce)
        {
            values["code_verifier"] = codeVerifier;
        }

        var clientSecretError = AddClientSecretIfConfigured(descriptor, values);
        if (clientSecretError is not null)
        {
            return Result<PluginOAuthTokenEnvelope>.Failure(clientSecretError);
        }

        return await SendTokenRequestAsync(descriptor, values, requestedScopes, previousRefreshToken: string.Empty, cancellationToken);
    }

    private async Task<Result<PluginOAuthTokenEnvelope>> RefreshTokenAsync(
        PluginOAuth2Descriptor descriptor,
        ConfigurationState settings,
        PluginOAuthTokenEnvelope previous,
        CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["client_id"] = ResolveClientId(descriptor, settings),
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = previous.RefreshToken
        };
        var clientSecretError = AddClientSecretIfConfigured(descriptor, values);
        if (clientSecretError is not null)
        {
            return Result<PluginOAuthTokenEnvelope>.Failure(clientSecretError);
        }

        return await SendTokenRequestAsync(descriptor, values, previous.Scopes, previous.RefreshToken, cancellationToken);
    }

    private async Task<Result<PluginOAuthTokenEnvelope>> SendTokenRequestAsync(
        PluginOAuth2Descriptor descriptor,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyList<string> fallbackScopes,
        string previousRefreshToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, descriptor.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(values)
        };
        using var response = await httpClientFactory.CreateClient(nameof(PluginOAuthService)).SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorText = await response.Content.ReadAsStringAsync(cancellationToken);
            return Result<PluginOAuthTokenEnvelope>.Failure(Error.Failure(
                $"OAuth token endpoint returned {(int)response.StatusCode} {response.ReasonPhrase}: {RedactProviderError(errorText)}",
                "plugins.oauth-token-endpoint-failed"));
        }

        var token = await response.Content.ReadFromJsonAsync<OAuthTokenResponse>(JsonOptions, cancellationToken);
        if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
        {
            return Result<PluginOAuthTokenEnvelope>.Failure(Error.Failure(
                "OAuth token endpoint response did not include an access token.",
                "plugins.oauth-access-token-missing"));
        }

        var timestamp = clock.GetUtcNow();
        var scopes = NormalizeScopes(SplitScopes(token.Scope).Count > 0 ? SplitScopes(token.Scope) : fallbackScopes);
        var expiresIn = token.ExpiresIn > 0 ? token.ExpiresIn : 3600;
        return Result<PluginOAuthTokenEnvelope>.Success(new PluginOAuthTokenEnvelope
        {
            ProviderKey = ResolveProviderKey(descriptor.ConnectionKey),
            AccessToken = token.AccessToken,
            RefreshToken = string.IsNullOrWhiteSpace(token.RefreshToken) ? previousRefreshToken : token.RefreshToken,
            TokenType = string.IsNullOrWhiteSpace(token.TokenType) ? "Bearer" : token.TokenType,
            AccessTokenExpiresAtUtc = timestamp.AddSeconds(expiresIn),
            RefreshTokenExpiresAtUtc = null,
            Scopes = scopes,
            AccountDisplay = string.Empty
        });
    }

    private async Task StoreTokenEnvelopeAsync(
        AppDbContext dbContext,
        PluginOAuthSessionRecord session,
        PluginOAuthTokenEnvelope envelope,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        var tokenVaultKey = ResolveTokenVaultKey(session.ConnectionId);
        await secretVault.SetAsync(tokenVaultKey, JsonSerializer.Serialize(envelope, JsonOptions), cancellationToken);

        var record = await dbContext.Set<PluginOAuthConnectionRecord>()
            .SingleOrDefaultAsync(item => item.ConnectionId == session.ConnectionId, cancellationToken);
        if (record is null)
        {
            record = new PluginOAuthConnectionRecord
            {
                ConnectionId = session.ConnectionId,
                PluginId = session.PluginId,
                ConnectionKey = session.ConnectionKey,
                ProviderKey = session.ProviderKey,
                CreatedAtUtc = timestamp
            };
            dbContext.Set<PluginOAuthConnectionRecord>().Add(record);
        }

        record.TokenVaultKey = tokenVaultKey;
        record.Status = nameof(PluginOAuthConnectionStatusKind.Connected);
        record.AccountDisplay = envelope.AccountDisplay;
        record.GrantedScopesJson = JsonSerializer.Serialize(envelope.Scopes, JsonOptions);
        record.AccessTokenExpiresAtUtc = envelope.AccessTokenExpiresAtUtc;
        record.RefreshTokenExpiresAtUtc = envelope.RefreshTokenExpiresAtUtc;
        record.LastErrorCode = string.Empty;
        record.LastErrorDescription = string.Empty;
        record.UpdatedAtUtc = timestamp;
    }

    private async Task<PluginOAuthTokenEnvelope> ReadTokenEnvelopeAsync(
        string tokenVaultKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tokenVaultKey))
        {
            throw new InvalidOperationException("OAuth token vault reference is missing.");
        }

        var json = await secretVault.GetAsync(tokenVaultKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("OAuth token payload was not found in the vault.");
        }

        return JsonSerializer.Deserialize<PluginOAuthTokenEnvelope>(json, JsonOptions)
               ?? throw new InvalidOperationException("OAuth token payload in the vault is invalid.");
    }

    private async Task MarkSessionFailedAsync(
        AppDbContext dbContext,
        PluginOAuthSessionRecord session,
        string errorCode,
        string errorDescription,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        session.Status = "Failed";
        session.ErrorCode = NormalizeErrorCode(errorCode);
        session.ErrorDescription = Truncate(errorDescription, 600);
        session.CompletedAtUtc = timestamp;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertOAuthConnectionErrorAsync(
        AppDbContext dbContext,
        PluginOAuthSessionRecord session,
        PluginOAuthConnectionStatusKind status,
        string errorCode,
        string errorDescription,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        var record = await dbContext.Set<PluginOAuthConnectionRecord>()
            .SingleOrDefaultAsync(item => item.ConnectionId == session.ConnectionId, cancellationToken);
        if (record is null)
        {
            record = new PluginOAuthConnectionRecord
            {
                ConnectionId = session.ConnectionId,
                PluginId = session.PluginId,
                ConnectionKey = session.ConnectionKey,
                ProviderKey = session.ProviderKey,
                CreatedAtUtc = timestamp
            };
            dbContext.Set<PluginOAuthConnectionRecord>().Add(record);
        }

        record.Status = status.ToString();
        record.LastErrorCode = NormalizeErrorCode(errorCode);
        record.LastErrorDescription = Truncate(errorDescription, 600);
        record.UpdatedAtUtc = timestamp;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task MarkReconnectRequiredAsync(
        AppDbContext dbContext,
        PluginOAuthConnectionRecord record,
        string errorCode,
        string errorDescription,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        record.Status = nameof(PluginOAuthConnectionStatusKind.ReconnectRequired);
        record.LastErrorCode = NormalizeErrorCode(errorCode);
        record.LastErrorDescription = Truncate(errorDescription, 600);
        record.UpdatedAtUtc = timestamp;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task DeleteSessionSecretAsync(
        PluginOAuthSessionRecord session,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(session.CodeVerifierVaultKey))
        {
            await secretVault.DeleteAsync(session.CodeVerifierVaultKey, cancellationToken);
        }
    }

    private static string BuildAuthorizationUrl(
        PluginOAuth2Descriptor descriptor,
        string clientId,
        string redirectUri,
        IReadOnlyList<string> requestedScopes,
        string state,
        string codeVerifier)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = string.Join(' ', requestedScopes),
            ["state"] = state
        };
        foreach (var parameter in descriptor.AuthorizationParameters)
        {
            if (!query.ContainsKey(parameter.Key))
            {
                query[parameter.Key] = parameter.Value;
            }
        }

        if (descriptor.UsesPkce)
        {
            query["code_challenge"] = HashBase64Url(codeVerifier);
            query["code_challenge_method"] = "S256";
        }

        return AppendQuery(descriptor.AuthorizationEndpoint, query);
    }

    private static string AppendQuery(Uri uri, IReadOnlyDictionary<string, string> query)
    {
        var separator = string.IsNullOrWhiteSpace(uri.Query) ? "?" : "&";
        var queryText = string.Join(
            "&",
            query.Select(item => $"{WebUtility.UrlEncode(item.Key)}={WebUtility.UrlEncode(item.Value)}"));
        return $"{uri}{separator}{queryText}";
    }

    private static string ResolveClientId(
        PluginOAuth2Descriptor descriptor,
        ConfigurationState settings)
    {
        var fromSettings = settings.GetText(PluginOAuthConnectionSettingKeys.ClientId);
        return string.IsNullOrWhiteSpace(fromSettings) ? descriptor.ClientId : fromSettings;
    }

    private static string ResolveRedirectUri(
        PluginOAuth2Descriptor descriptor,
        PluginOAuthStartRequest request,
        ConfigurationState settings,
        Uri requestBaseUri)
    {
        if (!string.IsNullOrWhiteSpace(request.RedirectUri))
        {
            return request.RedirectUri.Trim();
        }

        var configured = settings.GetText(PluginOAuthConnectionSettingKeys.RedirectUri);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return new Uri(requestBaseUri, descriptor.RedirectPath.TrimStart('/')).ToString();
    }

    private static Error? AddClientSecretIfConfigured(
        PluginOAuth2Descriptor descriptor,
        IDictionary<string, string> values)
    {
        var environmentVariable = descriptor.ClientSecretEnvironmentVariable?.Trim();
        if (string.IsNullOrWhiteSpace(environmentVariable))
        {
            return null;
        }

        var secret = ResolveEnvironmentVariable(environmentVariable);
        if (string.IsNullOrWhiteSpace(secret))
        {
            return Error.Validation(
                $"OAuth client secret environment variable '{environmentVariable}' is not set in the process, user, or machine environment scopes.",
                "plugins.oauth-client-secret-missing");
        }

        values["client_secret"] = secret;
        return null;
    }

    private static string? ResolveEnvironmentVariable(string environmentVariable)
    {
        foreach (var target in ClientSecretEnvironmentTargets)
        {
            var value = Environment.GetEnvironmentVariable(environmentVariable, target);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static void EnsureScopesGranted(
        IReadOnlyList<string> requestedScopes,
        IReadOnlyList<string> grantedScopes)
    {
        var granted = grantedScopes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = requestedScopes
            .Where(scope => !granted.Contains(scope))
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException($"OAuth connection is missing required scope(s): {string.Join(", ", missing)}.");
        }
    }

    private static IReadOnlyList<string> NormalizeScopes(IReadOnlyList<string> scopes)
        => scopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(scope => scope, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<string> SplitScopes(string scopes)
        => string.IsNullOrWhiteSpace(scopes)
            ? []
            : scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IReadOnlyList<string> DeserializeScopes(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<string>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static PluginOAuthConnectionStatusItem ToStatusItem(
        PluginOAuthConnectionRecord record,
        IReadOnlyDictionary<string, IReadOnlyList<string>> requiredScopesByConnectionKey)
    {
        var grantedScopes = DeserializeScopes(record.GrantedScopesJson);
        var status = Enum.TryParse<PluginOAuthConnectionStatusKind>(record.Status, out var parsedStatus)
            ? parsedStatus
            : PluginOAuthConnectionStatusKind.Error;
        var missingScopes = ResolveMissingScopes(record.ConnectionKey, status, grantedScopes, requiredScopesByConnectionKey);
        if (missingScopes.Count > 0)
        {
            status = PluginOAuthConnectionStatusKind.ReconnectRequired;
        }

        return new(
            new PluginConnectionId(record.ConnectionId),
            new PluginId(record.PluginId),
            new PluginConnectionKey(record.ConnectionKey),
            status,
            record.AccountDisplay,
            grantedScopes,
            record.AccessTokenExpiresAtUtc,
            record.RefreshTokenExpiresAtUtc,
            missingScopes.Count > 0 ? "oauth-scope-missing" : record.LastErrorCode,
            missingScopes.Count > 0
                ? $"Reconnect is required because the OAuth grant is missing required scope(s): {string.Join(", ", missingScopes)}."
                : record.LastErrorDescription,
            record.UpdatedAtUtc);
    }

    private static IReadOnlyList<string> ResolveMissingScopes(
        string connectionKey,
        PluginOAuthConnectionStatusKind status,
        IReadOnlyList<string> grantedScopes,
        IReadOnlyDictionary<string, IReadOnlyList<string>> requiredScopesByConnectionKey)
    {
        if (status != PluginOAuthConnectionStatusKind.Connected ||
            !requiredScopesByConnectionKey.TryGetValue(connectionKey, out var requiredScopes) ||
            requiredScopes.Count == 0)
        {
            return [];
        }

        var granted = grantedScopes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return requiredScopes
            .Where(scope => !granted.Contains(scope))
            .ToArray();
    }

    private static string NormalizeReturnPath(string? returnPath)
    {
        if (string.IsNullOrWhiteSpace(returnPath))
        {
            return "/plugins";
        }

        var normalized = returnPath.Trim();
        if (normalized[0] != '/' || normalized.StartsWith("//", StringComparison.Ordinal))
        {
            return "/plugins";
        }

        return normalized;
    }

    private static Uri BuildReturnUri(
        string returnPath,
        string oauthStatus,
        string reason,
        Guid connectionId)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["oauth"] = oauthStatus,
            ["connectionId"] = connectionId.ToString("D")
        };
        if (!string.IsNullOrWhiteSpace(reason))
        {
            query["reason"] = NormalizeErrorCode(reason);
        }

        var separator = returnPath.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var queryText = string.Join(
            "&",
            query.Select(item => $"{WebUtility.UrlEncode(item.Key)}={WebUtility.UrlEncode(item.Value)}"));
        return new Uri($"{returnPath}{separator}{queryText}", UriKind.Relative);
    }

    private static string ResolveProviderKey(PluginId pluginId, PluginConnectionKey connectionKey)
        => $"{pluginId.Value}:{connectionKey.Value}";

    private static string ResolveProviderKey(PluginConnectionKey connectionKey)
        => connectionKey.Value;

    private static string ResolveTokenVaultKey(Guid connectionId)
        => $"plugins/oauth/tokens/{connectionId:N}";

    private static string CreateRandomBase64Url(int byteCount)
        => Base64Url(RandomNumberGenerator.GetBytes(byteCount));

    private static string HashBase64Url(string value)
        => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(value)));

    private static string Base64Url(byte[] value)
        => Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string NormalizeErrorCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "oauth-error";
        }

        var sanitized = new string(code.Trim()
            .Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.' ? character : '-')
            .ToArray());
        return Truncate(sanitized, 120);
    }

    private static string RedactProviderError(string errorText)
    {
        if (string.IsNullOrWhiteSpace(errorText))
        {
            return string.Empty;
        }

        var redacted = errorText.Replace(Environment.NewLine, " ", StringComparison.Ordinal);
        return Truncate(redacted, 400);
    }

    private static string Truncate(string value, int maxLength)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Length <= maxLength
                ? value
                : value[..maxLength];

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record OAuthTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; init; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }

        [JsonPropertyName("scope")]
        public string Scope { get; init; } = string.Empty;

        [JsonPropertyName("token_type")]
        public string TokenType { get; init; } = string.Empty;
    }
}

public sealed class PluginOAuth2Capability(
    PluginId pluginId,
    PluginOAuthService oauthService) : IPluginOAuth2Capability
{
    public ValueTask<PluginOAuth2TokenSnapshot> GetAccessTokenAsync(
        PluginConnectionId connectionId,
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken = default)
        => oauthService.GetAccessTokenAsync(pluginId, connectionId, scopes, cancellationToken);
}
