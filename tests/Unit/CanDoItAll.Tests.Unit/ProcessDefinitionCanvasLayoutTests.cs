using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDefinitionCanvasLayoutTests
{
    private const double StepWidth = 240d;
    private const double StepHeight = 140d;

    [Fact]
    public void Recompose_linear_process_keeps_typed_start_to_end_path_on_one_lane()
    {
        var start = Step("start", ProcessDefinitionStepKind.Start);
        var work = Step("work", ProcessDefinitionStepKind.Work);
        var end = Step("end", ProcessDefinitionStepKind.End);
        var edges = new[]
        {
            Edge("start-work", start, work),
            Edge("work-end", work, end)
        };

        var result = ProcessDefinitionCanvasRecompositionEngine.Recompose([start, work, end], edges);

        var positioned = result.Nodes.ToDictionary(node => node.NodeKey);
        Assert.Equal(positioned[start.NodeKey].Y, positioned[work.NodeKey].Y);
        Assert.Equal(positioned[work.NodeKey].Y, positioned[end.NodeKey].Y);
        Assert.True(positioned[start.NodeKey].X < positioned[work.NodeKey].X);
        Assert.True(positioned[work.NodeKey].X < positioned[end.NodeKey].X);
        Assert.Equal(
            new HashSet<ProcessDefinitionCanvasNodeKey> { start.NodeKey, work.NodeKey, end.NodeKey },
            result.MainPathNodeKeys);
    }

    [Fact]
    public void Recompose_split_and_rejoin_keeps_primary_route_on_main_lane_and_fans_out_alternative()
    {
        var start = Step("start", ProcessDefinitionStepKind.Start);
        var primary = Step("primary", ProcessDefinitionStepKind.Work);
        var alternative = Step("alternative", ProcessDefinitionStepKind.Review);
        var join = Step("join", ProcessDefinitionStepKind.Approval);
        var end = Step("end", ProcessDefinitionStepKind.End);
        var edges = new[]
        {
            Edge("start-primary", start, primary),
            Edge("start-alternative", start, alternative),
            Edge("primary-join", primary, join),
            Edge("alternative-join", alternative, join),
            Edge("join-end", join, end)
        };

        var result = ProcessDefinitionCanvasRecompositionEngine.Recompose(
            [start, primary, alternative, join, end],
            edges);

        var positioned = result.Nodes.ToDictionary(node => node.NodeKey);
        var mainLaneY = positioned[start.NodeKey].Y;
        Assert.Equal(mainLaneY, positioned[primary.NodeKey].Y);
        Assert.Equal(mainLaneY, positioned[join.NodeKey].Y);
        Assert.Equal(mainLaneY, positioned[end.NodeKey].Y);
        Assert.NotEqual(mainLaneY, positioned[alternative.NodeKey].Y);
        Assert.Equal(positioned[primary.NodeKey].X, positioned[alternative.NodeKey].X);
        Assert.DoesNotContain(alternative.NodeKey, result.MainPathNodeKeys);
    }

    [Fact]
    public void Recompose_prefers_authored_success_terminal_over_later_failure_end()
    {
        var start = Step("start", ProcessDefinitionStepKind.Start);
        var decision = Step("decision", ProcessDefinitionStepKind.Decision);
        var approved = Step("approved", ProcessDefinitionStepKind.Approval);
        var failure = Step("failure", ProcessDefinitionStepKind.End);
        var edges = new[]
        {
            Edge("start-decision", start, decision),
            Edge("decision-approved", decision, approved, ProcessDefinitionCanvasEdgeKind.BranchRoute),
            Edge("decision-failure", decision, failure, ProcessDefinitionCanvasEdgeKind.BranchRoute)
        };

        var result = ProcessDefinitionCanvasRecompositionEngine.Recompose(
            [start, decision, approved, failure],
            edges);

        var positioned = result.Nodes.ToDictionary(node => node.NodeKey);
        Assert.Contains(approved.NodeKey, result.MainPathNodeKeys);
        Assert.DoesNotContain(failure.NodeKey, result.MainPathNodeKeys);
        Assert.Equal(positioned[start.NodeKey].Y, positioned[approved.NodeKey].Y);
        Assert.NotEqual(positioned[start.NodeKey].Y, positioned[failure.NodeKey].Y);
    }

    [Fact]
    public void Recompose_equal_priority_end_paths_uses_straight_authored_route_and_is_idempotent()
    {
        var start = Step("start", ProcessDefinitionStepKind.Start, x: 100d, y: 400d);
        var decision = Step("decision", ProcessDefinitionStepKind.Decision, x: 500d, y: 400d);
        var repair = Step("repair", ProcessDefinitionStepKind.Work, x: 900d, y: 800d);
        var repairEnd = Step("repair-end", ProcessDefinitionStepKind.End, x: 1_300d, y: 800d);
        var success = Step("success", ProcessDefinitionStepKind.Work, x: 900d, y: 400d);
        var successEnd = Step("success-end", ProcessDefinitionStepKind.End, x: 1_300d, y: 400d);
        var edges = new[]
        {
            Edge("start-decision", start, decision),
            Edge("decision-repair", decision, repair, ProcessDefinitionCanvasEdgeKind.BranchRoute),
            Edge("repair-end", repair, repairEnd),
            Edge("decision-success", decision, success, ProcessDefinitionCanvasEdgeKind.BranchRoute),
            Edge("success-end", success, successEnd)
        };

        var first = ProcessDefinitionCanvasRecompositionEngine.Recompose(
            [start, decision, repair, repairEnd, success, successEnd],
            edges);
        var second = ProcessDefinitionCanvasRecompositionEngine.Recompose(first.Nodes, first.Edges);

        var positioned = first.Nodes.ToDictionary(node => node.NodeKey);
        var expectedMainPath = new HashSet<ProcessDefinitionCanvasNodeKey>
        {
            start.NodeKey,
            decision.NodeKey,
            success.NodeKey,
            successEnd.NodeKey
        };
        Assert.Equal(expectedMainPath, first.MainPathNodeKeys);
        Assert.Equal(positioned[start.NodeKey].Y, positioned[decision.NodeKey].Y);
        Assert.Equal(positioned[decision.NodeKey].Y, positioned[success.NodeKey].Y);
        Assert.Equal(positioned[success.NodeKey].Y, positioned[successEnd.NodeKey].Y);
        Assert.NotEqual(positioned[decision.NodeKey].Y, positioned[repair.NodeKey].Y);
        Assert.NotEqual(positioned[decision.NodeKey].Y, positioned[repairEnd.NodeKey].Y);
        Assert.Equal(first.MainPathNodeKeys, second.MainPathNodeKeys);
        Assert.Equal(PositionMap(first.Nodes), PositionMap(second.Nodes));
    }

    [Fact]
    public void Recompose_typed_backward_loop_ignores_loop_for_ranking_and_preserves_route_metadata()
    {
        var start = Step("start", ProcessDefinitionStepKind.Start);
        var review = Step("review", ProcessDefinitionStepKind.Review);
        var end = Step("end", ProcessDefinitionStepKind.End);
        var backward = Edge(
            "review-start",
            review,
            start,
            ProcessDefinitionCanvasEdgeKind.BranchRoute,
            isBackwardRoute: true);
        var edges = new[]
        {
            Edge("start-review", start, review),
            Edge("review-end", review, end),
            backward
        };

        var result = ProcessDefinitionCanvasRecompositionEngine.Recompose([start, review, end], edges);

        var positioned = result.Nodes.ToDictionary(node => node.NodeKey);
        Assert.True(positioned[start.NodeKey].X < positioned[review.NodeKey].X);
        Assert.True(positioned[review.NodeKey].X < positioned[end.NodeKey].X);
        Assert.Equal(edges, result.Edges);
        Assert.Equal(backward, Assert.Single(result.Edges, edge => edge.IsBackwardRoute));
    }

    [Fact]
    public void Recompose_unmarked_forward_cycle_fails_with_actionable_error()
    {
        var start = Step("start", ProcessDefinitionStepKind.Start);
        var work = Step("work", ProcessDefinitionStepKind.Work);
        var end = Step("end", ProcessDefinitionStepKind.End);
        var edges = new[]
        {
            Edge("start-work", start, work),
            Edge("work-end", work, end),
            Edge("work-start", work, start)
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProcessDefinitionCanvasRecompositionEngine.Recompose([start, work, end], edges));

        Assert.Contains("Mark bounded loop routes as backward", exception.Message, StringComparison.Ordinal);
        Assert.Contains("start", exception.Message, StringComparison.Ordinal);
        Assert.Contains("work", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Recompose_mixed_graph_is_deterministic_idempotent_and_non_overlapping()
    {
        var role = Node("role", ProcessDefinitionCanvasNodeKind.Role, width: 220d, height: 120d);
        var start = Step("start", ProcessDefinitionStepKind.Start);
        var primary = Step("primary", ProcessDefinitionStepKind.Work);
        var alternative = Step("alternative", ProcessDefinitionStepKind.Review);
        var join = Step("join", ProcessDefinitionStepKind.Approval);
        var end = Step("end", ProcessDefinitionStepKind.End);
        var router = Node(
            "router",
            ProcessDefinitionCanvasNodeKind.BranchRouter,
            stepKey: start.StepKey,
            width: 64d,
            height: 64d);
        var artifact = Node(
            "artifact",
            ProcessDefinitionCanvasNodeKind.Artifact,
            stepKey: primary.StepKey,
            artifactKey: "proof",
            width: 200d,
            height: 100d);
        var reference = Node(
            "artifact-reference",
            ProcessDefinitionCanvasNodeKind.Artifact,
            artifactKey: "proof",
            width: 200d,
            height: 100d);
        var subprocess = Node(
            "subprocess",
            ProcessDefinitionCanvasNodeKind.SubprocessBoundary,
            stepKey: join.StepKey,
            width: 220d,
            height: 110d);
        var nodes = new[]
        {
            role,
            start,
            primary,
            alternative,
            join,
            end,
            router,
            artifact,
            reference,
            subprocess
        };
        var edges = new[]
        {
            Edge("start-router", start, router, ProcessDefinitionCanvasEdgeKind.BranchRoute),
            Edge("router-primary", router, primary, ProcessDefinitionCanvasEdgeKind.BranchRoute),
            Edge("router-alternative", router, alternative, ProcessDefinitionCanvasEdgeKind.BranchRoute),
            Edge("primary-join", primary, join),
            Edge("alternative-join", alternative, join),
            Edge("join-end", join, end),
            Edge("role-primary", role, primary, ProcessDefinitionCanvasEdgeKind.RoleBinding),
            Edge("primary-artifact", primary, artifact, ProcessDefinitionCanvasEdgeKind.ArtifactExpectation),
            Edge("join-subprocess", join, subprocess, ProcessDefinitionCanvasEdgeKind.SubprocessBoundary)
        };

        var first = ProcessDefinitionCanvasRecompositionEngine.Recompose(nodes, edges);
        var repeatedFromOriginal = ProcessDefinitionCanvasRecompositionEngine.Recompose(nodes, edges);
        var repeatedFromResult = ProcessDefinitionCanvasRecompositionEngine.Recompose(first.Nodes, first.Edges);

        Assert.Equal(PositionMap(first.Nodes), PositionMap(repeatedFromOriginal.Nodes));
        Assert.Equal(PositionMap(first.Nodes), PositionMap(repeatedFromResult.Nodes));
        Assert.Equal(first.MainPathNodeKeys, repeatedFromResult.MainPathNodeKeys);
        AssertNoOverlap(first.Nodes);
    }

    [Fact]
    public void Recompose_spaces_complete_step_groups_and_keeps_shared_role_representations_local()
    {
        var start = Step("start", ProcessDefinitionStepKind.Start);
        var finish = Step("finish", ProcessDefinitionStepKind.End);
        var roleKey = new ProcessDefinitionRoleKey("delivery-manager");
        var startRole = Node(
            "role-start",
            ProcessDefinitionCanvasNodeKind.Role,
            stepKey: start.StepKey,
            roleKey: roleKey,
            width: 220d,
            height: 120d);
        var finishRole = Node(
            "role-finish",
            ProcessDefinitionCanvasNodeKind.Role,
            stepKey: finish.StepKey,
            roleKey: roleKey,
            width: 220d,
            height: 120d);
        var startArtifact = Node(
            "artifact-start",
            ProcessDefinitionCanvasNodeKind.Artifact,
            stepKey: start.StepKey,
            artifactKey: "start-proof",
            width: 200d,
            height: 100d);
        var finishArtifact = Node(
            "artifact-finish",
            ProcessDefinitionCanvasNodeKind.Artifact,
            stepKey: finish.StepKey,
            artifactKey: "finish-proof",
            width: 200d,
            height: 100d);
        var edges = new[]
        {
            Edge("start-finish", start, finish),
            Edge("role-start-binding", startRole, start, ProcessDefinitionCanvasEdgeKind.RoleBinding),
            Edge("role-finish-binding", finishRole, finish, ProcessDefinitionCanvasEdgeKind.RoleBinding),
            Edge("start-artifact", start, startArtifact, ProcessDefinitionCanvasEdgeKind.ArtifactExpectation),
            Edge("finish-artifact", finish, finishArtifact, ProcessDefinitionCanvasEdgeKind.ArtifactExpectation)
        };

        var result = ProcessDefinitionCanvasRecompositionEngine.Recompose(
            [start, finish, startRole, finishRole, startArtifact, finishArtifact],
            edges);

        var positioned = result.Nodes.ToDictionary(node => node.NodeKey);
        Assert.Equal(roleKey, positioned[startRole.NodeKey].RoleKey);
        Assert.Equal(roleKey, positioned[finishRole.NodeKey].RoleKey);
        Assert.True(positioned[startRole.NodeKey].Y < positioned[start.NodeKey].Y);
        Assert.True(positioned[finishRole.NodeKey].Y < positioned[finish.NodeKey].Y);
        Assert.True(positioned[startArtifact.NodeKey].Y > positioned[start.NodeKey].Y);
        Assert.True(positioned[finishArtifact.NodeKey].Y > positioned[finish.NodeKey].Y);

        var startGroupBounds = ResolveBounds(
            positioned[start.NodeKey],
            positioned[startRole.NodeKey],
            positioned[startArtifact.NodeKey]);
        var finishGroupBounds = ResolveBounds(
            positioned[finish.NodeKey],
            positioned[finishRole.NodeKey],
            positioned[finishArtifact.NodeKey]);
        Assert.True(startGroupBounds.Right + 80d <= finishGroupBounds.Left);
        AssertNoOverlap(result.Nodes);
    }

    [Fact]
    public void Recompose_rejects_role_representation_whose_owner_disagrees_with_binding_target()
    {
        var start = Step("start", ProcessDefinitionStepKind.Start);
        var finish = Step("finish", ProcessDefinitionStepKind.End);
        var role = Node(
            "role",
            ProcessDefinitionCanvasNodeKind.Role,
            stepKey: start.StepKey,
            roleKey: new ProcessDefinitionRoleKey("reviewer"));
        var edges = new[]
        {
            Edge("start-finish", start, finish),
            Edge("role-finish", role, finish, ProcessDefinitionCanvasEdgeKind.RoleBinding)
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProcessDefinitionCanvasRecompositionEngine.Recompose([start, finish, role], edges));

        Assert.Contains("disagrees", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PlaceStep_with_existing_continuation_uses_first_free_side_lane_without_moving_existing_nodes()
    {
        var anchor = Step("anchor", ProcessDefinitionStepKind.Work, x: 240d, y: 420d);
        var continuation = Step("continuation", ProcessDefinitionStepKind.Work, x: 604d, y: 420d);
        var upperBlocker = Step("upper-blocker", ProcessDefinitionStepKind.Work, x: 604d, y: 100d);
        var nodes = new[] { anchor, continuation, upperBlocker };
        var originalPositions = PositionMap(nodes);
        var edges = new[] { Edge("anchor-continuation", anchor, continuation) };

        var first = ProcessDefinitionCanvasPlacementPolicy.PlaceStep(
            nodes,
            edges,
            anchor,
            StepWidth,
            StepHeight);
        var repeated = ProcessDefinitionCanvasPlacementPolicy.PlaceStep(
            nodes,
            edges,
            anchor,
            StepWidth,
            StepHeight);

        Assert.Equal(first, repeated);
        Assert.Equal((584d, 740d), first);
        Assert.Equal(originalPositions, PositionMap(nodes));
        var candidateBounds = ProcessDefinitionCanvasBounds.FromCenter(
            first.X,
            first.Y,
            StepWidth,
            StepHeight);
        Assert.DoesNotContain(nodes, node =>
            ProcessDefinitionCanvasPlacementPolicy.Intersects(
                candidateBounds,
                ProcessDefinitionCanvasPlacementPolicy.ResolveBounds(node)));
    }

    private static ProcessDefinitionCanvasEditorNodeProjection Step(
        string key,
        ProcessDefinitionStepKind stepKind,
        double x = 0d,
        double y = 0d)
        => Node(
            key,
            ProcessDefinitionCanvasNodeKind.Step,
            new ProcessDefinitionStepKey(key),
            stepKind: stepKind,
            x: x,
            y: y);

    private static ProcessDefinitionCanvasEditorNodeProjection Node(
        string key,
        ProcessDefinitionCanvasNodeKind kind,
        ProcessDefinitionStepKey? stepKey = null,
        string? artifactKey = null,
        ProcessDefinitionRoleKey? roleKey = null,
        ProcessDefinitionStepKind? stepKind = null,
        double x = 0d,
        double y = 0d,
        double width = StepWidth,
        double height = StepHeight)
        => new(
            new ProcessDefinitionCanvasNodeKey(key),
            kind,
            key,
            string.Empty,
            string.Empty,
            x,
            y,
            width,
            height,
            "neutral",
            stepKey,
            roleKey,
            artifactKey,
            Badges: [],
            Ports: [],
            stepKind);

    private static ProcessDefinitionCanvasBounds ResolveBounds(
        params ProcessDefinitionCanvasEditorNodeProjection[] nodes)
        => new(
            nodes.Min(node => node.X - (node.Width / 2d)),
            nodes.Min(node => node.Y - (node.Height / 2d)),
            nodes.Max(node => node.X + (node.Width / 2d)),
            nodes.Max(node => node.Y + (node.Height / 2d)));

    private static ProcessDefinitionCanvasEdgeProjection Edge(
        string key,
        ProcessDefinitionCanvasEditorNodeProjection from,
        ProcessDefinitionCanvasEditorNodeProjection to,
        ProcessDefinitionCanvasEdgeKind kind = ProcessDefinitionCanvasEdgeKind.Dependency,
        bool isBackwardRoute = false)
        => new(
            new ProcessDefinitionCanvasEdgeKey(key),
            kind,
            from.NodeKey,
            to.NodeKey,
            string.Empty,
            string.Empty,
            "neutral",
            isBackwardRoute);

    private static Dictionary<ProcessDefinitionCanvasNodeKey, (double X, double Y)> PositionMap(
        IEnumerable<ProcessDefinitionCanvasEditorNodeProjection> nodes)
        => nodes.ToDictionary(node => node.NodeKey, node => (node.X, node.Y));

    private static void AssertNoOverlap(IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> nodes)
    {
        for (var leftIndex = 0; leftIndex < nodes.Count; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < nodes.Count; rightIndex++)
            {
                var left = nodes[leftIndex];
                var right = nodes[rightIndex];
                Assert.False(
                    ProcessDefinitionCanvasPlacementPolicy.Intersects(
                        ProcessDefinitionCanvasPlacementPolicy.ResolveBounds(left),
                        ProcessDefinitionCanvasPlacementPolicy.ResolveBounds(right)),
                    $"Canvas nodes '{left.NodeKey.Value}' and '{right.NodeKey.Value}' overlap.");
            }
        }
    }
}
