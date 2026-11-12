using Npgsql;

namespace CanDoItAll.Infrastructure.ControlPlane;

internal static class DatabaseProfileConnection {
    internal static ResolvedDatabaseProfile Resolve(DatabaseProfileRecord profile,
        DatabaseProfileResolutionSource resolutionSource, IControlPlaneSecretProtector secrets) {
        HostBoundPathPolicy.ResolveRequired(profile.Storage.WorkspacePath, "database profile workspace");
        string connection = profile.ProviderKind switch {
            DatabaseProviderKind.PostgreSql => PostgreSql(profile, secrets),
            DatabaseProviderKind.InMemory => profile.InMemory?.DatabaseName
                ?? throw new InvalidOperationException("In-memory profile is missing a database name."),
            _ => throw new InvalidOperationException($"Unsupported provider '{profile.ProviderKind}'.")
        };
        return new ResolvedDatabaseProfile(profile, resolutionSource, connection);
    }

    private static string PostgreSql(DatabaseProfileRecord profile, IControlPlaneSecretProtector secrets) {
        var descriptor = profile.PostgreSql
            ?? throw new InvalidOperationException("PostgreSQL profile is missing connection metadata.");
        var builder = new NpgsqlConnectionStringBuilder {
            Host = descriptor.Host,
            Port = descriptor.Port,
            Database = descriptor.DatabaseName,
            Username = descriptor.Username
        };
        if (!string.IsNullOrWhiteSpace(descriptor.EncryptedPassword)) {
            builder.Password = secrets.Unprotect(descriptor.EncryptedPassword);
        }
        return builder.ConnectionString;
    }
}
