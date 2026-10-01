using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.FileTools.FileInteraction.Markdown;
using CanDoItAll.Projects.Files.UiSandbox.Components;

namespace CanDoItAll.Projects.Files.UiSandbox;

public static class FilesFixtureComposition {
    private const string MermaidProfile = "projects-files-fixture-mermaid";
    public static FileInteractionComponentComposition Create() => new FileInteractionComponentBuilder()
        .AddBuiltIns().AddMarkdown()
        .AddProfile(new(MermaidProfile, FileInteractionCapabilities.View, extensions: [".mermaid"], mediaTypes: ["text/vnd.mermaid"], priority: 200))
        .AddRenderer(new("projects-files-fixture-mermaid-view", MermaidProfile, FileInteractionMode.View, typeof(FilesFixtureMermaidView), FileInteractionContentKind.Text))
        .Build();
}
