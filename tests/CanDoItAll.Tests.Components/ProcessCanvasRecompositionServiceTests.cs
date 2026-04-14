using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Components;

public sealed class ProcessCanvasRecompositionServiceTests
{
    [Fact]
    public void Resolve_collisions_separates_overlapping_process_nodes()
    {
        var surfaceFactory = CreateSurfaceFactory();
        var service = new ProcessCanvasRecompositionService(surfaceFactory);
        var editor = BuildCollisionEditor();

        Assert.True(HasOverlap(ResolveBoxes(surfaceFactory, editor)));

        var result = service.Apply(editor, ProcessCanvasRecompositionMode.ResolveCollisions);

        Assert.True(result.RepositionedNodeCount > 0);
        AssertNoOverlaps(ResolveBoxes(surfaceFactory, editor));
    }

    [Fact]
    public void Add_space_around_expands_the_canvas_footprint()
    {
        var surfaceFactory = CreateSurfaceFactory();
        var service = new ProcessCanvasRecompositionService(surfaceFactory);
        var editor = BuildSpacingEditor();
        var beforeBounds = ResolveBounds(ResolveBoxes(surfaceFactory, editor));

        var result = service.Apply(editor, ProcessCanvasRecompositionMode.AddSpaceAround);

        var afterBounds = ResolveBounds(ResolveBoxes(surfaceFactory, editor));
        Assert.True(result.RepositionedNodeCount > 0);
        Assert.True(afterBounds.Width > beforeBounds.Width + 20d);
        Assert.True(afterBounds.Height > beforeBounds.Height + 10d);
    }

    [Fact]
    public void Recompose_builds_a_branching_process_layout_without_overlaps()
    {
        var surfaceFactory = CreateSurfaceFactory();
        var service = new ProcessCanvasRecompositionService(surfaceFactory);
        var editor = BuildBranchingEditor();

        var result = service.Apply(editor, ProcessCanvasRecompositionMode.Recompose);

        Assert.True(result.RepositionedNodeCount > 0);

        var boxesById = ResolveBoxes(surfaceFactory, editor)
            .ToDictionary(box => box.NodeId, StringComparer.Ordinal);
        AssertNoOverlaps(boxesById.Values);

        var intakeStep = editor.Steps.Single(step => string.Equals(step.Key, "intake", StringComparison.Ordinal));
        var decisionStep = editor.Steps.Single(step => string.Equals(step.Key, "review-path", StringComparison.Ordinal));
        var implementationStep = editor.Steps.Single(step => string.Equals(step.Key, "implement", StringComparison.Ordinal));
        var repairStep = editor.Steps.Single(step => string.Equals(step.Key, "repair", StringComparison.Ordinal));
        var mergeStep = editor.Steps.Single(step => string.Equals(step.Key, "merge", StringComparison.Ordinal));
        var role = Assert.Single(editor.Roles);

        var intakeBox = boxesById[ProcessCanvasBranching.BuildDefinitionStepNodeId(intakeStep)];
        var decisionBox = boxesById[ProcessCanvasBranching.BuildDefinitionStepNodeId(decisionStep)];
        var implementationBox = boxesById[ProcessCanvasBranching.BuildDefinitionStepNodeId(implementationStep)];
        var repairBox = boxesById[ProcessCanvasBranching.BuildDefinitionStepNodeId(repairStep)];
        var mergeBox = boxesById[ProcessCanvasBranching.BuildDefinitionStepNodeId(mergeStep)];
        var branchBox = boxesById[ProcessCanvasBranching.BuildDefinitionBranchNodeId(decisionStep)];
        var roleBox = boxesById[ProcessCanvasBranching.BuildDefinitionRoleNodeId(role)];

        Assert.True(roleBox.X < intakeBox.X);
        Assert.True(intakeBox.X < decisionBox.X);
        Assert.True(decisionBox.X < implementationBox.X);
        Assert.True(decisionBox.X < repairBox.X);
        Assert.True(branchBox.X > decisionBox.X);
        Assert.True(branchBox.X < Math.Min(implementationBox.X, repairBox.X));
        Assert.True(implementationBox.Y < mergeBox.Y || repairBox.Y < mergeBox.Y || mergeBox.X > implementationBox.X);
    }

    [Fact]
    public void Recompose_throws_for_cyclic_process_graph()
    {
        var surfaceFactory = CreateSurfaceFactory();
        var service = new ProcessCanvasRecompositionService(surfaceFactory);
        var editor = BuildCyclicEditor();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            service.Apply(editor, ProcessCanvasRecompositionMode.Recompose));

