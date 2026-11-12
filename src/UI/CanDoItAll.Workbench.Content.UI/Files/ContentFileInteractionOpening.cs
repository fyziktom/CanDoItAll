using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Workbench.Content.UI;

public sealed record ContentFileInteractionOpening {
    public Guid Id { get; } = Guid.NewGuid();
    public required string Title { get; init; }
    public required string Notice { get; init; }
    public FileInteractionRequest? Request { get; init; }
    public IFileContentSource? ContentSource { get; init; }
    public required FileInteractionComponentComposition Composition { get; init; }
    public required int MaximumContentBytes { get; init; }
    public Func<FileInteractionMode, FileInteractionRequest>? ChangeMode { get; init; }
    public Func<FileInteractionSaveRequestedEventArgs, Task>? Save { get; init; }
    public required Func<CancellationToken, Task<string?>> ReadNotes { get; init; }
    public EventCallback Close { get; init; }
    public EventCallback EditDetails { get; init; }
    public EventCallback OpenPreferred { get; init; }
    public EventCallback ShowInFolder { get; init; }
}
