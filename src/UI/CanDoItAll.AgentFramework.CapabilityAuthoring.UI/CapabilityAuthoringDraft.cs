using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.AgentFramework.CapabilityAuthoring.UI;

public sealed class CapabilityAuthoringDraft : IDisposable {
    private readonly ValidationMessageStore messages;
    private bool keyWasEdited;

    public CapabilityAuthoringDraft(CapabilityEditorModel model, bool wizard) {
        Model = CapabilityAuthoringSubmission.Capture(model).ToEditorModel();
        Model.ConfigurationJson = CapabilityConfigurationCodec.RemoveForbiddenPlaintext(Model.Kind, Model.ConfigurationJson, out var removed);
        RemovedPlaintext = removed;
        IsWizard = wizard;
        Context = new(Model);
        messages = new(Context);
        Context.OnFieldChanged += FieldChanged;
        RawConfigurationJson = Model.ConfigurationJson;
        RefreshConfiguration();
    }

    public CapabilityEditorModel Model { get; }
    public EditContext Context { get; }
    public bool IsWizard { get; }
    public bool RemovedPlaintext { get; private set; }
    public long Revision { get; private set; }
    public McpCapabilityDraft Mcp { get; private set; } = new();
    public SkillCapabilityDraft Skill { get; private set; } = new();
    public ToolCapabilityDraft Tool { get; private set; } = new();
    public string RawConfigurationJson { get; set; }
    public string? ConfigurationError { get; private set; }
    public CapabilitySkillInputMode SkillMode { get; set; }
    public string UploadedFileName { get; set; } = string.Empty;
    public bool IdentityLocked => Model.IsBuiltIn && Model.Kind == CapabilityKind.Tool;
    public bool RawLocked => Model.IsBuiltIn || ConfigurationError is null && IsTyped;
    public bool IsTyped => Model.Kind is CapabilityKind.McpServer or CapabilityKind.Skill or CapabilityKind.Tool;

    public void ChangeKind(CapabilityKind kind) {
        if (Model.IsBuiltIn || Model.Kind == kind) {
            return;
        }
        Model.Kind = kind;
        Changed();
        RefreshConfiguration();
    }

    public void ChangeName(string? value) {
        Model.Name = value ?? string.Empty;
        if (IsWizard && !keyWasEdited) {
            Model.Key = CapabilityConfigurationCodec.NormalizeKey(Model.Name);
        }
        if (IsWizard && string.IsNullOrWhiteSpace(Skill.InlineName)) {
            Skill.InlineName = CapabilityConfigurationCodec.NormalizeKey(Model.Name);
        }
        if (IsWizard && string.IsNullOrWhiteSpace(Tool.RuntimeToolName)) {
            Tool.RuntimeToolName = CapabilityConfigurationCodec.NormalizeRuntimeToolName(Model.Name);
        }
        if (IsWizard && Tool.ImplementationKey is "" or "external.") {
            Tool.ImplementationKey = $"external.{CapabilityConfigurationCodec.NormalizeKey(Model.Name)}";
        }
    }

    public void ChangeKey(string? value) {
        keyWasEdited = true;
        Model.Key = value ?? string.Empty;
    }

    public void Changed() {
        Revision++;
        messages.Clear();
        Context.NotifyValidationStateChanged();
    }

    public bool ApplyRawConfiguration() {
        if (Model.IsBuiltIn) {
            return false;
        }
        var previous = Model.ConfigurationJson;
        Model.ConfigurationJson = CapabilityConfigurationCodec.RemoveForbiddenPlaintext(Model.Kind, RawConfigurationJson, out var removed);
        RefreshConfiguration();
        Changed();
        if (ConfigurationError is not null) {
            Model.ConfigurationJson = previous;
            return false;
        }
        RemovedPlaintext |= removed;
        RawConfigurationJson = Model.ConfigurationJson;
        return true;
    }

