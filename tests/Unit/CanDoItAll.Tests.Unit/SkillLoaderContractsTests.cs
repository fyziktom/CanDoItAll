using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Capabilities.Access;
using CanDoItAll.AgentFramework.Skills;
using CanDoItAll.AgentFramework.Skills.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class SkillLoaderContractsTests
{
    [Fact]
    public async Task INV_FILE_001_file_skill_loader_validates_skill_md_and_exposes_descriptor()
    {
        using var workspace = TempWorkspace.Create();
        var skillRoot = workspace.CreateSkill(
            "repository-playbook",
            """
            ---
            name: repository-playbook
            description: Repository-specific engineering playbook.
            ---
            Use the repository conventions before changing code.
            """);
        var descriptor = SkillDescriptorFactory.File(
            CapabilityKey.Create("repository-playbook"),
            "Repository Playbook",
            "Repository-specific engineering playbook.",
            skillRoot,
            allowedExternalRoots: [],
            new SkillScriptExecutionPolicy(true, SkillScriptTrustLevel.WorkspaceSkillRoot),
            tags: [CapabilityTag.Create("repository")],
            operationClassifications: [CapabilityOperationClassification.ScriptExecution]);
        var loader = new FileSkillLoader(workspace.RootPath);

        var result = await loader.LoadAsync(descriptor, "INV_FILE_001", CancellationToken.None);
        var exposure = SkillExposureDescriptorFactory.Create(descriptor);

        Assert.True(result.IsSuccess);
        Assert.Equal("repository-playbook", result.Skill!.Name);
        Assert.Contains("repository conventions", result.Skill.Instructions, StringComparison.Ordinal);
        Assert.Equal(CapabilityKind.Skill, exposure.Identity.Kind);
        Assert.Contains(CapabilityOperationClassification.ScriptExecution, exposure.OperationClassifications);
        Assert.True(exposure.SideEffectProfile.RequiresApprovalByDefault);
    }

    [Fact]
    public async Task INV_FILE_002_file_loader_rejects_external_root_without_allowlist()
    {
        using var workspace = TempWorkspace.Create();
        using var external = TempWorkspace.Create();
        var skillRoot = external.CreateSkill(
            "external-skill",
            """
            ---
            name: external-skill
            description: External skill.
            ---
            External instructions.
            """);
        var descriptor = SkillDescriptorFactory.File(
            CapabilityKey.Create("external-skill"),
            "External Skill",
            "External skill.",
            skillRoot,
            allowedExternalRoots: [],
            new SkillScriptExecutionPolicy(true, SkillScriptTrustLevel.ExternalSkillRoot));
        var loader = new FileSkillLoader(workspace.RootPath);

        var result = await loader.LoadAsync(descriptor, "INV_FILE_002", CancellationToken.None);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(CapabilityDiagnosticCategory.CommandPolicy, diagnostic.Category);
        Assert.Equal(CapabilityTransportKind.FileSkill, diagnostic.Transport);
        Assert.Contains("allowedExternalRoots", diagnostic.RepairHint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task INV_FILE_003_file_loader_reports_missing_skill_md()
    {
        using var workspace = TempWorkspace.Create();
        var skillRoot = Path.Combine(workspace.RootPath, "skills", "missing-md");
        Directory.CreateDirectory(skillRoot);
        var descriptor = SkillDescriptorFactory.File(
            CapabilityKey.Create("missing-md"),
            "Missing Markdown",
            "Missing SKILL.md test.",
            skillRoot,
            allowedExternalRoots: [],
            new SkillScriptExecutionPolicy(true, SkillScriptTrustLevel.WorkspaceSkillRoot));
        var loader = new FileSkillLoader(workspace.RootPath);

        var result = await loader.LoadAsync(descriptor, "INV_FILE_003", CancellationToken.None);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(CapabilityDiagnosticCategory.TemplateValidation, diagnostic.Category);
        Assert.Equal("$.skillRoot", diagnostic.FieldPath);
        Assert.Contains("SKILL.md", diagnostic.MaskedDetail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task INV_INLINE_001_inline_loader_preserves_instructions_and_resources()
    {
        var descriptor = SkillDescriptorFactory.Inline(
            CapabilityKey.Create("mail-summary-inline-skill"),
            "Mail Summary",
            "Summarizes mail into concise next actions.",
            "mail-summary",
            "Summarize mail by sender, decision, and next action.",
            [
                new InlineSkillResource(
                    "summary-format",
                    "Use Decision, Evidence, Next Action sections.",
                    "Required output format.")
            ],
            tags: [CapabilityTag.Create("mail")],
            operationClassifications: [CapabilityOperationClassification.Validation]);
        var loader = new InlineSkillLoader();

        var result = await loader.LoadAsync(descriptor, "INV_INLINE_001", CancellationToken.None);
        var exposure = SkillExposureDescriptorFactory.Create(descriptor);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Skill!.Resources);
        Assert.Contains("next action", result.Skill.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(CapabilityTag.Create("inline"), exposure.Tags);
        Assert.Contains(CapabilityOperationClassification.Validation, exposure.OperationClassifications);
    }

    [Fact]
    public async Task INV_INLINE_002_inline_loader_rejects_empty_resource_content()
    {
        var descriptor = SkillDescriptorFactory.Inline(
            CapabilityKey.Create("broken-inline-skill"),
            "Broken Inline",
            "Broken inline skill.",
            "broken-inline",
            "Do useful work.",
            [new InlineSkillResource("empty-resource", " ", "Invalid resource.")]);
        var loader = new InlineSkillLoader();

        var result = await loader.LoadAsync(descriptor, "INV_INLINE_002", CancellationToken.None);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(CapabilityDiagnosticCategory.TemplateValidation, diagnostic.Category);
        Assert.Equal("$.inlineSkill.resources[0].content", diagnostic.FieldPath);
    }

    [Fact]
    public async Task INV_REGISTERED_001_registered_resolver_uses_key_registry_without_reflection()
    {
        var descriptor = SkillDescriptorFactory.Registered(
            CapabilityKey.Create("delivery-review-skill"),
            "Delivery Review",
            "Reviews delivery artifacts.",
            ImplementationKey.Create("skills.delivery-review"),
            tags: [CapabilityTag.Create("delivery")],
            operationClassifications: [CapabilityOperationClassification.Validation]);
        var registry = new RegisteredSkillRegistry();
        registry.Register(new RegisteredSkillBinding(
            descriptor.RegisteredSkillKey,
            (_, correlationId) => SkillLoadResult.Success(new LoadedSkill(
                descriptor.Identity,
                SkillDescriptorKind.Registered,
                "delivery-review",
                descriptor.Description,
                "Review delivery artifacts before QA starts.",
                [],
                null,
                descriptor.RegisteredSkillKey,
                null), correlationId)));
        var resolver = new RegisteredSkillResolver(registry);

        var result = await resolver.ResolveAsync(descriptor, "INV_REGISTERED_001", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("delivery-review", result.Skill!.Name);
        Assert.Equal(ImplementationKey.Create("skills.delivery-review"), result.Skill.RegisteredSkillKey);
    }

    [Fact]
    public async Task INV_REGISTERED_002_missing_registered_key_reports_implementation_missing()
    {
        var descriptor = SkillDescriptorFactory.Registered(
            CapabilityKey.Create("missing-registered-skill"),
            "Missing Registered",
            "Missing registered skill.",
            ImplementationKey.Create("skills.missing"));
        var resolver = new RegisteredSkillResolver(new RegisteredSkillRegistry());

        var result = await resolver.ResolveAsync(descriptor, "INV_REGISTERED_002", CancellationToken.None);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(CapabilityDiagnosticCategory.ImplementationMissing, diagnostic.Category);
        Assert.Equal(ImplementationKey.Create("skills.missing"), diagnostic.ImplementationKey);
        Assert.Contains("registered skill", diagnostic.RepairHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task INV_REGISTERED_003_retired_registered_skill_fails_with_capability_unavailable()
    {
        var descriptor = SkillDescriptorFactory.Registered(
            CapabilityKey.Create("workspace-delivery-skill"),
            "Workspace Delivery",
            "Retired workspace delivery skill.",
            ImplementationKey.Create("skills.workspace-delivery"),
            availabilityState: CapabilityAvailabilityState.Retired);
        var resolver = new RegisteredSkillResolver(new RegisteredSkillRegistry());

        var result = await resolver.ResolveAsync(descriptor, "INV_REGISTERED_003", CancellationToken.None);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(CapabilityDiagnosticCategory.CapabilityUnavailable, diagnostic.Category);
        Assert.Contains("retired", diagnostic.MaskedDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void INV_POLICY_001_file_inline_and_registered_descriptors_participate_in_access_policy()
    {
        var fileDescriptor = SkillExposureDescriptorFactory.Create(SkillDescriptorFactory.File(
            CapabilityKey.Create("repository-playbook"),
            "Repository Playbook",
            "Repository-specific engineering playbook.",
            "skills/repository-playbook",
            allowedExternalRoots: [],
            new SkillScriptExecutionPolicy(true, SkillScriptTrustLevel.WorkspaceSkillRoot),
            operationClassifications: [CapabilityOperationClassification.ScriptExecution]));
        var inlineDescriptor = SkillExposureDescriptorFactory.Create(SkillDescriptorFactory.Inline(
            CapabilityKey.Create("mail-summary-inline-skill"),
            "Mail Summary",
            "Summarizes mail.",
            "mail-summary",
            "Summarize mail.",
            [],
            tags: [CapabilityTag.Create("mail")]));
        var registeredDescriptor = SkillExposureDescriptorFactory.Create(SkillDescriptorFactory.Registered(
            CapabilityKey.Create("delivery-review-skill"),
            "Delivery Review",
            "Reviews delivery artifacts.",
            ImplementationKey.Create("skills.delivery-review"),
            tags: [CapabilityTag.Create("delivery")]));
        var evaluator = new CapabilityAccessPolicyEvaluator();
        var policy = new CapabilityAccessPolicy(
        [
            new CapabilityAccessRule(
                CapabilityRuleId.Create("deny-script"),
                CapabilityAccessEffect.Deny,
                CapabilityAccessScope.ProcessStep,
                CapabilitySelector.ByOperationClassification(CapabilityOperationClassification.ScriptExecution),
                "No script-capable skills."),
            new CapabilityAccessRule(
                CapabilityRuleId.Create("deny-inline"),
                CapabilityAccessEffect.Deny,
                CapabilityAccessScope.ProcessStep,
                CapabilitySelector.ByTag(CapabilityTag.Create("inline")),
                "No inline skills."),
            new CapabilityAccessRule(
                CapabilityRuleId.Create("deny-registered-key"),
                CapabilityAccessEffect.Deny,
                CapabilityAccessScope.ProcessStep,
                CapabilitySelector.ByImplementationKey(ImplementationKey.Create("skills.delivery-review")),
                "No registered delivery skill.")
        ]);

        var result = evaluator.Evaluate(new CapabilityAccessEvaluationContext(
            [fileDescriptor, inlineDescriptor, registeredDescriptor],
            [],
            [policy],
            "INV_POLICY_001"));

        Assert.Empty(result.AllowedCapabilities);
        Assert.Contains(result.Diagnostics, item => item.Identity.Key == fileDescriptor.Identity.Key);
        Assert.Contains(result.Diagnostics, item => item.Identity.Key == inlineDescriptor.Identity.Key);
        Assert.Contains(result.Diagnostics, item => item.Identity.Key == registeredDescriptor.Identity.Key);
    }

    [Fact]
    public async Task INV_SEED_001_existing_seeded_inline_skill_assets_load_through_inline_loader()
    {
        var seedSkillsRoot = Path.GetFullPath(Path.Combine(
            FindRepoRoot(),
            "Templates",
            "Capabilities",
            "skills",
            "instructions"));
        var seedFiles = Directory.GetFiles(seedSkillsRoot, "*.md");
        var loader = new InlineSkillLoader();
        var failures = new List<string>();

        foreach (var seedFile in seedFiles)
        {
            var name = Path.GetFileNameWithoutExtension(seedFile);
            if (!CapabilityKey.TryCreate(name + "-inline-skill", out var key))
            {
                failures.Add($"{name}: invalid capability key");
                continue;
            }

            var instructions = await File.ReadAllTextAsync(seedFile);
            var descriptor = SkillDescriptorFactory.Inline(
                key,
                name,
                $"Seeded inline skill asset {name}.",
                name,
                instructions,
                []);
            var result = await loader.LoadAsync(descriptor, "INV_SEED_001", CancellationToken.None);
            if (!result.IsSuccess)
            {
                failures.Add($"{name}: {string.Join(", ", result.Diagnostics.Select(item => item.MaskedDetail))}");
            }
        }

        Assert.NotEmpty(seedFiles);
        Assert.Empty(failures);
    }

    private sealed class TempWorkspace : IDisposable
    {
        private TempWorkspace(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public static TempWorkspace Create()
        {
            var rootPath = Path.Combine(Path.GetTempPath(), "candoitall-regression-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootPath);
            return new TempWorkspace(rootPath);
        }

        public string CreateSkill(string name, string content)
        {
            var skillRoot = Path.Combine(RootPath, "skills", name);
            Directory.CreateDirectory(skillRoot);
            File.WriteAllText(Path.Combine(skillRoot, "SKILL.md"), content);
            return skillRoot;
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test base directory.");
    }
}
