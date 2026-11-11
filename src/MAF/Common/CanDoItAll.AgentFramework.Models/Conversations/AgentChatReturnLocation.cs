namespace CanDoItAll.AgentFramework.Models;

public sealed record AgentChatReturnLocation {
    public AgentChatReturnLocation(string route) {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        if (route.Length > AgentChatPositionLimits.MaximumRouteLength || !route.StartsWith('/') ||
                route.StartsWith("//", StringComparison.Ordinal) || route.Contains('\\') ||
                route.Any(char.IsControl) || route != route.Trim()) {
            throw new ArgumentException("An Agent chat return location must be a bounded application-relative route.", nameof(route));
        }
        var path = route.Split('?', '#')[0];
        var decodedPath = Uri.UnescapeDataString(path);
        if (decodedPath.StartsWith("//", StringComparison.Ordinal) || decodedPath.Contains('\\') ||
                decodedPath.Any(char.IsControl) || decodedPath.Split('/').Any(segment => segment is "." or "..")) {
            throw new ArgumentException("An Agent chat return location cannot escape the application path.", nameof(route));
        }
        Route = route;
    }

    public string Route { get; }

    public Uri ResolveUri(Uri applicationBaseUri) {
        ArgumentNullException.ThrowIfNull(applicationBaseUri);
        if (!applicationBaseUri.IsAbsoluteUri || !applicationBaseUri.AbsolutePath.EndsWith('/') ||
                !string.IsNullOrEmpty(applicationBaseUri.Query) || !string.IsNullOrEmpty(applicationBaseUri.Fragment)) {
            throw new ArgumentException("An application base URI must identify an absolute directory without a query or fragment.", nameof(applicationBaseUri));
        }
        var target = new Uri(applicationBaseUri, "." + Route);
        if (!string.Equals(target.Scheme, applicationBaseUri.Scheme, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(target.Authority, applicationBaseUri.Authority, StringComparison.OrdinalIgnoreCase) ||
                !applicationBaseUri.IsBaseOf(target)) {
            throw new InvalidOperationException("An Agent chat return location cannot escape the application base URI.");
        }
        return target;
    }

    public static AgentChatReturnLocation? FromContext(AgentChatContextSnapshot? context)
        => context?.Scope.SurfacePosition is { } surface ? new(surface.Route) : null;
}
