using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

internal static class CapabilityTemplateSeedMaterializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<CapabilityCatalogItem> MaterializeDefaultCapabilities(CapabilityTemplatePack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);

        return pack.Capabilities
            .Select(template => Materialize(template, pack.Manifest.SeedVersion, pack.RootPath))
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static CapabilityCatalogItem Materialize(
        CapabilitySeedTemplateDescriptor template,
        string seedVersion,
        string packRoot)
    {
        var kind = ParseKind(template.Kind, template.Key);
        var configurationJson = BuildConfigurationJson(template, seedVersion, packRoot);
        return new CapabilityCatalogItem(
            CreateStableGuid(Require(template.StableGuidKey, template.Key, "stableGuidKey")),
            kind,
            Require(template.Key, template.Key, "key"),
            Require(template.DisplayName, template.Key, "displayName"),
            Require(template.Description, template.Key, "description"),
            ResolveEndpointOrPath(template, kind),
            configurationJson,
            CapabilityProofStatus.NotRun,
            template.ProofNotes?.Trim() ?? string.Empty,
            null,
            template.IsBuiltIn)
        {
            Tags = template.Tags
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static string BuildConfigurationJson(
        CapabilitySeedTemplateDescriptor template,
        string seedVersion,
        string packRoot)
    {
        return NormalizeKind(template.Kind) switch
        {
            "skill" when string.Equals(template.SkillSource, "file", StringComparison.OrdinalIgnoreCase) =>
                BuildFileSkillConfiguration(template, seedVersion),
            "skill" when string.Equals(template.SkillSource, "inline", StringComparison.OrdinalIgnoreCase) =>
                BuildInlineSkillConfiguration(template, seedVersion, packRoot),
            "tool" => BuildToolConfiguration(template, seedVersion),
            "aiContext" => SerializeConfiguration(new { message = Require(template.Message, template.Key, "message"), role = "system" }),
            _ => BuildRawConfiguration(template, seedVersion)
        };
    }

    private static string BuildFileSkillConfiguration(CapabilitySeedTemplateDescriptor template, string seedVersion)
    {
        var skillRoot = SandboxWorkspaceSeedAssets.Current.GetSkillRoot(Require(template.SkillRootKey, template.Key, "skillRootKey"));
        var allowExternalRoot = Path.IsPathRooted(skillRoot) || skillRoot.StartsWith("~", StringComparison.Ordinal);
        return SerializeConfiguration(new
        {
            managedSeedVersion = seedVersion,
            skillSource = "file",
            skillRoot,
            allowedExternalRoots = allowExternalRoot ? new[] { skillRoot } : Array.Empty<string>(),
            scriptApproval = true,
            scriptExecution = new
            {
                approvalRequired = true,
                trustLevel = allowExternalRoot ? "ExternalSkillRoot" : "WorkspaceSkillRoot"
            }
        });
    }

    private static string BuildInlineSkillConfiguration(
        CapabilitySeedTemplateDescriptor template,
        string seedVersion,
        string packRoot)
    {
        var inlineSkill = template.InlineSkill
            ?? throw new InvalidOperationException($"Capability template '{template.Key}' is missing inlineSkill settings.");
        var inlineSkillConfiguration = new
        {
            inlineSkill = new
            {
                name = Require(inlineSkill.Name, template.Key, "inlineSkill.name"),
                description = Require(inlineSkill.Description, template.Key, "inlineSkill.description"),
                instructions = ReadTemplateAsset(packRoot, Require(
                    inlineSkill.InstructionsAssetKey,
                    template.Key,
                    "inlineSkill.instructionsAssetKey"),
                    template.Key,
                    "inlineSkill.instructionsAssetKey"),
                resources = inlineSkill.Resources.Count > 0
                    ? inlineSkill.Resources.Select(resource => new
                    {
                        resource.Name,
                        Content = ResolveResourceContent(resource, packRoot),
                        resource.Description
                    }).ToArray()
                    : null
            }
        };

        if (!template.IncludeManagedSeedVersion)
        {
            return SerializeConfiguration(new
            {
                skillSource = "inline",
                inlineSkillConfiguration.inlineSkill
            });
        }

        return SerializeConfiguration(new
        {
            managedSeedVersion = seedVersion,
            skillSource = "inline",
            inlineSkillConfiguration.inlineSkill
        });
    }

    private static string BuildToolConfiguration(CapabilitySeedTemplateDescriptor template, string seedVersion)
    {
        var configuration = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["managedSeedVersion"] = seedVersion,
            ["tool"] = Require(template.RuntimeToolName, template.Key, "runtimeToolName"),
            ["approvalRequired"] = template.ApprovalRequired
        };

        foreach (var item in template.AdditionalConfiguration)
        {
            configuration[item.Key] = ConvertSeedConfigurationValue(item.Value);
        }

        return SerializeConfiguration(configuration);
    }

    private static string BuildRawConfiguration(CapabilitySeedTemplateDescriptor template, string seedVersion)
    {
        if (template.Configuration.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return "{}";
        }

        var configuration = ConvertSeedConfigurationValue(template.Configuration);
        if (configuration is Dictionary<string, object?> dictionary &&
            string.Equals(template.ExcludePathsSource, "workspaceRagDefault", StringComparison.OrdinalIgnoreCase))
        {
            dictionary["excludePaths"] = WorkspaceRetrievalNoisePolicy.BuildSeedWorkspaceRagExcludedPaths();
        }

        if (template.IncludeManagedSeedVersion &&
            configuration is Dictionary<string, object?> versionedDictionary &&
            !versionedDictionary.ContainsKey("managedSeedVersion"))
        {
            versionedDictionary["managedSeedVersion"] = seedVersion;
        }

        return SerializeConfiguration(configuration);
    }

    private static string ResolveEndpointOrPath(CapabilitySeedTemplateDescriptor template, CapabilityKind kind)
    {
        if (kind == CapabilityKind.Skill &&
            string.Equals(template.SkillSource, "file", StringComparison.OrdinalIgnoreCase))
        {
            var skillRoot = SandboxWorkspaceSeedAssets.Current.GetSkillRoot(Require(template.SkillRootKey, template.Key, "skillRootKey"));
            return Path.Combine(skillRoot, "SKILL.md");
        }

        if (kind == CapabilityKind.AiContext)
        {
            return string.Empty;
        }

        return Require(template.EndpointOrPath, template.Key, "endpointOrPath");
    }

    private static string ResolveResourceContent(
        InlineSkillResourceTemplate resource,
        string packRoot)
    {
        if (!string.IsNullOrWhiteSpace(resource.ContentAssetKey))
        {
            return ReadTemplateAsset(
                packRoot,
                resource.ContentAssetKey.Trim(),
                resource.Name,
                "inlineSkill.resources[].contentAssetKey");
        }

        return resource.Content ?? string.Empty;
    }

    private static string ReadTemplateAsset(
        string packRoot,
        string relativePath,
        string templateKey,
        string fieldPath)
    {
        var fullPath = ResolveTemplateAssetPath(packRoot, relativePath, templateKey, fieldPath);
        return File.ReadAllText(fullPath, Encoding.UTF8);
    }

    internal static string ResolveTemplateAssetPath(
        string packRoot,
        string relativePath,
        string templateKey,
        string fieldPath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidOperationException($"Capability template '{templateKey}' is missing required asset path '{fieldPath}'.");
        }

        var normalizedRelativePath = relativePath.Trim().Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalizedRelativePath))
        {
            throw new InvalidOperationException($"Capability template '{templateKey}' asset path '{fieldPath}' must be relative to the capability template pack root.");
        }

        var root = Path.GetFullPath(packRoot);
        var fullPath = Path.GetFullPath(Path.Combine(root, normalizedRelativePath));
        if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Capability template '{templateKey}' asset path '{fieldPath}' escapes the capability template pack root.");
        }

        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException($"Capability template '{templateKey}' asset path '{fieldPath}' was not found: {relativePath}");
        }

        return fullPath;
    }

    private static CapabilityKind ParseKind(string value, string key)
    {
        var normalized = NormalizeKind(value);
        foreach (var kind in Enum.GetValues<CapabilityKind>())
        {
            if (string.Equals(NormalizeKind(kind.ToString()), normalized, StringComparison.OrdinalIgnoreCase))
            {
                return kind;
            }
        }

        throw new InvalidOperationException($"Capability template '{key}' has unsupported kind '{value}'.");
    }

    private static string NormalizeKind(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal);

    private static object? ConvertSeedConfigurationValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number when value.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when value.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.Array => value.EnumerateArray().Select(ConvertSeedConfigurationValue).ToList(),
            JsonValueKind.Object => value.EnumerateObject().ToDictionary(
                property => property.Name,
                property => ConvertSeedConfigurationValue(property.Value),
                StringComparer.OrdinalIgnoreCase),
            _ => value.ToString()
        };
    }

    private static Guid CreateStableGuid(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        Span<byte> buffer = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(buffer);
        buffer[6] = (byte)((buffer[6] & 0x0F) | 0x50);
        buffer[8] = (byte)((buffer[8] & 0x3F) | 0x80);
        return new Guid(buffer);
    }

    private static string Require(string value, string key, string label)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Capability template '{key}' is missing required setting '{label}'.")
            : value.Trim();
    }

    private static string SerializeConfiguration<T>(T value)
        => JsonSerializer.Serialize(value, SerializerOptions);
}
