using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public sealed record CapabilityDetailsDialogResult(Guid CapabilityId);

public partial class CapabilityDetailsDialog
{
    [Parameter]
    public Guid CapabilityId { get; set; }

    [Parameter]
    public IReadOnlyList<string> TagSuggestions { get; set; } = [];

    [Inject]
    public IAgentFrameworkWorkspaceService WorkspaceService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [CascadingParameter]
    public DialogReference? DialogReference { get; set; }

    private CapabilityEditorModel editorModel = new();
    private IReadOnlyList<string> capabilityTags = [];
    private CapabilityConfigurationEditorSupport.McpCapabilityEditorState mcpState = new();
    private CapabilityConfigurationEditorSupport.SkillCapabilityEditorState skillState = new();
    private string rawConfigurationJson = string.Empty;
    private int selectedTabIndex;
    private bool isLoading = true;
    private bool isBusy;

    private bool IsKindLocked => editorModel.IsBuiltIn;

    private bool IsIdentityLocked => editorModel.IsBuiltIn && editorModel.Kind == CapabilityKind.Tool;

    private bool IsEndpointLocked => editorModel.IsBuiltIn && editorModel.Kind == CapabilityKind.Tool;

    private bool IsRawConfigurationLocked => editorModel.Kind is CapabilityKind.McpServer or CapabilityKind.Skill ||
                                            editorModel.IsBuiltIn;

    private IReadOnlyList<string> VisibleTagSuggestions => TagSuggestions
        .Where(tag => !string.IsNullOrWhiteSpace(tag))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
        .ToList();

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        isLoading = true;
        try
        {
            editorModel = await WorkspaceService.GetCapabilityEditorAsync(CapabilityId);
            capabilityTags = NormalizeTags(editorModel.Tags);
            rawConfigurationJson = editorModel.ConfigurationJson;
            RefreshTypedConfigurationState();
        }
        catch (Exception exception)
        {
            NotificationService.Error("Capability details failed to load", exception.Message);
        }
        finally
        {
            isLoading = false;
        }
    }

    private Task HandleSelectedTabIndexChanged(int index)
    {
        selectedTabIndex = index;
        return Task.CompletedTask;
    }

    private Task HandleKindChangedAsync(CapabilityKind kind)
    {
        if (editorModel.Kind != kind)
        {
            editorModel.Kind = kind;
            RefreshTypedConfigurationState();
        }

        return Task.CompletedTask;
    }

    private Task HandleTagsChangedAsync(IReadOnlyList<string> tags)
    {
        capabilityTags = NormalizeTags(tags);
        return Task.CompletedTask;
    }

    private Task HandleRawConfigurationChangedAsync(string? value)
    {
        rawConfigurationJson = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private async Task SaveAsync()
    {
        if (isBusy)
        {
            return;
        }

        isBusy = true;
        try
        {
            var errors = PrepareEditorForSave();
            if (errors.Count > 0)
            {
                NotificationService.Warning("Capability was not saved", string.Join(" ", errors));
                return;
            }

            var capabilityId = await WorkspaceService.SaveCapabilityAsync(editorModel);
            NotificationService.Success("Capability saved", "Capability metadata was saved.");
            if (DialogReference is not null)
            {
                await DialogReference.CloseAsync(new CapabilityDetailsDialogResult(capabilityId));
            }
        }
        catch (Exception exception)
        {
            NotificationService.Error("Capability save failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    private IReadOnlyList<string> PrepareEditorForSave()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(editorModel.Name))
        {
            errors.Add("Name is required.");
        }

        editorModel.Key = string.IsNullOrWhiteSpace(editorModel.Key)
            ? CapabilityConfigurationEditorSupport.NormalizeKey(editorModel.Name)
            : CapabilityConfigurationEditorSupport.NormalizeKey(editorModel.Key);
        if (string.IsNullOrWhiteSpace(editorModel.Key))
        {
            errors.Add("Key is required.");
        }

        editorModel.Tags = NormalizeTags(capabilityTags).ToList();
        if (editorModel.Kind == CapabilityKind.McpServer)
        {
            errors.AddRange(CapabilityConfigurationEditorSupport.WriteMcp(editorModel, mcpState));
        }
        else if (editorModel.Kind == CapabilityKind.Skill)
        {
            errors.AddRange(CapabilityConfigurationEditorSupport.WriteSkill(editorModel, skillState));
        }
        else if (!IsRawConfigurationLocked)
        {
            if (!IsValidJsonObject(rawConfigurationJson))
            {
                errors.Add("Configuration JSON must be a valid JSON object.");
            }
            else
            {
                editorModel.ConfigurationJson = rawConfigurationJson.Trim();
            }
        }

        rawConfigurationJson = editorModel.ConfigurationJson;
        return errors;
    }

    private void RefreshTypedConfigurationState()
    {
        if (editorModel.Kind == CapabilityKind.McpServer)
        {
            mcpState = CapabilityConfigurationEditorSupport.ReadMcp(editorModel);
        }
        else if (editorModel.Kind == CapabilityKind.Skill)
        {
            skillState = CapabilityConfigurationEditorSupport.ReadSkill(editorModel);
        }
    }

    private Task CancelAsync()
        => DialogReference?.CloseAsync() ?? Task.CompletedTask;

    private string ResolveIdentityDescription()
    {
        return editorModel.IsBuiltIn && editorModel.Kind == CapabilityKind.Tool
            ? "Built-in tool runtime identity is locked; tags remain editable for future catalog search and prompt-time grouping."
            : "Edit display metadata and grouping tags. Runtime configuration stays explicit in the configuration tab.";
    }

    private static bool IsValidJsonObject(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static IReadOnlyList<string> NormalizeTags(IEnumerable<string> tags)
    {
        return tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim().TrimStart('#').ToLowerInvariant())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ResolveCapabilityKindLabel(CapabilityKind kind)
    {
        return kind switch
        {
            CapabilityKind.McpServer => "MCP server",
            CapabilityKind.AiContext => "AI context",
            _ => kind.ToString()
        };
    }
}
