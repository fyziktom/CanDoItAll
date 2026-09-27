using System.Xml.Linq;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.Infrastructure;

/// <summary>
/// A library that references an executable project passes it on to every host that consumes the library. The library
/// builds the executable without a runtime identifier while a RID-specific host publish also builds it with one, so
/// both copies reach the publish output and the publish fails with NETSDK1152. Such helpers run as
/// <c>dotnet helper.dll</c> from the host output, so they must be a single portable build without an app host.
/// </summary>
public sealed class ExecutableProjectReferenceTests
{
    private static readonly string[] SkippedDirectories = ["bin", "obj", "node_modules"];

    [Fact]
    public void Executables_referenced_by_libraries_are_rid_agnostic_and_app_host_free()
    {
        string sourceRoot = Path.Combine(TestRepositoryRoot.Find(), "src");
        Dictionary<string, XDocument> projects = EnumerateProjects(sourceRoot)
            .ToDictionary(path => path, path => XDocument.Load(path), StringComparer.Ordinal);

        var references = projects
            .Where(project => !IsExecutable(project.Value))
            .SelectMany(project => ProjectReferences(project.Key, project.Value)
                .Where(projects.ContainsKey)
                .Where(reference => IsExecutable(projects[reference]))
                .Select(reference => (Library: project.Key, Executable: reference)))
            .ToList();

        Assert.Contains(references, reference => Path.GetFileName(reference.Executable) == "CanDoItAll.Tools.StaticHost.csproj");
        var violations = references
            .Where(reference => !string.Equals(Property(projects[reference.Executable], "IsRidAgnostic"), "true", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Property(projects[reference.Executable], "UseAppHost"), "false", StringComparison.OrdinalIgnoreCase))
            .Select(reference => $"{Path.GetRelativePath(sourceRoot, reference.Library)} -> {Path.GetRelativePath(sourceRoot, reference.Executable)}")
            .ToList();
        Assert.True(violations.Count == 0,
            "Executables referenced by libraries must declare <IsRidAgnostic>true</IsRidAgnostic> and <UseAppHost>false</UseAppHost>: " +
            string.Join("; ", violations));
    }

    private static IEnumerable<string> EnumerateProjects(string directory)
    {
        foreach (string project in Directory.EnumerateFiles(directory, "*.csproj"))
        {
            yield return Path.GetFullPath(project);
        }

        foreach (string child in Directory.EnumerateDirectories(directory))
        {
            if (SkippedDirectories.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase) ||
                (File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            foreach (string project in EnumerateProjects(child))
            {
                yield return project;
            }
        }
    }

    private static IEnumerable<string> ProjectReferences(string projectPath, XDocument project)
        => project.Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .OfType<string>()
            .Select(include => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectPath)!, include.Replace('\\', '/'))));

    private static bool IsExecutable(XDocument project)
    {
        string? outputType = Property(project, "OutputType");
        if (outputType is not null)
        {
            return outputType.Equals("Exe", StringComparison.OrdinalIgnoreCase) ||
                outputType.Equals("WinExe", StringComparison.OrdinalIgnoreCase);
        }

        string sdk = project.Root?.Attribute("Sdk")?.Value ?? string.Empty;
        return sdk.StartsWith("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) ||
            sdk.StartsWith("Microsoft.NET.Sdk.Worker", StringComparison.OrdinalIgnoreCase);
    }

    private static string? Property(XDocument project, string name)
        => project.Root?.Elements()
            .Where(group => group.Name.LocalName == "PropertyGroup" && group.Attribute("Condition") is null)
            .Elements()
            .Where(property => property.Name.LocalName == name && property.Attribute("Condition") is null)
            .Select(property => property.Value.Trim())
            .LastOrDefault();
}
