using System.Reflection;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Capabilities.Templates;
using CanDoItAll.SharedKernel;
using ModelCapabilityKind = CanDoItAll.AgentFramework.Models.CapabilityKind;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed class CapabilityTemplatePackLoader
{
    private const string ManifestFileName = "manifest.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string? configuredPackRoot;
    private readonly Lazy<CapabilityTemplatePack> pack;

    public CapabilityTemplatePackLoader(string? packRoot = null)
    {
        configuredPackRoot = packRoot;
        pack = new Lazy<CapabilityTemplatePack>(LoadCore, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public CapabilityTemplatePack Load() => pack.Value;

    public static string FindPackRoot(string? packRoot = null) => ResolvePackRoot(packRoot);

    private CapabilityTemplatePack LoadCore()
    {
        var root = ResolvePackRoot(configuredPackRoot);
        var manifest = ReadJson<CapabilityTemplatePackManifest>(Path.Combine(root, ManifestFileName));
        var issues = new List<CapabilityValidationIssue>();
        var capabilities = new List<CapabilitySeedTemplateDescriptor>();

        foreach (var fileReference in manifest.CapabilityFiles)
        {
            var path = Path.GetFullPath(Path.Combine(root, Require(fileReference.RelativePath, "capability file relative path")));
            if (!File.Exists(path))
            {
                issues.Add(Issue(
                    CapabilityDiagnosticCategory.TemplateValidation,
                    null,
                    null,
                    RelativePath(root, path),
                    "$.capabilityFiles",
                    "Capability template file is missing.",
                    "Create the referenced file or remove it from the capability manifest."));
                continue;
            }

            var file = ReadJson<CapabilitySeedTemplateFile>(path);
            for (var index = 0; index < file.Capabilities.Count; index++)
            {
                var capability = file.Capabilities[index];
                capability.TemplatePath = RelativePath(root, path);
                capability.ManifestIndex = index;
                capabilities.Add(capability);
                issues.AddRange(ValidateCapability(capability, root));
            }
        }

        issues.AddRange(ValidateDuplicateKeys(capabilities));

        var compiledPolicies = new List<CapabilityTemplatePolicy>();
        foreach (var fileReference in manifest.PolicyFiles)
        {
            var path = Path.GetFullPath(Path.Combine(root, Require(fileReference.RelativePath, "policy file relative path")));
            var templatePath = TemplatePath.Create(RelativePath(root, path));
            if (!File.Exists(path))
            {
                issues.Add(Issue(
                    CapabilityDiagnosticCategory.TemplateValidation,
                    null,
                    null,
                    templatePath.Value,
                    "$.policyFiles",
                    "Capability policy template file is missing.",
                    "Create the referenced policy file or remove it from the capability manifest."));
                continue;
            }

            var policyTemplate = ReadJson<CapabilityAccessPolicyTemplateDto>(path);
            var compileResult = new CapabilityAccessPolicyTemplateCompiler().Compile(policyTemplate, templatePath);
            issues.AddRange(compileResult.ValidationResult.Issues);
            issues.AddRange(CapabilityTemplateSeedPolicyValidator.ValidatePolicyReferences(policyTemplate, templatePath, capabilities));
            if (compileResult.Policy is not null)
            {
                compiledPolicies.Add(new CapabilityTemplatePolicy(Require(fileReference.Key, "policy key"), templatePath, compileResult.Policy));
            }
        }

        if (issues.Any(issue => issue.Severity == CapabilityValidationSeverity.Error))
        {
            throw new CapabilityTemplatePackValidationException(issues);
        }

        return new CapabilityTemplatePack(
            root,
            manifest,
            capabilities
                .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            compiledPolicies
                .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    private static IReadOnlyList<CapabilityValidationIssue> ValidateCapability(
        CapabilitySeedTemplateDescriptor capability,
        string packRoot)
    {
        var templatePath = TemplatePath.Create(capability.TemplatePath);
        var descriptor = new CapabilityTemplateDescriptorDto
        {
            Kind = capability.Kind,
            Key = capability.Key,
            DisplayName = capability.DisplayName,
            Description = capability.Description,
            StableId = capability.StableId,
            RuntimeToolName = capability.RuntimeToolName,
            ImplementationKey = capability.ImplementationKey,
            McpServerKey = capability.McpServerKey,
            Tags = capability.Tags,
            OperationClassifications = capability.OperationClassifications,
            SideEffects = capability.SideEffects,
            ExternalProcess = capability.ExternalProcess,
            ExternalHttp = capability.ExternalHttp,
            McpTransport = capability.McpTransport,
            CapabilityAccessPolicy = capability.CapabilityAccessPolicy
        };
        var issues = new List<CapabilityValidationIssue>(
            new CapabilityTemplateValidator().Validate(descriptor, templatePath).Issues);
        var kind = TryParseAbstractionKind(capability.Kind);
        var key = TryParseCapabilityKey(capability.Key);

        if (string.IsNullOrWhiteSpace(capability.StableGuidKey))
        {
            issues.Add(Issue(
                CapabilityDiagnosticCategory.TemplateValidation,
                kind,
                key,
                capability.TemplatePath,
                "$.stableGuidKey",
                "Seed stable GUID key is required.",
                "Set stableGuidKey to the historical CreateStableGuid input, for example 'capabilities/workspace-read-file'."));
        }

        if (capability.IncludeManagedSeedVersion && string.IsNullOrWhiteSpace(capability.StableId))
        {
            issues.Add(Issue(
                CapabilityDiagnosticCategory.TemplateValidation,
                kind,
                key,
                capability.TemplatePath,
                "$.stableId",
                "Managed capability templates must define a stable id.",
                "Set a versioned stableId such as 'tool:workspace-read-file:v1'."));
        }

        if (string.IsNullOrWhiteSpace(capability.EndpointOrPath) &&
            !string.Equals(capability.Kind, "ai-context", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Issue(
                CapabilityDiagnosticCategory.TemplateValidation,
                kind,
                key,
                capability.TemplatePath,
                "$.endpointOrPath",
                "Capability endpoint or path is required.",
                "Declare the endpoint, sandbox URI, skill URI, RAG root, or external service URL that the seed catalog exposes."));
        }

        if (string.Equals(capability.Kind, "mcp-server", StringComparison.OrdinalIgnoreCase) &&
            capability.McpTransport?.AllowedTools.Count is 0)
        {
            issues.Add(Issue(
                CapabilityDiagnosticCategory.TemplateValidation,
                kind,
                key,
                capability.TemplatePath,
                "$.mcpTransport.allowedTools",
                "MCP server templates must define at least one allowed tool.",
                "List the MCP tools expected during setup list-tools validation."));
        }

        if (string.Equals(capability.Kind, "tool", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(capability.RuntimeToolName))
        {
            issues.Add(Issue(
                CapabilityDiagnosticCategory.TemplateValidation,
                kind,
                key,
                capability.TemplatePath,
                "$.runtimeToolName",
                "Tool templates must define a runtime tool name.",
                "Add the lower snake_case runtime tool name used by the agent runtime."));
        }

        if (capability.CapabilityAccessPolicy is not null)
        {
            issues.AddRange(CapabilityTemplateSeedPolicyValidator.ValidatePolicyReferences(
                capability.CapabilityAccessPolicy,
                templatePath,
                [capability]));
        }

        issues.AddRange(ValidateInlineSkillAssets(capability, packRoot, kind, key));

        return issues;
    }

    private static IEnumerable<CapabilityValidationIssue> ValidateInlineSkillAssets(
        CapabilitySeedTemplateDescriptor capability,
        string packRoot,
        CapabilityKind? kind,
        CapabilityKey? key)
    {
        if (!string.Equals(capability.Kind, "skill", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(capability.SkillSource, "inline", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        if (capability.InlineSkill is null)
        {
            yield return Issue(
                CapabilityDiagnosticCategory.TemplateValidation,
                kind,
                key,
                capability.TemplatePath,
                "$.inlineSkill",
                "Inline skill templates must define inlineSkill settings.",
                "Add inlineSkill.name, inlineSkill.description, and inlineSkill.instructionsAssetKey.");
            yield break;
        }

        if (!SkillName.TryCreate(capability.InlineSkill.Name, out _))
        {
            yield return Issue(
                CapabilityDiagnosticCategory.TemplateValidation,
                kind,
                key,
                capability.TemplatePath,
                "$.inlineSkill.name",
                "Inline skill names must use only lowercase ASCII letters, numbers, and single hyphens.",
                "Use a lowercase kebab-case name without leading, trailing, or consecutive hyphens.");
        }

        foreach (var issue in ValidateTemplateAssetPath(
                     capability,
                     packRoot,
                     capability.InlineSkill.InstructionsAssetKey,
                     "$.inlineSkill.instructionsAssetKey",
                     kind,
                     key))
        {
            yield return issue;
        }

        for (var index = 0; index < capability.InlineSkill.Resources.Count; index++)
        {
            var resource = capability.InlineSkill.Resources[index];
            if (string.IsNullOrWhiteSpace(resource.ContentAssetKey))
            {
                continue;
            }

            foreach (var issue in ValidateTemplateAssetPath(
                         capability,
                         packRoot,
                         resource.ContentAssetKey,
                         $"$.inlineSkill.resources[{index}].contentAssetKey",
                         kind,
                         key))
            {
                yield return issue;
            }
        }
    }

    private static IEnumerable<CapabilityValidationIssue> ValidateTemplateAssetPath(
        CapabilitySeedTemplateDescriptor capability,
        string packRoot,
        string relativePath,
        string fieldPath,
        CapabilityKind? kind,
        CapabilityKey? key)
    {
        CapabilityValidationIssue? validationIssue = null;
        try
        {
            CapabilityTemplateSeedMaterializer.ResolveTemplateAssetPath(
                packRoot,
                relativePath,
                capability.Key,
                fieldPath);
        }
        catch (InvalidOperationException exception)
        {
            validationIssue = Issue(
                CapabilityDiagnosticCategory.TemplateValidation,
                kind,
                key,
                capability.TemplatePath,
                fieldPath,
                exception.Message,
                "Create the referenced file under Templates/Capabilities or correct the relative asset path.");
        }

        if (validationIssue is not null)
        {
            yield return validationIssue;
        }
    }

    private static IEnumerable<CapabilityValidationIssue> ValidateDuplicateKeys(IReadOnlyList<CapabilitySeedTemplateDescriptor> capabilities)
    {
        foreach (var group in capabilities.GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
        {
            foreach (var duplicate in group.Skip(1))
            {
                yield return Issue(
                    CapabilityDiagnosticCategory.TemplateValidation,
                    TryParseAbstractionKind(duplicate.Kind),
                    TryParseCapabilityKey(duplicate.Key),
                    duplicate.TemplatePath,
                    $"$.capabilities[{duplicate.ManifestIndex}].key",
                    $"Duplicate capability key '{group.Key}' is not allowed.",
                    "Keep one descriptor per canonical capability key.");
            }
        }
    }

    private static T ReadJson<T>(string path)
        where T : class, new()
    {
        try
        {
            return JsonFileLoader.ReadRequired<T>(path, JsonOptions);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new InvalidOperationException(
                $"Capability template JSON file '{path}' could not be loaded: {exception.Message}",
                exception);
        }
    }

    private static string ResolvePackRoot(string? explicitRoot)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            var normalizedExplicitRoot = Path.GetFullPath(explicitRoot);
            if (File.Exists(Path.Combine(normalizedExplicitRoot, ManifestFileName)))
            {
                return normalizedExplicitRoot;
            }

            if (File.Exists(normalizedExplicitRoot) &&
                string.Equals(Path.GetFileName(normalizedExplicitRoot), ManifestFileName, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetDirectoryName(normalizedExplicitRoot)!;
            }
        }

        var relativeManifestPath = Path.Combine("Templates", "Capabilities", ManifestFileName);
        var discoveredRoot = AncestorFileLocator.FindContainingDirectory(
            relativeManifestPath,
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
        if (!string.IsNullOrWhiteSpace(discoveredRoot))
        {
            return discoveredRoot;
        }

        throw new InvalidOperationException(
            $"Unable to locate Templates/Capabilities/{ManifestFileName} from the current execution root.");
    }

    private static string Require(string value, string label)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Capability template {label} is required.")
            : value.Trim();
    }

    private static CapabilityKind? TryParseAbstractionKind(string? value)
        => CapabilityText.TryParseEnum<CapabilityKind>(value, out var kind) ? kind : null;

    private static CapabilityKey? TryParseCapabilityKey(string? value)
        => CapabilityKey.TryCreate(value, out var key) ? key : null;

    private static CapabilityValidationIssue Issue(
        CapabilityDiagnosticCategory category,
        CapabilityKind? kind,
        CapabilityKey? key,
        string templatePath,
        string fieldPath,
        string message,
        string repairHint)
    {
        return new CapabilityValidationIssue(
            category,
            CapabilityValidationSeverity.Error,
            kind,
            key,
            TemplatePath.Create(templatePath),
            fieldPath,
            message,
            repairHint);
    }

    private static string RelativePath(string root, string path)
        => Path.GetRelativePath(Path.GetDirectoryName(root)!, path).Replace('\\', '/');
}

internal sealed class CapabilityTemplatePackValidationException : InvalidOperationException
{
    public CapabilityTemplatePackValidationException(IReadOnlyList<CapabilityValidationIssue> issues)
        : base(BuildMessage(issues))
    {
        Issues = issues;
    }

    public IReadOnlyList<CapabilityValidationIssue> Issues { get; }

    private static string BuildMessage(IReadOnlyList<CapabilityValidationIssue> issues)
    {
        var builder = new StringBuilder("Capability template pack validation failed.");
        foreach (var issue in issues.Take(8))
        {
            builder.Append(" [")
                .Append(issue.Category)
                .Append("] ")
                .Append(issue.TemplatePath?.Value ?? "<unknown>")
                .Append(' ')
                .Append(issue.FieldPath)
                .Append(": ")
                .Append(issue.Message)
                .Append(" Repair: ")
                .Append(issue.RepairHint);
        }

        return builder.ToString();
    }
}

internal sealed record CapabilityTemplatePack(
    string RootPath,
    CapabilityTemplatePackManifest Manifest,
    IReadOnlyList<CapabilitySeedTemplateDescriptor> Capabilities,
    IReadOnlyList<CapabilityTemplatePolicy> Policies);

internal sealed record CapabilityTemplatePolicy(
    string Key,
    TemplatePath TemplatePath,
    CapabilityAccessPolicy Policy);

internal sealed class CapabilityTemplatePackManifest
{
    public string PackKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string SeedMarker { get; set; } = string.Empty;

    public string SeedVersion { get; set; } = string.Empty;

    public List<CapabilityTemplateFileReference> CapabilityFiles { get; set; } = [];

    public List<CapabilityTemplatePolicyReference> PolicyFiles { get; set; } = [];
}

internal sealed class CapabilityTemplateFileReference
{
    public string RelativePath { get; set; } = string.Empty;
}

internal sealed class CapabilityTemplatePolicyReference
{
    public string Key { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;
}

internal sealed class CapabilitySeedTemplateFile
{
    public List<CapabilitySeedTemplateDescriptor> Capabilities { get; set; } = [];
}

internal sealed class CapabilitySeedTemplateDescriptor
{
    public string Kind { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string StableId { get; set; } = string.Empty;

    public string StableGuidKey { get; set; } = string.Empty;

    public string EndpointOrPath { get; set; } = string.Empty;

    public string RuntimeToolName { get; set; } = string.Empty;

    public string ImplementationKey { get; set; } = string.Empty;

    public string McpServerKey { get; set; } = string.Empty;

    public string ProofNotes { get; set; } = string.Empty;

    public bool IsBuiltIn { get; set; } = true;

    public bool ApprovalRequired { get; set; }

    public bool IncludeManagedSeedVersion { get; set; } = true;

    public string SkillSource { get; set; } = string.Empty;

    public string SkillRootKey { get; set; } = string.Empty;

    public InlineSkillTemplate? InlineSkill { get; set; }

    public string Message { get; set; } = string.Empty;

    public string ExcludePathsSource { get; set; } = string.Empty;

    public Dictionary<string, JsonElement> AdditionalConfiguration { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public JsonElement Configuration { get; set; }

    public List<string> Tags { get; set; } = [];

    public List<string> OperationClassifications { get; set; } = [];

    public CapabilitySideEffectTemplateDto? SideEffects { get; set; }

    public ExternalProcessToolTemplateDto? ExternalProcess { get; set; }

    public ExternalHttpToolTemplateDto? ExternalHttp { get; set; }

    public McpTransportTemplateDto? McpTransport { get; set; }

    public CapabilityAccessPolicyTemplateDto? CapabilityAccessPolicy { get; set; }

    public string TemplatePath { get; set; } = string.Empty;

    public int ManifestIndex { get; set; }
}

internal sealed class InlineSkillTemplate
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string InstructionsAssetKey { get; set; } = string.Empty;

    public List<InlineSkillResourceTemplate> Resources { get; set; } = [];
}

internal sealed class InlineSkillResourceTemplate
{
    public string Name { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string ContentAssetKey { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}
