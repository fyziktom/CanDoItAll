using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.WebGlLib;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessWebGlLayoutEngine
{
    public static IReadOnlyDictionary<string, ProcessWebGlLayoutPosition> Build(
        ProcessDefinitionEditorModel editor,
        CanvasWorkbenchSurface canvasSurface,
        string layoutMode,
        double nodeSpacingFactor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(canvasSurface);

        var nodesById = canvasSurface.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var laneEntries = BuildLaneEntries(editor, nodesById);
        var metrics = BuildNodeMetrics(canvasSurface);
        var laneLayouts = BuildLaneLayouts(laneEntries, layoutMode, nodeSpacingFactor, metrics);
        var laneProgressByNodeId = laneEntries.ToDictionary(entry => entry.Node.Id, entry => entry.Progress, StringComparer.Ordinal);
        var roleLayouts = BuildRoleLayouts(editor, canvasSurface, nodesById, laneProgressByNodeId, laneLayouts, layoutMode, nodeSpacingFactor, metrics);
        var artifactLayouts = BuildArtifactLayouts(canvasSurface, laneProgressByNodeId, laneLayouts, layoutMode, nodeSpacingFactor, metrics);

        return laneLayouts
            .Concat(roleLayouts)
            .Concat(artifactLayouts)
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, ProcessWebGlNodeMetrics> BuildNodeMetrics(CanvasWorkbenchSurface canvasSurface)
    {
        var metrics = canvasSurface.Nodes.ToDictionary(
            node => node.Id,
            node => new ProcessWebGlNodeMetrics(node),
            StringComparer.Ordinal);

        foreach (var link in canvasSurface.Links)
        {
            var isPrimary = IsPrimaryPath(link);
            var isBranchRoute = IsBranchRoute(link);

            if (metrics.TryGetValue(link.SourceId, out var sourceMetrics))
            {
                sourceMetrics.RegisterOutbound(isPrimary, isBranchRoute);
            }

            if (metrics.TryGetValue(link.TargetId, out var targetMetrics))
            {
                targetMetrics.RegisterInbound(isPrimary, isBranchRoute);
            }
        }

        return metrics;
    }

    private static List<ProcessLaneEntry> BuildLaneEntries(
        ProcessDefinitionEditorModel editor,
        IReadOnlyDictionary<string, CanvasWorkbenchNode> nodesById)
    {
        var entries = new List<ProcessLaneEntry>();
        for (var stepIndex = 0; stepIndex < editor.Steps.Count; stepIndex++)
        {
            var step = editor.Steps[stepIndex];
            var stepNodeId = ProcessCanvasBranching.BuildDefinitionStepNodeId(step);
            if (nodesById.TryGetValue(stepNodeId, out var stepNode))
            {
                entries.Add(new ProcessLaneEntry(
                    stepNode,
                    stepIndex,
                    stepIndex * 1.25d,
                    ResolveDefaultStepCanvasX(stepIndex),
                    ResolveDefaultStepCanvasY()));
            }

            if (!ProcessCanvasBranching.ShouldRenderBranchRouter(step))
            {
                continue;
            }

            var branchNodeId = ProcessCanvasBranching.BuildDefinitionBranchNodeId(step);
            if (!nodesById.TryGetValue(branchNodeId, out var branchNode))
            {
                continue;
            }

            entries.Add(new ProcessLaneEntry(
                branchNode,
                stepIndex,
                (stepIndex * 1.25d) + 0.62d,
                ResolveDefaultBranchCanvasX(editor.Steps, stepIndex),
                ResolveDefaultBranchCanvasY(editor.Steps, stepIndex)));
        }

        return entries;
    }

    private static Dictionary<string, ProcessWebGlLayoutPosition> BuildLaneLayouts(
        IReadOnlyList<ProcessLaneEntry> laneEntries,
        string layoutMode,
        double nodeSpacingFactor,
        IReadOnlyDictionary<string, ProcessWebGlNodeMetrics> metrics)
    {
        var layouts = new Dictionary<string, ProcessWebGlLayoutPosition>(StringComparer.Ordinal);
        if (laneEntries.Count == 0)
        {
            return layouts;
        }

        var spacing = NormalizeNodeSpacingFactor(nodeSpacingFactor);
        var maxProgress = laneEntries.Max(entry => entry.Progress);
        var criticalDepthMap = BuildDepthMap(laneEntries, metrics, spacing, 168d, 0.76d);
        var corridorDepthMap = BuildDepthMap(laneEntries, metrics, spacing, 152d, 0.68d);

        foreach (var entry in laneEntries)
        {
            var normalized = maxProgress <= 0
                ? 0.5d
                : entry.Progress / maxProgress;
            var lateralOffset = Math.Clamp((entry.Node.X - entry.DefaultCanvasX) / 280d, -2.2d, 2.2d) * 92d;
            var verticalOffset = Math.Clamp((entry.Node.Y - entry.DefaultCanvasY) / 220d, -2d, 2d) * 88d;
            layouts[entry.Node.Id] = ResolveLaneLayout(
                entry,
                normalized,
                lateralOffset,
                verticalOffset,
                layoutMode,
                spacing,
                metrics.GetValueOrDefault(entry.Node.Id),
                criticalDepthMap,
                corridorDepthMap);
        }

        return layouts;
    }

    private static Dictionary<string, ProcessWebGlLayoutPosition> BuildArtifactLayouts(
        CanvasWorkbenchSurface canvasSurface,
        IReadOnlyDictionary<string, double> laneProgressByNodeId,
        IReadOnlyDictionary<string, ProcessWebGlLayoutPosition> laneLayouts,
        string layoutMode,
        double nodeSpacingFactor,
        IReadOnlyDictionary<string, ProcessWebGlNodeMetrics> metrics)
    {
        var layouts = new Dictionary<string, ProcessWebGlLayoutPosition>(StringComparer.Ordinal);
        var artifactNodes = canvasSurface.Nodes
            .Where(IsArtifactNode)
            .OrderBy(node => node.X)
            .ThenBy(node => node.Y)
            .ThenBy(node => node.Id, StringComparer.Ordinal)
            .ToList();
        if (artifactNodes.Count == 0)
        {
            return layouts;
        }

        var maxProgress = laneProgressByNodeId.Count == 0
            ? 1d
            : laneProgressByNodeId.Values.Max();
        var spacing = NormalizeNodeSpacingFactor(nodeSpacingFactor);
        for (var artifactIndex = 0; artifactIndex < artifactNodes.Count; artifactIndex++)
        {
            var artifactNode = artifactNodes[artifactIndex];
            var linkedProgress = ResolveLinkedLaneProgress(canvasSurface, artifactNode.Id, laneProgressByNodeId);
            var linkedDepth = ResolveLinkedLaneDepth(canvasSurface, artifactNode.Id, laneLayouts);
            var normalized = maxProgress <= 0
                ? 0.5d
                : linkedProgress / maxProgress;
            var side = ProcessCanvasBranching.IsDefinitionArtifactCloneNodeId(artifactNode.Id)
                ? -1d
                : 1d;
            var nodeMetrics = metrics.GetValueOrDefault(artifactNode.Id) ?? ProcessWebGlNodeMetrics.Empty;
            var lateralOffset = Math.Clamp((artifactNode.X - 140d) / 360d, -2d, 2d) * 54d;
            var verticalOffset = Math.Clamp((artifactNode.Y - 180d) / 260d, -3d, 3d) * 42d;
            layouts[artifactNode.Id] = ResolveArtifactLayout(
                artifactIndex,
                normalized,
                linkedDepth,
                lateralOffset,
                verticalOffset,
                layoutMode,
                spacing,
                side,
                nodeMetrics);
        }

        return layouts;
    }

    private static Dictionary<string, ProcessWebGlLayoutPosition> BuildRoleLayouts(
        ProcessDefinitionEditorModel editor,
        CanvasWorkbenchSurface canvasSurface,
        IReadOnlyDictionary<string, CanvasWorkbenchNode> nodesById,
        IReadOnlyDictionary<string, double> laneProgressByNodeId,
        IReadOnlyDictionary<string, ProcessWebGlLayoutPosition> laneLayouts,
        string layoutMode,
        double nodeSpacingFactor,
        IReadOnlyDictionary<string, ProcessWebGlNodeMetrics> metrics)
    {
        var layouts = new Dictionary<string, ProcessWebGlLayoutPosition>(StringComparer.Ordinal);
        var roleNodes = canvasSurface.Nodes
            .Where(IsRoleNode)
            .OrderBy(node => node.X)
            .ThenBy(node => node.Y)
            .ThenBy(node => node.Id, StringComparer.Ordinal)
            .ToList();
        if (roleNodes.Count == 0)
        {
            return layouts;
        }

        var maxProgress = laneProgressByNodeId.Count == 0
            ? 1d
            : laneProgressByNodeId.Values.Max();
        for (var roleIndex = 0; roleIndex < roleNodes.Count; roleIndex++)
        {
            var roleNode = roleNodes[roleIndex];
            var linkedProgress = ResolveLinkedLaneProgress(canvasSurface, roleNode.Id, laneProgressByNodeId);
            var linkedDepth = ResolveLinkedLaneDepth(canvasSurface, roleNode.Id, laneLayouts);
            var normalized = maxProgress <= 0
                ? 0.5d
                : linkedProgress / maxProgress;
            var defaultX = ProcessCanvasBranching.IsDefinitionRoleInstanceNodeId(roleNode.Id)
                ? roleNode.X
                : ResolveDefaultRoleCanvasX(editor);
            var defaultY = ProcessCanvasBranching.IsDefinitionRoleInstanceNodeId(roleNode.Id)
                ? roleNode.Y
                : ResolveDefaultRoleCanvasY(roleIndex);
            var lateralOffset = Math.Clamp((roleNode.X - defaultX) / 260d, -2d, 2d) * 118d;
            var verticalOffset = Math.Clamp((roleNode.Y - defaultY) / 240d, -2d, 2d) * 96d;
            layouts[roleNode.Id] = ResolveRoleLayout(
                roleNode,
                roleIndex,
                normalized,
                linkedDepth,
                lateralOffset,
                verticalOffset,
                layoutMode,
                NormalizeNodeSpacingFactor(nodeSpacingFactor),
                metrics.GetValueOrDefault(roleNode.Id));
        }

        return layouts;
    }

    private static ProcessWebGlLayoutPosition ResolveLaneLayout(
        ProcessLaneEntry entry,
        double normalizedProgress,
        double lateralOffset,
        double verticalOffset,
        string layoutMode,
        double spacing,
        ProcessWebGlNodeMetrics? metrics,
        IReadOnlyDictionary<string, double> criticalDepthMap,
        IReadOnlyDictionary<string, double> corridorDepthMap)
    {
        var nodeMetrics = metrics ?? ProcessWebGlNodeMetrics.Empty;
        var branchLateralBias = IsBranchNode(entry.Node)
            ? 22d * spacing
            : 0d;
        var branchDepthBias = IsBranchNode(entry.Node)
            ? -118d * spacing
            : 0d;
        var alternatingSide = entry.StepIndex % 2 == 0
            ? -1d
            : 1d;

        return WebGlWorkbenchLayoutModes.Normalize(layoutMode) switch
        {
            WebGlWorkbenchLayoutModes.AlternatingArc => new ProcessWebGlLayoutPosition(
                Round((Math.Sin((normalizedProgress * Math.PI * 2d) - (Math.PI / 2d)) * (168d * spacing)) + (normalizedProgress * 96d * spacing) + (lateralOffset * 0.82d) + branchLateralBias),
                Round((Math.Cos(normalizedProgress * Math.PI * 2d) * (34d * spacing)) + verticalOffset + ResolveLaneVerticalBias(entry.Node)),
                Round(Lerp(360d * spacing, -1420d * spacing, normalizedProgress) - (Math.Sin(normalizedProgress * Math.PI) * (152d * spacing)) + branchDepthBias - (lateralOffset * 0.36d))),
            WebGlWorkbenchLayoutModes.LayeredOrbit => new ProcessWebGlLayoutPosition(
                Round(((normalizedProgress - 0.5d) * (148d * spacing)) + (Math.Sin(normalizedProgress * Math.PI * 3d) * (124d * spacing)) + (lateralOffset * 0.68d) + branchLateralBias),
                Round((((entry.StepIndex % 2 == 0) ? -22d : 26d) * spacing) + verticalOffset + ResolveLaneVerticalBias(entry.Node)),
                Round(Lerp(280d * spacing, -1180d * spacing, normalizedProgress) + (Math.Cos(normalizedProgress * Math.PI * 2d) * (118d * spacing)) + branchDepthBias - (lateralOffset * 0.28d))),
            WebGlWorkbenchLayoutModes.CriticalPathSpine => new ProcessWebGlLayoutPosition(
                Round(
                    (IsBranchNode(entry.Node)
                        ? alternatingSide * ((138d + (nodeMetrics.Clearance * 0.82d)) * spacing)
                        : Math.Sin(normalizedProgress * Math.PI * 1.28d) * (34d * spacing)) +
                    (lateralOffset * 0.4d) +
                    branchLateralBias),
                Round(
                    verticalOffset +
                    ResolveLaneVerticalBias(entry.Node) +
                    (IsBranchNode(entry.Node)
                        ? (-36d - (nodeMetrics.TotalConnections * 4.2d)) * spacing
                        : (nodeMetrics.PrimaryConnections * -2.4d))),
                Round(criticalDepthMap.GetValueOrDefault(entry.Node.Id) + branchDepthBias)),
            WebGlWorkbenchLayoutModes.FanoutCorridor => new ProcessWebGlLayoutPosition(
                Round(
                    (IsBranchNode(entry.Node)
                        ? alternatingSide * ((116d + (nodeMetrics.Clearance * 0.7d)) * spacing)
                        : (Math.Sin((normalizedProgress * Math.PI * 2.6d) + (entry.StepIndex * 0.22d)) * (72d * spacing)) +
                            (alternatingSide * (nodeMetrics.SpreadBias * 22d * spacing))) +
                    (lateralOffset * 0.56d) +
                    branchLateralBias),
                Round(
                    verticalOffset +
                    ResolveLaneVerticalBias(entry.Node) +
                    (Math.Cos(normalizedProgress * Math.PI * 2d) * (12d * spacing)) +
                    ((nodeMetrics.OutboundCount - nodeMetrics.InboundCount) * 6d)),
                Round(corridorDepthMap.GetValueOrDefault(entry.Node.Id) - (lateralOffset * 0.18d) + branchDepthBias)),
            WebGlWorkbenchLayoutModes.RadialBurst => ResolveRadialBurstLaneLayout(
                entry,
                normalizedProgress,
                lateralOffset,
                verticalOffset,
                spacing,
                nodeMetrics),
            _ => new ProcessWebGlLayoutPosition(
                Round(Lerp(-190d * spacing, 190d * spacing, normalizedProgress) + (Math.Sin(normalizedProgress * Math.PI) * (42d * spacing)) + (lateralOffset * 0.9d) + branchLateralBias),
                Round(verticalOffset + ResolveLaneVerticalBias(entry.Node)),
                Round(Lerp(260d * spacing, -1120d * spacing, normalizedProgress) + branchDepthBias - (lateralOffset * 0.45d)))
        };
    }

    private static ProcessWebGlLayoutPosition ResolveRoleLayout(
        CanvasWorkbenchNode roleNode,
        int roleIndex,
        double normalizedProgress,
        double linkedDepth,
        double lateralOffset,
        double verticalOffset,
        string layoutMode,
        double spacing,
        ProcessWebGlNodeMetrics? metrics)
    {
        var nodeMetrics = metrics ?? ProcessWebGlNodeMetrics.Empty;
        var side = roleIndex % 2 == 0
            ? -1d
            : 1d;
        var verticalBand = roleIndex % 4 switch
        {
            0 => -190d,
            1 => 170d,
            2 => 112d,
            _ => -126d
        };

        return WebGlWorkbenchLayoutModes.Normalize(layoutMode) switch
        {
            WebGlWorkbenchLayoutModes.AlternatingArc => new ProcessWebGlLayoutPosition(
                Round((side * (564d + (Math.Cos(normalizedProgress * Math.PI * 2d) * 112d))) * spacing + (lateralOffset * 0.46d)),
                Round((((roleIndex % 6) - 2.5d) * (86d * spacing)) + (verticalOffset * 0.82d) + (side * 24d)),
                Round(linkedDepth + (side * Math.Sin(normalizedProgress * Math.PI) * (92d * spacing)) + (((roleIndex % 4) - 1.5d) * (104d * spacing)) - (lateralOffset * 0.22d))),
            WebGlWorkbenchLayoutModes.LayeredOrbit => ResolveLayeredOrbitRoleLayout(
                roleNode,
                roleIndex,
                linkedDepth,
                lateralOffset,
                verticalOffset,
                spacing,
                side),
            WebGlWorkbenchLayoutModes.CriticalPathSpine => new ProcessWebGlLayoutPosition(
                Round((side * (548d + (nodeMetrics.Clearance * 0.72d))) * spacing + (lateralOffset * 0.34d)),
                Round((verticalBand * 0.82d * spacing) + (verticalOffset * 0.72d) + (side * 18d)),
                Round(linkedDepth + (((roleIndex % 3) - 1) * (88d * spacing)) - (lateralOffset * 0.14d))),
            WebGlWorkbenchLayoutModes.FanoutCorridor => new ProcessWebGlLayoutPosition(
                Round((side * (412d + (nodeMetrics.Clearance * 0.54d))) * spacing + (Math.Sin(normalizedProgress * Math.PI * 2d) * (58d * spacing)) + (lateralOffset * 0.22d)),
                Round((((roleIndex % 5) - 2d) * (74d * spacing)) + (verticalOffset * 0.78d)),
                Round(linkedDepth + (((roleIndex % 4) - 1.5d) * (118d * spacing)) + (Math.Cos(normalizedProgress * Math.PI * 2d) * (42d * spacing)))),
            WebGlWorkbenchLayoutModes.RadialBurst => ResolveRadialBurstRoleLayout(
                roleIndex,
                normalizedProgress,
                linkedDepth,
                lateralOffset,
                verticalOffset,
                spacing,
                side,
                nodeMetrics),
            _ => new ProcessWebGlLayoutPosition(
                Round((side * (468d + (Math.Abs(normalizedProgress - 0.5d) * 120d))) * spacing + (lateralOffset * 0.38d)),
                Round((verticalBand * spacing) + (verticalOffset * 0.72d)),
                Round(linkedDepth + (((roleIndex % 3) - 1) * (94d * spacing)) - (lateralOffset * 0.28d)))
        };
    }

    private static ProcessWebGlLayoutPosition ResolveArtifactLayout(
        int artifactIndex,
        double normalizedProgress,
        double linkedDepth,
        double lateralOffset,
        double verticalOffset,
        string layoutMode,
        double spacing,
        double side,
        ProcessWebGlNodeMetrics metrics)
    {
        var bandOffset = ((artifactIndex % 5) - 2d) * (34d * spacing);
        var distance = (244d + ((artifactIndex % 3) * 48d) + (metrics.Clearance * 0.22d)) * spacing;
        return WebGlWorkbenchLayoutModes.Normalize(layoutMode) switch
        {
            WebGlWorkbenchLayoutModes.RadialBurst => new ProcessWebGlLayoutPosition(
                Round((side * (distance + 90d)) + (Math.Sin(normalizedProgress * Math.PI) * 58d * spacing) + (lateralOffset * 0.32d)),
                Round(verticalOffset + bandOffset),
                Round(linkedDepth + (Math.Cos(normalizedProgress * Math.PI * 2d) * 78d * spacing) + (side * 54d * spacing))),
            WebGlWorkbenchLayoutModes.CriticalPathSpine or WebGlWorkbenchLayoutModes.FanoutCorridor => new ProcessWebGlLayoutPosition(
                Round((side * distance) + (lateralOffset * 0.28d)),
                Round(verticalOffset + bandOffset),
                Round(linkedDepth + (((artifactIndex % 3) - 1d) * 64d * spacing))),
            _ => new ProcessWebGlLayoutPosition(
                Round((side * distance) + (Math.Sin(normalizedProgress * Math.PI * 2d) * 36d * spacing) + (lateralOffset * 0.34d)),
                Round(verticalOffset + bandOffset),
                Round(linkedDepth + (((artifactIndex % 4) - 1.5d) * 52d * spacing)))
        };
    }

    private static ProcessWebGlLayoutPosition ResolveLayeredOrbitRoleLayout(
        CanvasWorkbenchNode roleNode,
        int roleIndex,
        double linkedDepth,
        double lateralOffset,
        double verticalOffset,
        double spacing,
        double side)
    {
        var orbitRadius = (392d + ((roleIndex % 3) * 108d)) * spacing;
        var angle = Lerp(-1.04d, 1.04d, roleIndex / Math.Max(1d, roleIndex + 1d)) + (side * 0.42d);
        return new ProcessWebGlLayoutPosition(
            Round((Math.Sin(angle) * orbitRadius) + (side * 184d * spacing) + (lateralOffset * 0.34d)),
            Round((((roleIndex % 5) - 2d) * (92d * spacing)) + (verticalOffset * 0.76d)),
            Round(linkedDepth + (Math.Cos(angle) * (148d * spacing)) + (((roleIndex % 2 == 0) ? -1 : 1) * (82d * spacing))));
    }

    private static ProcessWebGlLayoutPosition ResolveRadialBurstLaneLayout(
        ProcessLaneEntry entry,
        double normalizedProgress,
        double lateralOffset,
        double verticalOffset,
        double spacing,
        ProcessWebGlNodeMetrics metrics)
    {
        var side = entry.StepIndex % 2 == 0
            ? -1d
            : 1d;
        var radius = (220d + (normalizedProgress * 760d) + (metrics.Clearance * 0.44d)) * spacing;
        var angle = Lerp(-0.88d, 0.88d, normalizedProgress) + (side * (0.18d + (metrics.TotalConnections * 0.026d)));
        if (IsBranchNode(entry.Node))
        {
            angle += side * 0.24d;
        }

        return new ProcessWebGlLayoutPosition(
            Round((Math.Sin(angle) * radius) + (lateralOffset * 0.28d)),
            Round((Math.Cos(angle * 1.6d) * (56d * spacing)) + verticalOffset + ResolveLaneVerticalBias(entry.Node)),
            Round((-Math.Cos(angle) * radius) - (normalizedProgress * 96d * spacing)));
    }

    private static ProcessWebGlLayoutPosition ResolveRadialBurstRoleLayout(
        int roleIndex,
        double normalizedProgress,
        double linkedDepth,
        double lateralOffset,
        double verticalOffset,
        double spacing,
        double side,
        ProcessWebGlNodeMetrics metrics)
    {
        var radius = (620d + (metrics.Clearance * 0.52d) + ((roleIndex % 3) * 86d)) * spacing;
        var angle = Lerp(-0.94d, 0.94d, normalizedProgress) + (side * 0.64d);
        return new ProcessWebGlLayoutPosition(
            Round((Math.Sin(angle) * radius) + (lateralOffset * 0.18d)),
            Round((((roleIndex % 4) - 1.5d) * (88d * spacing)) + (verticalOffset * 0.68d)),
            Round(linkedDepth + (Math.Cos(angle) * (182d * spacing)) + (((roleIndex % 2 == 0) ? -1 : 1) * (96d * spacing))));
    }

    private static IReadOnlyDictionary<string, double> BuildDepthMap(
        IReadOnlyList<ProcessLaneEntry> laneEntries,
        IReadOnlyDictionary<string, ProcessWebGlNodeMetrics> metrics,
        double spacing,
        double baselineGap,
        double metricWeight)
    {
        var orderedEntries = laneEntries
            .OrderBy(entry => entry.Progress)
            .ThenBy(entry => entry.StepIndex)
            .ToList();
        var depths = new Dictionary<string, double>(StringComparer.Ordinal);
        var cursor = 320d * spacing;
        for (var index = 0; index < orderedEntries.Count; index++)
        {
            var entry = orderedEntries[index];
            depths[entry.Node.Id] = Round(cursor);
            var metricsForNode = metrics.GetValueOrDefault(entry.Node.Id) ?? ProcessWebGlNodeMetrics.Empty;
            var gap = (baselineGap + (metricsForNode.Clearance * metricWeight)) * spacing;
            if (IsBranchNode(entry.Node))
            {
                gap *= 0.72d;
            }

            cursor -= gap;
        }

        return depths;
    }

    private static double ResolveLinkedLaneProgress(
        CanvasWorkbenchSurface canvasSurface,
        string roleNodeId,
        IReadOnlyDictionary<string, double> laneProgressByNodeId)
    {
        var linkedProgress = canvasSurface.Links
            .Where(link =>
                string.Equals(link.SourceId, roleNodeId, StringComparison.Ordinal) ||
                string.Equals(link.TargetId, roleNodeId, StringComparison.Ordinal))
            .Select(link => string.Equals(link.SourceId, roleNodeId, StringComparison.Ordinal)
                ? link.TargetId
                : link.SourceId)
            .Where(candidateId => laneProgressByNodeId.ContainsKey(candidateId))
            .Select(candidateId => laneProgressByNodeId[candidateId])
            .ToList();
        if (linkedProgress.Count == 0)
        {
            return laneProgressByNodeId.Count == 0
                ? 0d
                : laneProgressByNodeId.Values.Average();
        }

        return linkedProgress.Average();
    }

    private static double ResolveLinkedLaneDepth(
        CanvasWorkbenchSurface canvasSurface,
        string roleNodeId,
        IReadOnlyDictionary<string, ProcessWebGlLayoutPosition> laneLayouts)
    {
        var linkedDepths = canvasSurface.Links
            .Where(link =>
                string.Equals(link.SourceId, roleNodeId, StringComparison.Ordinal) ||
                string.Equals(link.TargetId, roleNodeId, StringComparison.Ordinal))
            .Select(link => string.Equals(link.SourceId, roleNodeId, StringComparison.Ordinal)
                ? link.TargetId
                : link.SourceId)
            .Where(candidateId => laneLayouts.ContainsKey(candidateId))
            .Select(candidateId => laneLayouts[candidateId].Z)
            .ToList();

        return linkedDepths.Count == 0
            ? 0d
            : linkedDepths.Average();
    }

    private static double ResolveDefaultStepCanvasX(int stepIndex)
        => 140d + (stepIndex * 280d);

    private static double ResolveDefaultStepCanvasY()
        => 180d;

    private static double ResolveDefaultBranchCanvasX(
        IReadOnlyList<ProcessStepEditorModel> allSteps,
        int stepIndex)
    {
        var step = allSteps[stepIndex];
        var stepX = ResolveDefaultStepCanvasX(stepIndex);
        var directDependents = allSteps
            .Select((candidate, candidateIndex) => (candidate, candidateIndex))
            .Where(item => ProcessCanvasBranching.GetOrderedDependencies(item.candidate)
                .Any(dependency => dependency.DependsOnStepId == step.Id))
            .Select(item => ResolveDefaultStepCanvasX(item.candidateIndex))
            .ToList();
        if (directDependents.Count == 0)
        {
            return stepX + 320d;
        }

        var closestDependentX = directDependents.Min();
        return closestDependentX - stepX < 420d
            ? stepX + 320d
            : stepX + ((closestDependentX - stepX) / 2d);
    }

    private static double ResolveDefaultBranchCanvasY(
        IReadOnlyList<ProcessStepEditorModel> allSteps,
        int stepIndex)
    {
        var step = allSteps[stepIndex];
        var stepY = ResolveDefaultStepCanvasY();
        var directDependents = allSteps
            .Where(candidate => ProcessCanvasBranching.GetOrderedDependencies(candidate)
                .Any(dependency => dependency.DependsOnStepId == step.Id))
            .ToList();
        if (directDependents.Count == 0)
        {
            return stepY;
        }

        return directDependents.All(candidate =>
                Math.Abs(ResolveStepCanvasY(candidate) - stepY) < 90d)
            ? stepY + 220d
            : directDependents.Average(ResolveStepCanvasY);
    }

    private static double ResolveDefaultRoleCanvasX(ProcessDefinitionEditorModel editor)
    {
        if (editor.Steps.Count == 0)
        {
            return -180d;
        }

        return editor.Steps
            .Select((_, index) => ResolveDefaultStepCanvasX(index))
            .Min() - 360d;
    }

    private static double ResolveDefaultRoleCanvasY(int roleIndex)
        => 120d + (roleIndex * 210d);

    private static double ResolveStepCanvasY(ProcessStepEditorModel step)
        => step.CanvasY != 0
            ? step.CanvasY
            : ResolveDefaultStepCanvasY();

    private static double ResolveLaneVerticalBias(CanvasWorkbenchNode node)
    {
        if (IsBranchNode(node))
        {
            return -116d;
        }

        return node.Status switch
        {
            "approval" => -28d,
            "review" => -16d,
            "required" => -18d,
            _ => 0d
        };
    }

    private static bool IsPrimaryPath(CanvasWorkbenchLink link)
    {
        return string.Equals(ResolveLinkCategory(link), ProcessCanvasCatalog.ConnectionCategories.Structural, StringComparison.Ordinal) ||
            IsBranchRoute(link);
    }

    private static bool IsBranchRoute(CanvasWorkbenchLink link)
        => link.SourcePortId.StartsWith(ProcessCanvasCatalog.DefinitionPorts.BranchOutcomeOutputPrefix, StringComparison.Ordinal);

    private static string ResolveLinkCategory(CanvasWorkbenchLink link)
    {
        return link.Kind.ToLowerInvariant() switch
        {
            "artifact" => ProcessCanvasCatalog.ConnectionCategories.Artifact,
            "messaging" => ProcessCanvasCatalog.ConnectionCategories.Messaging,
            _ when IsBranchRoute(link) => ProcessCanvasCatalog.ConnectionCategories.BranchRoute,
            _ when link.SourcePortId.StartsWith(ProcessCanvasCatalog.DefinitionPorts.StepArtifactOutputPrefix, StringComparison.Ordinal)
                => ProcessCanvasCatalog.ConnectionCategories.Artifact,
            _ when ProcessCanvasCatalog.DefinitionPorts.TryGetRoleResponsibilityKind(link.SourcePortId, out var responsibilityKind)
                => ProcessCanvasCatalog.GetResponsibilityVisual(responsibilityKind).CategoryKey,
            _ when string.Equals(link.SourcePortId, ProcessCanvasBranching.RoleDecisionOutputPortId, StringComparison.Ordinal)
                => ProcessCanvasCatalog.ConnectionCategories.DecisionAuthority,
            _ => ProcessCanvasCatalog.ConnectionCategories.Structural
        };
    }

    private static bool IsRoleNode(CanvasWorkbenchNode node)
        => node.Kind.Contains("role", StringComparison.OrdinalIgnoreCase);

    private static bool IsBranchNode(CanvasWorkbenchNode node)
        => node.Kind.Contains("branch", StringComparison.OrdinalIgnoreCase);

    private static bool IsArtifactNode(CanvasWorkbenchNode node)
        => node.Kind.Contains("artifact", StringComparison.OrdinalIgnoreCase);

    private static double NormalizeNodeSpacingFactor(double value)
        => Math.Round(Math.Clamp(double.IsFinite(value) ? value : 1d, 0.75d, 1.85d), 2, MidpointRounding.AwayFromZero);

    private static double Lerp(double start, double end, double amount)
        => start + ((end - start) * amount);

    private static double Round(double value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

internal sealed class ProcessWebGlNodeMetrics
{
    public static readonly ProcessWebGlNodeMetrics Empty = new(new CanvasWorkbenchNode());

    public ProcessWebGlNodeMetrics(CanvasWorkbenchNode node)
    {
        IsRole = node.Kind.Contains("role", StringComparison.OrdinalIgnoreCase);
        IsBranch = node.Kind.Contains("branch", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsRole { get; }

    public bool IsBranch { get; }

    public int InboundCount { get; private set; }

    public int OutboundCount { get; private set; }

    public int PrimaryInboundCount { get; private set; }

    public int PrimaryOutboundCount { get; private set; }

    public int BranchRouteCount { get; private set; }

    public int TotalConnections => InboundCount + OutboundCount;

    public int PrimaryConnections => PrimaryInboundCount + PrimaryOutboundCount;

    public double Clearance
        => 104d +
            (TotalConnections * 26d) +
            (PrimaryConnections * 18d) +
            (BranchRouteCount * 24d) +
            (IsBranch ? 42d : 0d) +
            (IsRole ? 34d : 0d);

    public double SpreadBias
        => (OutboundCount - InboundCount) +
            (BranchRouteCount * 0.55d) +
            (PrimaryOutboundCount * 0.25d);

    public void RegisterInbound(bool isPrimary, bool isBranchRoute)
    {
        InboundCount += 1;
        if (isPrimary)
        {
            PrimaryInboundCount += 1;
        }

        if (isBranchRoute)
        {
            BranchRouteCount += 1;
        }
    }

    public void RegisterOutbound(bool isPrimary, bool isBranchRoute)
    {
        OutboundCount += 1;
        if (isPrimary)
        {
            PrimaryOutboundCount += 1;
        }

        if (isBranchRoute)
        {
            BranchRouteCount += 1;
        }
    }
}

internal sealed record ProcessLaneEntry(
    CanvasWorkbenchNode Node,
    int StepIndex,
    double Progress,
    double DefaultCanvasX,
    double DefaultCanvasY);

internal sealed record ProcessWebGlLayoutPosition(
    double X,
    double Y,
    double Z);
