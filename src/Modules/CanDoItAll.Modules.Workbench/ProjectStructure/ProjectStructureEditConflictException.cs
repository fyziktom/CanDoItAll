namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureEditConflictException()
    : InvalidOperationException("The original node changed. Reopen its editor before saving.");
