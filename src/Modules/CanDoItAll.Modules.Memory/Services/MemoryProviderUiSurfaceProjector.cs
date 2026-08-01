using CanDoItAll.Memory.Abstractions;
using System.Text.Json;

namespace CanDoItAll.Modules.Memory.Services;

public sealed class MemoryProviderUiSurfaceProjector(
    IMemoryProviderUiSurfaceComponentRegistry componentRegistry)
{
    public IReadOnlyList<MemoryProviderUiSurfaceProjection> Project(MemoryProviderManagementProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var supportedCapabilities = provider.Capabilities
            .Where(capability => capability.Supported)
            .Select(capability => capability.Id)
            .ToHashSet();

        return provider.UiSurfaces
            .Select((surface, index) => ProjectSurface(provider, surface, supportedCapabilities, index))
            .ToArray();
    }

    private MemoryProviderUiSurfaceProjection ProjectSurface(
        MemoryProviderManagementProfile provider,
        MemoryProviderUiSurface surface,
        IReadOnlySet<MemoryCapabilityId> supportedCapabilities,
        int index)
    {
        var name = string.IsNullOrWhiteSpace(surface.Name)
            ? surface.Kind.ToString()
            : surface.Name.Trim();
        var surfaceId = MemoryProviderUiSurfaceId.Create(index, name);
        if (!provider.IsEnabled || provider.HealthState != MemoryProviderHealthState.Healthy)
        {
            return Unavailable(
                surface,
                surfaceId,
                name,
                MemoryProviderUiSurfaceAvailability.ProviderUnavailable,
                "Selected provider must be enabled and healthy before provider UI can render.");
        }

        if (!supportedCapabilities.Contains(surface.CapabilityId))
        {
            return Unavailable(
                surface,
                surfaceId,
                name,
                MemoryProviderUiSurfaceAvailability.CapabilityUnavailable,
                $"Required capability '{surface.CapabilityId.Value}' is not declared by the selected provider.");
        }

        return surface.Kind switch
        {
            MemoryProviderUiSurfaceKind.RazorComponentLibrary => ProjectRclSurface(surface, surfaceId, name),
            MemoryProviderUiSurfaceKind.Iframe or MemoryProviderUiSurfaceKind.ExternalUrl =>
                ProjectUrlSurface(surface, provider.Extensions, surfaceId, name),
            _ => Unavailable(
                surface,
                surfaceId,
                name,
                MemoryProviderUiSurfaceAvailability.UnsupportedKind,
                "Provider UI surface kind is not supported.")
        };
    }

    private MemoryProviderUiSurfaceProjection ProjectRclSurface(
        MemoryProviderUiSurface surface,
        string surfaceId,
        string name)
    {
        if (string.IsNullOrWhiteSpace(surface.ComponentKey))
        {
            return Unavailable(
                surface,
                surfaceId,
                name,
                MemoryProviderUiSurfaceAvailability.MissingComponentRegistration,
                "Provider UI surface did not declare a component key.");
        }

        var componentKey = surface.ComponentKey.Trim();
        if (!componentRegistry.TryResolve(componentKey, out var componentType))
        {
            return Unavailable(
                surface with { ComponentKey = componentKey },
                surfaceId,
                name,
                MemoryProviderUiSurfaceAvailability.MissingComponentRegistration,
                $"No RCL component is registered for '{componentKey}'.");
        }

        return new MemoryProviderUiSurfaceProjection(
            surfaceId,
            surface.Kind,
            name,
            componentKey,
            Url: null,
            surface.CapabilityId,
            MemoryProviderUiSurfaceAvailability.Available,
            "Provider RCL surface is available.",
            componentType);
    }

    private static MemoryProviderUiSurfaceProjection ProjectUrlSurface(
        MemoryProviderUiSurface surface,
        MemoryExtensionData extensions,
        string surfaceId,
        string name)
    {
        if (string.IsNullOrWhiteSpace(surface.UrlSettingKey))
        {
            return Unavailable(
                surface,
                surfaceId,
                name,
                MemoryProviderUiSurfaceAvailability.MissingUrl,
                "Provider UI surface did not declare a URL setting key.");
        }

        if (!TryGetExtensionString(extensions, surface.UrlSettingKey.Trim(), out var configuredUrl))
        {
            return Unavailable(
                surface,
                surfaceId,
                name,
                MemoryProviderUiSurfaceAvailability.MissingUrl,
                "Provider UI URL is not configured.");
        }

        if (!TryNormalizeProviderUiUrl(configuredUrl, out var safeUrl))
        {
            return Unavailable(
                surface,
                surfaceId,
                name,
                MemoryProviderUiSurfaceAvailability.InvalidUrl,
                "Provider UI URL must use HTTPS or loopback HTTP.");
        }

        return new MemoryProviderUiSurfaceProjection(
            surfaceId,
            surface.Kind,
            name,
            surface.ComponentKey,
            safeUrl,
            surface.CapabilityId,
            MemoryProviderUiSurfaceAvailability.Available,
            "Provider URL surface is available.",
            ComponentType: null);
    }

    private static bool TryGetExtensionString(
        MemoryExtensionData extensions,
        string key,
        out string value)
    {
        value = string.Empty;
        if (!extensions.Values.TryGetValue(key, out var element) ||
            element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryNormalizeProviderUiUrl(string configuredUrl, out string safeUrl)
    {
        safeUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(configuredUrl) ||
            !Uri.TryCreate(configuredUrl.Trim(), UriKind.Absolute, out var uri) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)))
        {
            return false;
        }

        var isHttps = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        var isLoopbackHttp = string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && uri.IsLoopback;
        if (!isHttps && !isLoopbackHttp)
        {
            return false;
        }

        safeUrl = uri.AbsoluteUri;
        return true;
    }

    private static MemoryProviderUiSurfaceProjection Unavailable(
        MemoryProviderUiSurface surface,
        string surfaceId,
        string name,
        MemoryProviderUiSurfaceAvailability availability,
        string diagnostic) =>
        new(
            surfaceId,
            surface.Kind,
            name,
            surface.ComponentKey,
            Url: null,
            surface.CapabilityId,
            availability,
            diagnostic,
            ComponentType: null);
}

internal static class MemoryProviderUiSurfaceId
{
    public static string Create(int index, string name)
    {
        var chars = name
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray();
        var normalizedName = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(normalizedName)
            ? $"surface-{index}"
            : $"surface-{index}-{normalizedName}";
    }
}
