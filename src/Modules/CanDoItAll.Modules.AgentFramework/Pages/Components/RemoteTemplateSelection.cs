using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

internal sealed class RemoteTemplateSelection(RemoteTemplateRow row) {
    public RemoteTemplateRow Row { get; } = row;
    public RemoteTemplatePreview? Preview { get; private set; }
    public IReadOnlyList<RemoteTemplateModelSlot> Slots { get; private set; } = [];
    public string? Error { get; set; }
    public Guid? AcceptedId { get; set; }

    public void Load(RemoteTemplatePreview preview, IReadOnlyList<RemoteTemplateModelOption> options) {
        Preview = preview;
        Slots = preview.Models.Select(requirement => new RemoteTemplateModelSlot(requirement,
            RemoteTemplateProviderCatalog.Preferred(requirement, options))).ToArray();
    }
}

internal sealed class RemoteTemplateModelSlot(RemoteTemplateModelRequirement requirement, RemoteTemplateModelChoice? choice) {
    public RemoteTemplateModelRequirement Requirement { get; } = requirement;
    public RemoteTemplateModelChoice? Choice { get; private set; } = choice;
    public bool Confirmed { get; private set; }

    public void Reconcile(IReadOnlyList<RemoteTemplateModelOption> options) {
        if (Choice is not null && options.Any(option => option.Choice == Choice && option.Supports(Requirement))) {
            return;
        }
        Choice = RemoteTemplateProviderCatalog.Preferred(Requirement, options);
        Confirmed = false;
    }

    public void Confirm(RemoteTemplateModelOption option) {
        if (!option.Supports(Requirement)) {
            throw new ArgumentException("The selected model does not meet this requirement.", nameof(option));
        }
        Choice = option.Choice;
        Confirmed = true;
    }
}
