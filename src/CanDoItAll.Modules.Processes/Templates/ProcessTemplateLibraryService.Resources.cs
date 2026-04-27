using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessTemplateLibraryService
{
    private static IReadOnlyList<ProcessTemplateLibraryLinkedResource> BuildProcessRoleLinks(
        ProcessTemplatePack pack,
        ProcessTemplateDefinition process)
    {
        var roles = new List<ProcessTemplateLibraryLinkedResource>();

        foreach (var roleKey in process.SharedRoleRefs)
        {
            if (!pack.SharedRoles.TryGetValue(roleKey, out var role))
            {
                continue;
            }

            roles.Add(new ProcessTemplateLibraryLinkedResource(
                BuildSharedRoleItemId(role.Key),
                role.Key,
                role.DisplayName,
                role.Summary,
                "Shared role library",
                string.Empty,
                "Shared role library"));
        }

        foreach (var roleKey in process.LocalRoleRefs)
        {
            var role = process.LocalRoles.FirstOrDefault(item =>
                string.Equals(item.Key, roleKey, StringComparison.OrdinalIgnoreCase));
            if (role is null)
            {
                continue;
            }

            roles.Add(new ProcessTemplateLibraryLinkedResource(
                BuildLocalRoleItemId(process.Key, role.Key),
                role.Key,
                role.DisplayName,
                role.Summary,
                process.DisplayName,
                process.Key,
                process.DisplayName));
        }

        return roles;
    }

    private static IReadOnlyList<ProcessTemplateLibraryLinkedResource> BuildProcessArtifactLinks(
        ProcessTemplatePack pack,
        ProcessTemplateDefinition process)
    {
        var artifacts = new List<ProcessTemplateLibraryLinkedResource>();

        foreach (var artifactKey in process.SharedArtifactRefs)
        {
            if (!pack.SharedArtifacts.TryGetValue(artifactKey, out var artifact))
            {
                continue;
            }

            artifacts.Add(new ProcessTemplateLibraryLinkedResource(
                BuildSharedArtifactItemId(artifact.Key),
                artifact.Key,
                artifact.DisplayName,
                artifact.Summary,
                "Shared artifact library",
                string.Empty,
                "Shared artifact library"));
        }

        foreach (var artifactKey in process.LocalArtifactRefs)
        {
            var artifact = process.LocalArtifacts.FirstOrDefault(item =>
                string.Equals(item.Key, artifactKey, StringComparison.OrdinalIgnoreCase));
            if (artifact is null)
            {
                continue;
            }

            artifacts.Add(new ProcessTemplateLibraryLinkedResource(
                BuildLocalArtifactItemId(process.Key, artifact.Key),
                artifact.Key,
                artifact.DisplayName,
                artifact.Summary,
                process.DisplayName,
                process.Key,
                process.DisplayName));
        }

        return artifacts;
    }

    private static IEnumerable<RoleDescriptor> EnumerateRoleDescriptors(ProcessTemplatePack pack)
    {
        foreach (var role in pack.SharedRoles.Values.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            yield return new RoleDescriptor(
                BuildSharedRoleItemId(role.Key),
                role,
                string.Empty,
                "Shared role library",
                true);
        }

        foreach (var process in pack.Processes.Values.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var role in process.LocalRoles.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                yield return new RoleDescriptor(
                    BuildLocalRoleItemId(process.Key, role.Key),
                    role,
                    process.Key,
                    process.DisplayName,
                    false);
            }
        }
    }

    private static IEnumerable<ArtifactDescriptor> EnumerateArtifactDescriptors(ProcessTemplatePack pack)
    {
        foreach (var artifact in pack.SharedArtifacts.Values.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            yield return new ArtifactDescriptor(
                BuildSharedArtifactItemId(artifact.Key),
                artifact,
                string.Empty,
                "Shared artifact library",
                true);
        }

        foreach (var process in pack.Processes.Values.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var artifact in process.LocalArtifacts.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                yield return new ArtifactDescriptor(
                    BuildLocalArtifactItemId(process.Key, artifact.Key),
                    artifact,
                    process.Key,
                    process.DisplayName,
                    false);
            }
        }
    }

    private static ProcessTemplateDefinition ResolveProcess(ProcessTemplatePack pack, string itemId)
    {
        if (pack.Processes.TryGetValue(itemId, out var process))
        {
            return process;
        }

        throw new InvalidOperationException($"Process template '{itemId}' was not found in the template pack.");
    }

    private static RoleDescriptor ResolveRole(ProcessTemplatePack pack, string itemId)
    {
        const string sharedPrefix = "shared-role:";
        const string localPrefix = "process-role:";

        if (itemId.StartsWith(sharedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var roleKey = itemId[sharedPrefix.Length..];
            if (pack.SharedRoles.TryGetValue(roleKey, out var role))
            {
                return new RoleDescriptor(itemId, role, string.Empty, "Shared role library", true);
            }
        }
        else if (itemId.StartsWith(localPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var remainder = itemId[localPrefix.Length..];
            var separatorIndex = remainder.IndexOf(':');
            if (separatorIndex > 0)
            {
                var processKey = remainder[..separatorIndex];
                var roleKey = remainder[(separatorIndex + 1)..];
                var process = ResolveProcess(pack, processKey);
                var role = process.LocalRoles.FirstOrDefault(item =>
                    string.Equals(item.Key, roleKey, StringComparison.OrdinalIgnoreCase));
                if (role is not null)
                {
                    return new RoleDescriptor(itemId, role, process.Key, process.DisplayName, false);
                }
            }
        }

        throw new InvalidOperationException($"Role template '{itemId}' was not found in the template pack.");
    }

    private static ArtifactDescriptor ResolveArtifact(ProcessTemplatePack pack, string itemId)
    {
        const string sharedPrefix = "shared-artifact:";
        const string localPrefix = "process-artifact:";

        if (itemId.StartsWith(sharedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var artifactKey = itemId[sharedPrefix.Length..];
            if (pack.SharedArtifacts.TryGetValue(artifactKey, out var artifact))
            {
                return new ArtifactDescriptor(itemId, artifact, string.Empty, "Shared artifact library", true);
            }
        }
        else if (itemId.StartsWith(localPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var remainder = itemId[localPrefix.Length..];
            var separatorIndex = remainder.IndexOf(':');
            if (separatorIndex > 0)
            {
                var processKey = remainder[..separatorIndex];
                var artifactKey = remainder[(separatorIndex + 1)..];
                var process = ResolveProcess(pack, processKey);
                var artifact = process.LocalArtifacts.FirstOrDefault(item =>
                    string.Equals(item.Key, artifactKey, StringComparison.OrdinalIgnoreCase));
                if (artifact is not null)
                {
                    return new ArtifactDescriptor(itemId, artifact, process.Key, process.DisplayName, false);
                }
            }
        }

        throw new InvalidOperationException($"Artifact template '{itemId}' was not found in the template pack.");
    }

    private static string BuildSharedRoleItemId(string roleKey)
    {
        return $"shared-role:{roleKey}";
    }

    private static string BuildLocalRoleItemId(string processKey, string roleKey)
    {
        return $"process-role:{processKey}:{roleKey}";
    }

    private static string BuildSharedArtifactItemId(string artifactKey)
    {
        return $"shared-artifact:{artifactKey}";
    }

    private static string BuildLocalArtifactItemId(string processKey, string artifactKey)
    {
        return $"process-artifact:{processKey}:{artifactKey}";
    }

    private static string ResolveSiblingJsonPath(string? docPath)
    {
        if (string.IsNullOrWhiteSpace(docPath))
        {
            return string.Empty;
        }

        var jsonPath = Path.ChangeExtension(docPath, ".json");
        return File.Exists(jsonPath)
            ? jsonPath
            : string.Empty;
    }

    private static string ReadOptionalText(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return string.Empty;
        }

        return File.ReadAllText(path);
    }

    private static string NormalizeValue(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value
            .Replace('-', ' ')
            .Replace('_', ' ')
            .Trim();
        if (normalized.Length == 0)
        {
            return fallback;
        }

        var characters = new List<char>(normalized.Length + 8);
        for (var index = 0; index < normalized.Length; index++)
        {
            var current = normalized[index];
            if (index > 0 &&
                char.IsUpper(current) &&
                !char.IsWhiteSpace(normalized[index - 1]) &&
                !char.IsUpper(normalized[index - 1]))
            {
                characters.Add(' ');
            }

            characters.Add(current);
        }

        return new string(characters.ToArray());
    }

    private sealed record RoleDescriptor(
        string ItemId,
        ProcessTemplateRoleResource Resource,
        string ProcessKey,
        string ProcessDisplayName,
        bool IsShared);

    private sealed record ArtifactDescriptor(
        string ItemId,
        ProcessTemplateArtifactResource Resource,
        string ProcessKey,
        string ProcessDisplayName,
        bool IsShared);
}
