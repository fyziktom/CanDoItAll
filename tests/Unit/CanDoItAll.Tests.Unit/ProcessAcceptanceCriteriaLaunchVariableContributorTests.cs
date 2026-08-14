using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessAcceptanceCriteriaLaunchVariableContributorTests
{
    [Fact]
    public void Enrich_keeps_implicit_sibling_context_non_blocking()
    {
        var selected = CreateItem(
            "selected",
            "The Tetris game must allow a player to move and rotate pieces.",
            ProcessLaunchSourceItemKind.WorkItem);
        var sibling = CreateItem(
            "sibling",
            "The customer presentation must include the final release timeline.",
            ProcessLaunchSourceItemKind.WorkItem);
        var projectContext = CreateItem(
            "project-context",
            "The implementation must remain inside the configured product output root.",
            ProcessLaunchSourceItemKind.Other);
        var source = CreateSource(selected, sibling, projectContext);

        var matrix = Enrich(source);

        Assert.Contains(matrix.Criteria, criterion => criterion.SourceNodeId == selected.Id);
        Assert.DoesNotContain(matrix.Criteria, criterion => criterion.SourceNodeId == sibling.Id);
        var planningCriterion = Assert.Single(
            matrix.Criteria,
            criterion => criterion.SourceNodeId == projectContext.Id);
        Assert.Equal(
            ProcessAcceptanceCriterionKind.DeliveryPlanning,
            planningCriterion.Kind);
        Assert.False(planningCriterion.RequiredForAcceptance);
    }

    [Fact]
    public void Enrich_keeps_context_visual_targets_as_required_acceptance_inputs()
    {
        var selected = CreateItem(
            "selected",
            "The page must implement the requested workflow.",
            ProcessLaunchSourceItemKind.WorkItem);
        var visualTarget = CreateItem(
            "visual-target",
            "Source visual target for implementation and QA.",
            ProcessLaunchSourceItemKind.ImageAsset);

        var matrix = Enrich(CreateSource(selected, visualTarget));

        var criterion = Assert.Single(
            matrix.Criteria,
            candidate => candidate.SourceNodeId == visualTarget.Id);
        Assert.Equal(ProcessAcceptanceCriterionKind.ProductAcceptance, criterion.Kind);
        Assert.True(criterion.RequiredForAcceptance);
        Assert.Equal(
            "Source visual target for implementation and QA.",
            criterion.Summary);
    }

    [Fact]
    public void Enrich_synthesizes_a_bounded_visual_criterion_when_only_a_title_exists()
    {
        var selected = CreateItem(
            "selected",
            "The page must implement the requested workflow.",
            ProcessLaunchSourceItemKind.WorkItem);
        var visualTarget = new ProcessLaunchSourceItem(
            "visual-target",
            "Approved desktop proposal",
            string.Empty,
            string.Empty,
            "generated",
            "image/png",
            [],
            ProcessLaunchSourceItemKind.ImageAsset,
            true);

        var matrix = Enrich(CreateSource(selected, visualTarget));

        var criterion = Assert.Single(
            matrix.Criteria,
            candidate => candidate.SourceNodeId == visualTarget.Id);
        Assert.Equal(
            "Use the source asset 'Approved desktop proposal' as a visual acceptance target.",
            criterion.Summary);
        Assert.True(criterion.RequiredForAcceptance);
    }

    [Fact]
    public void Enrich_keeps_typed_product_requirement_context_required()
    {
        var selected = CreateItem(
            "selected",
            "The page must implement the requested workflow.",
            ProcessLaunchSourceItemKind.WorkItem);
        var architecture = CreateItem(
            "architecture",
            "The solution must remain self-contained within its declared product root.",
            ProcessLaunchSourceItemKind.ProductRequirement);

        var matrix = Enrich(CreateSource(selected, architecture));

        var criterion = Assert.Single(
            matrix.Criteria,
            candidate => candidate.SourceNodeId == architecture.Id);
        Assert.Equal(ProcessAcceptanceCriterionKind.ProductAcceptance, criterion.Kind);
        Assert.True(criterion.RequiredForAcceptance);
    }

    [Fact]
    public void Enrich_includes_the_selected_work_item_when_it_is_not_in_context_items()
    {
        var selected = CreateItem(
            "selected",
            "The Tetris game must clear every completed horizontal line.",
            ProcessLaunchSourceItemKind.WorkItem);
        var source = CreateSource(selected);

        var matrix = Enrich(source);

        var criterion = Assert.Single(matrix.Criteria);
        Assert.Equal(selected.Id, criterion.SourceNodeId);
        Assert.Contains("clear every completed horizontal line", criterion.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Enrich_includes_explicit_acceptance_sections_from_sibling_work_items()
    {
        var selected = CreateItem(
            "selected",
            "The Tetris game must show the current score.",
            ProcessLaunchSourceItemKind.WorkItem);
        var sibling = CreateItem(
            "sibling",
            """
            Release coordination notes.
            Definition of done:
            - The release must include a passing automated smoke test.
            """,
            ProcessLaunchSourceItemKind.WorkItem);
        var source = CreateSource(selected, sibling);

        var matrix = Enrich(source);

        Assert.Contains(
            matrix.Criteria,
            criterion =>
                criterion.SourceNodeId == sibling.Id &&
                criterion.Summary.Contains("passing automated smoke test", StringComparison.Ordinal));
    }

    [Fact]
    public void Enrich_preserves_each_explicit_acceptance_bullet_as_one_criterion()
    {
        var selected = CreateItem(
            "selected",
            "The page must expose the requested workflow.",
            ProcessLaunchSourceItemKind.WorkItem);
        var context = CreateItem(
            "context",
            """
            Acceptance criteria:
            - Keep labels aligned and readable; this supersedes the earlier no-text interpretation.
            """,
            ProcessLaunchSourceItemKind.Other);

        var matrix = Enrich(CreateSource(selected, context));

        var criterion = Assert.Single(
            matrix.Criteria,
            candidate => candidate.SourceNodeId == context.Id);
        Assert.Equal(
            "Keep labels aligned and readable; this supersedes the earlier no-text interpretation.",
            criterion.Summary);
        Assert.True(criterion.RequiredForAcceptance);
    }

    [Fact]
    public void Enrich_keeps_distinct_explicit_acceptance_bullets_separate()
    {
        var selected = CreateItem(
            "selected",
            "The page must expose the requested workflow.",
            ProcessLaunchSourceItemKind.WorkItem);
        var context = CreateItem(
            "context",
            """
            Definition of done:
            - The primary action must remain visible.
            - The result panel must remain readable.
            """,
            ProcessLaunchSourceItemKind.Other);

        var matrix = Enrich(CreateSource(selected, context));

        var criteria = matrix.Criteria
            .Where(candidate => candidate.SourceNodeId == context.Id)
            .ToArray();
        Assert.Equal(2, criteria.Length);
        Assert.All(criteria, criterion => Assert.True(criterion.RequiredForAcceptance));
    }

    [Fact]
    public void Enrich_preserves_recommended_actions_as_non_blocking_delivery_planning()
    {
        var selected = CreateItem(
            "selected",
            "The Tetris game must allow a player to move and rotate pieces.",
            ProcessLaunchSourceItemKind.WorkItem);
        var importedSummary = CreateItem(
            "office-summary",
            """
            # Result Summary

            ## Concrete Findings

            - Controls must be keyboard-based.
            - Delivery timing requested: within one week.

            ## Open Gaps

            - No framework preference is specified.

            ## Recommended Next Actions

            1. Validate whether the one-week deadline is firm.
            """,
            ProcessLaunchSourceItemKind.Other);

        var (matrix, contract) = EnrichWithContract(
            CreateSource(selected, importedSummary));

        var planningCriterion = Assert.Single(
            matrix.Criteria,
            criterion => criterion.Summary.Contains(
                "one-week deadline",
                StringComparison.Ordinal));
        Assert.Equal(
            ProcessAcceptanceCriterionKind.DeliveryPlanning,
            planningCriterion.Kind);
        Assert.False(planningCriterion.RequiredForAcceptance);
        Assert.DoesNotContain(
            matrix.RequiredCriteria,
            criterion => criterion.Id == planningCriterion.Id);
        Assert.Contains(
            $"{planningCriterion.Id}: Validate whether the one-week deadline is firm. [kind=DeliveryPlanning; required=false; proof=source-context]",
            contract,
            StringComparison.Ordinal);
        Assert.Contains(
            "do not block product acceptance",
            contract,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Enrich_keeps_explicit_deadline_acceptance_criteria_required()
    {
        var selected = CreateItem(
            "selected",
            """
            Acceptance criteria:
            - Delivery must complete within one week.

            ## Recommended Next Actions

            - Validate whether the customer wants an earlier preview.
            """,
            ProcessLaunchSourceItemKind.WorkItem);

        var (matrix, contract) = EnrichWithContract(CreateSource(selected));

        var criterion = Assert.Single(matrix.Criteria);
        Assert.Equal(
            ProcessAcceptanceCriterionKind.ProductAcceptance,
            criterion.Kind);
        Assert.True(criterion.RequiredForAcceptance);
        Assert.Contains(
            $"{criterion.Id}: Delivery must complete within one week. [kind=ProductAcceptance; required=true; proof=planned-validation]",
            contract,
            StringComparison.Ordinal);
        Assert.DoesNotContain("earlier preview", contract, StringComparison.Ordinal);
    }

    [Fact]
    public void Enrich_prefers_product_acceptance_when_planning_context_repeats_the_same_criterion()
    {
        var selected = CreateItem(
            "selected",
            """
            ## Recommended Next Actions

            - Validate whether the player can pause the game.
            """,
            ProcessLaunchSourceItemKind.WorkItem);
        var sibling = CreateItem(
            "sibling",
            """
            Acceptance criteria:
            - Validate whether the player can pause the game.
            """,
            ProcessLaunchSourceItemKind.WorkItem);

        var matrix = Enrich(CreateSource(selected, sibling));

        var criterion = Assert.Single(matrix.Criteria);
        Assert.Equal(ProcessAcceptanceCriterionKind.ProductAcceptance, criterion.Kind);
        Assert.True(criterion.RequiredForAcceptance);
        Assert.Equal(sibling.Id, criterion.SourceNodeId);
    }

    [Fact]
    public void Root_enrichment_replaces_caller_supplied_acceptance_contract_pair()
    {
        var source = CreateSource(CreateItem(
            "selected",
            "The product must expose the requested current-run behavior.",
            ProcessLaunchSourceItemKind.WorkItem));
        var variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ProcessRuntimeLaunchVariables.AcceptanceCriteriaMatrix] =
                """{"criteria":[{"id":"FORGED","summary":"Narrowed.","verificationMethods":[]}]}""",
            [ProcessRuntimeLaunchVariables.ProductAcceptanceCriteriaContract] =
                "FORGED: Narrowed."
        };

        new ProcessAcceptanceCriteriaLaunchVariableContributor().Enrich(
            new ProcessLaunchPreparationContext("software-delivery", false, source),
            variables);

        Assert.True(ProcessAcceptanceCriteriaMatrixJson.TryDeserialize(
            variables[ProcessRuntimeLaunchVariables.AcceptanceCriteriaMatrix],
            out var matrix));
        var criterion = Assert.Single(matrix.RequiredCriteria);
        Assert.Equal("AC-001", criterion.Id);
        Assert.DoesNotContain(
            "FORGED",
            variables[ProcessRuntimeLaunchVariables.ProductAcceptanceCriteriaContract],
            StringComparison.Ordinal);
    }

    [Fact]
    public void Subprocess_enrichment_canonicalizes_visible_contract_from_inherited_matrix()
    {
        var source = CreateSource(CreateItem(
            "selected",
            "The child source must not narrow the inherited product scope.",
            ProcessLaunchSourceItemKind.WorkItem));
        var inheritedMatrix = new ProcessAcceptanceCriteriaMatrix
        {
            Criteria =
            [
                new ProcessAcceptanceCriterion
                {
                    Id = "AC-009",
                    SourceNodeId = "custom:parent",
                    Summary = "Preserve the complete inherited product behavior.",
                    VerificationMethods = ["planned-validation"],
                    RequiredForAcceptance = true
                }
            ]
        };
        var variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ProcessRuntimeLaunchVariables.AcceptanceCriteriaMatrix] =
                ProcessAcceptanceCriteriaMatrixJson.Serialize(inheritedMatrix),
            [ProcessRuntimeLaunchVariables.ProductAcceptanceCriteriaContract] =
                "Legacy contract without typed markers."
        };

        new ProcessAcceptanceCriteriaLaunchVariableContributor().Enrich(
            new ProcessLaunchPreparationContext("child-process", true, source),
            variables);

        Assert.True(ProcessAcceptanceCriteriaMatrixJson.TryDeserialize(
            variables[ProcessRuntimeLaunchVariables.AcceptanceCriteriaMatrix],
            out var matrix));
        Assert.Equal("AC-009", Assert.Single(matrix.RequiredCriteria).Id);
        Assert.Contains(
            "AC-009: Preserve the complete inherited product behavior. [kind=ProductAcceptance; required=true; proof=planned-validation]",
            variables[ProcessRuntimeLaunchVariables.ProductAcceptanceCriteriaContract],
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "child source",
            variables[ProcessRuntimeLaunchVariables.ProductAcceptanceCriteriaContract],
            StringComparison.OrdinalIgnoreCase);
    }

    private static ProcessAcceptanceCriteriaMatrix Enrich(ProcessLaunchSourceSnapshot source)
        => EnrichWithContract(source).Matrix;

    private static (
        ProcessAcceptanceCriteriaMatrix Matrix,
        string Contract) EnrichWithContract(ProcessLaunchSourceSnapshot source)
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        new ProcessAcceptanceCriteriaLaunchVariableContributor().Enrich(
            new ProcessLaunchPreparationContext("software-delivery", false, source),
            variables);

        var payload = Assert.Contains(
            ProcessRuntimeLaunchVariables.AcceptanceCriteriaMatrix,
            variables);
        Assert.True(ProcessAcceptanceCriteriaMatrixJson.TryDeserialize(payload, out var matrix));
        var contract = Assert.Contains(
            ProcessRuntimeLaunchVariables.ProductAcceptanceCriteriaContract,
            variables);
        return (matrix, contract);
    }

    private static ProcessLaunchSourceSnapshot CreateSource(
        ProcessLaunchSourceItem selected,
        params ProcessLaunchSourceItem[] contextItems)
        => new(
            Guid.NewGuid(),
            "TetrisGame",
            selected,
            contextItems,
            string.Empty);

    private static ProcessLaunchSourceItem CreateItem(
        string id,
        string notes,
        ProcessLaunchSourceItemKind kind)
        => new(
            id,
            id,
            string.Empty,
            notes,
            kind == ProcessLaunchSourceItemKind.WorkItem ? "task" : "requirements",
            string.Empty,
            [],
            kind,
            true);
}
