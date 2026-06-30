using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using System.Text.Json;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureNodeKindJsonConverterTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void CreateInput_accepts_numeric_object_type_from_public_api_clients()
    {
        var json = $$"""
            {
              "objectType": {{(int)ProjectObjectType.WorkItem}},
              "objectSubtype": "task",
              "title": "Build app",
              "subtitle": "Implementation",
              "notes": "Create the requested app."
            }
            """;

        var input = JsonSerializer.Deserialize<ProjectStructureNodeCreateInput>(json, SerializerOptions);

        Assert.NotNull(input);
        Assert.Equal(ProjectObjectType.WorkItem, input.ObjectType);
        Assert.Equal("task", input.ObjectSubtype);
        Assert.Equal("Build app", input.Title);
    }

    [Fact]
    public void EditInput_accepts_numeric_object_type_from_public_api_clients()
    {
        var json = $$"""
            {
              "objectType": {{(int)ProjectObjectType.WorkItem}},
              "objectSubtype": "issue",
              "title": "Fix app",
              "subtitle": "Repair",
              "notes": "Repair the validation failure."
            }
            """;

        var input = JsonSerializer.Deserialize<ProjectStructureNodeEditInput>(json, SerializerOptions);

        Assert.NotNull(input);
        Assert.Equal(ProjectObjectType.WorkItem, input.ObjectType);
        Assert.Equal("issue", input.ObjectSubtype);
        Assert.Equal("Fix app", input.Title);
    }

    [Fact]
    public void CreateInput_rejects_unknown_numeric_object_type()
    {
        const string json = """
            {
              "objectType": 999,
              "title": "Invalid node"
            }
            """;

        var exception = Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<ProjectStructureNodeCreateInput>(json, SerializerOptions));

        Assert.Contains("Unsupported objectType numeric value", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("FolderNode", ProjectObjectType.Repository, "folder")]
    [InlineData("GitHubRepository", ProjectObjectType.Repository, "remote")]
    [InlineData("GitLabLink", ProjectObjectType.Link, null)]
    [InlineData("PowerShellRuntime", ProjectObjectType.Script, "powershell")]
    [InlineData("PythonRuntime", ProjectObjectType.Environment, "python")]
    [InlineData("DockerRuntime", ProjectObjectType.Infrastructure, "docker-mode")]
    [InlineData("MarkdownFile", ProjectObjectType.File, "markdown")]
    public void CreateInput_accepts_project_structure_node_aliases(string objectTypeAlias, ProjectObjectType expectedType, string? expectedSubtype)
    {
        var json = $$"""
            {
              "objectType": "{{objectTypeAlias}}",
              "title": "Aliased node"
            }
            """;

        var input = JsonSerializer.Deserialize<ProjectStructureNodeCreateInput>(json, SerializerOptions);

        Assert.NotNull(input);
        Assert.Equal(expectedType, input.ObjectType);
        Assert.Equal(expectedSubtype, input.ObjectSubtype);
    }

    [Theory]
    [InlineData("Repository", "local folder", "folder")]
    [InlineData("Script", "ef migration", "ef-migration")]
    [InlineData("Environment", "dotnet watch", "dotnet-watch")]
    [InlineData("Infrastructure", "docker compose", "docker-mode")]
    [InlineData("File", "word document", "docx")]
    public void CreateInput_normalizes_subtype_aliases_for_canonical_object_types(string objectType, string objectSubtype, string expectedSubtype)
    {
        var json = $$"""
            {
              "objectType": "{{objectType}}",
              "objectSubtype": "{{objectSubtype}}",
              "title": "Typed node"
            }
            """;

        var input = JsonSerializer.Deserialize<ProjectStructureNodeCreateInput>(json, SerializerOptions);

        Assert.NotNull(input);
        Assert.Equal(expectedSubtype, input.ObjectSubtype);
    }
}
