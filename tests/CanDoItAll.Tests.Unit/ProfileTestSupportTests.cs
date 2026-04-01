using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

public sealed class ProfileTestSupportTests
{
    [Fact]
    public async Task Managed_sqlite_profiles_create_isolated_database_and_storage_roots()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-profile-tests");
        var alphaProfile = testEnvironment.CreateManagedSqliteProfile("alpha");
        var betaProfile = testEnvironment.CreateManagedSqliteProfile("beta");

        Assert.NotEqual(alphaProfile.DatabasePath, betaProfile.DatabasePath);
        Assert.NotEqual(alphaProfile.WorkspaceRootPath, betaProfile.WorkspaceRootPath);
        Assert.NotEqual(alphaProfile.ManagerArtifactsRootPath, betaProfile.ManagerArtifactsRootPath);
        Assert.StartsWith(testEnvironment.RootPath, alphaProfile.ProfileRootPath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(testEnvironment.RootPath, betaProfile.ProfileRootPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(Path.IsPathFullyQualified(alphaProfile.WorkspaceRootPath));
        Assert.True(Path.IsPathFullyQualified(betaProfile.WorkspaceRootPath));
        Assert.True(Directory.Exists(Path.GetDirectoryName(alphaProfile.DatabasePath!)!));
        Assert.True(Directory.Exists(Path.GetDirectoryName(betaProfile.DatabasePath!)!));
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
}
