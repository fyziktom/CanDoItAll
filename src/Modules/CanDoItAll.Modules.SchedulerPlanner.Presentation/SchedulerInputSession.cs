using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SchedulerPlanner.UI;

namespace CanDoItAll.Modules.SchedulerPlanner.Presentation;

internal sealed class SchedulerInputSession(SchedulerDraft draft, ISchedulerWorkspaceOwner owner, Action changed) : IDisposable {
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly SchedulerReadLane schemaLane = new();
    private readonly Dictionary<string, SchedulerReadLane> optionLanes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> optionDependencies = new(StringComparer.Ordinal);
    private bool disposed;

    public async Task LoadAsync() {
        using var load = schemaLane.Begin();
        var target = draft.Values;
        foreach (var lane in optionLanes.Values) {
            lane.Dispose();
        }
        optionDependencies.Clear();
        draft.Schema = null;
        draft.Options.Clear();
        draft.InputValues.Clear();
        draft.SchemaStatus = SchedulerReadStatus.Loading;
        changed();
        if (target.TargetKind != SchedulerPlanTargetKind.Workflow || target.TargetId == Guid.Empty) {
            draft.SchemaStatus = SchedulerReadStatus.Ready;
            changed();
            return;
        }
        try {
            var schema = await owner.SchemaAsync(target, load.Token);
            if (!load.IsCurrent || !SameTarget(target, draft.Values)) {
                return;
            }
            draft.Schema = schema;
            draft.SchemaStatus = SchedulerReadStatus.Ready;
            ParseRaw();
            await OptionsAsync();
        } catch (OperationCanceledException) when (load.Token.IsCancellationRequested) {
        } catch (Exception exception) {
            if (load.IsCurrent && SameTarget(target, draft.Values)) {
                draft.SchemaStatus = SchedulerReadStatus.Unavailable;
                draft.Error = exception.Message;
            }
        } finally {
            changed();
        }
    }

    public async Task RawAsync(string value) {
        draft.Values = draft.Values with { InputJson = value };
        ParseRaw();
        await OptionsAsync();
    }

    public async Task InputAsync(WorkflowInputParameterDescriptor parameter, string value) {
        if (draft.InputValues.GetValueOrDefault(parameter.Key) == value) {
            return;
        }
        draft.MarkInputEdited();
        draft.InputValues[parameter.Key] = value;
        if (!TryParse(draft.Values.InputJson, out var root, out var error)) {
            draft.Issues = [new(string.Empty, error)];
            changed();
            return;
        }
        if (!TryRootProperty(parameter, out var property)) {
            draft.Issues = [new(parameter.Key, "This path requires the advanced JSON editor.")];
            changed();
            return;
        }
        draft.Issues = draft.Issues.Where(item => item.ParameterKey != parameter.Key).ToArray();
        if (string.IsNullOrWhiteSpace(value)) {
            root.Remove(property);
        } else if (parameter.Kind is WorkflowInputParameterKind.Integer or WorkflowInputParameterKind.DurationMinutes) {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)) {
                draft.Issues = [.. draft.Issues, new(parameter.Key, "Enter a complete integer.")];
                changed();
                return;
            }
            root[property] = number;
        } else {
            root[property] = value;
        }
        foreach (var dependent in draft.Schema?.Parameters.Where(item => item.Key != parameter.Key && item.OptionSource.DependsOnParameterKey == parameter.Key) ?? []) {
            draft.InputValues.Remove(dependent.Key);
            draft.Issues = draft.Issues.Where(item => item.ParameterKey != dependent.Key).ToArray();
            if (TryRootProperty(dependent, out var dependentProperty)) {
                root.Remove(dependentProperty);
            }
        }
        draft.Values = draft.Values with { InputJson = root.ToJsonString(JsonOptions) };
        changed();
        await OptionsAsync();
    }

    private void ParseRaw() {
        draft.HasUnappliedInput = false;
        draft.InputValues.Clear();
        draft.Issues = [];
        if (!TryParse(draft.Values.InputJson, out var root, out var error)) {
            draft.Issues = [new(string.Empty, error)];
            return;
        }
        foreach (var parameter in draft.Schema?.Parameters ?? []) {
            if (TryRootProperty(parameter, out var property) && root[property] is JsonValue value) {
                draft.InputValues[parameter.Key] = value.TryGetValue<string>(out var text) ? text : value.ToJsonString();
            } else if (!string.IsNullOrWhiteSpace(parameter.DefaultValue)) {
                draft.InputValues[parameter.Key] = parameter.DefaultValue;
            }
        }
    }

    private Task OptionsAsync() => Task.WhenAll((draft.Schema?.Parameters ?? []).Select(OptionsAsync));

    private async Task OptionsAsync(WorkflowInputParameterDescriptor parameter) {
        var dependency = parameter.OptionSource.DependsOnParameterKey;
        var dependencyValue = dependency is null ? null : draft.InputValues.GetValueOrDefault(dependency);
        if (optionDependencies.TryGetValue(parameter.Key, out var previous) && previous == dependencyValue) {
            return;
        }
        optionDependencies[parameter.Key] = dependencyValue;
        if (!optionLanes.TryGetValue(parameter.Key, out var lane)) {
            optionLanes[parameter.Key] = lane = new();
        }
        using var load = lane.Begin();
        var target = draft.Values;
        var values = new Dictionary<string, string>(draft.InputValues, StringComparer.Ordinal);
        var retained = draft.Options.GetValueOrDefault(parameter.Key)?.Values ?? [];
        draft.Options[parameter.Key] = new(SchedulerReadStatus.Loading, retained);
        changed();
        try {
            var options = parameter.OptionSource.Kind switch {
                WorkflowInputParameterOptionSourceKind.None => [],
                WorkflowInputParameterOptionSourceKind.Static => parameter.OptionSource.StaticOptions,
                _ => await owner.OptionsAsync(parameter, values, load.Token)
            };
            if (load.IsCurrent && SameTarget(target, draft.Values)) {
                draft.Options[parameter.Key] = new(SchedulerReadStatus.Ready, options);
            }
        } catch (OperationCanceledException) when (load.Token.IsCancellationRequested) {
        } catch (Exception exception) {
            if (load.IsCurrent && SameTarget(target, draft.Values)) {
                draft.Options[parameter.Key] = new(SchedulerReadStatus.Unavailable, retained, exception.Message);
                optionDependencies.Remove(parameter.Key);
            }
        } finally {
            changed();
        }
    }

    internal static bool SameTarget(SchedulerDraftValues left, SchedulerDraftValues right)
        => (left.TargetKind, left.TargetId, left.TargetVersionId) == (right.TargetKind, right.TargetId, right.TargetVersionId);

    internal static bool TryParse(string raw, out JsonObject root, out string error) {
        try {
            root = JsonNode.Parse(raw) as JsonObject ?? throw new JsonException("Workflow input must be a JSON object.");
            error = string.Empty;
            return true;
        } catch (JsonException exception) {
            root = null!;
            error = exception.Message;
            return false;
        }
    }

    private static bool TryRootProperty(WorkflowInputParameterDescriptor parameter, out string property) {
        var path = string.IsNullOrWhiteSpace(parameter.JsonPath) ? "$." + parameter.Key : parameter.JsonPath.Trim();
        property = path.StartsWith("$.", StringComparison.Ordinal) ? path[2..] : string.Empty;
        return property.Length > 0 && !property.Contains('.', StringComparison.Ordinal) && !property.Contains('[', StringComparison.Ordinal);
    }

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        schemaLane.Dispose();
        foreach (var lane in optionLanes.Values) {
            lane.Dispose();
        }
    }
}
