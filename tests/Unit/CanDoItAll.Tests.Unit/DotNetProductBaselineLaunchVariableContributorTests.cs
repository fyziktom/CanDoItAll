using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Tests.Unit;

public sealed class DotNetProductBaselineLaunchVariableContributorTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        $"CanDoItAll.DotNetBaseline.{Guid.NewGuid():N}");

    [Fact]
    public void Enrich_emits_a_bounded_relative_topology_contract()
    {
        var workspaceRoot = Path.Combine(root, "workspace");
        var productRoot = Path.Combine(root, "product");
        var appProject = Path.Combine(productRoot, "src", "Calculator", "Calculator.csproj");
        var testProject = Path.Combine(productRoot, "tests", "Calculator.Tests", "Calculator.Tests.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(appProject)!);
        Directory.CreateDirectory(Path.GetDirectoryName(testProject)!);
        File.WriteAllText(
            Path.Combine(productRoot, "Calculator.slnx"),
            """<Solution><Project Path="src/Calculator/Calculator.csproj" /></Solution>""");
        File.WriteAllText(
            appProject,
            """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>""");
        File.WriteAllText(
            testProject,
            """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><IsTestProject>true</IsTestProject></PropertyGroup></Project>""");
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductRoot"] = productRoot
        };
        var contributor = new DotNetProductBaselineLaunchVariableContributor(
            new WorkspaceFileService(workspaceRoot));

        contributor.Enrich(CreateActivatedContext(), variables);

        var json = variables[DotNetProductBaselineLaunchVariableContributor.VariableName];
        using var document = JsonDocument.Parse(json);
        var contract = document.RootElement;
        Assert.Equal(DotNetProductBaselineLaunchVariableContributor.Schema, contract.GetProperty("schema").GetString());
        Assert.Equal("discovered", contract.GetProperty("status").GetString());
        Assert.True(contract.GetProperty("discoveryComplete").GetBoolean());
        Assert.Equal(1, contract.GetProperty("solutionFileCount").GetInt32());
        Assert.Equal(2, contract.GetProperty("projectFileCount").GetInt32());
        Assert.True(contract.GetProperty("topologySampleComplete").GetBoolean());
        Assert.True(contract.GetProperty("metadataInspectionComplete").GetBoolean());
        Assert.Equal(0, contract.GetProperty("duplicateProjectNameCount").GetInt32());
        Assert.True(contract.GetProperty("duplicateProjectNameSampleComplete").GetBoolean());
        Assert.Equal(
            "Calculator.slnx",
            Assert.Single(contract.GetProperty("solutionFiles").EnumerateArray()).GetString());
        var projects = contract.GetProperty("projects").EnumerateArray().ToArray();
        Assert.Equal(2, projects.Length);
        Assert.Contains(projects, project =>
            project.GetProperty("file").GetString() == "src/Calculator/Calculator.csproj" &&
            project.GetProperty("targetFrameworks")[0].GetString() == "net10.0" &&
            !project.GetProperty("isTestProject").GetBoolean());
        Assert.Contains(projects, project =>
            project.GetProperty("file").GetString() == "tests/Calculator.Tests/Calculator.Tests.csproj" &&
            project.GetProperty("isTestProject").GetBoolean());
        Assert.DoesNotContain(root, json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Enrich_marks_large_topology_as_sampled_without_losing_discovered_counts()
    {
        var workspaceRoot = Path.Combine(root, "workspace");
        var productRoot = Path.Combine(root, "product");
        Directory.CreateDirectory(productRoot);
        File.WriteAllText(
            Path.Combine(productRoot, "Product.slnx"),
            "<Solution />");
        for (var index = 0; index < 13; index++)
        {
            var projectDirectory = Path.Combine(productRoot, "src", $"Project{index:D2}");
            Directory.CreateDirectory(projectDirectory);
            File.WriteAllText(
                Path.Combine(projectDirectory, $"Project{index:D2}.csproj"),
                """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>""");
        }

        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductRoot"] = productRoot
        };
        var contributor = new DotNetProductBaselineLaunchVariableContributor(
            new WorkspaceFileService(workspaceRoot));

        contributor.Enrich(CreateActivatedContext(), variables);

        using var document = JsonDocument.Parse(
            variables[DotNetProductBaselineLaunchVariableContributor.VariableName]);
        var contract = document.RootElement;
        Assert.Equal("discovered", contract.GetProperty("status").GetString());
        Assert.True(contract.GetProperty("discoveryComplete").GetBoolean());
        Assert.Equal(1, contract.GetProperty("solutionFileCount").GetInt32());
        Assert.Equal(13, contract.GetProperty("projectFileCount").GetInt32());
        Assert.False(contract.GetProperty("topologySampleComplete").GetBoolean());
        Assert.False(contract.GetProperty("metadataInspectionComplete").GetBoolean());
        Assert.Equal(8, contract.GetProperty("projects").GetArrayLength());
        Assert.True(
            variables[DotNetProductBaselineLaunchVariableContributor.VariableName].Length <=
            DotNetProductBaselineLaunchVariableContributor.MaximumSerializedContractCharacters);
    }

    [Fact]
    public void Enrich_marks_unreadable_project_metadata_incomplete()
    {
        var workspaceRoot = Path.Combine(root, "workspace");
        var productRoot = Path.Combine(root, "product");
        var projectDirectory = Path.Combine(productRoot, "src", "Unreadable");
        Directory.CreateDirectory(projectDirectory);
        File.WriteAllText(
            Path.Combine(productRoot, "Product.slnx"),
            "<Solution />");
        File.WriteAllBytes(
            Path.Combine(projectDirectory, "Unreadable.csproj"),
            [0, 1, 2, 3, 0, 4]);
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductRoot"] = productRoot
        };
        var contributor = new DotNetProductBaselineLaunchVariableContributor(
            new WorkspaceFileService(workspaceRoot));

        contributor.Enrich(CreateActivatedContext(), variables);

        using var document = JsonDocument.Parse(
            variables[DotNetProductBaselineLaunchVariableContributor.VariableName]);
        var contract = document.RootElement;
        Assert.Equal("discovered", contract.GetProperty("status").GetString());
        Assert.True(contract.GetProperty("discoveryComplete").GetBoolean());
        Assert.True(contract.GetProperty("topologySampleComplete").GetBoolean());
        Assert.False(contract.GetProperty("metadataInspectionComplete").GetBoolean());
        var project = Assert.Single(contract.GetProperty("projects").EnumerateArray());
        Assert.False(project.GetProperty("inspectionComplete").GetBoolean());
        Assert.Empty(project.GetProperty("targetFrameworks").EnumerateArray());
    }

    [Fact]
    public void Enrich_reports_no_baseline_after_a_complete_missing_root_observation()
    {
        Directory.CreateDirectory(Path.Combine(root, "workspace"));
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductRoot"] = Path.Combine(root, "missing-product")
        };
        var contributor = new DotNetProductBaselineLaunchVariableContributor(
            new WorkspaceFileService(Path.Combine(root, "workspace")));

        contributor.Enrich(CreateActivatedContext(), variables);

        using var document = JsonDocument.Parse(
            variables[DotNetProductBaselineLaunchVariableContributor.VariableName]);
        Assert.Equal("not-found", document.RootElement.GetProperty("status").GetString());
        Assert.True(document.RootElement.GetProperty("discoveryComplete").GetBoolean());
        Assert.Empty(document.RootElement.GetProperty("solutionFiles").EnumerateArray());
        Assert.Empty(document.RootElement.GetProperty("projects").EnumerateArray());
    }

    [Fact]
    public void Enrich_does_nothing_without_the_template_activation()
    {
        Directory.CreateDirectory(Path.Combine(root, "workspace"));
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductRoot"] = Path.Combine(root, "product")
        };
        var contributor = new DotNetProductBaselineLaunchVariableContributor(
            new WorkspaceFileService(Path.Combine(root, "workspace")));
        var context = CreateActivatedContext() with { DriverActivations = [] };

        contributor.Enrich(context, variables);

        Assert.DoesNotContain(
            DotNetProductBaselineLaunchVariableContributor.VariableName,
            variables.Keys,
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Development_slice_template_opts_into_baseline_discovery()
    {
        var definition = new ProcessTemplatePackLoader().LoadDefinition("dotnet-development-slice");

        var activation = Assert.Single(definition.LaunchDriverActivations);

        Assert.Equal(DotNetProductBaselineLaunchVariableContributor.DriverKey, activation.DriverKey);
        Assert.Empty(activation.InputArtifactBindings);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static ProcessLaunchPreparationContext CreateActivatedContext()
    {
        var source = new ProcessLaunchSourceItem(
            "test",
            "Test source",
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            ProcessLaunchSourceItemKind.Other,
            IsIncludedInProcessContext: true);
        return new ProcessLaunchPreparationContext(
            "dotnet-development-slice",
            IsSubprocess: true,
            new ProcessLaunchSourceSnapshot(
                Guid.NewGuid(),
                "Test",
                source,
                [source],
                string.Empty))
        {
            DriverActivations =
            [
                new ProcessLaunchDriverActivation(
                    DotNetProductBaselineLaunchVariableContributor.DriverKey,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
            ]
        };
    }
}
