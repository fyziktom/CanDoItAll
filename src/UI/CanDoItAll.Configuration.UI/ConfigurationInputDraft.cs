using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Configuration.UI;

public sealed record ConfigurationSecretOption(Guid Id, string Name);

public sealed class ConfigurationInputDraft(ConfigurationState state) {
    private readonly Dictionary<string, string> raw = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> versions = new(StringComparer.OrdinalIgnoreCase);
    public ConfigurationState State { get; } = state;
    public string Text(string key) => raw.TryGetValue(key, out var value) ? value : State.GetText(key);
    public long Version(string key) => versions.GetValueOrDefault(key);
    public IReadOnlyDictionary<string, long> CaptureVersions() => new Dictionary<string, long>(versions, StringComparer.OrdinalIgnoreCase);

    public void SetText(string key, string text) {
        raw[key] = text;
        versions[key] = Version(key) + 1;
        State.SetText(key, text);
    }

    public void Reconcile(ConfigurationState submitted, ConfigurationState stored, IReadOnlyDictionary<string, long> submittedVersions) {
        foreach (var key in submitted.Values.Keys.Concat(stored.Values.Keys).Distinct(StringComparer.OrdinalIgnoreCase)) {
            if (Version(key) == submittedVersions.GetValueOrDefault(key) && State.GetText(key) == submitted.GetText(key)) {
                State.SetText(key, stored.GetText(key));
                raw.Remove(key);
            }
        }
    }
}
