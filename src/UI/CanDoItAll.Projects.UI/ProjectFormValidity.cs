namespace CanDoItAll.Modules.Projects.Pages.Components;

public sealed record ProjectFormValidity(bool IsValid, int? Step, string? Message);
