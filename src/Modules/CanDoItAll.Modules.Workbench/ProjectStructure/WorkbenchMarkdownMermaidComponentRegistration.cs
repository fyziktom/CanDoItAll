using CanDoItAll.FileTools.FileInteraction.Markdown;

namespace CanDoItAll.Modules.Workbench;

internal sealed class WorkbenchMarkdownMermaidComponentRegistration
    : IMarkdownFencedCodeComponentRegistration
{
    private const string MermaidLanguage = "mermaid";

    public string Language => MermaidLanguage;

    public Type ComponentType => typeof(WorkbenchMarkdownMermaidBlock);
}
