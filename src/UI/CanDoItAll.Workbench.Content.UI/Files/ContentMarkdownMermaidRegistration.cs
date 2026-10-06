using CanDoItAll.FileTools.FileInteraction.Markdown;

namespace CanDoItAll.Workbench.Content.UI;

public sealed class ContentMarkdownMermaidRegistration : IMarkdownFencedCodeComponentRegistration {
    public string Language => "mermaid";
    public Type ComponentType => typeof(CanDoItAll.Modules.Workbench.WorkbenchMarkdownMermaidBlock);
}
