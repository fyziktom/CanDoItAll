using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.ScenarioSeeder;

internal sealed partial class UnitsConverterDeliveryProvisioningSeeder
{
    private static readonly IReadOnlyList<ScenarioSkillSeed> ScenarioSkillSeeds =
    [
        new("solution-architecture", "Solution architecture", "Engineering", "Defines maintainable boundaries, source-of-truth choices, and layered delivery design."),
        new("csharp-dotnet-delivery", "C# / .NET delivery", "Engineering", "Implements maintainable C# and .NET application changes with explicit validation."),
        new("blazor-ssr-delivery", "Blazor SSR delivery", "Engineering", "Builds and reviews static SSR Blazor surfaces with credible product-level composition."),
        new("component-library-delivery", "Component-library delivery", "Engineering", "Uses the shared component library intentionally instead of raw wrapper churn."),
        new("playwright-ui-qa", "Playwright UI QA", "Quality", "Validates rendered browser behavior, captures screenshots, and judges visible UI quality."),
        new("ui-composition-review", "UI composition review", "Quality", "Reviews hierarchy, spacing, readability, and visual intent from real screenshots."),
        new("code-review", "Code review", "Quality", "Challenges maintainability, regressions, and residual-risk framing with explicit findings."),
        new("security-review", "Security review", "Quality", "Reviews trust boundaries, validation, and predictable failure behavior."),
        new("release-governance", "Release governance", "Operations", "Synthesizes evidence into explicit release decisions and rollout accountability.")
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ScenarioSkillKeysByRoleKey =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["solution-architect"] = ["solution-architecture", "csharp-dotnet-delivery", "blazor-ssr-delivery"],
            ["lead-engineer"] = ["csharp-dotnet-delivery", "blazor-ssr-delivery", "component-library-delivery"],
            ["review-lead"] = ["code-review", "csharp-dotnet-delivery", "blazor-ssr-delivery"],
            ["qa-lead"] = ["playwright-ui-qa", "blazor-ssr-delivery", "component-library-delivery"],
            ["ui-review-lead"] = ["ui-composition-review", "playwright-ui-qa", "blazor-ssr-delivery", "component-library-delivery"],
            ["security-reviewer"] = ["security-review", "csharp-dotnet-delivery"],
            ["release-manager"] = ["release-governance", "playwright-ui-qa"]
        };

    private async Task<UnitsConverterSkillCatalog> EnsureScenarioSkillsAsync(
        IReadOnlyDictionary<string, DeliveryRoleBinding> bindingsByRoleKey,
        CancellationToken cancellationToken)
    {
        var skillIdsByKey = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var seed in ScenarioSkillSeeds)
        {
            var result = await hrService.SaveSkillDefinitionAsync(
                new SkillDefinitionEditorModel
                {
                    Name = seed.Name,
                    Category = seed.Category,
                    Description = seed.Description,
                    IsActive = true
                },
                cancellationToken);
            skillIdsByKey[seed.Key] = EnsureSuccess(result);
        }

        foreach (var binding in bindingsByRoleKey.Values.Where(item => !item.IsHuman))
        {
            if (!ScenarioSkillKeysByRoleKey.TryGetValue(binding.RoleKey, out var skillKeys))
            {
                continue;
            }

            foreach (var skillKey in skillKeys)
            {
                if (!skillIdsByKey.TryGetValue(skillKey, out var skillId))
                {
                    continue;
                }

                EnsureSuccess(await hrService.SavePartySkillAsync(
                    new PartySkillEditorModel
                    {
                        PartyId = binding.PartyId,
                        SkillId = skillId,
                        Proficiency = SkillProficiencyLevel.Expert,
                        YearsExperience = 5,
                        CertificationStatus = "Scenario validated",
                        Notes = $"Seeded serious delivery skill '{skillKey}' for role '{binding.RoleKey}'."
                    },
                    cancellationToken));
            }
        }

        return new UnitsConverterSkillCatalog(skillIdsByKey);
    }

    private sealed record ScenarioSkillSeed(string Key, string Name, string Category, string Description);

    private sealed record UnitsConverterSkillCatalog(IReadOnlyDictionary<string, Guid> SkillIdsByKey)
    {
        public List<Guid> GetRequiredSkillIds(params string[] skillKeys)
        {
            return skillKeys
                .Where(skillKey => SkillIdsByKey.ContainsKey(skillKey))
                .Select(skillKey => SkillIdsByKey[skillKey])
                .Distinct()
                .ToList();
        }
    }
}
