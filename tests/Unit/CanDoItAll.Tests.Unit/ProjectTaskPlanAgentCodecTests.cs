using System.Text.Json;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectTaskPlanAgentCodecTests {
    [Theory]
    [InlineData("{\"version\":1,\"tasks\":[]}")]
    [InlineData("{\"version\":1,\"tasks\":null}")]
    [InlineData("{\"version\":2,\"tasks\":[{\"title\":\"a\",\"description\":\"\",\"notes\":\"\",\"priority\":1}]}")]
    [InlineData("{\"version\":1,\"tasks\":[{\"title\":\"a\",\"description\":\"\",\"notes\":\"\",\"priority\":1,\"assignee\":\"someone\"}]}")]
    [InlineData("{\"version\":1,\"tasks\":[{\"title\":\"a\",\"description\":\"\",\"notes\":\"\"}]}")]
    [InlineData("{\"version\":1,\"tasks\":[{\"title\":null,\"description\":\"\",\"notes\":\"\",\"priority\":1}]}")]
    [InlineData("{\"version\":1,\"tasks\":[{\"title\":\"a\",\"title\":\"b\",\"description\":\"\",\"notes\":\"\",\"priority\":1}]}")]
    [InlineData("{\"version\":1,\"tasks\":[],\"tasks\":[{\"title\":\"a\",\"description\":\"\",\"notes\":\"\",\"priority\":1}]}")]
    [InlineData("{\"version\":1,\"tasks\":[{\"title\":\"a\",\"description\":\"\",\"notes\":\"\",\"priority\":7}]}")]
    [InlineData("{\"version\":1,\"tasks\":[{\"title\":\"  \",\"description\":\"\",\"notes\":\"\",\"priority\":1}]}")]
    public void Unsupported_or_ambiguous_provider_draft_is_refused(string input) {
        using var document = JsonDocument.Parse(input);
        Assert.NotNull(Record.Exception(() => ProjectTaskPlanAgentCodec.ParseDraft(document.RootElement)));
    }

    [Fact]
    public void Draft_normalizes_strings_without_accepting_additional_commands() {
        using var document = JsonDocument.Parse("""{"version":1,"tasks":[{"title":" Task ","description":" Description ","notes":" Notes ","priority":2}]}""");
        var draft = ProjectTaskPlanAgentCodec.ParseDraft(document.RootElement);
        Assert.Equal(new ProjectTaskPlanDraftItem("Task", "Description", "Notes", 2), Assert.Single(draft.Tasks));
    }
}
