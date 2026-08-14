using System.Text.Json;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessExternalTargetRootBindingResolver
{
    internal static IReadOnlyList<ExternalTargetRootBinding> Resolve(
        IReadOnlyDictionary<string, string> launchVariables,
        IReadOnlyList<string> trustedAliases)
    {
        ArgumentNullException.ThrowIfNull(launchVariables);
        ArgumentNullException.ThrowIfNull(trustedAliases);

        var requiredRootIds = trustedAliases
            .Select(ResolveVersionedAliasRootId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (requiredRootIds.Count == 0)
        {
            return [];
        }

        if (!launchVariables.TryGetValue(
                ProcessRuntimeLaunchVariables.ExternalTargetRootBindings,
                out var bindingJson) ||
            string.IsNullOrWhiteSpace(bindingJson))
        {
            throw new InvalidOperationException(
                "A versioned external-target alias requires a protected root binding in process launch variables.");
        }

        ExternalTargetRootBinding[] bindings;
        try
        {
            bindings = JsonSerializer.Deserialize<ExternalTargetRootBinding[]>(bindingJson) ?? [];
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "The protected external-target root bindings in process launch variables are malformed.",
                exception);
        }

        if (bindings.Any(binding =>
                binding is null ||
                string.IsNullOrWhiteSpace(binding.RootId) ||
                binding.RootId.Length != ExternalTargetAliasCodec.RootIdLength ||
                !binding.RootId.All(Uri.IsHexDigit) ||
                string.IsNullOrWhiteSpace(binding.HostPlatform) ||
                string.IsNullOrWhiteSpace(binding.ProtectedRootToken)))
        {
            throw new InvalidOperationException(
                "The protected external-target root bindings in process launch variables are malformed.");
        }

        var selectedBindings = bindings
            .Where(binding => requiredRootIds.Contains(binding.RootId))
            .ToArray();
        if (selectedBindings
                .Select(binding => binding.RootId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() != requiredRootIds.Count)
        {
            throw new InvalidOperationException(
                "A versioned external-target alias has no matching protected root binding in process launch variables.");
        }

        return selectedBindings;
    }

    private static string ResolveVersionedAliasRootId(string alias)
    {
        if (!ExternalTargetAliasCodec.TryParseVersionedAlias(
                alias,
                out var rootId,
                out _,
                out _))
        {
            throw new InvalidOperationException(
                "A trusted external-target alias is not a valid versioned alias.");
        }

        return rootId;
    }
}
