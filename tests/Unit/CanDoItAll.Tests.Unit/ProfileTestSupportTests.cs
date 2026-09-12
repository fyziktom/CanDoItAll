using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit.Infrastructure;

public sealed class ProfileTestSupportTests
{
    [Fact]
    [Trait("RequiresHostDocker", "true")]
    public async Task PostgreSql_profiles_create_isolated_database_and_storage_roots()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-profile-tests");
        var alphaProfile = testEnvironment.CreatePostgreSqlProfile("alpha");
        var betaProfile = testEnvironment.CreatePostgreSqlProfile("beta");

        Assert.NotEqual(alphaProfile.ConnectionString, betaProfile.ConnectionString);
        Assert.NotEqual(alphaProfile.WorkspaceRootPath, betaProfile.WorkspaceRootPath);
        Assert.NotEqual(alphaProfile.ManagerArtifactsRootPath, betaProfile.ManagerArtifactsRootPath);
        Assert.StartsWith(testEnvironment.RootPath, alphaProfile.ProfileRootPath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(testEnvironment.RootPath, betaProfile.ProfileRootPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(Path.IsPathFullyQualified(alphaProfile.WorkspaceRootPath));
        Assert.True(Path.IsPathFullyQualified(betaProfile.WorkspaceRootPath));
        Assert.Equal(TestDatabaseProviderKind.PostgreSql, alphaProfile.Provider);
        Assert.Equal(TestDatabaseProviderKind.PostgreSql, betaProfile.Provider);
    }

    [Fact]
    public async Task Environment_variables_map_profile_configuration_keys_to_double_underscore_names()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-profile-env-tests");
        var profile = testEnvironment.CreateInMemoryProfile("alpha", "profile-memory");

        var variables = profile.CreateEnvironmentVariables(new Dictionary<string, string?>
        {
            ["DevelopmentManager:TuningModeEnabled"] = "false"
        });

        Assert.Equal("InMemory", variables["Database__Provider"]);
        Assert.Equal("profile-memory", variables["Database__ConnectionString"]);
        Assert.Equal(profile.WorkspaceRootPath, variables["Storage__WorkspaceRoot"]);
        Assert.Equal("false", variables["DevelopmentManager__TuningModeEnabled"]);
    }

    [Fact]
    public async Task Default_inmemory_profiles_isolate_owner_cursors_across_environments_and_keep_same_profile_state() {
        await using var firstEnvironment = CanDoItAllTestEnvironment.Create("profile-memory-isolation");
        await using var secondEnvironment = CanDoItAllTestEnvironment.Create("profile-memory-isolation");
        var first = firstEnvironment.CreateInMemoryProfile("api-host");
        var second = secondEnvironment.CreateInMemoryProfile("api-host", databaseName: " ");
        var otherProfile = firstEnvironment.CreateInMemoryProfile("other");
        var profileId = Guid.NewGuid();
        await using (var writer = OpenCrmOwner(first)) {
            writer.Add(Cursor(profileId, new string('a', 64)));
            await writer.SaveChangesAsync();
        }

        await using (var secondOwner = OpenCrmOwner(second)) {
            Assert.Empty(await secondOwner.Set<AiTechnicalProjectionCursor>().ToArrayAsync());
            secondOwner.Add(Cursor(profileId, new string('b', 64)));
            await secondOwner.SaveChangesAsync();
        }
        await using (var sameEnvironmentOtherProfile = OpenCrmOwner(otherProfile)) {
            Assert.Empty(await sameEnvironmentOtherProfile.Set<AiTechnicalProjectionCursor>().ToArrayAsync());
        }
        await using (var reopenedFirst = OpenCrmOwner(first)) {
            var cursor = Assert.Single(await reopenedFirst.Set<AiTechnicalProjectionCursor>().ToArrayAsync());
            Assert.Equal(profileId, cursor.DatabaseProfileId);
            Assert.Equal(37, cursor.CatalogRevision);
            Assert.Equal(new string('a', 64), cursor.ProjectionSha256);
        }
        await using (var reopenedSecond = OpenCrmOwner(second)) {
            Assert.Equal(new string('b', 64),
                (await reopenedSecond.Set<AiTechnicalProjectionCursor>().SingleAsync()).ProjectionSha256);
        }
        Assert.NotEqual(first.ConnectionString, second.ConnectionString);
        Assert.NotEqual(first.ConnectionString, otherProfile.ConnectionString);
        Assert.Equal(first.ConnectionString, first.CreateConfigurationValues()["Database:ConnectionString"]);
        Assert.Equal(first.ConnectionString, first.CreateEnvironmentVariables()["Database__ConnectionString"]);
        Assert.Throws<InvalidOperationException>(() => firstEnvironment.CreateInMemoryProfile("API-HOST"));
    }

    [Fact]
    public async Task Explicit_inmemory_name_preserves_deliberate_sharing_across_independent_environments() {
        await using var firstEnvironment = CanDoItAllTestEnvironment.Create("profile-memory-sharing");
        await using var secondEnvironment = CanDoItAllTestEnvironment.Create("profile-memory-sharing");
        var databaseName = $"explicit-shared-{Guid.NewGuid():N}";
        var first = firstEnvironment.CreateInMemoryProfile("api-host", databaseName);
        var second = secondEnvironment.CreateInMemoryProfile("api-host", databaseName);
        var profileId = Guid.NewGuid();
        await using (var writer = OpenCrmOwner(first)) {
            writer.Add(Cursor(profileId, new string('c', 64)));
            await writer.SaveChangesAsync();
        }

        await using (var sharedOwner = OpenCrmOwner(second)) {
            var cursor = Assert.Single(await sharedOwner.Set<AiTechnicalProjectionCursor>().ToArrayAsync());
            Assert.Equal(profileId, cursor.DatabaseProfileId);
            Assert.Equal(37, cursor.CatalogRevision);
            Assert.Equal(new string('c', 64), cursor.ProjectionSha256);
            cursor.CatalogRevision++;
            await sharedOwner.SaveChangesAsync();
        }
        await using (var reopenedFirst = OpenCrmOwner(first)) {
            Assert.Equal(38, (await reopenedFirst.Set<AiTechnicalProjectionCursor>().SingleAsync()).CatalogRevision);
        }
        Assert.Equal(databaseName, first.ConnectionString);
        Assert.Equal(databaseName, second.ConnectionString);
        Assert.NotEqual(first.WorkspaceRootPath, second.WorkspaceRootPath);
    }

    private static CrmHrDbContext OpenCrmOwner(TestDatabaseProfile profile) {
        var options = new DbContextOptionsBuilder<CrmHrDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, new DatabaseOptions {
            Provider = "InMemory",
            ConnectionString = profile.ConnectionString
        }, profile.EnvironmentRootPath);
        return new CrmHrDbContext(options.Options);
    }

    private static AiTechnicalProjectionCursor Cursor(Guid profileId, string hash) => new() {
        DatabaseProfileId = profileId,
        SourceScopeKind = WorkspaceScopeKind.Organization,
        SourceScopeKey = "test-organization",
        CatalogRevision = 37,
        ProjectionSha256 = hash,
        UpdatedAtUtc = DateTimeOffset.UtcNow
    };
}
