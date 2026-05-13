using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Workspace;

public enum SettingsRendererTrustLevel
{
    Application,
    BundledPlugin
}

public sealed record SettingsRendererDescriptor
{
    public SettingsRendererDescriptor(
        string RendererKey,
        Type ComponentType,
        SettingsRendererTrustLevel TrustLevel,
        string OwnerId,
        string SupportedSchemaVersion)
    {
        this.RendererKey = NormalizeRendererKey(RendererKey);
        this.ComponentType = ValidateComponentType(ComponentType);
        this.TrustLevel = TrustLevel;
        this.OwnerId = NormalizeRequired(OwnerId, nameof(OwnerId));
        this.SupportedSchemaVersion = NormalizeRequired(SupportedSchemaVersion, nameof(SupportedSchemaVersion));
    }

    public string RendererKey { get; }

    public Type ComponentType { get; }

    public SettingsRendererTrustLevel TrustLevel { get; }

    public string OwnerId { get; }

    public string SupportedSchemaVersion { get; }

    private static string NormalizeRendererKey(string rendererKey)
    {
        if (string.IsNullOrWhiteSpace(rendererKey))
        {
            throw new ArgumentException("Settings renderer key is required.", nameof(rendererKey));
        }

        return rendererKey.Trim();
    }

    private static Type ValidateComponentType(Type componentType)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        if (!typeof(IComponent).IsAssignableFrom(componentType))
        {
            throw new ArgumentException($"Settings renderer component '{componentType.FullName}' must implement {nameof(IComponent)}.", nameof(componentType));
        }

        return componentType;
    }

    private static string NormalizeRequired(
        string value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        return value.Trim();
    }
}

public interface ISettingsRendererSource
{
    IReadOnlyList<SettingsRendererDescriptor> ListRenderers();
}

public sealed class StaticSettingsRendererSource(IReadOnlyList<SettingsRendererDescriptor> descriptors) : ISettingsRendererSource
{
    public IReadOnlyList<SettingsRendererDescriptor> ListRenderers()
    {
        return descriptors;
    }
}

public interface ISettingsRendererRegistry
{
    IReadOnlyList<SettingsRendererDescriptor> ListRenderers();

    SettingsRendererDescriptor? FindRenderer(string? rendererKey);
}

public sealed class SettingsRendererRegistry : ISettingsRendererRegistry
{
    private readonly IReadOnlyDictionary<string, SettingsRendererDescriptor> descriptorsByKey;

    public SettingsRendererRegistry(IEnumerable<ISettingsRendererSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var descriptors = sources
            .SelectMany(source => source.ListRenderers())
            .ToArray();

        descriptorsByKey = BuildRegistry(descriptors);
    }

    public IReadOnlyList<SettingsRendererDescriptor> ListRenderers()
    {
        return descriptorsByKey.Values
            .OrderBy(descriptor => descriptor.RendererKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public SettingsRendererDescriptor? FindRenderer(string? rendererKey)
    {
        return string.IsNullOrWhiteSpace(rendererKey)
            ? null
            : descriptorsByKey.GetValueOrDefault(rendererKey.Trim());
    }

    private static IReadOnlyDictionary<string, SettingsRendererDescriptor> BuildRegistry(
        IReadOnlyList<SettingsRendererDescriptor> descriptors)
    {
        var byKey = new Dictionary<string, SettingsRendererDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var descriptor in descriptors)
        {
            if (byKey.TryGetValue(descriptor.RendererKey, out var existing))
            {
                throw new InvalidOperationException(
                    $"Settings renderer key '{descriptor.RendererKey}' is already registered by '{existing.OwnerId}' and cannot also be registered by '{descriptor.OwnerId}'.");
            }

            byKey.Add(descriptor.RendererKey, descriptor);
        }

        return byKey;
    }
}
