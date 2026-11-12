using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Workspace.StorageCatalog.Contracts;

namespace CanDoItAll.Workspace.StorageCatalog.UI;

internal static class CatalogSummaries {
    internal static StorageSummaryModel Catalog(CatalogSession session) {
        var selected = session.Catalog.Value.FirstOrDefault(row => row.Id == session.SelectedId);
        return selected is null ? new() {
            Eyebrow = "Workspace storage lanes", Title = $"{session.Catalog.Value.Length} catalog target(s)",
            Description = "Shared by project attachments, prompt exports, evidence packages and release artifacts.",
            Badges = [new($"{session.Catalog.Value.Count(row => row.IsEnabled)} enabled", "success"), new($"{session.Routes.Value.Count(route => route.IsEnabled)} defaults", "info")],
            Facts = [new("Healthy", session.Catalog.Value.Count(row => row.Health.Status == CatalogHealth.Healthy).ToString()),
                new("Preview ready", session.Catalog.Value.Count(row => row.Health.Capabilities.HasFlag(CatalogCapability.InlinePreview)).ToString())]
        } : new() {
            Eyebrow = "Selected storage target", Title = selected.Name, Description = session.ProviderLabel(selected.ProviderKind),
            Badges = Badges(session, selected.Health, selected.IsReadOnly, selected.IsSystemDefault),
            Facts = [new("Endpoint", selected.EndpointOrRoot), new("Last check", selected.Health.TestedAtUtc?.LocalDateTime.ToString("g") ?? "Not tested")],
            Footnote = selected.Health.Message
        };
    }
    internal static StorageSummaryModel Capabilities(CatalogSession session, CatalogDraft draft) {
        var choice = session.Choices.Providers.FirstOrDefault(item => item.Value == draft.ProviderKind);
        var mask = draft.IsReadOnly ? choice?.ReadOnlyCapabilities ?? CatalogCapability.None : choice?.WritableCapabilities ?? CatalogCapability.None;
        return new() {
            Eyebrow = "Capability preview", Title = session.ProviderLabel(draft.ProviderKind),
            Description = "Provider policy preview. Only an explicit Test can establish health for this configuration.",
            Badges = Badges(session, draft.Health, draft.IsReadOnly, draft.IsSystemDefault),
            Facts = session.Choices.Capabilities.Where(item => mask.HasFlag(item.Value)).Select(item => new StorageSummaryFact(item.Label, "Available in the provider policy.")).ToArray(),
            Footnote = draft.Health.Message
        };
    }
    private static IReadOnlyList<StorageSummaryBadge> Badges(CatalogSession session, CatalogHealthFact health, bool readOnly, bool system) {
        List<StorageSummaryBadge> badges = [new(health.Status.ToString(), HealthTone(health.Status))];
        if (readOnly) {
            badges.Add(new("Read only", "warning"));
        }
        if (system) {
            badges.Add(new("System default", "info"));
        }
        badges.AddRange(session.Choices.Capabilities.Where(item => health.Capabilities.HasFlag(item.Value)).Take(4).Select(item => new StorageSummaryBadge(item.Label)));
        return badges;
    }
    internal static string HealthTone(CatalogHealth health) => health switch {
        CatalogHealth.Healthy => "success", CatalogHealth.Degraded => "warning", CatalogHealth.Unavailable => "danger", _ => "neutral"
    };
}
