using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.DataProtection;

namespace CanDoItAll.Infrastructure.Storage;

public sealed class ExternalTargetPathRegistryFactory : IExternalTargetPathRegistryFactory
{
    private readonly IDataProtectionProvider dataProtectionProvider;
    private readonly IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory;

    public ExternalTargetPathRegistryFactory()
        : this(new EphemeralDataProtectionProvider(), new PhysicalFileSystemPathPolicyFactory())
    {
    }

    public ExternalTargetPathRegistryFactory(IDataProtectionProvider dataProtectionProvider)
        : this(dataProtectionProvider, new PhysicalFileSystemPathPolicyFactory())
    {
    }

    public ExternalTargetPathRegistryFactory(
        IDataProtectionProvider dataProtectionProvider,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory)
    {
        this.dataProtectionProvider = dataProtectionProvider ??
            throw new ArgumentNullException(nameof(dataProtectionProvider));
        this.physicalPathPolicyFactory = physicalPathPolicyFactory ??
            throw new ArgumentNullException(nameof(physicalPathPolicyFactory));
    }

    public IExternalTargetPathRegistry Create(IEnumerable<ExternalTargetRootBinding> bindings)
    {
        return new ExternalTargetPathRegistry(
            bindings,
            dataProtectionProvider,
            physicalPathPolicyFactory);
    }
}

public sealed class ExternalTargetPathRegistry : IExternalTargetPathRegistry
{
    private const string ProtectionPurpose = "CanDoItAll.ExternalTargetRootBinding.v1";

    private readonly Dictionary<string, BoundRoot> bindingsById = new(StringComparer.Ordinal);
    private readonly IDataProtector protector;
    private readonly IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory;

    public ExternalTargetPathRegistry()
        : this(new EphemeralDataProtectionProvider(), new PhysicalFileSystemPathPolicyFactory())
    {
    }

    public ExternalTargetPathRegistry(IDataProtectionProvider dataProtectionProvider)
        : this(dataProtectionProvider, new PhysicalFileSystemPathPolicyFactory())
    {
    }

    public ExternalTargetPathRegistry(
        IDataProtectionProvider dataProtectionProvider,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        this.physicalPathPolicyFactory = physicalPathPolicyFactory ??
            throw new ArgumentNullException(nameof(physicalPathPolicyFactory));
        protector = dataProtectionProvider.CreateProtector(ProtectionPurpose);
    }

    internal ExternalTargetPathRegistry(
        IEnumerable<ExternalTargetRootBinding> bindings,
        IDataProtectionProvider dataProtectionProvider,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory)
        : this(dataProtectionProvider, physicalPathPolicyFactory)
    {
        ImportBindings(bindings);
    }

    public bool TryCreateAlias(string physicalPath, out string alias)
    {
        alias = string.Empty;
        if (!TryGetNativeFullPath(physicalPath, out var fullPath))
        {
            return false;
        }

        var filesystemRoot = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(filesystemRoot))
        {
            return false;
        }

        IPhysicalFileSystemPathPolicy nativeRootPolicy;
        try
        {
            nativeRootPolicy = physicalPathPolicyFactory.Create(filesystemRoot);
            nativeRootPolicy.EnsureSafePath(fullPath, allowMissingLeaf: true);
        }
        catch (PhysicalPathValidationException)
        {
            return false;
        }

        if (nativeRootPolicy.PathComparer.Equals(
                Path.TrimEndingDirectorySeparator(fullPath),
                Path.TrimEndingDirectorySeparator(filesystemRoot)))
        {
            return false;
        }

        var boundRoot = bindingsById.Values
            .Where(candidate => candidate.PhysicalRootPath is not null)
            .Where(candidate => physicalPathPolicyFactory
                .Create(candidate.PhysicalRootPath!)
                .IsWithinRoot(fullPath))
            .OrderByDescending(candidate => candidate.PhysicalRootPath!.Length)
            .FirstOrDefault();
        if (boundRoot is null)
        {
            boundRoot = CreateBinding(fullPath);
            bindingsById.Add(boundRoot.Binding.RootId, boundRoot);
        }

