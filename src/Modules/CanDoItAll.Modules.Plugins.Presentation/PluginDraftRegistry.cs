using System.Text.Json;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.Plugins.UI;

namespace CanDoItAll.Modules.Plugins.Presentation;

public sealed class PluginDraftRegistry(PluginsWorkspaceView view) {
    public const int MaximumDrafts = 64;

    public void Reconcile(PluginSettingsDetail detail) {
        var pluginId = detail.CatalogItem.PluginId;
        foreach (var editor in view.Editors.Values.Where(editor => editor.Origin.Key.PluginId == pluginId)) {
            var descriptor = detail.ConnectionDescriptors.SingleOrDefault(item => item.Key == editor.ConnectionKey);
            editor.ReferenceChanged = descriptor is null || JsonSerializer.Serialize(descriptor) != editor.DescriptorIdentity ||
                (editor.ConnectionId is { } id && !detail.Connections.Any(item => item.Id == id));
            if (detail.Connections.SingleOrDefault(item => item.Id == editor.ConnectionId) is { } saved) {
                editor.RefreshExact(saved);
            }
        }
        foreach (var descriptor in detail.ConnectionDescriptors) {
            var key = new PluginEditorKey(pluginId, descriptor.Key);
            if (view.Editors.ContainsKey(key)) {
                continue;
            }
            MakeRoom();
            var connection = detail.Connections.Where(item => item.ConnectionKey == descriptor.Key)
                .OrderByDescending(item => item.UpdatedAtUtc).FirstOrDefault();
            view.Editors.Add(key, new(pluginId, descriptor, connection));
        }
    }

    public bool IsLive(PluginConnectionEditorState editor)
        => !view.IsDisposed && !editor.Retired && view.Editors.TryGetValue(editor.Origin.Key, out var current) && ReferenceEquals(editor, current);

    public void Reset(PluginConnectionEditorState editor, PluginConnectionId? connectionId, bool reviewedUnknown) {
        if (!IsLive(editor) || (editor.Operation?.PreventsReplay == true && !reviewedUnknown) || !view.Settings.IsCurrent ||
            view.Settings.Value is not { } detail || detail.CatalogItem.PluginId != editor.Origin.Key.PluginId) {
            return;
        }
        var descriptor = detail.ConnectionDescriptors.SingleOrDefault(item => item.Key == editor.ConnectionKey);
        var connection = connectionId is { } id ? detail.Connections.SingleOrDefault(item => item.Id == id && item.ConnectionKey == editor.ConnectionKey) : null;
        if (connectionId is not null && connection is null) {
            return;
        }
        editor.Retired = true;
        view.Operations.Remove(new PluginOperationTarget.Connection(editor.Origin));
        if (descriptor is null) {
            view.Editors.Remove(editor.Origin.Key);
        } else {
            view.Editors[editor.Origin.Key] = new(editor.Origin.Key.PluginId, descriptor, connection);
        }
    }

    private void MakeRoom() {
        if (view.Editors.Count < MaximumDrafts) {
            return;
        }
        var unused = view.Editors.Values.FirstOrDefault(editor => editor.Origin.Key.PluginId != view.SelectedPluginId &&
            !editor.IsDirty && editor.Operation?.PreventsReplay != true);
        if (unused is null) {
            view.Notice = "The retained draft limit was reached. Review and reset existing drafts before opening more connections.";
            view.NoticeStatus = PluginMutationStatus.Refused;
            throw new InvalidOperationException("Review or reset retained connection drafts before opening another editor.");
        }
        unused.Retired = true;
        view.Editors.Remove(unused.Origin.Key);
        view.Operations.Remove(new PluginOperationTarget.Connection(unused.Origin));
    }
}
