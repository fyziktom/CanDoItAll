using System.Text.Json;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Components;

public sealed class ProcessCanvasRecompositionServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly string[] DefaultProcessKeys =
    [
        "dotnet-solution-setup",
        "dotnet-feature-function-implementation",
        "dotnet-development-slice",
        "blazor-app-delivery",
        "blazor-app-repair-fix",
        "blazor-backend-feature",
        "blazor-frontend-feature",
        "blazor-fullstack-feature",
        "software-delivery",
        "ai-assisted-change-delivery",
        "branching-code-review",
        "architecture-decision-governance",
        "release-readiness-and-deployment"
    ];

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
        var decisionRoleBox = boxesById[ProcessCanvasBranching.BuildDefinitionRoleInstanceNodeId(role, decisionStep)];
        var implementationRoleBox = boxesById[ProcessCanvasBranching.BuildDefinitionRoleInstanceNodeId(role, implementationStep)];

        Assert.True(intakeBox.X < decisionBox.X);
        Assert.True(decisionBox.X < implementationBox.X);
        Assert.True(decisionBox.X < repairBox.X);
        Assert.True(decisionRoleBox.X < decisionBox.X);
        Assert.True(implementationRoleBox.X < implementationBox.X);
        Assert.True(Math.Abs(decisionRoleBox.Y - decisionBox.Y) < 1d);
        Assert.True(Math.Abs(implementationRoleBox.Y - implementationBox.Y) < 1d);
        Assert.True(Math.Abs(implementationBox.Y - decisionBox.Y) < 24d);
        Assert.True(Math.Abs(mergeBox.Y - decisionBox.Y) < 1d);
        Assert.True(Math.Abs(repairBox.Y - decisionBox.Y) > 150d);
        Assert.True(branchBox.X > decisionBox.X);
        Assert.True(branchBox.X < Math.Min(implementationBox.X, repairBox.X));
        Assert.True(Math.Abs(branchBox.Y - decisionBox.Y) > 80d);
    }

    [Fact]
    public void Recompose_keeps_multi_dependency_primary_continuations_on_main_lane()
    {
        var surfaceFactory = CreateSurfaceFactory();
        var service = new ProcessCanvasRecompositionService(surfaceFactory);
        var editor = BuildPrimaryContinuationEditor();

        service.Apply(editor, ProcessCanvasRecompositionMode.Recompose);

        var boxesById = ResolveBoxes(surfaceFactory, editor)
            .ToDictionary(box => box.NodeId, StringComparer.Ordinal);
        var reviewStep = editor.Steps.Single(step => string.Equals(step.Key, "review", StringComparison.Ordinal));
        var implementationStep = editor.Steps.Single(step => string.Equals(step.Key, "implement", StringComparison.Ordinal));

        var reviewBox = boxesById[ProcessCanvasBranching.BuildDefinitionStepNodeId(reviewStep)];
        var implementationBox = boxesById[ProcessCanvasBranching.BuildDefinitionStepNodeId(implementationStep)];

        Assert.True(reviewBox.X < implementationBox.X);
        Assert.True(Math.Abs(reviewBox.Y - implementationBox.Y) < 1d);
    }

    [Fact]
    public void Default_process_template_projections_load_in_balanced_flow()
    {
        var packLoader = new ProcessTemplatePackLoader();
        var surfaceFactory = CreateSurfaceFactory(packLoader);
        var service = new ProcessCanvasRecompositionService(surfaceFactory);
        var projectionService = new ProcessTemplateProjectionService(packLoader, service);

        foreach (var processKey in DefaultProcessKeys)
        {
            var envelope = projectionService.GetProjectedEnvelope(processKey);
            var editor = ToEditorModel(envelope.Definition);
            var beforePositions = editor.Steps.ToDictionary(
                step => ProcessCanvasBranching.BuildDefinitionStepNodeId(step),
                step => $"{step.Key} ({step.CanvasX}, {step.CanvasY})",
                StringComparer.Ordinal);
            foreach (var step in editor.Steps.Where(ProcessCanvasBranching.ShouldRenderBranchRouter))
            {
                beforePositions[ProcessCanvasBranching.BuildDefinitionBranchNodeId(step)] =
                    $"{step.Key} branch ({step.BranchCanvasX}, {step.BranchCanvasY})";
            }
            var repeatResult = service.Apply(editor, ProcessCanvasRecompositionMode.Recompose);

            Assert.Contains(
                envelope.Warnings,
                warning => warning.Contains("Balanced Flow", StringComparison.Ordinal));
            Assert.True(
                repeatResult.RepositionedNodeCount == 0,
                $"{processKey} changed {repeatResult.RepositionedNodeCount} node(s) after projection: {string.Join("; ", repeatResult.RepositionedNodeIds.Select(nodeId => beforePositions.GetValueOrDefault(nodeId, nodeId)))}.");
            AssertNoOverlaps(ResolveBalancedFlowBoxes(surfaceFactory, editor), processKey);
        }
    }

    [Fact]
    public void Main_path_spine_pushes_branches_farther_from_the_default_path()
    {
        var surfaceFactory = CreateSurfaceFactory();
        var balancedEditor = BuildBranchingEditor();
        var spineEditor = BuildBranchingEditor();
        var service = new ProcessCanvasRecompositionService(surfaceFactory);

        service.Apply(balancedEditor, ProcessCanvasRecompositionMode.Recompose);
        service.Apply(spineEditor, ProcessCanvasRecompositionMode.MainPathSpine);

        var balancedBoxesById = ResolveBoxes(surfaceFactory, balancedEditor)
            .ToDictionary(box => box.NodeId, StringComparer.Ordinal);
        var spineBoxesById = ResolveBoxes(surfaceFactory, spineEditor)
            .ToDictionary(box => box.NodeId, StringComparer.Ordinal);
        var balancedDecisionBox = ResolveStepBox(balancedEditor, balancedBoxesById, "review-path");
        var balancedRepairBox = ResolveStepBox(balancedEditor, balancedBoxesById, "repair");
        var spineDecisionBox = ResolveStepBox(spineEditor, spineBoxesById, "review-path");
        var spineImplementationBox = ResolveStepBox(spineEditor, spineBoxesById, "implement");
        var spineRepairBox = ResolveStepBox(spineEditor, spineBoxesById, "repair");

        AssertNoOverlaps(spineBoxesById.Values);
        Assert.True(Math.Abs(spineImplementationBox.Y - spineDecisionBox.Y) < 1d);
        Assert.True(
            Math.Abs(spineRepairBox.Y - spineDecisionBox.Y) >
            Math.Abs(balancedRepairBox.Y - balancedDecisionBox.Y) + 250d);
    }

    [Fact]
    public void Branch_fan_out_increases_step_columns_and_branch_lane_spacing()
    {
        var surfaceFactory = CreateSurfaceFactory();
        var balancedEditor = BuildBranchingEditor();
        var fanOutEditor = BuildBranchingEditor();
        var service = new ProcessCanvasRecompositionService(surfaceFactory);

        service.Apply(balancedEditor, ProcessCanvasRecompositionMode.Recompose);
        service.Apply(fanOutEditor, ProcessCanvasRecompositionMode.BranchFanOut);

        var balancedBoxesById = ResolveBoxes(surfaceFactory, balancedEditor)
            .ToDictionary(box => box.NodeId, StringComparer.Ordinal);
        var fanOutBoxesById = ResolveBoxes(surfaceFactory, fanOutEditor)
            .ToDictionary(box => box.NodeId, StringComparer.Ordinal);
        var balancedDecisionBox = ResolveStepBox(balancedEditor, balancedBoxesById, "review-path");
        var balancedImplementationBox = ResolveStepBox(balancedEditor, balancedBoxesById, "implement");
        var fanOutDecisionBox = ResolveStepBox(fanOutEditor, fanOutBoxesById, "review-path");
        var fanOutImplementationBox = ResolveStepBox(fanOutEditor, fanOutBoxesById, "implement");
        var fanOutRepairBox = ResolveStepBox(fanOutEditor, fanOutBoxesById, "repair");

        AssertNoOverlaps(fanOutBoxesById.Values);
        Assert.True(fanOutImplementationBox.X - fanOutDecisionBox.X > balancedImplementationBox.X - balancedDecisionBox.X + 120d);
        Assert.True(Math.Abs(fanOutRepairBox.Y - fanOutDecisionBox.Y) > 850d);
    }

    [Fact]
    public void Feedback_lanes_place_feedback_steps_below_main_path_and_alternatives_above()
    {
        var surfaceFactory = CreateSurfaceFactory();
        var service = new ProcessCanvasRecompositionService(surfaceFactory);
        var editor = BuildFeedbackLaneEditor();

        service.Apply(editor, ProcessCanvasRecompositionMode.FeedbackLanes);

        var boxesById = ResolveBoxes(surfaceFactory, editor)
            .ToDictionary(box => box.NodeId, StringComparer.Ordinal);
        var decisionBox = ResolveStepBox(editor, boxesById, "review-path");
        var implementationBox = ResolveStepBox(editor, boxesById, "implement");
        var consultBox = ResolveStepBox(editor, boxesById, "consult-legal");
        var repairBox = ResolveStepBox(editor, boxesById, "repair-change");

        AssertNoOverlaps(boxesById.Values);
        Assert.True(Math.Abs(implementationBox.Y - decisionBox.Y) < 1d);
        Assert.True(consultBox.Y < decisionBox.Y - 700d);
        Assert.True(repairBox.Y > decisionBox.Y + 700d);
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

    private static ProcessCanvasSurfaceFactory CreateSurfaceFactory(ProcessTemplatePackLoader? packLoader = null)
    {
        return new ProcessCanvasSurfaceFactory(
            new ProcessCanvasChromeCatalogService(
                packLoader ?? new ProcessTemplatePackLoader()));
    }

    private static IReadOnlyList<CanvasLayoutNodeBox> ResolveBoxes(
        ProcessCanvasSurfaceFactory surfaceFactory,
        ProcessDefinitionEditorModel editor)
    {
        return surfaceFactory.BuildDefinitionSurface(editor).Nodes
            .Select(node => CanvasLayoutNodeBox.FromNode(node))
            .ToList();
    }

    private static IReadOnlyList<CanvasLayoutNodeBox> ResolveBalancedFlowBoxes(
        ProcessCanvasSurfaceFactory surfaceFactory,
        ProcessDefinitionEditorModel editor)
    {
        return surfaceFactory.BuildDefinitionSurface(editor).Nodes
            .Where(node =>
                string.Equals(node.Kind, ProcessCanvasCatalog.NodeKinds.DefinitionStep, StringComparison.Ordinal) ||
                string.Equals(node.Kind, ProcessCanvasCatalog.NodeKinds.DefinitionBranchRouter, StringComparison.Ordinal) ||
                string.Equals(node.Kind, ProcessCanvasCatalog.NodeKinds.DefinitionRole, StringComparison.Ordinal))
            .Select(node => CanvasLayoutNodeBox.FromNode(node))
            .ToList();
    }

    private static ProcessDefinitionEditorModel ToEditorModel(ProcessDefinitionImportExportModel definition)
    {
        return JsonSerializer.Deserialize<ProcessDefinitionEditorModel>(
            JsonSerializer.Serialize(definition, JsonOptions),
            JsonOptions) ?? new ProcessDefinitionEditorModel();
    }

    private static CanvasLayoutNodeBox ResolveStepBox(
        ProcessDefinitionEditorModel editor,
        IReadOnlyDictionary<string, CanvasLayoutNodeBox> boxesById,
        string stepKey)
    {
        var step = editor.Steps.Single(item => string.Equals(item.Key, stepKey, StringComparison.Ordinal));
        return boxesById[ProcessCanvasBranching.BuildDefinitionStepNodeId(step)];
    }

    private static void AssertNoOverlaps(IEnumerable<CanvasLayoutNodeBox> boxes, string scope = "")
    {
        var materialized = boxes.ToList();
        for (var index = 0; index < materialized.Count - 1; index++)
        {
            for (var otherIndex = index + 1; otherIndex < materialized.Count; otherIndex++)
            {
                var left = materialized[index];
                var right = materialized[otherIndex];
                Assert.False(
                    Overlaps(left, right),
                    $"{scope} {left.NodeId} ({left.X}, {left.Y}, {left.Width}x{left.Height}) overlaps {right.NodeId} ({right.X}, {right.Y}, {right.Width}x{right.Height}).".Trim());
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

    private static ProcessDefinitionEditorModel BuildFeedbackLaneEditor()
    {
        var roleId = Guid.NewGuid();
        var intakeStepId = Guid.NewGuid();
        var decisionStepId = Guid.NewGuid();
        var acceptedOutcomeId = Guid.NewGuid();
        var consultOutcomeId = Guid.NewGuid();
        var repairOutcomeId = Guid.NewGuid();
        var implementationStepId = Guid.NewGuid();
        var consultStepId = Guid.NewGuid();
        var repairStepId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            Name = "Feedback lane recomposition test",
            Summary = "Separate feedback lanes from ordinary alternatives.",
            ValueStatement = "Repair paths should not visually cut through the main delivery spine.",
            CustomerName = "Acme",
            OwnerName = "Owner",
            GovernancePolicySummary = "Feedback lane proof.",
            ChangeSummary = "Feedback lane proof.",
            ConstitutionRuleSummary = "Feedback lane proof.",
            OperatingModeSummary = "Feedback lane proof.",
            SimulationReadinessSummary = "Feedback lane proof.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = roleId,
                    Key = "reviewer",
                    DisplayName = "Reviewer",
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
                            Id = consultOutcomeId,
                            Key = "consult-needed",
                            Title = "Consult needed"
                        },
                        new ProcessStepBranchOutcomeEditorModel
                        {
                            Id = repairOutcomeId,
                            Key = "repair-needed",
                            Title = "Repair needed"
                        },
                        new ProcessStepBranchOutcomeEditorModel
                        {
                            Id = acceptedOutcomeId,
                            Key = "accepted",
                            Title = "Accepted"
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = implementationStepId,
                    Key = "implement",
                    Title = "Implement change",
                    StepKind = ProcessStepKind.Work,
                    Dependencies = CreateDependencies((decisionStepId, acceptedOutcomeId)),
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
                    Id = consultStepId,
                    Key = "consult-legal",
                    Title = "Consult legal",
                    StepKind = ProcessStepKind.Review,
                    Dependencies = CreateDependencies((decisionStepId, consultOutcomeId)),
                    CanvasX = 1040,
                    CanvasY = 380
                },
                new ProcessStepEditorModel
                {
                    Id = repairStepId,
                    Key = "repair-change",
                    Title = "Repair change",
                    StepKind = ProcessStepKind.Work,
                    Dependencies = CreateDependencies((decisionStepId, repairOutcomeId)),
                    CanvasX = 1040,
                    CanvasY = 380
                }
            ]
        };
    }

    private static ProcessDefinitionEditorModel BuildPrimaryContinuationEditor()
    {
        var roleId = Guid.NewGuid();
        var intakeStepId = Guid.NewGuid();
        var reviewStepId = Guid.NewGuid();
        var implementationStepId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            Name = "Primary continuation recomposition test",
            Summary = "Keep multi-input continuation on the main path.",
            ValueStatement = "A step that depends on an earlier root and its direct continuation should not become a branch lane.",
            CustomerName = "Acme",
            OwnerName = "Owner",
            GovernancePolicySummary = "Primary continuation proof.",
            ChangeSummary = "Primary continuation proof.",
            ConstitutionRuleSummary = "Primary continuation proof.",
            OperatingModeSummary = "Primary continuation proof.",
            SimulationReadinessSummary = "Primary continuation proof.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = roleId,
                    Key = "engineer",
                    DisplayName = "Engineer",
                    CanvasX = 120,
                    CanvasY = 40
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
                    CanvasX = 120,
                    CanvasY = 120
                },
                new ProcessStepEditorModel
                {
                    Id = reviewStepId,
                    Key = "review",
                    Title = "Review request",
                    StepKind = ProcessStepKind.Review,
                    Dependencies = CreateDependencies((intakeStepId, null)),
                    CanvasX = 430,
                    CanvasY = 120
                },
                new ProcessStepEditorModel
                {
                    Id = implementationStepId,
                    Key = "implement",
                    Title = "Implement request",
                    StepKind = ProcessStepKind.Work,
                    Dependencies = CreateDependencies((intakeStepId, null), (reviewStepId, null)),
                    CanvasX = 740,
                    CanvasY = 120,
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
