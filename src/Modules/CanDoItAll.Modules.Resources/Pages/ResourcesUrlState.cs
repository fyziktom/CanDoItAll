using CanDoItAll.AppComponents.Navigation;

namespace CanDoItAll.Modules.Resources.Pages;

public enum ResourcesView {
    Registry,
    Browse
}

// Both tabs keep their state while the other one is visible, so no key depends on the view.
public sealed record ResourcesUrlState(
    ResourcesView View = ResourcesView.Registry,
    Guid? ResourceId = null,
    Guid? ProjectId = null,
    string? Search = null,
    Guid? ProjectFilter = null,
    string? Connector = null,
    ResourceValidationStatus? Validation = null,
    ResourceFileSourceKey? Source = null) {
    public bool HasSameFilters(ResourcesUrlState other)
        => string.Equals(Search, other.Search, StringComparison.Ordinal) &&
           ProjectFilter == other.ProjectFilter &&
           string.Equals(Connector, other.Connector, StringComparison.Ordinal) &&
           Validation == other.Validation;
}

public sealed class ResourcesUrlCodec : IUrlStateCodec<ResourcesUrlState> {
    public const string ViewKey = "view";
    public const string ResourceKey = "resourceId";
    public const string ProjectKey = "projectId";
    public const string SearchKey = "q";
    public const string ProjectFilterKey = "project";
    public const string ConnectorKey = "connector";
    public const string ValidationKey = "validation";
    public const string SourceKey = "source";
    public const string Path = "/resources";
    public const int MaximumSearchLength = 512;
    public const int MaximumConnectorLength = 200;

    public static ResourcesUrlCodec Instance { get; } = new();

    public static UrlTokenMap<ResourcesView> Views { get; } = new(
        (ResourcesView.Registry, "registry"),
        (ResourcesView.Browse, "browse"));

    public static UrlTokenMap<ResourceValidationStatus> Validations { get; } = new(
        (ResourceValidationStatus.Unknown, "unknown"),
        (ResourceValidationStatus.Valid, "valid"),
        (ResourceValidationStatus.Warning, "warning"),
        (ResourceValidationStatus.Invalid, "invalid"));

    public IReadOnlyList<string> Keys { get; } = [
        ViewKey, ResourceKey, ProjectKey, SearchKey, ProjectFilterKey, ConnectorKey, ValidationKey, SourceKey
    ];

    public ResourcesUrlState Read(UrlQuery query) => new(
        query.GetToken(ViewKey, Views, ResourcesView.Registry),
        query.GetId(ResourceKey),
        query.GetId(ProjectKey),
        query.GetText(SearchKey, MaximumSearchLength),
        query.GetId(ProjectFilterKey),
        query.GetText(ConnectorKey, MaximumConnectorLength),
        query.GetToken(ValidationKey, Validations),
        ResourceFileSourceKey.TryParse(query.Get(SourceKey), out var source) ? source : null);

    public void Write(ResourcesUrlState state, UrlQueryWriter query) {
        query.Token(ViewKey, state.View, Views, ResourcesView.Registry);
        query.Id(ResourceKey, state.ResourceId);
        query.Id(ProjectKey, state.ProjectId);
        query.Text(SearchKey, state.Search);
        query.Id(ProjectFilterKey, state.ProjectFilter);
        query.Text(ConnectorKey, state.Connector);
        query.Token(ValidationKey, state.Validation, Validations);
        query.Text(SourceKey, state.Source?.Value);
    }
}
