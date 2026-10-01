using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects.Pages.Components;

public sealed class StarterObjectDraft
{
    public bool IsSeeded { get; internal set; }

    public ProjectObjectType ObjectType { get; set; } = ProjectObjectType.Note;

    public string Title { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;
}
