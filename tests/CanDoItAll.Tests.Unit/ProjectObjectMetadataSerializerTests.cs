using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectObjectMetadataSerializerTests
{
    [Fact]
    public void Parse_accepts_script_argument_array_and_normalizes_to_command_line_string()
    {
        const string json = """
            {
              "script": {
                "scriptKind": "console",
                "command": "dotnet test",
                "arguments": [
                  "C:\\workspace path\\tests\\TetrisGame.Tests.csproj",
                  "-c",
                  "Debug",
                  "--no-build",
                  "O'Brien"
                ]
              }
            }
            """;

        var metadata = ProjectObjectMetadataSerializer.Parse(json);

        Assert.NotNull(metadata.Script);
        Assert.Equal(
            "'C:\\workspace path\\tests\\TetrisGame.Tests.csproj' '-c' 'Debug' '--no-build' 'O''Brien'",
            metadata.Script!.Arguments);
    }

    [Fact]
    public void Parse_rejects_script_argument_array_with_non_string_tokens()
    {
        const string json = """
            {
              "script": {
                "scriptKind": "console",
                "command": "dotnet test",
                "arguments": [
                  { "value": "--no-build" }
                ]
              }
            }
            """;

        var exception = Assert.Throws<InvalidOperationException>(() => ProjectObjectMetadataSerializer.Parse(json));

        Assert.IsType<System.Text.Json.JsonException>(exception.InnerException);
    }
}