    public CapabilityAuthoringSubmission? Prepare(StringComparer environmentNameComparer, bool identityOnly = false) {
        messages.Clear();
        var errors = new List<string>();
        var submission = CapabilityAuthoringSubmission.Capture(Model).ToEditorModel();
        if (string.IsNullOrWhiteSpace(submission.Name)) {
            errors.Add("Name is required.");
        }
        submission.Key = CapabilityConfigurationCodec.NormalizeKey(string.IsNullOrWhiteSpace(submission.Key) ? submission.Name : submission.Key);
        if (string.IsNullOrWhiteSpace(submission.Key)) {
            errors.Add("Key is required.");
        }
        if (!identityOnly) {
            if (ConfigurationError is not null) {
                errors.Add(ConfigurationError);
            } else if (!IdentityLocked) {
                try {
                    switch (Model.Kind) {
                        case CapabilityKind.McpServer:
                            errors.AddRange(CapabilityConfigurationCodec.WriteMcp(submission, Mcp, environmentNameComparer));
                            break;
                        case CapabilityKind.Skill:
                            if (IsWizard) {
                                Skill.SkillSource = SkillMode switch {
                                    CapabilitySkillInputMode.FilePath => "file",
                                    CapabilitySkillInputMode.Registered => "registered",
                                    _ => "inline"
                                };
                            }
                            errors.AddRange(CapabilityConfigurationCodec.WriteSkill(submission, Skill));
                            break;
                        case CapabilityKind.Tool:
                            errors.AddRange(CapabilityConfigurationCodec.WriteTool(submission, Tool));
                            break;
                        default:
                            if (!Model.IsBuiltIn) {
                                using var json = JsonDocument.Parse(string.IsNullOrWhiteSpace(RawConfigurationJson) ? "{}" : RawConfigurationJson);
                                if (json.RootElement.ValueKind != JsonValueKind.Object) {
                                    throw new JsonException();
                                }
                                submission.ConfigurationJson = RawConfigurationJson;
                            }
                            break;
                    }
                } catch (JsonException) {
                    errors.Add("Configuration JSON must be a supported JSON object. Repair the raw configuration explicitly.");
                } catch (ArgumentException) {
                    errors.Add("A configuration identifier is invalid. Review the configuration before saving.");
                }
            }
        }
        foreach (var error in errors) {
            messages.Add(new FieldIdentifier(Model, string.Empty), error);
        }
        Context.NotifyValidationStateChanged();
        return Context.Validate() ? CapabilityAuthoringSubmission.Capture(submission) : null;
    }

    private void RefreshConfiguration() {
        ConfigurationError = null;
        try {
            switch (Model.Kind) {
                case CapabilityKind.McpServer:
                    Mcp = CapabilityConfigurationCodec.ReadMcp(Model);
                    if (IsWizard && string.IsNullOrWhiteSpace(Model.ConfigurationJson)) {
                        Mcp.Transport = "stdio";
                    }
                    if (Mcp.Transport is not ("stdio" or "http" or "sse" or "logical")) {
                        throw new JsonException();
                    }
                    break;
                case CapabilityKind.Skill:
                    Skill = CapabilityConfigurationCodec.ReadSkill(Model);
                    if (IsWizard && string.IsNullOrWhiteSpace(Model.ConfigurationJson)) {
                        Skill.ScriptTrustLevel = "WorkspaceSkillRoot";
                    }
                    SkillMode = Skill.SkillSource switch {
                        "file" => CapabilitySkillInputMode.FilePath,
                        "inline" => CapabilitySkillInputMode.Inline,
                        "registered" => CapabilitySkillInputMode.Registered,
                        _ => throw new JsonException()
                    };
                    break;
                case CapabilityKind.Tool:
                    Tool = CapabilityConfigurationCodec.ReadTool(Model);
                    if (Tool.ToolKind is not ("externalProcess" or "externalHttp")) {
                        throw new JsonException();
                    }
                    break;
            }
        } catch (JsonException) {
            ConfigurationError = "Stored configuration is malformed or unavailable. Repair it explicitly in Raw before saving or testing setup.";
        }
    }

    private void FieldChanged(object? sender, FieldChangedEventArgs args) => Changed();
    public void Dispose() => Context.OnFieldChanged -= FieldChanged;
}
