using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.DependencyInjection;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Unit.Infrastructure;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class DatabaseConfigurationTests
{
    [Fact]
    public void DatabaseOptions_DisablesEntityFrameworkConsoleLogging_ByDefault()
    {
        var options = new DatabaseOptions();

        Assert.False(options.EnableEntityFrameworkConsoleLogging);
    }

    [Fact]
    public void DatabaseOptions_BindsEntityFrameworkConsoleLoggingSwitch()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:EnableEntityFrameworkConsoleLogging"] = "true"
            })
            .Build();

        var options = configuration.GetSection("Database").Get<DatabaseOptions>();

        Assert.NotNull(options);
        Assert.True(options!.EnableEntityFrameworkConsoleLogging);
    }

    [Fact]
    public void DatabasePasswordFileConfiguration_AppliesPasswordToPostgreSqlConnectionString()
    {
        var temporaryDirectory = TestFileSystem.CreateTemporaryRoot("database-password-file");
        try
        {
            var passwordPath = Path.Combine(temporaryDirectory, "db-password");
            File.WriteAllText(passwordPath, "compose-secret\n");
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ConnectionString"] = "Host=db;Database=candoitall;Username=candoitall",
                    ["Database:PasswordFile"] = passwordPath
                })
                .Build();

            DatabasePasswordFileConfiguration.Apply(configuration, temporaryDirectory);

            var connectionString = new NpgsqlConnectionStringBuilder(
                configuration["Database:ConnectionString"]);
            Assert.Equal("compose-secret", connectionString.Password);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(temporaryDirectory);
        }
    }

    [Fact]
    public void DatabasePasswordFileConfiguration_RejectsMissingPasswordFile()
    {
        var temporaryDirectory = TestFileSystem.CreateTemporaryRoot("database-password-file");
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ConnectionString"] = "Host=db;Database=candoitall;Username=candoitall",
                    ["Database:PasswordFile"] = "missing-password"
                })
                .Build();

            Assert.Throws<FileNotFoundException>(() =>
            {
                DatabasePasswordFileConfiguration.Apply(configuration, temporaryDirectory);
            });
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(temporaryDirectory);
        }
    }

    [Fact]
    public void DatabasePasswordFileConfiguration_RejectsOversizedPasswordFileWithoutDisclosingContent()
    {
        var temporaryDirectory = TestFileSystem.CreateTemporaryRoot("database-password-file");
        try
        {
            string secret = new('s', 4097);
            var passwordPath = Path.Combine(temporaryDirectory, "db-password");
            File.WriteAllText(passwordPath, secret);
            IConfiguration configuration = CreatePasswordFileConfiguration(passwordPath);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                DatabasePasswordFileConfiguration.Apply(configuration, temporaryDirectory));

            Assert.Contains("4096-byte limit", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, exception.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(temporaryDirectory);
        }
    }

    [Fact]
    public void DatabasePasswordFileConfiguration_RejectsNulWithoutDisclosingContent()
    {
        var temporaryDirectory = TestFileSystem.CreateTemporaryRoot("database-password-file");
        try
        {
            const string secret = "secret-before-nul\0secret-after-nul";
            var passwordPath = Path.Combine(temporaryDirectory, "db-password");
            File.WriteAllText(passwordPath, secret);
            IConfiguration configuration = CreatePasswordFileConfiguration(passwordPath);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                DatabasePasswordFileConfiguration.Apply(configuration, temporaryDirectory));

            Assert.Contains("NUL", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, exception.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(temporaryDirectory);
        }
    }

    [Fact]
    public void DatabasePasswordFileConfiguration_RejectsSymbolicLink()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var temporaryDirectory = TestFileSystem.CreateTemporaryRoot("database-password-file");
        try
        {
            var targetPath = Path.Combine(temporaryDirectory, "target-password");
            var passwordPath = Path.Combine(temporaryDirectory, "db-password");
            File.WriteAllText(targetPath, "must-not-be-read");
            File.CreateSymbolicLink(passwordPath, targetPath);
            IConfiguration configuration = CreatePasswordFileConfiguration(passwordPath);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                DatabasePasswordFileConfiguration.Apply(configuration, temporaryDirectory));

            Assert.Contains("cannot be a link", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("must-not-be-read", exception.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(temporaryDirectory);
        }
    }

    [Fact]
    public async Task AddCanDoItAllInfrastructure_UsesInMemoryProvider_WhenConfigured()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-inmemory-tests");
        var profile = testEnvironment.CreateInMemoryProfile("unit", "rpi3-validation");
        var configuration = TestApplicationBootstrap.BuildConfiguration(
            profile,
            new Dictionary<string, string?>
            {
                ["ControlPlane:RootPath"] = testEnvironment.ControlPlaneRootPath
            });

        var services = new ServiceCollection();
        TestApplicationBootstrap.ConfigureDefaultServices(
            services,
            configuration,
            testEnvironment.CreateHostEnvironment("CanDoItAll.Tests.Unit"));

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        await using var scope = provider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        Assert.Equal("Microsoft.EntityFrameworkCore.InMemory", dbContext.Database.ProviderName);
    }

    [Fact]
    public void AppDbContextFactory_UsesInMemoryProvider_WhenConfiguredViaEnvironment()
    {
        const string providerVariable = "CANDOITALL_DATABASE_PROVIDER";
        const string connectionVariable = "CANDOITALL_DATABASE_CONNECTION";
        var originalProvider = Environment.GetEnvironmentVariable(providerVariable);
        var originalConnection = Environment.GetEnvironmentVariable(connectionVariable);

        try
        {
            Environment.SetEnvironmentVariable(providerVariable, "InMemory");
            Environment.SetEnvironmentVariable(connectionVariable, "factory-validation");

            using var context = new AppDbContextFactory().CreateDbContext([]);
            Assert.Equal("Microsoft.EntityFrameworkCore.InMemory", context.Database.ProviderName);
        }
        finally
        {
            Environment.SetEnvironmentVariable(providerVariable, originalProvider);
            Environment.SetEnvironmentVariable(connectionVariable, originalConnection);
        }
    }

    [Fact]
    public void AppDbContextFactory_rejects_unknown_provider_when_configured_via_environment()
    {
        const string providerVariable = "CANDOITALL_DATABASE_PROVIDER";
        const string connectionVariable = "CANDOITALL_DATABASE_CONNECTION";
        var originalProvider = Environment.GetEnvironmentVariable(providerVariable);
        var originalConnection = Environment.GetEnvironmentVariable(connectionVariable);

        try
        {
            Environment.SetEnvironmentVariable(providerVariable, string.Concat("Sql", "ite"));
            Environment.SetEnvironmentVariable(connectionVariable, "Data Source=:memory:");

            var ex = Assert.Throws<InvalidOperationException>(() => new AppDbContextFactory().CreateDbContext([]));

            Assert.Contains("Unsupported database provider", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(providerVariable, originalProvider);
            Environment.SetEnvironmentVariable(connectionVariable, originalConnection);
        }
    }

    [Fact]
    public void AppDbContextFactory_UsesPostgreSqlMigrationsAssembly_WhenConfiguredViaEnvironment()
    {
        const string providerVariable = "CANDOITALL_DATABASE_PROVIDER";
        const string connectionVariable = "CANDOITALL_DATABASE_CONNECTION";
        var originalProvider = Environment.GetEnvironmentVariable(providerVariable);
        var originalConnection = Environment.GetEnvironmentVariable(connectionVariable);

        try
        {
            Environment.SetEnvironmentVariable(providerVariable, "PostgreSql");
            Environment.SetEnvironmentVariable(
                connectionVariable,
                "Host=127.0.0.1;Database=candoitall;Username=postgres;Password=postgres");

            using var context = new AppDbContextFactory().CreateDbContext([]);
            Assert.Equal(
                "CanDoItAll.Migrations.PostgreSql",
                GetRelationalOptions(context).MigrationsAssembly);
        }
        finally
        {
            Environment.SetEnvironmentVariable(providerVariable, originalProvider);
            Environment.SetEnvironmentVariable(connectionVariable, originalConnection);
        }
    }

    private static RelationalOptionsExtension GetRelationalOptions(AppDbContext context)
    {
        var dbContextOptions = context.GetService<IDbContextOptions>();
        return Assert.Single(dbContextOptions.Extensions.OfType<RelationalOptionsExtension>());
    }

    private static IConfiguration CreatePasswordFileConfiguration(string passwordPath)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = "Host=db;Database=candoitall;Username=candoitall",
                ["Database:PasswordFile"] = passwordPath
            })
            .Build();
}
