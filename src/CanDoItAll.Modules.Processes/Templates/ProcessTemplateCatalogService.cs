using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessTemplateCatalogService
{
    private readonly ProcessTemplatePackLoader packLoader;

    public ProcessTemplateCatalogService(ProcessTemplatePackLoader packLoader)
    {
        this.packLoader = packLoader;
    }

    public IReadOnlyList<ProcessTemplateCatalogItem> ListProcessTemplates()
    {
        var pack = packLoader.Load();
        return pack.Processes.Values
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(item => new ProcessTemplateCatalogItem(
                item.Key,
                item.DisplayName,
                item.Summary,
                item.Criticality,
                item.AutonomyLevel,
                item.Steps.Count,
                item.SharedRoleRefs.Count,
                item.LocalRoleRefs.Count,
                item.RelativePath))
            .ToList();
    }

    public IReadOnlyList<ProcessCanvasRoleTemplate> GetRoleTemplates()
    {
        var pack = packLoader.Load();
        return pack.RoleTemplates
            .Select(seed => new ProcessCanvasRoleTemplate(
                seed.ActionId,
                seed.Label,
                seed.Summary,
                ordinal => BuildRoleDraft(pack, seed, ordinal)))
            .ToList();
    }

    public IReadOnlyList<ProcessCanvasStepTemplate> GetStepTemplates()
    {
        var pack = packLoader.Load();
        return pack.StepTemplates
            .Select(seed => new ProcessCanvasStepTemplate(
                seed.ActionId,
                seed.Label,
                seed.Summary,
                (ordinal, dependsOnStepId, x, y) => BuildStepDraft(pack, seed, ordinal, dependsOnStepId, x, y)))
            .ToList();
    }

    public IReadOnlyList<ProcessCanvasToolboxGroup> GetDefinitionToolboxGroups()
    {
        return
        [
            new ProcessCanvasToolboxGroup(
                "role-templates",
                "Role templates",
                "Start from reusable staffing contracts so the process stays role-first and executor-agnostic.",
                GetRoleTemplates()
                    .Select(template => new ProcessCanvasToolboxAction(template.ActionId, template.Label, template.Summary, "neutral"))
                    .ToList()),
            new ProcessCanvasToolboxGroup(
                "step-templates",
                "Step templates",
                "Seed explicit process steps with realistic governance, proof, and delivery expectations.",
                GetStepTemplates()
                    .Select(template => new ProcessCanvasToolboxAction(template.ActionId, template.Label, template.Summary, "accent"))
                    .ToList())
        ];
    }

    public bool TryCreateRoleDraft(string actionId, int ordinal, out ProcessRoleEditorModel role)
    {
        var template = GetRoleTemplates().FirstOrDefault(item => string.Equals(item.ActionId, actionId, StringComparison.Ordinal));
        if (template is null)
        {
            role = new ProcessRoleEditorModel();
            return false;
        }

        role = template.Factory(ordinal);
        return true;
    }

    public bool TryCreateStepDraft(
        string actionId,
        int ordinal,
        Guid? dependsOnStepId,
        double x,
        double y,
        out ProcessStepEditorModel step)
    {
        var template = GetStepTemplates().FirstOrDefault(item => string.Equals(item.ActionId, actionId, StringComparison.Ordinal));
        if (template is null)
        {
            step = new ProcessStepEditorModel();
            return false;
        }

        step = template.Factory(ordinal, dependsOnStepId, x, y);
        return true;
    }

    private static ProcessRoleEditorModel BuildRoleDraft(
        ProcessTemplatePack pack,
        ProcessTemplateToolboxRoleSeed seed,
        int ordinal)
    {
        if (string.IsNullOrWhiteSpace(seed.TemplateRoleKey))
        {
            return new ProcessRoleEditorModel
            {
                Id = Guid.NewGuid(),
                Key = $"{(string.IsNullOrWhiteSpace(seed.KeyPrefix) ? "role" : seed.KeyPrefix)}-{ordinal}",
                DisplayName = string.IsNullOrWhiteSpace(seed.DisplayNameTemplate)
                    ? $"Role {ordinal}"
                    : seed.DisplayNameTemplate.Replace("{ordinal}", ordinal.ToString(), StringComparison.Ordinal),
                PreferredExecutorKind = seed.PreferredExecutorKind,
                DefaultAllocationPercent = Math.Max(0, seed.DefaultAllocationPercent)
            };
        }

        var roleResource = ResolveRoleResource(pack, seed.TemplateRoleKey)
            ?? throw new InvalidOperationException($"Role template '{seed.TemplateRoleKey}' was not found in the template pack.");

        return ProcessTemplateEditorModelFactory.CreateRoleFromResource(
            roleResource,
            Guid.NewGuid(),
            $"{(string.IsNullOrWhiteSpace(seed.KeyPrefix) ? roleResource.Key : seed.KeyPrefix)}-{ordinal}",
            string.IsNullOrWhiteSpace(seed.DisplayNameTemplate)
                ? roleResource.DisplayName
                : seed.DisplayNameTemplate.Replace("{ordinal}", ordinal.ToString(), StringComparison.Ordinal),
            seed.PreferredExecutorKind,
            Math.Max(0, seed.DefaultAllocationPercent));
    }

    private static ProcessStepEditorModel BuildStepDraft(
        ProcessTemplatePack pack,
        ProcessTemplateToolboxStepSeed seed,
        int ordinal,
        Guid? dependsOnStepId,
        double x,
        double y)
    {
        var template = seed.Template;
        var step = new ProcessStepEditorModel
        {
            Id = Guid.NewGuid(),
            Key = $"{template.Key}-{ordinal}",
            Title = template.Title,
            Subtitle = template.Subtitle,
            Notes = BuildStepNotes(template),
            StepKind = EnumValueParser.ParseOrDefault(template.StepKind, ProcessStepKind.Work),
            AllowedOperations = ProcessStepOperationContractState.NormalizeAllowedOperations(template.AllowedOperations),
            OperationTargetScope = template.OperationTargetScope,
            SubprocessDefinitionSnapshotName = string.IsNullOrWhiteSpace(template.SubprocessDefinitionSnapshotName)
                ? template.SubprocessProcessKey
                : template.SubprocessDefinitionSnapshotName,
            AllowsManualSkip = template.AllowsManualSkip,
            AllowsSafeRefusal = template.AllowsSafeRefusal,
            RequiresApproval = template.RequiresApproval,
            RequiresDecisionRecord = template.RequiresDecisionRecord,
            InputContractSummary = template.InputContractSummary,
            OutputContractSummary = template.OutputContractSummary,
            EvidenceContractSummary = template.EvidenceContractSummary,
            DecisionRightsSummary = template.DecisionRightsSummary,
            ExceptionPolicySummary = template.ExceptionPolicySummary,
            TargetLeadHours = template.TargetLeadHours,
            CanvasX = x,
            CanvasY = y,
            ArtifactExpectations = template.ArtifactExpectations
                .Select(item => BuildArtifactExpectation(pack, item))
                .ToList(),
            BranchOutcomes = template.BranchOutcomes
                .Select(item => new ProcessStepBranchOutcomeEditorModel
                {
                    Id = Guid.NewGuid(),
                    Key = item.Key,
                    Title = item.Title,
                    Description = item.Description
                })
                .ToList()
        };

        if (dependsOnStepId.HasValue && dependsOnStepId.Value != Guid.Empty)
        {
            ProcessStepDependencyCollection.SetEditorDependencies(
                step,
                [ProcessStepDependencyCollection.CreateEditorDependency(dependsOnStepId.Value, null)]);
        }

        return step;
    }

    private static ProcessArtifactExpectationEditorModel BuildArtifactExpectation(
        ProcessTemplatePack pack,
        ProcessTemplateArtifactExpectation template)
    {
        return ProcessTemplateEditorModelFactory.CreateArtifactExpectationFromTemplate(
            template,
            ResolveArtifactResource(pack, template.TemplateKey),
            Guid.NewGuid());
    }

    private static ProcessTemplateRoleResource? ResolveRoleResource(ProcessTemplatePack pack, string roleKey)
    {
        if (pack.SharedRoles.TryGetValue(roleKey, out var sharedRole))
        {
            return sharedRole;
        }

        foreach (var process in pack.Processes.Values)
        {
            var localRole = process.LocalRoles.FirstOrDefault(item => string.Equals(item.Key, roleKey, StringComparison.OrdinalIgnoreCase));
            if (localRole is not null)
            {
                return localRole;
            }
        }

        return null;
    }

    private static ProcessTemplateArtifactResource? ResolveArtifactResource(ProcessTemplatePack pack, string artifactKey)
    {
        if (string.IsNullOrWhiteSpace(artifactKey))
        {
            return null;
        }

        if (pack.SharedArtifacts.TryGetValue(artifactKey, out var sharedArtifact))
        {
            return sharedArtifact;
        }

        foreach (var process in pack.Processes.Values)
        {
            var localArtifact = process.LocalArtifacts.FirstOrDefault(item => string.Equals(item.Key, artifactKey, StringComparison.OrdinalIgnoreCase));
            if (localArtifact is not null)
            {
                return localArtifact;
            }
        }

        return null;
    }

    private static string BuildStepNotes(ProcessTemplateStepDefinition template)
    {
        var addendum = new List<string>();

        if (template.ChecklistRefs.Count > 0)
        {
            addendum.Add("Checklist refs: " + string.Join(", ", template.ChecklistRefs));
        }

        if (template.ValidationRefs.Count > 0)
        {
            addendum.Add("Validation refs: " + string.Join(", ", template.ValidationRefs));
        }

        if (template.PromptRefs.Count > 0)
        {
            addendum.Add("Prompt refs: " + string.Join(", ", template.PromptRefs));
        }

        if (addendum.Count == 0)
        {
            return template.Notes;
        }

        return string.IsNullOrWhiteSpace(template.Notes)
            ? string.Join(Environment.NewLine, addendum)
            : template.Notes + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, addendum);
    }

}