        var relativePath = Path.GetRelativePath(boundRoot.PhysicalRootPath!, fullPath);
        var segments = string.Equals(relativePath, ".", StringComparison.Ordinal)
            ? []
            : relativePath.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);
        alias = ExternalTargetAliasCodec.BuildAlias(boundRoot.Binding.RootId, segments);
        return true;
    }

    public ExternalTargetAliasResolutionKind TryResolve(
        string alias,
        out string fullPath,
        out string validationMessage)
    {
        fullPath = string.Empty;
        validationMessage = string.Empty;
        if (!ExternalTargetAliasCodec.TryParseVersionedAlias(
                alias,
                out var rootId,
                out var physicalSegments,
                out validationMessage))
        {
            return string.IsNullOrEmpty(validationMessage)
                ? ExternalTargetAliasResolutionKind.NotVersionedAlias
                : ExternalTargetAliasResolutionKind.Invalid;
        }

        if (!bindingsById.TryGetValue(rootId, out var boundRoot) ||
            boundRoot.PhysicalRootPath is null)
        {
            validationMessage = "The external-target root is not bound on this host and requires explicit rebind or migration.";
            return ExternalTargetAliasResolutionKind.Unbound;
        }

        if (physicalSegments.Any(ContainsNativeSeparator))
        {
            validationMessage = "The external-target alias contains a physical path segment that is invalid on this host.";
            return ExternalTargetAliasResolutionKind.Invalid;
        }

        try
        {
            var rootPathPolicy = physicalPathPolicyFactory.Create(boundRoot.PhysicalRootPath);
            fullPath = physicalSegments.Count == 0
                ? rootPathPolicy.RootPath
                : rootPathPolicy.ResolveContainedPath(Path.Combine(physicalSegments.ToArray()));
            rootPathPolicy.EnsureSafePath(fullPath, allowMissingLeaf: true);
            return ExternalTargetAliasResolutionKind.Resolved;
        }
        catch (PhysicalPathValidationException exception)
        {
            validationMessage = exception.Message;
            return ExternalTargetAliasResolutionKind.Invalid;
        }
    }

    public string MigrateLegacyAliasForWrite(string alias)
    {
        if (ExternalTargetAliasCodec.NormalizeVersionedAlias(alias) is { } versionedAlias)
        {
            return versionedAlias;
        }

        if (!ExternalTargetAliasCodec.TryNormalizeLegacyAlias(alias, out var legacyAlias))
        {
            throw new InvalidOperationException("The external-target alias is not a supported legacy or versioned alias.");
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new InvalidOperationException(
                "A legacy Windows external-target alias cannot be written on this host. Rebind it to a native external root first.");
        }

        var segments = legacyAlias.Split('/');
        var physicalPath = Path.GetFullPath(Path.Combine(
            $"{segments[1]}:{Path.DirectorySeparatorChar}",
            Path.Combine(segments.Skip(2).ToArray())));
        if (!TryCreateAlias(physicalPath, out var migratedAlias))
        {
            throw new InvalidOperationException("The legacy external-target alias could not be rebound on this host.");
        }

        return migratedAlias;
    }

    public IReadOnlyList<ExternalTargetRootBinding> ExportBindings(IEnumerable<string> aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);
        var rootIds = aliases
            .Select(alias => ExternalTargetAliasCodec.TryParseVersionedAlias(alias, out var rootId, out _, out _)
                ? rootId
                : null)
            .Where(rootId => rootId is not null)
            .ToHashSet(StringComparer.Ordinal);

        return rootIds
            .Select(rootId => bindingsById.GetValueOrDefault(rootId!))
            .Where(boundRoot => boundRoot is not null)
            .Select(boundRoot => boundRoot!.Binding)
            .OrderBy(binding => binding.RootId, StringComparer.Ordinal)
            .ToArray();
    }

    private void ImportBindings(IEnumerable<ExternalTargetRootBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        foreach (var binding in bindings)
        {
            EnsureValidBinding(binding);
            var normalizedBinding = binding with
            {
                RootId = binding.RootId.ToLowerInvariant(),
                HostPlatform = binding.HostPlatform.Trim().ToLowerInvariant()
            };
            var physicalRootPath = IsCurrentHostBinding(normalizedBinding)
                ? UnprotectRoot(normalizedBinding)
                : null;
            var boundRoot = new BoundRoot(normalizedBinding, physicalRootPath);

            if (bindingsById.TryGetValue(normalizedBinding.RootId, out var existing) &&
                existing.Binding != normalizedBinding)
            {
                throw new InvalidOperationException(
                    $"Conflicting external-target root bindings use identity '{normalizedBinding.RootId}'.");
            }

            bindingsById[normalizedBinding.RootId] = boundRoot;
        }
    }

    private BoundRoot CreateBinding(string fullPath)
    {
        string rootId;
        do
        {
            rootId = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
        }
        while (bindingsById.ContainsKey(rootId));

        var payload = new ProtectedRootPayload(rootId, CurrentHostPlatform, fullPath);
        var protectedToken = protector.Protect(JsonSerializer.Serialize(payload));
        var binding = new ExternalTargetRootBinding(rootId, CurrentHostPlatform, protectedToken);
        return new BoundRoot(binding, fullPath);
    }

    private string UnprotectRoot(ExternalTargetRootBinding binding)
    {
        try
        {
            var payloadJson = protector.Unprotect(binding.ProtectedRootToken);
            var payload = JsonSerializer.Deserialize<ProtectedRootPayload>(payloadJson) ??
                throw new InvalidOperationException("The protected external-target root payload is empty.");
            if (!string.Equals(payload.RootId, binding.RootId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(payload.HostPlatform, binding.HostPlatform, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The protected external-target root payload does not match its binding identity.");
            }

            if (!TryGetNativeFullPath(payload.PhysicalRootPath, out var fullPath))
            {
                throw new InvalidOperationException("The protected external-target root is not a native absolute path for this host.");
            }

            var filesystemRoot = Path.GetPathRoot(fullPath)!;
            physicalPathPolicyFactory.Create(filesystemRoot).EnsureSafePath(
                fullPath,
                allowMissingLeaf: true);

            return fullPath;
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            throw new InvalidOperationException(
                "The protected external-target root binding cannot be opened on this host and requires explicit rebind or migration.",
                exception);
        }
    }

    private static void EnsureValidBinding(ExternalTargetRootBinding binding)
    {
        if (binding is null ||
            binding.RootId.Length != ExternalTargetAliasCodec.RootIdLength ||
            !binding.RootId.All(Uri.IsHexDigit) ||
            string.IsNullOrWhiteSpace(binding.HostPlatform) ||
            string.IsNullOrWhiteSpace(binding.ProtectedRootToken))
        {
            throw new InvalidOperationException("An external-target root binding is malformed.");
        }
    }

    private static bool TryGetNativeFullPath(string path, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(path, "external-target physical path");
            if (!Path.IsPathRooted(path))
            {
                return false;
            }

            fullPath = Path.GetFullPath(path);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool ContainsNativeSeparator(string segment)
    {
        return segment.Contains(Path.DirectorySeparatorChar) ||
               segment.Contains(Path.AltDirectorySeparatorChar);
    }

    private static bool IsCurrentHostBinding(ExternalTargetRootBinding binding)
    {
        return string.Equals(binding.HostPlatform, CurrentHostPlatform, StringComparison.Ordinal);
    }

    private static string CurrentHostPlatform => OperatingSystem.IsWindows()
        ? "windows"
        : OperatingSystem.IsMacOS()
            ? "macos"
            : OperatingSystem.IsLinux()
                ? "linux"
                : "other";

    private sealed record BoundRoot(
        ExternalTargetRootBinding Binding,
        string? PhysicalRootPath);

    private sealed record ProtectedRootPayload(
        string RootId,
        string HostPlatform,
        string PhysicalRootPath);
}
