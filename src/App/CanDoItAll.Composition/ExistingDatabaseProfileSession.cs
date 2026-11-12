using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CanDoItAll.Composition;

internal sealed class ExistingDatabaseProfileSession(ServiceProvider secrets, ExistingDatabaseProfileReader reader,
    ExistingDatabaseProfileSnapshot snapshot, IControlPlanePathResolver paths, DurableFileWriter files) : IAsyncDisposable {
    internal ExistingDatabaseProfileReader Reader { get; } = reader;
    internal ExistingDatabaseProfileSnapshot Snapshot { get; } = snapshot;
    internal IControlPlanePathResolver Paths { get; } = paths;
    internal DurableFileWriter Files { get; } = files;
    internal ResolvedDatabaseProfile Profile { get; } = snapshot.CreateResolvedProfile();
    internal string ConnectionString { get; } = new NpgsqlConnectionStringBuilder(snapshot.CreateResolvedProfile().ConnectionString) {
        Pooling = false, Timeout = 5, CommandTimeout = 5, IncludeErrorDetail = false
    }.ConnectionString;

    internal async Task RequireOriginalAsync(CancellationToken cancellationToken)
        => Snapshot.RequireSame(await Reader.ReadAsync(Snapshot.ProfileId, cancellationToken));

    internal static async Task<ExistingDatabaseProfileSession> OpenAsync(IConfiguration configuration, IHostEnvironment host,
        Guid profileId, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(host);
        var configured = configuration.GetSection("ControlPlane").Get<ControlPlaneOptions>() ?? new();
        var files = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory());
        var paths = new ControlPlanePathResolver(Options.Create(configured), host, files);
        var services = new ServiceCollection();
        DataProtectionKeyRingProtection.ConfigureExistingReader(services.AddDataProtection(), configuration, host,
            ControlPlanePathDefaults.ResolveDataProtectionKeysPath(host, configured));
        var secrets = services.BuildServiceProvider();
        try {
            var reader = new ExistingDatabaseProfileReader(paths, files,
                new ControlPlaneSecretProtector(secrets.GetRequiredService<IDataProtectionProvider>()));
            return new(secrets, reader, await reader.ReadAsync(profileId, cancellationToken), paths, files);
        } catch (Exception primary) {
            try {
                await secrets.DisposeAsync();
            } catch (Exception release) {
                throw new AggregateException("Existing-profile composition and secret-reader release both failed.", primary, release);
            }
            throw;
        }
    }

    public ValueTask DisposeAsync() => secrets.DisposeAsync();
}
