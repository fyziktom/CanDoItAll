using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Plugins;

public sealed class PluginCapabilityGrantRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string PluginId { get; set; } = string.Empty;

    public int Capability { get; set; }

    public string RecipeId { get; set; } = string.Empty;

    public string ScopeKind { get; set; } = "Plugin";

    public string ScopeKey { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public string RiskKind { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string UpdatedBy { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class PluginConnectionRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string PluginId { get; set; } = string.Empty;

    public string ConnectionKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string SettingsJson { get; set; } = "{}";

    public bool IsEnabled { get; set; } = true;

    public string HealthStatus { get; set; } = "Not checked";

    public string UpdatedBy { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class PluginOAuthConnectionRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ConnectionId { get; set; }

    public string PluginId { get; set; } = string.Empty;

    public string ConnectionKey { get; set; } = string.Empty;

    public string ProviderKey { get; set; } = string.Empty;

    public string TokenVaultKey { get; set; } = string.Empty;

    public string Status { get; set; } = nameof(PluginOAuthConnectionStatusKind.NotConnected);

    public string AccountDisplay { get; set; } = string.Empty;

    public string GrantedScopesJson { get; set; } = "[]";

    public DateTimeOffset? AccessTokenExpiresAtUtc { get; set; }

    public DateTimeOffset? RefreshTokenExpiresAtUtc { get; set; }

    public string LastErrorCode { get; set; } = string.Empty;

    public string LastErrorDescription { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class PluginOAuthSessionRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string StateHash { get; set; } = string.Empty;

    public string PluginId { get; set; } = string.Empty;

    public Guid ConnectionId { get; set; }

    public string ConnectionKey { get; set; } = string.Empty;

    public string ProviderKey { get; set; } = string.Empty;

    public string CodeVerifierVaultKey { get; set; } = string.Empty;

    public string RedirectUri { get; set; } = string.Empty;

    public string ReturnPath { get; set; } = "/plugins";

    public string RequestedScopesJson { get; set; } = "[]";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public string Status { get; set; } = "Pending";

    public string ErrorCode { get; set; } = string.Empty;

    public string ErrorDescription { get; set; } = string.Empty;

    public Guid ConcurrencyToken { get; set; }
}

internal sealed class PluginCapabilityGrantRecordConfiguration : IEntityTypeConfiguration<PluginCapabilityGrantRecord>
{
    public void Configure(EntityTypeBuilder<PluginCapabilityGrantRecord> builder)
    {
        builder.ToTable("Plugins_CapabilityGrants");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.PluginId).HasMaxLength(180).IsRequired();
        builder.Property(item => item.RecipeId).HasMaxLength(180).IsRequired();
        builder.Property(item => item.ScopeKind).HasMaxLength(40).IsRequired();
        builder.Property(item => item.ScopeKey).HasMaxLength(180).IsRequired();
        builder.Property(item => item.State).HasMaxLength(40).IsRequired();
        builder.Property(item => item.RiskKind).HasMaxLength(40).IsRequired();
        builder.Property(item => item.Reason).HasMaxLength(600).IsRequired();
        builder.Property(item => item.UpdatedBy).HasMaxLength(180).IsRequired();
        builder.HasIndex(item => new
        {
            item.PluginId,
            item.Capability,
            item.RecipeId,
            item.ScopeKind,
            item.ScopeKey
        }).IsUnique();
        builder.HasIndex(item => new
        {
            item.PluginId,
            item.State,
            item.UpdatedAtUtc
        });
    }
}

internal sealed class PluginConnectionRecordConfiguration : IEntityTypeConfiguration<PluginConnectionRecord>
{
    public void Configure(EntityTypeBuilder<PluginConnectionRecord> builder)
    {
        builder.ToTable("Plugins_Connections");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.PluginId).HasMaxLength(180).IsRequired();
        builder.Property(item => item.ConnectionKey).HasMaxLength(180).IsRequired();
        builder.Property(item => item.DisplayName).HasMaxLength(240).IsRequired();
        builder.Property(item => item.SettingsJson).HasColumnType("TEXT").IsRequired();
        builder.Property(item => item.HealthStatus).HasMaxLength(180).IsRequired();
        builder.Property(item => item.UpdatedBy).HasMaxLength(180).IsRequired();
        builder.HasIndex(item => new
        {
            item.PluginId,
            item.ConnectionKey,
            item.DisplayName
        });
        builder.HasIndex(item => new
        {
            item.PluginId,
            item.ConnectionKey
        });
    }
}

internal sealed class PluginOAuthConnectionRecordConfiguration : IEntityTypeConfiguration<PluginOAuthConnectionRecord>
{
    public void Configure(EntityTypeBuilder<PluginOAuthConnectionRecord> builder)
    {
        builder.ToTable("Plugins_OAuthConnections");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.PluginId).HasMaxLength(180).IsRequired();
        builder.Property(item => item.ConnectionKey).HasMaxLength(180).IsRequired();
        builder.Property(item => item.ProviderKey).HasMaxLength(180).IsRequired();
        builder.Property(item => item.TokenVaultKey).HasMaxLength(260).IsRequired();
        builder.Property(item => item.Status).HasMaxLength(40).IsRequired();
        builder.Property(item => item.AccountDisplay).HasMaxLength(320).IsRequired();
        builder.Property(item => item.GrantedScopesJson).HasColumnType("TEXT").IsRequired();
        builder.Property(item => item.LastErrorCode).HasMaxLength(160).IsRequired();
        builder.Property(item => item.LastErrorDescription).HasMaxLength(600).IsRequired();
        builder.HasIndex(item => item.ConnectionId).IsUnique();
        builder.HasIndex(item => new
        {
            item.PluginId,
            item.ConnectionKey
        });
    }
}

internal sealed class PluginOAuthSessionRecordConfiguration : IEntityTypeConfiguration<PluginOAuthSessionRecord>
{
    public void Configure(EntityTypeBuilder<PluginOAuthSessionRecord> builder)
    {
        builder.ToTable("Plugins_OAuthSessions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.StateHash).HasMaxLength(128).IsRequired();
        builder.Property(item => item.PluginId).HasMaxLength(180).IsRequired();
        builder.Property(item => item.ConnectionKey).HasMaxLength(180).IsRequired();
        builder.Property(item => item.ProviderKey).HasMaxLength(180).IsRequired();
        builder.Property(item => item.CodeVerifierVaultKey).HasMaxLength(260).IsRequired();
        builder.Property(item => item.RedirectUri).HasMaxLength(1000).IsRequired();
        builder.Property(item => item.ReturnPath).HasMaxLength(1000).IsRequired();
        builder.Property(item => item.RequestedScopesJson).HasColumnType("TEXT").IsRequired();
        builder.Property(item => item.Status).HasMaxLength(40).IsRequired();
        builder.Property(item => item.ErrorCode).HasMaxLength(160).IsRequired();
        builder.Property(item => item.ErrorDescription).HasMaxLength(600).IsRequired();
        builder.HasIndex(item => item.StateHash).IsUnique();
        builder.HasIndex(item => new
        {
            item.PluginId,
            item.ConnectionId,
            item.Status
        });
    }
}
