using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Components;

public sealed class ProcessCanvasCatalogTests
{
    [Fact]
    public void Catalog_exposes_all_definition_and_runtime_node_kinds()
    {
        Assert.Equal(
            [
                ProcessCanvasCatalog.NodeKinds.DefinitionStep,
                ProcessCanvasCatalog.NodeKinds.DefinitionBranchRouter,
                ProcessCanvasCatalog.NodeKinds.DefinitionRole
            ],
            ProcessCanvasCatalog.DefinitionNodeKinds);
        Assert.Equal(
            [
                ProcessCanvasCatalog.NodeKinds.RuntimeStep,
                ProcessCanvasCatalog.NodeKinds.RuntimeBranchRouter
            ],
            ProcessCanvasCatalog.RuntimeNodeKinds);
    }

    [Fact]
    public void Catalog_maps_role_and_step_responsibility_ports_for_all_responsibility_kinds()
    {
        foreach (var responsibilityKind in ProcessCanvasCatalog.OrderedResponsibilities)
        {
            var rolePortId = ProcessCanvasCatalog.DefinitionPorts.GetRoleResponsibilityOutputPortId(responsibilityKind);
            var stepPortId = ProcessCanvasCatalog.DefinitionPorts.GetStepResponsibilityInputPortId(responsibilityKind);
            var runtimePortId = ProcessCanvasCatalog.RuntimePorts.GetStepResponsibilityInputPortId(responsibilityKind);

            Assert.True(ProcessCanvasCatalog.DefinitionPorts.TryGetRoleResponsibilityKind(rolePortId, out var resolvedRoleKind));
            Assert.True(ProcessCanvasCatalog.DefinitionPorts.TryGetStepResponsibilityKind(stepPortId, out var resolvedStepKind));
            Assert.Equal(responsibilityKind, resolvedRoleKind);
            Assert.Equal(responsibilityKind, resolvedStepKind);
            Assert.StartsWith("run-step:", runtimePortId, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Catalog_describes_step_kind_profiles_for_start_decision_and_end()
    {
        var start = ProcessCanvasCatalog.GetStepKindProfile(ProcessStepKind.Start);
        var decision = ProcessCanvasCatalog.GetStepKindProfile(ProcessStepKind.Decision);
        var end = ProcessCanvasCatalog.GetStepKindProfile(ProcessStepKind.End);

        Assert.False(start.AllowsStructuralInput);
        Assert.True(start.AllowsStructuralOutput);
        Assert.Equal(ProcessCanvasCatalog.RouterRelevance.Optional, start.RouterRelevance);

        Assert.True(decision.AllowsStructuralInput);
        Assert.True(decision.AllowsStructuralOutput);
        Assert.True(decision.AllowsDecisionAuthorityInput);
        Assert.Equal(ProcessCanvasCatalog.RouterRelevance.Primary, decision.RouterRelevance);

        Assert.True(end.AllowsStructuralInput);
        Assert.False(end.AllowsStructuralOutput);
        Assert.Equal(ProcessCanvasCatalog.RouterRelevance.Optional, end.RouterRelevance);
    }

    [Fact]
    public void Catalog_reports_cardinality_and_canonical_status_for_key_port_families()
    {
        var roleBinding = ProcessCanvasCatalog.GetPortFamilyMetadata(ProcessCanvasCatalog.PortFamily.RoleResponsibilityOutput);
        var stepDependency = ProcessCanvasCatalog.GetPortFamilyMetadata(ProcessCanvasCatalog.PortFamily.StepStructuralInput);
        var artifactInput = ProcessCanvasCatalog.GetPortFamilyMetadata(ProcessCanvasCatalog.PortFamily.StepArtifactInput);
        var branchDecision = ProcessCanvasCatalog.GetPortFamilyMetadata(ProcessCanvasCatalog.PortFamily.BranchDecisionRoleInput);

        Assert.Equal(ProcessCanvasCatalog.PortDirection.Output, roleBinding.Direction);
        Assert.Equal(ProcessCanvasCatalog.PortCardinality.ManyToMany, roleBinding.Cardinality);
        Assert.Equal(ProcessCanvasCatalog.CanonicalStatus.CanonicalToday, roleBinding.CanonicalStatus);

        Assert.Equal(ProcessCanvasCatalog.PortDirection.Input, stepDependency.Direction);
        Assert.Equal(ProcessCanvasCatalog.PortCardinality.ManyToSingle, stepDependency.Cardinality);
        Assert.Equal(ProcessCanvasCatalog.CanonicalStatus.CanonicalToday, stepDependency.CanonicalStatus);

        Assert.Equal(ProcessCanvasCatalog.PortDirection.Input, artifactInput.Direction);
        Assert.Equal(ProcessCanvasCatalog.PortCardinality.ManyToSingle, artifactInput.Cardinality);
        Assert.Equal(ProcessCanvasCatalog.CanonicalStatus.CanonicalToday, artifactInput.CanonicalStatus);

        Assert.Equal(ProcessCanvasCatalog.PortCardinality.SingleToSingle, branchDecision.Cardinality);
        Assert.Equal(ProcessCanvasCatalog.CanonicalStatus.CanonicalToday, branchDecision.CanonicalStatus);

        Assert.Equal("Responsible", ProcessCanvasCatalog.DefinitionPorts.GetResponsibilityLabel(ProcessResponsibilityKind.Responsible));
        Assert.Equal("Reviewer", ProcessCanvasCatalog.DefinitionPorts.GetResponsibilityLabel(ProcessResponsibilityKind.Reviewer));
        Assert.Equal("Approver", ProcessCanvasCatalog.DefinitionPorts.GetResponsibilityLabel(ProcessResponsibilityKind.Approver));
        Assert.Equal("Backup", ProcessCanvasCatalog.DefinitionPorts.GetResponsibilityLabel(ProcessResponsibilityKind.Backup));
    }

    [Fact]
    public void Catalog_classifies_branch_outcome_families_and_structural_compatibility()
    {
        var defaultOutcome = new ProcessStepBranchOutcomeEditorModel
        {
            Id = Guid.NewGuid(),
            Key = ProcessCanvasBranching.DefaultRouteKey,
            Title = ProcessCanvasBranching.DefaultRouteTitle
        };
        var errorOutcome = new ProcessStepBranchOutcomeEditorModel
        {
            Id = Guid.NewGuid(),
            Key = ProcessCanvasBranching.ErrorRouteKey,
            Title = ProcessCanvasBranching.ErrorRouteTitle
        };
        var customOutcome = new ProcessStepBranchOutcomeEditorModel
        {
            Id = Guid.NewGuid(),
            Key = "qa-lane",
            Title = "QA lane"
        };

        Assert.Equal(ProcessCanvasCatalog.PortFamily.BranchDefaultOutput, ProcessCanvasCatalog.GetBranchOutcomePortFamily(defaultOutcome));
        Assert.Equal(ProcessCanvasCatalog.PortFamily.BranchErrorOutput, ProcessCanvasCatalog.GetBranchOutcomePortFamily(errorOutcome));
        Assert.Equal(ProcessCanvasCatalog.PortFamily.BranchOutcomeOutput, ProcessCanvasCatalog.GetBranchOutcomePortFamily(customOutcome));
        Assert.True(ProcessCanvasCatalog.DefinitionPorts.IsStepStructuralInputPortId(CanvasWorkbenchAnchorPorts.Left));
        Assert.True(ProcessCanvasCatalog.DefinitionPorts.IsStepStructuralInputPortId(ProcessCanvasCatalog.DefinitionPorts.StepStructuralInput));
        Assert.True(ProcessCanvasCatalog.DefinitionPorts.IsStepStructuralOutputPortId(CanvasWorkbenchAnchorPorts.Right));
        Assert.True(ProcessCanvasCatalog.DefinitionPorts.IsStepStructuralOutputPortId(ProcessCanvasCatalog.DefinitionPorts.StepStructuralOutput));
    }
}
