using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Conversations.Components.Presentation;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.AgentFramework.Editor.UI;

public partial class AgentEditorCoreSurface {
    [Parameter, EditorRequired]
    public AgentEditorCoreState State { get; set; } = default!;
    [Parameter]
    public RenderFragment<AgentEditorSlotContext>? DeferredSection { get; set; }
    [Parameter]
    public RenderFragment<AgentEditorSlotContext>? AvatarActions { get; set; }
    [Parameter]
    public RenderFragment<AgentEditorSlotContext>? ProviderRefresh { get; set; }

    private static ConversationAvatarPresentation IdentityAvatar(AgentEditorModel editorModel) {
        var name = FirstNonEmpty(editorModel.Name, editorModel.RoleTitle, "Agent avatar");
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(2);
        var initials = string.Concat(words.Select(word => char.ToUpperInvariant(word[0])));
        return new(name, editorModel.AvatarImageUrl, initials.Length == 0 ? "A" : initials, name);
    }

    private static string ResolveAvatarSelectionText(AgentEditorModel editorModel) {
        if (string.IsNullOrWhiteSpace(editorModel.AvatarImageUrl)) {
            return "Default generated avatar";
        }
        if (AgentAvatarImageCatalog.IsBundledAvatarUrl(editorModel.AvatarImageUrl)) {
            return "Bundled avatar selected";
        }
        return editorModel.AvatarImageUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)
            ? "Custom avatar loaded"
            : "Custom avatar selected";
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
