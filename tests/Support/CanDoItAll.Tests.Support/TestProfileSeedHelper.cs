using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Support;

public static class TestProfileSeedHelper
{
    public static async Task<TestProfileSeedResult> SeedDistinctProjectAndManagedFileAsync(
        IServiceProvider serviceProvider,
        string label,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        var projectsService = serviceProvider.GetRequiredService<ProjectsService>();
        var managedArtifactStore = serviceProvider.GetRequiredService<IManagedArtifactStore>();
        var projectName = $"{label} Project";

        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = projectName,
            Description = $"{label} description",
            Objective = $"{label} objective",
            CurrentPhase = "Discovery"
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Failed to seed project for '{label}': {string.Join("; ", result.Errors.Select(error => error.Message))}");
        }

        var fileName = $"{SanitizeFileSegment(label)}.txt";
        var relativePath = managedArtifactStore.GetRelativePath("profile-seeds", fileName);
        var fileContent = $"seed:{label}";
        var fullPath = await managedArtifactStore.SaveTextAsync("profile-seeds", fileName, fileContent, cancellationToken);

        return new TestProfileSeedResult(result.Value, projectName, relativePath, fullPath, fileContent);
    }

    private static string SanitizeFileSegment(string value)
        => PortablePhysicalFileNamePolicy.Encode(
            string.IsNullOrWhiteSpace(value) ? "seed" : value.Trim()).PhysicalName;
}

public sealed record TestProfileSeedResult(
    Guid ProjectId,
    string ProjectName,
    string ManagedFileRelativePath,
    string ManagedFileFullPath,
    string ManagedFileContent);
