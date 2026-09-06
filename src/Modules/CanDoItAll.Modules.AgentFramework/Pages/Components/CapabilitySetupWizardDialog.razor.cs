using System.Text.Json;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using CapabilityDiagnostic = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityDiagnostic;
using CapabilitySetupTestResult = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilitySetupTestResult;
using McpSetupTestResult = CanDoItAll.AgentFramework.Mcp.Abstractions.McpSetupTestResult;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class CapabilitySetupWizardDialog : IDisposable
{
    private const long MaxSkillUploadBytes = 1_048_576;
    private const int LastWizardStep = 2;

    [Parameter] public CancellationToken OwnerCancellationToken { get; set; }
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenRegistration ownerRegistration;
    private bool disposed;
    private bool IsCurrent => !disposed && !lifetime.IsCancellationRequested;

    [Parameter]
    public CapabilityKind InitialKind { get; set; } = CapabilityKind.McpServer;

    [Parameter]
    public IReadOnlyList<string> TagSuggestions { get; set; } = [];

    [Inject]
    public IAgentFrameworkWorkspaceService WorkspaceService { get; set; } = default!;

    [Inject]
    public IAgentCapabilitySetupFlowService CapabilitySetupFlowService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [CascadingParameter]
    public DialogReference? DialogReference { get; set; }

    private CapabilityEditorModel editorModel = new();
    private IReadOnlyList<string> capabilityTags = [];
    private CapabilityConfigurationEditorSupport.McpCapabilityEditorState mcpState = new();
    private CapabilityConfigurationEditorSupport.SkillCapabilityEditorState skillState = new();
    private CapabilityConfigurationEditorSupport.ToolCapabilityEditorState toolState = new();
    private CapabilityWizardSkillInputMode skillInputMode = CapabilityWizardSkillInputMode.FilePath;
    private CapabilitySetupTestResult? toolSetupResult;
    private McpSetupTestResult? mcpSetupResult;
    private string uploadedSkillFileName = string.Empty;
    private bool keyWasEdited;
    private bool isBusy;
    private int wizardStep;

    private IReadOnlyList<string> VisibleTagSuggestions => TagSuggestions
        .Where(tag => !string.IsNullOrWhiteSpace(tag))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
        .ToList();

    protected override void OnInitialized()
    {
        ownerRegistration = OwnerCancellationToken.Register(lifetime.Cancel);
        editorModel = new CapabilityEditorModel
        {
            Kind = InitialKind is CapabilityKind.Skill or CapabilityKind.McpServer
                or CapabilityKind.Tool
                ? InitialKind
                : CapabilityKind.McpServer,
            IsBuiltIn = false
        };

        ApplyKindDefaults();
    }

    private Task HandleKindChangedAsync(CapabilityKind kind)
    {
        if (kind is not CapabilityKind.Skill and not CapabilityKind.McpServer and not CapabilityKind.Tool)
        {
            kind = CapabilityKind.McpServer;
        }

        if (editorModel.Kind != kind)
        {
            editorModel.Kind = kind;
            ApplyKindDefaults();
        }

        return Task.CompletedTask;
    }

    private Task HandleNameChangedAsync(string? value)
    {
        editorModel.Name = value ?? string.Empty;
        if (!keyWasEdited)
        {
            editorModel.Key = CapabilityConfigurationEditorSupport.NormalizeKey(editorModel.Name);
        }

        if (editorModel.Kind == CapabilityKind.Skill &&
            string.IsNullOrWhiteSpace(skillState.InlineName))
        {
            skillState.InlineName = CapabilityConfigurationEditorSupport.NormalizeKey(editorModel.Name);
        }
        else if (editorModel.Kind == CapabilityKind.Tool)
        {
            if (string.IsNullOrWhiteSpace(toolState.RuntimeToolName))
            {
                toolState.RuntimeToolName = CapabilityConfigurationEditorSupport.NormalizeRuntimeToolName(editorModel.Name);
            }

            if (NeedsGeneratedToolImplementationKey(toolState.ImplementationKey))
            {
                toolState.ImplementationKey = $"external.{CapabilityConfigurationEditorSupport.NormalizeKey(editorModel.Name)}";
            }
        }

        return Task.CompletedTask;
    }

    private Task HandleKeyChangedAsync(string? value)
    {
        keyWasEdited = true;
        editorModel.Key = CapabilityConfigurationEditorSupport.NormalizeKey(value ?? string.Empty);
        if (editorModel.Kind == CapabilityKind.Tool)
        {
            ApplyToolIdentityDefaults();
        }

        return Task.CompletedTask;
    }

    private Task HandleTagsChangedAsync(IReadOnlyList<string> tags)
    {
        capabilityTags = NormalizeTags(tags);
        return Task.CompletedTask;
    }

    private async Task UploadSkillAsync(InputFileChangeEventArgs args)
    {
        if (!IsCurrent || isBusy) {
            return;
        }
        isBusy = true;
        var file = args.File;
        uploadedSkillFileName = file.Name;

        try
        {
            await using var stream = file.OpenReadStream(MaxSkillUploadBytes, lifetime.Token);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var content = await reader.ReadToEndAsync(lifetime.Token);
            if (!IsCurrent) {
                return;
            }
            skillInputMode = CapabilityWizardSkillInputMode.Upload;
            skillState.SkillSource = "inline";
            skillState.InlineInstructions = content;
            if (string.IsNullOrWhiteSpace(editorModel.Name))
            {
                await HandleNameChangedAsync(Path.GetFileNameWithoutExtension(file.Name));
            }

            if (string.IsNullOrWhiteSpace(skillState.InlineName))
            {
                skillState.InlineName = CapabilityConfigurationEditorSupport.NormalizeKey(editorModel.Name);
            }

            NotificationService.Success("Skill file loaded", "SKILL.md content was loaded into the inline skill draft.");
        }
        catch (Exception) when (!IsCurrent) {
        }
        catch (Exception)
        {
            NotificationService.Error("Skill file upload failed", "The file could not be read.");
        } finally {
            if (IsCurrent) {
                isBusy = false;
            }
        }
    }

    private Task MoveBackAsync()
    {
        wizardStep = Math.Max(0, wizardStep - 1);
        return Task.CompletedTask;
    }

    private Task MoveNextAsync()
    {
        var errors = ValidateStep(wizardStep);
        if (errors.Count > 0)
        {
            NotificationService.Warning("Capability setup needs attention", string.Join(" ", errors));
            return Task.CompletedTask;
        }

        wizardStep = Math.Min(LastWizardStep, wizardStep + 1);
        return Task.CompletedTask;
    }

    private async Task CreateAsync()
    {
        if (!IsCurrent || isBusy)
        {
            return;
        }

        isBusy = true;
        try
        {
            var errors = PrepareEditorForSave();
            if (errors.Count > 0)
            {
                NotificationService.Warning("Capability was not created", string.Join(" ", errors));
                return;
            }

            var submission = JsonSerializer.Deserialize<CapabilityEditorModel>(JsonSerializer.SerializeToUtf8Bytes(editorModel))!;
            var capabilityId = await WorkspaceService.SaveCapabilityAsync(submission, CancellationToken.None);
            if (!IsCurrent) {
                return;
            }
            editorModel.Id = capabilityId;
            NotificationService.Success("Capability created", "Capability was added to the catalog.");
            if (DialogReference is not null)
            {
                await DialogReference.CloseAsync(new CapabilityDetailsDialogResult(capabilityId));
            }
        }
        catch (Exception) when (!IsCurrent) {
        }
        catch (Exception exception)
        {
            NotificationService.Error("Capability create failed", exception.Message);
        }
        finally
        {
            if (IsCurrent) {
                isBusy = false;
            }
        }
    }

    private IReadOnlyList<string> PrepareEditorForSave()
    {
        var errors = ValidateStep(0).Concat(ValidateStep(1)).ToList();
        editorModel.Key = string.IsNullOrWhiteSpace(editorModel.Key)
            ? CapabilityConfigurationEditorSupport.NormalizeKey(editorModel.Name)
            : CapabilityConfigurationEditorSupport.NormalizeKey(editorModel.Key);
        editorModel.Tags = NormalizeTags(capabilityTags).ToList();

        if (editorModel.Kind == CapabilityKind.McpServer)
        {
            errors.AddRange(CapabilityConfigurationEditorSupport.WriteMcp(editorModel, mcpState));
        }
        else if (editorModel.Kind == CapabilityKind.Skill)
        {
            ApplySkillModeBeforeSave();
            errors.AddRange(CapabilityConfigurationEditorSupport.WriteSkill(editorModel, skillState));
        }
        else
        {
            errors.AddRange(CapabilityConfigurationEditorSupport.WriteTool(editorModel, toolState));
        }

        return errors
            .Where(error => !string.IsNullOrWhiteSpace(error))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IReadOnlyList<string> ValidateStep(int step)
    {
        var errors = new List<string>();
        if (step == 0)
        {
            if (string.IsNullOrWhiteSpace(editorModel.Name))
            {
                errors.Add("Name is required.");
            }

            var key = string.IsNullOrWhiteSpace(editorModel.Key)
                ? CapabilityConfigurationEditorSupport.NormalizeKey(editorModel.Name)
                : CapabilityConfigurationEditorSupport.NormalizeKey(editorModel.Key);
            if (string.IsNullOrWhiteSpace(key))
            {
                errors.Add("Key is required.");
            }
        }
        else if (step == 1 && editorModel.Kind == CapabilityKind.Skill)
        {
            if (skillInputMode == CapabilityWizardSkillInputMode.FilePath &&
                string.IsNullOrWhiteSpace(skillState.SkillRoot))
            {
                errors.Add("Skill path setup requires a skill root or SKILL.md path.");
            }
            else if (skillInputMode != CapabilityWizardSkillInputMode.FilePath &&
                     string.IsNullOrWhiteSpace(skillState.InlineInstructions))
            {
                errors.Add("Inline or uploaded skill setup requires instructions.");
            }
        }
        else if (step == 1 && editorModel.Kind == CapabilityKind.Tool)
        {
            if (string.Equals(toolState.ToolKind, "externalHttp", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(toolState.Endpoint))
                {
                    errors.Add("External HTTP tool setup requires an endpoint.");
                }
            }
            else if (string.IsNullOrWhiteSpace(toolState.Command))
            {
                errors.Add("External process tool setup requires a command.");
            }
        }

        return errors;
    }

    private void ApplySkillModeBeforeSave()
    {
        if (skillInputMode == CapabilityWizardSkillInputMode.FilePath)
        {
            skillState.SkillSource = "file";
            return;
        }

        skillState.SkillSource = "inline";
        if (string.IsNullOrWhiteSpace(skillState.InlineName))
        {
            skillState.InlineName = CapabilityConfigurationEditorSupport.NormalizeKey(editorModel.Key);
        }

        if (string.IsNullOrWhiteSpace(skillState.InlineDescription))
        {
            skillState.InlineDescription = editorModel.Description;
        }
    }

    private void ApplyKindDefaults()
    {
        if (editorModel.Kind == CapabilityKind.McpServer)
        {
            capabilityTags = MergeTags(capabilityTags, ["mcp"]);
            if (string.IsNullOrWhiteSpace(mcpState.Transport))
            {
                mcpState.Transport = "stdio";
            }

            if (string.IsNullOrWhiteSpace(mcpState.ApprovalMode))
            {
                mcpState.ApprovalMode = "NeverRequire";
            }

            return;
        }

        if (editorModel.Kind == CapabilityKind.Tool)
        {
            capabilityTags = MergeTags(capabilityTags, ["tool", "external"]);
            if (string.IsNullOrWhiteSpace(toolState.ToolKind))
            {
                toolState.ToolKind = "externalProcess";
            }

            ApplyToolIdentityDefaults();

            return;
        }

        capabilityTags = MergeTags(capabilityTags, ["skill"]);
        skillState.SkillSource = skillInputMode == CapabilityWizardSkillInputMode.FilePath ? "file" : "inline";
        if (string.IsNullOrWhiteSpace(skillState.ScriptTrustLevel))
        {
            skillState.ScriptTrustLevel = "WorkspaceSkillRoot";
        }
    }

    private string ResolveEndpointPreview()
    {
        if (editorModel.Kind == CapabilityKind.McpServer)
        {
            if (!string.IsNullOrWhiteSpace(mcpState.Command))
            {
                return mcpState.Command;
            }

            return string.IsNullOrWhiteSpace(mcpState.Endpoint) ? "Not configured" : mcpState.Endpoint;
        }

        if (editorModel.Kind == CapabilityKind.Tool)
        {
            return string.Equals(toolState.ToolKind, "externalHttp", StringComparison.OrdinalIgnoreCase)
                ? string.IsNullOrWhiteSpace(toolState.Endpoint) ? "Not configured" : toolState.Endpoint
                : string.IsNullOrWhiteSpace(toolState.Command) ? "Not configured" : toolState.Command;
        }

        if (skillInputMode == CapabilityWizardSkillInputMode.FilePath)
        {
            if (string.IsNullOrWhiteSpace(skillState.SkillRoot))
            {
                return "Not configured";
            }

            return Path.GetFileName(skillState.SkillRoot).Equals("SKILL.md", StringComparison.OrdinalIgnoreCase)
                ? skillState.SkillRoot
                : Path.Combine(skillState.SkillRoot, "SKILL.md");
        }

        var key = string.IsNullOrWhiteSpace(editorModel.Key)
            ? CapabilityConfigurationEditorSupport.NormalizeKey(editorModel.Name)
            : editorModel.Key;
        return $"inline://{key}";
    }

    private string ResolveReviewSummary()
    {
        if (editorModel.Kind == CapabilityKind.McpServer)
        {
            var toolCount = CapabilityConfigurationEditorSupport.SplitLines(mcpState.AllowedToolsText).Count;
            return $"{mcpState.Transport} MCP setup with {toolCount} allowed tool(s).";
        }

        if (editorModel.Kind == CapabilityKind.Tool)
        {
            return string.Equals(toolState.ToolKind, "externalHttp", StringComparison.OrdinalIgnoreCase)
                ? $"{toolState.HttpMethod} external HTTP tool setup."
                : "External process tool setup.";
        }

        return skillInputMode switch
        {
            CapabilityWizardSkillInputMode.FilePath => "File-backed skill entry using the configured workspace or allowed external root.",
            CapabilityWizardSkillInputMode.Upload => "Uploaded SKILL.md will be persisted as inline skill instructions.",
            _ => "Inline skill entry stored directly in the capability catalog."
        };
    }

    private Task CancelAsync() {
        Dispose();
        return DialogReference?.CloseAsync() ?? Task.CompletedTask;
    }

    private void ApplyToolIdentityDefaults()
    {
        var capabilityKey = CapabilityConfigurationEditorSupport.NormalizeKey(editorModel.Key);
        if (string.IsNullOrWhiteSpace(capabilityKey))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(toolState.RuntimeToolName))
        {
            toolState.RuntimeToolName = CapabilityConfigurationEditorSupport.NormalizeRuntimeToolName(capabilityKey);
        }

        if (NeedsGeneratedToolImplementationKey(toolState.ImplementationKey))
        {
            toolState.ImplementationKey = $"external.{capabilityKey}";
        }
    }

    private static bool NeedsGeneratedToolImplementationKey(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ||
               string.Equals(value.Trim(), "external.", StringComparison.OrdinalIgnoreCase);
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

    private static IReadOnlyList<string> MergeTags(IEnumerable<string> current, IEnumerable<string> additions)
    {
        return NormalizeTags(current.Concat(additions));
    }

    private static string ResolveCapabilityKindLabel(CapabilityKind kind)
    {
        return kind switch
        {
            CapabilityKind.McpServer => "MCP server",
            CapabilityKind.Tool => "Tool",
            _ => kind.ToString()
        };
    }

    private enum CapabilityWizardSkillInputMode
    {
        FilePath,
        Inline,
        Upload
    }
    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        ownerRegistration.Dispose();
        lifetime.Cancel();
        lifetime.Dispose();
    }
}
