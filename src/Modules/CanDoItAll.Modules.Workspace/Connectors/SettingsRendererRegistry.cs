using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Workspace;

public enum SettingsRendererTrustLevel
{
    Application,
    BundledPlugin
}

public enum SettingsRendererResolutionStatus
{
    NotRequested,
    IncompleteRequest,
    Resolved,
    NotRegistered,
    TrustMismatch,
    OwnerMismatch,
    SchemaVersionMismatch
}

public sealed record SettingsRendererResolutionRequest
{
    public SettingsRendererResolutionRequest(
        string rendererKey,
        SettingsRendererTrustLevel trustLevel,
        string ownerId,
        string schemaVersion)
    {
        if (!Enum.IsDefined(trustLevel))
        {
            throw new ArgumentOutOfRangeException(nameof(trustLevel), trustLevel, "Settings renderer trust level is not defined.");
        }

        RendererKey = NormalizeRequired(rendererKey, nameof(rendererKey));
        TrustLevel = trustLevel;
        OwnerId = NormalizeRequired(ownerId, nameof(ownerId));
        SchemaVersion = NormalizeRequired(schemaVersion, nameof(schemaVersion));
    }

    public string RendererKey { get; }

    public SettingsRendererTrustLevel TrustLevel { get; }

    public string OwnerId { get; }

    public string SchemaVersion { get; }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        return value.Trim();
    }
}

public sealed record SettingsRendererResolution(
    SettingsRendererResolutionStatus Status,
    SettingsRendererDescriptor? Descriptor)
{
    public bool IsResolved => Status == SettingsRendererResolutionStatus.Resolved && Descriptor is not null;
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
        if (!Enum.IsDefined(TrustLevel))
        {
            throw new ArgumentOutOfRangeException(nameof(TrustLevel), TrustLevel, "Settings renderer trust level is not defined.");
        }

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

    SettingsRendererResolution ResolveRenderer(SettingsRendererResolutionRequest request);
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

    public SettingsRendererResolution ResolveRenderer(SettingsRendererResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!descriptorsByKey.TryGetValue(request.RendererKey, out var descriptor))
        {
            return new(SettingsRendererResolutionStatus.NotRegistered, Descriptor: null);
        }

        if (descriptor.TrustLevel != request.TrustLevel)
        {
            return new(SettingsRendererResolutionStatus.TrustMismatch, Descriptor: null);
        }

        if (!string.Equals(descriptor.OwnerId, request.OwnerId, StringComparison.OrdinalIgnoreCase))
        {
            return new(SettingsRendererResolutionStatus.OwnerMismatch, Descriptor: null);
        }

        if (!string.Equals(
                descriptor.SupportedSchemaVersion,
                request.SchemaVersion,
                StringComparison.OrdinalIgnoreCase))
        {
            return new(SettingsRendererResolutionStatus.SchemaVersionMismatch, Descriptor: null);
        }

        return new(SettingsRendererResolutionStatus.Resolved, descriptor);
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
