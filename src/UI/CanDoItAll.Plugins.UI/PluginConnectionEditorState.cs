using System.Text.Json;
using CanDoItAll.Modules.Plugins;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Plugins.UI;

public sealed record PluginConnectionSubmission(PluginDraftOrigin Origin, PluginConnectionSaveRequest Request,
    string RawName, bool RawEnabled, IReadOnlyDictionary<string, string> RawValues);

public sealed class PluginConnectionEditorState {
    private static readonly ConfigurationSchemaValidator Validator = new();
    private string acceptedName;
    private bool acceptedEnabled;
    private IReadOnlyDictionary<string, string> acceptedValues;

    public PluginConnectionEditorState(PluginId pluginId, PluginConnectionDescriptor descriptor, PluginConnectionItem? connection) {
        Origin = new(new(pluginId, descriptor.Key), connection?.Id, Guid.NewGuid());
        Descriptor = descriptor;
        DescriptorIdentity = JsonSerializer.Serialize(descriptor);
        ConnectionId = connection?.Id;
        ConcurrencyToken = connection?.ConcurrencyToken;
        DisplayName = acceptedName = connection?.DisplayName ?? descriptor.DisplayName;
        IsEnabled = acceptedEnabled = connection?.IsEnabled ?? true;
        State = ConfigurationState.FromJson(connection?.SettingsJson);
        acceptedValues = Copy(State.Values);
        Validate();
    }

    public PluginDraftOrigin Origin { get; }
    public PluginConnectionDescriptor Descriptor { get; }
    public string DescriptorIdentity { get; }
    public PluginConnectionId? ConnectionId { get; private set; }
    public Guid? ConcurrencyToken { get; private set; }
    public PluginConnectionKey ConnectionKey => Origin.Key.ConnectionKey;
    public string DisplayName { get; private set; }
    public bool IsEnabled { get; private set; }
    public ConfigurationState State { get; private set; }
    public ConfigurationValidationResult Validation { get; private set; } = ConfigurationValidationResult.Success;
    public bool ReferenceChanged { get; set; }
    public bool Retired { get; set; }
    public bool ReviewedUnknown { get; set; }
    public PluginConnectionSubmission? Submission { get; private set; }
    public PluginOperationState? Operation { get; set; }
    public bool IsDirty => DisplayName != acceptedName || IsEnabled != acceptedEnabled || !Equal(State.Values, acceptedValues);
    public bool PreventsReplay => Retired || ReferenceChanged || Operation?.PreventsReplay == true;

    public void SetName(string? value) {
        DisplayName = value ?? string.Empty;
        Validate();
    }

    public void SetEnabled(bool value) => IsEnabled = value;

    public void SetField(string key, string? value) {
        var values = Copy(State.Values);
        values[key] = value ?? string.Empty;
        State = new(values);
        Validate();
    }

    public PluginConnectionSubmission? Capture() {
        Validate();
        if (PreventsReplay || !Validation.Succeeded) {
            return null;
        }
        var filtered = State.Clone();
        filtered.KeepOnly(Descriptor.SettingsSchema.Fields.Select(field => field.Key));
        Submission = new(Origin, new(ConnectionId, ConnectionKey, DisplayName.Trim(), filtered.ToJson(), IsEnabled),
            DisplayName, IsEnabled, Copy(State.Values));
        return Submission;
    }

    public void Accept(PluginConnectionSubmission submission, PluginConnectionItem saved) {
        if (Retired || submission.Origin != Origin || saved.PluginId != Origin.Key.PluginId || saved.ConnectionKey != ConnectionKey) {
            return;
        }
        ConnectionId = saved.Id;
        ConcurrencyToken = saved.ConcurrencyToken;
        var accepted = ConfigurationState.FromJson(saved.SettingsJson).Values;
        if (DisplayName == submission.RawName) {
            DisplayName = saved.DisplayName;
        }
        if (IsEnabled == submission.RawEnabled) {
            IsEnabled = saved.IsEnabled;
        }
        var merged = Copy(State.Values);
        foreach (var key in submission.RawValues.Keys.Union(accepted.Keys, StringComparer.OrdinalIgnoreCase)) {
            if (Value(State.Values, key) != Value(submission.RawValues, key)) {
                continue;
            }
            if (accepted.TryGetValue(key, out var value)) {
                merged[key] = value;
            } else {
                merged.Remove(key);
            }
        }
        State = new(merged);
        acceptedName = saved.DisplayName;
        acceptedEnabled = saved.IsEnabled;
        acceptedValues = Copy(accepted);
        Validate();
    }

    public void RefreshExact(PluginConnectionItem saved) {
        if (ConnectionId != saved.Id || IsDirty || Operation?.PreventsReplay == true || ReferenceChanged) {
            return;
        }
        Accept(new(Origin, new(ConnectionId, ConnectionKey, DisplayName, State.ToJson(), IsEnabled),
            DisplayName, IsEnabled, Copy(State.Values)), saved);
    }

    private void Validate() {
        var issues = Validator.Validate(Descriptor.SettingsSchema, State).Issues.ToList();
        if (string.IsNullOrWhiteSpace(DisplayName)) {
            issues.Add(new("connectionName", "Connection name is required."));
        }
        Validation = new(issues);
    }

    private static Dictionary<string, string> Copy(IReadOnlyDictionary<string, string> values)
        => new(values, StringComparer.OrdinalIgnoreCase);
    private static string Value(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out var value) ? value : string.Empty;
    private static bool Equal(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right)
        => left.Keys.Union(right.Keys, StringComparer.OrdinalIgnoreCase).All(key => Value(left, key) == Value(right, key));
}
