using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Plugins;

public static class PluginSchemaInitializer
{
    public static async Task EnsureAsync(DbContext dbContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        var provider = dbContext.Database.ProviderName ?? string.Empty;
        if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            await EnsurePostgreSqlAsync(dbContext, cancellationToken);
            return;
        }

        await EnsureSqliteAsync(dbContext, cancellationToken);
    }

    private static async Task EnsureSqliteAsync(DbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS Plugins_CapabilityGrants (
                Id TEXT NOT NULL CONSTRAINT PK_Plugins_CapabilityGrants PRIMARY KEY,
                PluginId TEXT NOT NULL,
                Capability INTEGER NOT NULL,
                RecipeId TEXT NOT NULL,
                ScopeKind TEXT NOT NULL,
                ScopeKey TEXT NOT NULL,
                State TEXT NOT NULL,
                RiskKind TEXT NOT NULL,
                Reason TEXT NOT NULL,
                UpdatedBy TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                ConcurrencyToken TEXT NOT NULL
            );
            """,
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS Plugins_Connections (
                Id TEXT NOT NULL CONSTRAINT PK_Plugins_Connections PRIMARY KEY,
                PluginId TEXT NOT NULL,
                ConnectionKey TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                SettingsJson TEXT NOT NULL,
                IsEnabled INTEGER NOT NULL,
                HealthStatus TEXT NOT NULL,
                UpdatedBy TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                ConcurrencyToken TEXT NOT NULL
            );
            """,
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS Plugins_OAuthConnections (
                Id TEXT NOT NULL CONSTRAINT PK_Plugins_OAuthConnections PRIMARY KEY,
                ConnectionId TEXT NOT NULL,
                PluginId TEXT NOT NULL,
                ConnectionKey TEXT NOT NULL,
                ProviderKey TEXT NOT NULL,
                TokenVaultKey TEXT NOT NULL,
                Status TEXT NOT NULL,
                AccountDisplay TEXT NOT NULL,
                GrantedScopesJson TEXT NOT NULL,
                AccessTokenExpiresAtUtc TEXT NULL,
                RefreshTokenExpiresAtUtc TEXT NULL,
                LastErrorCode TEXT NOT NULL,
                LastErrorDescription TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                ConcurrencyToken TEXT NOT NULL
            );
            """,
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS Plugins_OAuthSessions (
                Id TEXT NOT NULL CONSTRAINT PK_Plugins_OAuthSessions PRIMARY KEY,
                StateHash TEXT NOT NULL,
                PluginId TEXT NOT NULL,
                ConnectionId TEXT NOT NULL,
                ConnectionKey TEXT NOT NULL,
                ProviderKey TEXT NOT NULL,
                CodeVerifierVaultKey TEXT NOT NULL,
                RedirectUri TEXT NOT NULL,
                ReturnPath TEXT NOT NULL,
                RequestedScopesJson TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                ExpiresAtUtc TEXT NOT NULL,
                CompletedAtUtc TEXT NULL,
                Status TEXT NOT NULL,
                ErrorCode TEXT NOT NULL,
                ErrorDescription TEXT NOT NULL,
                ConcurrencyToken TEXT NOT NULL
            );
            """,
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Plugins_CapabilityGrants_PluginId_Capability_RecipeId_ScopeKind_ScopeKey
            ON Plugins_CapabilityGrants (PluginId, Capability, RecipeId, ScopeKind, ScopeKey);
            """,
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS IX_Plugins_CapabilityGrants_PluginId_State_UpdatedAtUtc
            ON Plugins_CapabilityGrants (PluginId, State, UpdatedAtUtc);
            """,
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS IX_Plugins_Connections_PluginId_ConnectionKey
            ON Plugins_Connections (PluginId, ConnectionKey);
            """,
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Plugins_OAuthConnections_ConnectionId
            ON Plugins_OAuthConnections (ConnectionId);
            """,
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS IX_Plugins_OAuthConnections_PluginId_ConnectionKey
            ON Plugins_OAuthConnections (PluginId, ConnectionKey);
            """,
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Plugins_OAuthSessions_StateHash
            ON Plugins_OAuthSessions (StateHash);
            """,
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS IX_Plugins_OAuthSessions_PluginId_ConnectionId_Status
            ON Plugins_OAuthSessions (PluginId, ConnectionId, Status);
            """,
            cancellationToken);
    }

    private static async Task EnsurePostgreSqlAsync(DbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "Plugins_CapabilityGrants" (
                "Id" uuid NOT NULL CONSTRAINT "PK_Plugins_CapabilityGrants" PRIMARY KEY,
                "PluginId" character varying(180) NOT NULL,
                "Capability" integer NOT NULL,
                "RecipeId" character varying(180) NOT NULL,
                "ScopeKind" character varying(40) NOT NULL,
                "ScopeKey" character varying(180) NOT NULL,
                "State" character varying(40) NOT NULL,
                "RiskKind" character varying(40) NOT NULL,
                "Reason" character varying(600) NOT NULL,
                "UpdatedBy" character varying(180) NOT NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NOT NULL,
                "ConcurrencyToken" uuid NOT NULL
            );
            """,
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "Plugins_Connections" (
                "Id" uuid NOT NULL CONSTRAINT "PK_Plugins_Connections" PRIMARY KEY,
                "PluginId" character varying(180) NOT NULL,
                "ConnectionKey" character varying(180) NOT NULL,
                "DisplayName" character varying(240) NOT NULL,
                "SettingsJson" TEXT NOT NULL,
                "IsEnabled" boolean NOT NULL,
                "HealthStatus" character varying(180) NOT NULL,
                "UpdatedBy" character varying(180) NOT NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NOT NULL,
                "ConcurrencyToken" uuid NOT NULL
            );
            """,
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "Plugins_OAuthConnections" (
                "Id" uuid NOT NULL CONSTRAINT "PK_Plugins_OAuthConnections" PRIMARY KEY,
                "ConnectionId" uuid NOT NULL,
                "PluginId" character varying(180) NOT NULL,
                "ConnectionKey" character varying(180) NOT NULL,
                "ProviderKey" character varying(180) NOT NULL,
                "TokenVaultKey" character varying(260) NOT NULL,
                "Status" character varying(40) NOT NULL,
                "AccountDisplay" character varying(320) NOT NULL,
                "GrantedScopesJson" TEXT NOT NULL,
                "AccessTokenExpiresAtUtc" timestamp with time zone NULL,
                "RefreshTokenExpiresAtUtc" timestamp with time zone NULL,
                "LastErrorCode" character varying(160) NOT NULL,
                "LastErrorDescription" character varying(600) NOT NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NOT NULL,
                "ConcurrencyToken" uuid NOT NULL
            );
            """,
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "Plugins_OAuthSessions" (
                "Id" uuid NOT NULL CONSTRAINT "PK_Plugins_OAuthSessions" PRIMARY KEY,
                "StateHash" character varying(128) NOT NULL,
                "PluginId" character varying(180) NOT NULL,
                "ConnectionId" uuid NOT NULL,
                "ConnectionKey" character varying(180) NOT NULL,
                "ProviderKey" character varying(180) NOT NULL,
                "CodeVerifierVaultKey" character varying(260) NOT NULL,
                "RedirectUri" character varying(1000) NOT NULL,
                "ReturnPath" character varying(1000) NOT NULL,
                "RequestedScopesJson" TEXT NOT NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "ExpiresAtUtc" timestamp with time zone NOT NULL,
                "CompletedAtUtc" timestamp with time zone NULL,
                "Status" character varying(40) NOT NULL,
                "ErrorCode" character varying(160) NOT NULL,
                "ErrorDescription" character varying(600) NOT NULL,
                "ConcurrencyToken" uuid NOT NULL
            );
            """,
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Plugins_CapabilityGrants_PluginId_Capability_RecipeId_ScopeKind_ScopeKey"
            ON "Plugins_CapabilityGrants" ("PluginId", "Capability", "RecipeId", "ScopeKind", "ScopeKey");
            """,
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_Plugins_CapabilityGrants_PluginId_State_UpdatedAtUtc"
            ON "Plugins_CapabilityGrants" ("PluginId", "State", "UpdatedAtUtc");
            """,
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_Plugins_Connections_PluginId_ConnectionKey"
            ON "Plugins_Connections" ("PluginId", "ConnectionKey");
            """,
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Plugins_OAuthConnections_ConnectionId"
            ON "Plugins_OAuthConnections" ("ConnectionId");
            """,
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_Plugins_OAuthConnections_PluginId_ConnectionKey"
            ON "Plugins_OAuthConnections" ("PluginId", "ConnectionKey");
            """,
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Plugins_OAuthSessions_StateHash"
            ON "Plugins_OAuthSessions" ("StateHash");
            """,
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_Plugins_OAuthSessions_PluginId_ConnectionId_Status"
            ON "Plugins_OAuthSessions" ("PluginId", "ConnectionId", "Status");
            """,
            cancellationToken);
    }
}
