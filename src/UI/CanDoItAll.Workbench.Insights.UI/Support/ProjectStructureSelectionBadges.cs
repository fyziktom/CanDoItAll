namespace CanDoItAll.Modules.Workbench.Pages;

public sealed record ProjectStructureSelectionBadgePresentation(
    string Text,
    ProjectStructureSelectionBadgeStyle Style,
    string TestId);

public enum ProjectStructureSelectionBadgeStyle
{
    Standard,
    Uploaded,
    Scheduled,
    Synced,
    FileGeneric,
    FilePdf,
    FileExcel,
    FileDocx,
    FileMarkdown,
    FileMermaid,
    FileScreenshot,
    FileLog,
    FileArchive,
    FileAudio,
    FileJson,
    FileText
}