        Assert.Contains("acyclic dependency graph", exception.Message, StringComparison.Ordinal);
    }

    private static ProcessCanvasSurfaceFactory CreateSurfaceFactory()
    {
        return new ProcessCanvasSurfaceFactory(
            new ProcessCanvasChromeCatalogService(
                new ProcessTemplatePackLoader()));
    }

    private static IReadOnlyList<CanvasLayoutNodeBox> ResolveBoxes(
        ProcessCanvasSurfaceFactory surfaceFactory,
        ProcessDefinitionEditorModel editor)
    {
        return surfaceFactory.BuildDefinitionSurface(editor).Nodes
            .Select(node => CanvasLayoutNodeBox.FromNode(node))
            .ToList();
    }

    private static void AssertNoOverlaps(IEnumerable<CanvasLayoutNodeBox> boxes)
    {
        var materialized = boxes.ToList();
        for (var index = 0; index < materialized.Count - 1; index++)
        {
            for (var otherIndex = index + 1; otherIndex < materialized.Count; otherIndex++)
            {
                Assert.False(
                    Overlaps(materialized[index], materialized[otherIndex]),
                    $"{materialized[index].NodeId} overlaps {materialized[otherIndex].NodeId}.");
            }
        }
    }

    private static bool HasOverlap(IReadOnlyList<CanvasLayoutNodeBox> boxes)
    {
        for (var index = 0; index < boxes.Count - 1; index++)
        {
            for (var otherIndex = index + 1; otherIndex < boxes.Count; otherIndex++)
            {
                if (Overlaps(boxes[index], boxes[otherIndex]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool Overlaps(CanvasLayoutNodeBox left, CanvasLayoutNodeBox right)
    {
        return left.Left < right.Right &&
               left.Right > right.Left &&
               left.Top < right.Bottom &&
               left.Bottom > right.Top;
    }

    private static (double Width, double Height) ResolveBounds(IReadOnlyList<CanvasLayoutNodeBox> boxes)
    {
        return (
            boxes.Max(box => box.Right) - boxes.Min(box => box.Left),
            boxes.Max(box => box.Bottom) - boxes.Min(box => box.Top));
    }

    private static ProcessDefinitionEditorModel BuildCollisionEditor()
    {
        var roleId = Guid.NewGuid();
        var firstStepId = Guid.NewGuid();
        var secondStepId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            Name = "Collision relief test",
            Summary = "Resolve overlaps.",
            ValueStatement = "Process nodes should separate cleanly.",
            CustomerName = "Acme",
            OwnerName = "Owner",
            GovernancePolicySummary = "Collision proof.",
            ChangeSummary = "Collision proof.",
            ConstitutionRuleSummary = "Collision proof.",
            OperatingModeSummary = "Collision proof.",
            SimulationReadinessSummary = "Collision proof.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = roleId,
                    Key = "owner",
                    DisplayName = "Owner",
                    CanvasX = 320,
                    CanvasY = 180
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = firstStepId,
                    Key = "capture",
                    Title = "Capture request",
                    StepKind = ProcessStepKind.Start,
                    CanvasX = 320,
                    CanvasY = 180
                },
                new ProcessStepEditorModel
                {
                    Id = secondStepId,
                    Key = "review",
                    Title = "Review request",
                    StepKind = ProcessStepKind.Work,
                    Dependencies = CreateDependencies((firstStepId, null)),
                    CanvasX = 320,
                    CanvasY = 180,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = roleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                }
            ]
        };
    }

    private static ProcessDefinitionEditorModel BuildSpacingEditor()
    {
        var roleId = Guid.NewGuid();
        var intakeStepId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            Name = "Spacing test",
            Summary = "Expand spacing.",
            ValueStatement = "Keep more air between nodes.",
            CustomerName = "Acme",
            OwnerName = "Owner",
            GovernancePolicySummary = "Spacing proof.",
            ChangeSummary = "Spacing proof.",
            ConstitutionRuleSummary = "Spacing proof.",
            OperatingModeSummary = "Spacing proof.",
            SimulationReadinessSummary = "Spacing proof.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = roleId,
                    Key = "reviewer",
                    DisplayName = "Reviewer",
                    CanvasX = -20,
                    CanvasY = 140
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = intakeStepId,
                    Key = "capture",
                    Title = "Capture request",
                    StepKind = ProcessStepKind.Start,
                    CanvasX = 180,
                    CanvasY = 140
                },
                new ProcessStepEditorModel
                {
                    Key = "validate",
                    Title = "Validate request",
                    StepKind = ProcessStepKind.Work,
                    Dependencies = CreateDependencies((intakeStepId, null)),
                    CanvasX = 470,
                    CanvasY = 140,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = roleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                }
            ]
        };
    }

    private static ProcessDefinitionEditorModel BuildBranchingEditor()
    {
        var roleId = Guid.NewGuid();
        var intakeStepId = Guid.NewGuid();
        var decisionStepId = Guid.NewGuid();
        var defaultOutcomeId = Guid.NewGuid();
        var repairOutcomeId = Guid.NewGuid();
        var implementationStepId = Guid.NewGuid();
        var repairStepId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            Name = "Branch recomposition test",
            Summary = "Rebuild branching layout.",
            ValueStatement = "Process recomposition should create a readable fishbone.",
            CustomerName = "Acme",
            OwnerName = "Owner",
            GovernancePolicySummary = "Recomposition proof.",
            ChangeSummary = "Recomposition proof.",
            ConstitutionRuleSummary = "Recomposition proof.",
            OperatingModeSummary = "Recomposition proof.",
            SimulationReadinessSummary = "Recomposition proof.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = roleId,
                    Key = "qa-lead",
                    DisplayName = "QA lead",
                    CanvasX = 980,
                    CanvasY = 440
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = intakeStepId,
                    Key = "intake",
                    Title = "Capture request",
                    StepKind = ProcessStepKind.Start,
                    CanvasX = 920,
                    CanvasY = 380
                },
                new ProcessStepEditorModel
                {
                    Id = decisionStepId,
                    Key = "review-path",
                    Title = "Review outcome",
                    StepKind = ProcessStepKind.Decision,
                    Dependencies = CreateDependencies((intakeStepId, null)),
                    DecisionRoleRequirementId = roleId,
                    CanvasX = 1080,
                    CanvasY = 380,
                    BranchOutcomes =
                    [
                        new ProcessStepBranchOutcomeEditorModel
                        {
                            Id = repairOutcomeId,
                            Key = "repair-needed",
                            Title = "Repair needed"
                        },
                        new ProcessStepBranchOutcomeEditorModel
                        {
                            Id = defaultOutcomeId,
                            Key = ProcessCanvasBranching.DefaultRouteKey,
                            Title = ProcessCanvasBranching.DefaultRouteTitle
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = implementationStepId,
                    Key = "implement",
                    Title = "Implement change",
                    StepKind = ProcessStepKind.Work,
                    Dependencies = CreateDependencies((decisionStepId, defaultOutcomeId)),
                    CanvasX = 1040,
                    CanvasY = 380,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = roleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = repairStepId,
                    Key = "repair",
                    Title = "Repair change",
                    StepKind = ProcessStepKind.Work,
                    Dependencies = CreateDependencies((decisionStepId, repairOutcomeId)),
                    CanvasX = 1040,
                    CanvasY = 380
                },
                new ProcessStepEditorModel
                {
                    Key = "merge",
                    Title = "Merge change",
                    StepKind = ProcessStepKind.End,
                    Dependencies = CreateDependencies((implementationStepId, null)),
                    CanvasX = 1040,
                    CanvasY = 380
                }
            ]
        };
    }

    private static List<ProcessStepDependencyEditorModel> CreateDependencies(params (Guid StepId, Guid? BranchOutcomeId)[] items)
    {
        return items
            .Select(item => new ProcessStepDependencyEditorModel
            {
                Id = Guid.NewGuid(),
                DependsOnStepId = item.StepId,
                DependsOnBranchOutcomeId = item.BranchOutcomeId
            })
            .ToList();
    }

    private static ProcessDefinitionEditorModel BuildCyclicEditor()
    {
        var firstStepId = Guid.NewGuid();
        var secondStepId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            Name = "Cycle recomposition test",
            Summary = "Reject cyclic graph recomposition.",
            ValueStatement = "Recomposition must fail loudly for invalid graphs.",
            CustomerName = "Acme",
            OwnerName = "Owner",
            GovernancePolicySummary = "Cycle proof.",
            ChangeSummary = "Cycle proof.",
            ConstitutionRuleSummary = "Cycle proof.",
            OperatingModeSummary = "Cycle proof.",
            SimulationReadinessSummary = "Cycle proof.",
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = firstStepId,
                    Key = "capture",
                    Title = "Capture request",
                    StepKind = ProcessStepKind.Start,
                    Dependencies = CreateDependencies((secondStepId, null)),
                    CanvasX = 180,
                    CanvasY = 160
                },
                new ProcessStepEditorModel
                {
                    Id = secondStepId,
                    Key = "review",
                    Title = "Review request",
                    StepKind = ProcessStepKind.Work,
                    Dependencies = CreateDependencies((firstStepId, null)),
                    CanvasX = 460,
                    CanvasY = 160
                }
            ]
        };
    }
}
