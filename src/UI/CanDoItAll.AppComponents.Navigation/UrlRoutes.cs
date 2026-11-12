using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.AppComponents.Navigation;

public static class UrlRoutes {
    public static IReadOnlyList<string> Templates(Type page) {
        ArgumentNullException.ThrowIfNull(page);
        var templates = page.GetCustomAttributes<RouteAttribute>(inherit: false).Select(route => route.Template).ToArray();
        if (templates.Length == 0) {
            throw new ArgumentException($"{page.Name} declares no route.", nameof(page));
        }
        return templates;
    }

    public static bool Matches(IEnumerable<string> templates, string path) {
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(path);
        var end = path.IndexOfAny(['?', '#']);
        var segments = Segments(end < 0 ? path : path[..end]).Select(Uri.UnescapeDataString).ToArray();
        return templates.Any(template => Matches(Segments(template), segments));
    }

    private static bool Matches(string[] template, string[] path) {
        for (var index = 0; index < template.Length; index++) {
            var part = template[index];
            if (part.StartsWith("{*", StringComparison.Ordinal)) {
                return true;
            }
            if (index >= path.Length) {
                return part.StartsWith('{') && part.EndsWith("?}", StringComparison.Ordinal) && index == template.Length - 1;
            }
            if (!part.StartsWith('{') && !string.Equals(part, path[index], StringComparison.OrdinalIgnoreCase)) {
                return false;
            }
        }
        return template.Length == path.Length;
    }

    private static string[] Segments(string path) => path.Split('/', StringSplitOptions.RemoveEmptyEntries);
}
