using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProfileHarnessIntegrationTests
{
    [Fact]
    public async Task Test_application_bootstraps_two_profiles_with_isolated_data_and_managed_files()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-profile-harness");
        var alphaProfile = testEnvironment.CreatePostgreSqlProfile("alpha");
        var betaProfile = testEnvironment.CreatePostgreSqlProfile("beta");

        await using var alphaApplication = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            TestEnvironment = testEnvironment,
            ActiveProfile = alphaProfile,
            SchemaModules = TestSchemaBootstrapModules.Full
        });

        await using var betaApplication = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            TestEnvironment = testEnvironment,
            ActiveProfile = betaProfile,
            SchemaModules = TestSchemaBootstrapModules.Full
        });

        await using var alphaScope = alphaApplication.Services.CreateAsyncScope();
        await using var betaScope = betaApplication.Services.CreateAsyncScope();

        var alphaSeed = await TestProfileSeedHelper.SeedDistinctProjectAndManagedFileAsync(alphaScope.ServiceProvider, "Alpha");
        var betaSeed = await TestProfileSeedHelper.SeedDistinctProjectAndManagedFileAsync(betaScope.ServiceProvider, "Beta");

        var alphaProjectsService = alphaScope.ServiceProvider.GetRequiredService<ProjectsService>();
        var betaProjectsService = betaScope.ServiceProvider.GetRequiredService<ProjectsService>();
        var alphaFileStore = alphaScope.ServiceProvider.GetRequiredService<IFileStore>();
        var betaFileStore = betaScope.ServiceProvider.GetRequiredService<IFileStore>();

        var alphaProjects = await alphaProjectsService.ListAsync();
        var betaProjects = await betaProjectsService.ListAsync();
        var alphaFileContent = await alphaFileStore.ReadTextAsync(alphaSeed.ManagedFileRelativePath);
        var betaFileContent = await betaFileStore.ReadTextAsync(betaSeed.ManagedFileRelativePath);

        Assert.Contains(alphaProjects, project => project.Name == alphaSeed.ProjectName);
        Assert.DoesNotContain(alphaProjects, project => project.Name == betaSeed.ProjectName);
        Assert.Contains(betaProjects, project => project.Name == betaSeed.ProjectName);
        Assert.DoesNotContain(betaProjects, project => project.Name == alphaSeed.ProjectName);
        Assert.Equal(alphaSeed.ManagedFileContent, alphaFileContent);
        Assert.Equal(betaSeed.ManagedFileContent, betaFileContent);
        Assert.StartsWith(alphaProfile.WorkspaceRootPath, alphaSeed.ManagedFileFullPath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(betaProfile.WorkspaceRootPath, betaSeed.ManagedFileFullPath, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(alphaProfile.WorkspaceRootPath, betaSeed.ManagedFileRelativePath)));
        Assert.False(File.Exists(Path.Combine(betaProfile.WorkspaceRootPath, alphaSeed.ManagedFileRelativePath)));
    }
}
