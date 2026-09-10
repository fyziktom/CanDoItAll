using System.Reflection;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Migrations.PostgreSql;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CanDoItAll.Tests.Unit.Infrastructure;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class AppDbContextMigrationAuthorityTests {
    [Fact]
    public void Compatibility_factory_loads_the_complete_canonical_migration_model() {
        using var modelRegistryScope = AppDbContextModelRegistry.UseIsolatedAssembliesForTesting();
        const string providerVariable = "CANDOITALL_DATABASE_PROVIDER";
        const string connectionVariable = "CANDOITALL_DATABASE_CONNECTION";
        const string migrationsConnectionVariable = "CANDOITALL_MIGRATIONS_POSTGRES_CONNECTION";
        var previousProvider = Environment.GetEnvironmentVariable(providerVariable);
        var previousConnection = Environment.GetEnvironmentVariable(connectionVariable);
        var previousMigrationsConnection = Environment.GetEnvironmentVariable(migrationsConnectionVariable);
        try {
            Environment.SetEnvironmentVariable(providerVariable, "PostgreSql");
            const string modelConnection = "Host=127.0.0.1;Database=migration_authority_model_only;Username=postgres;Password=postgres";
            Environment.SetEnvironmentVariable(connectionVariable, modelConnection);
            Environment.SetEnvironmentVariable(migrationsConnectionVariable, modelConnection);
            using var compatibilityContext = new AppDbContextFactory().CreateDbContext([]);
            var expectedAssemblies = ModuleAssemblies.All.Distinct().Where(assembly => assembly.GetTypes().Any(type =>
                    type is { IsAbstract: false, IsInterface: false } && !type.ContainsGenericParameters &&
                    type.GetInterfaces().Any(contract => contract.IsGenericType &&
                        contract.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>))))
                .OrderBy(assembly => assembly.FullName, StringComparer.Ordinal).ToArray();
            Assert.Equal(expectedAssemblies, AppDbContextModelRegistry.Assemblies);
            var compatibilityModel = compatibilityContext.GetService<IDesignTimeModel>().Model;

            using var canonicalContext = new PostgreSqlAppDbContextFactory().CreateDbContext([]);
            var canonicalModel = canonicalContext.GetService<IDesignTimeModel>().Model;
            Assert.Equal(canonicalModel.GetEntityTypes().Select(entity => entity.Name).Order(StringComparer.Ordinal),
                compatibilityModel.GetEntityTypes().Select(entity => entity.Name).Order(StringComparer.Ordinal));
            Assert.False(canonicalContext.GetService<IMigrationsModelDiffer>().HasDifferences(
                canonicalModel.GetRelationalModel(), compatibilityModel.GetRelationalModel()));
        } finally {
            Environment.SetEnvironmentVariable(providerVariable, previousProvider);
            Environment.SetEnvironmentVariable(connectionVariable, previousConnection);
            Environment.SetEnvironmentVariable(migrationsConnectionVariable, previousMigrationsConnection);
        }
    }

    [Fact]
    public void Compatibility_factory_rejects_a_missing_catalog_type() {
        using var modelRegistryScope = AppDbContextModelRegistry.UseIsolatedAssembliesForTesting();
        var failure = Assert.Throws<InvalidOperationException>(() =>
            AppDbContextFactory.ConfigureModuleAssemblies(new CatalogAssembly(null)));

        Assert.Contains("CanDoItAll.Composition.ModuleAssemblies", failure.Message, StringComparison.Ordinal);
        Assert.Contains("CompositionCatalogFixture", failure.Message, StringComparison.Ordinal);
        Assert.Empty(AppDbContextModelRegistry.Assemblies);
    }

    [Theory]
    [InlineData(typeof(MissingListCatalog))]
    [InlineData(typeof(NullListCatalog))]
    [InlineData(typeof(EmptyListCatalog))]
    [InlineData(typeof(NullEntryCatalog))]
    [InlineData(typeof(WrongListTypeCatalog))]
    public void Compatibility_factory_rejects_an_invalid_catalog_without_reusing_a_previous_partial_model(Type catalogType) {
        using var modelRegistryScope = AppDbContextModelRegistry.UseIsolatedAssembliesForTesting();
        AppDbContextModelRegistry.ConfigureAssemblies([typeof(Project).Assembly]);
        var previousAssemblies = AppDbContextModelRegistry.Assemblies;
        var previousModelKey = AppDbContextModelRegistry.ModelCacheKey;

        var failure = Assert.Throws<InvalidOperationException>(() =>
            AppDbContextFactory.ConfigureModuleAssemblies(new CatalogAssembly(catalogType)));

        Assert.Contains("CanDoItAll.Composition.ModuleAssemblies.All", failure.Message, StringComparison.Ordinal);
        Assert.Same(previousAssemblies, AppDbContextModelRegistry.Assemblies);
        Assert.Equal(previousModelKey, AppDbContextModelRegistry.ModelCacheKey);
    }

    private sealed class CatalogAssembly(Type? catalogType) : Assembly {
        public override string FullName => "CompositionCatalogFixture";

        public override Type? GetType(string name, bool throwOnError, bool ignoreCase) => catalogType;
    }

    private sealed class MissingListCatalog;

    private static class NullListCatalog {
        public static readonly Assembly[]? All = null;
    }

    private static class EmptyListCatalog {
        public static readonly Assembly[] All = [];
    }

    private static class NullEntryCatalog {
        public static readonly Assembly[] All = [typeof(Project).Assembly, null!];
    }

    private static class WrongListTypeCatalog {
        public static readonly string All = string.Empty;
    }
}
