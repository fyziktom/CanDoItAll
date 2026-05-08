using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.WebGlLib;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessWebGlSceneAdapter
{
    private static readonly IReadOnlyList<RepresentativeTemplateDefinition> RepresentativeTemplates =
    [
        new("customer-onboarding", "Simple", "Fast sanity check for sparse scenes."),
        new("architecture-decision-governance", "Medium", "Branching plus governance semantics without maximum density."),
        new("branching-code-review", "Dense", "Stress case for overlap, routing, and authoring."),
        new("software-delivery", "Complex", "Multi-team delivery path with shared ownership, validations, and release governance."),
        new("ai-assisted-change-delivery", "Complex", "Guardrailed delegation flow with approvals, evaluation, and rollback-minded checkpoints."),
        new("release-readiness-and-deployment", "Complex", "Release control path with rehearsal, cutover, and deployment verification.")
    ];

    private readonly ProcessTemplateCatalogService templateCatalogService;
    private readonly ProcessTemplateProjectionService projectionService;
    private readonly ProcessCanvasSurfaceFactory canvasSurfaceFactory;

    public ProcessWebGlSceneAdapter(
        ProcessTemplateCatalogService templateCatalogService,
        ProcessTemplateProjectionService projectionService,
        ProcessCanvasSurfaceFactory canvasSurfaceFactory)
    {
        this.templateCatalogService = templateCatalogService;
        this.projectionService = projectionService;
        this.canvasSurfaceFactory = canvasSurfaceFactory;
    }

    public IReadOnlyList<ProcessWebGlTemplateDescriptor> ListRepresentativeTemplates()
    {
        var catalog = templateCatalogService.ListProcessTemplates()
            .ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);

        return RepresentativeTemplates
            .Select(template =>
            {
                catalog.TryGetValue(template.Key, out var item);
                return new ProcessWebGlTemplateDescriptor(
                    template.Key,
                    item?.DisplayName ?? template.Key,
                    template.Complexity,
                    template.Summary,
                    item?.StepCount ?? 0);
            })
            .ToList();
    }

    public ProcessDefinitionEditorModel LoadProjectedDefinition(string templateKey)
    {
        var envelope = projectionService.GetProjectedEnvelope(templateKey, definitionName: ResolveDefinitionName(templateKey));
        var editor = ProcessDependencyCompatibilityBridge.ToEditorModel(envelope.Definition);
        StabilizeEditorIds(editor, templateKey);
        ProcessCanvasBranching.NormalizeDefinitionEditor(editor);
        return editor;
    }

    public WebGlWorkbenchSurface BuildDefinitionScene(
        ProcessDefinitionEditorModel editor,
        ProcessWebGlSceneOptions options)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(options);

        ProcessCanvasBranching.NormalizeDefinitionEditor(editor);

        var canvasSurface = canvasSurfaceFactory.BuildDefinitionSurface(editor, options.SelectedNodeId, mode: "authoring");
        var descriptor = ListRepresentativeTemplates()
            .FirstOrDefault(item => string.Equals(item.Key, options.TemplateKey, StringComparison.OrdinalIgnoreCase));
        var layoutMode = WebGlWorkbenchLayoutModes.Normalize(options.LayoutMode);
        var nodeSpacingFactor = NormalizeNodeSpacingFactor(options.NodeSpacingFactor);
        var layout = ProcessWebGlLayoutEngine.Build(editor, canvasSurface, layoutMode, nodeSpacingFactor);

        return new WebGlWorkbenchSurface
        {
            SurfaceId = $"{options.TemplateKey}:webgl-workbench",
            SceneKey = options.TemplateKey,
            Title = editor.Name,
            Subtitle = editor.Summary,
            Nodes = canvasSurface.Nodes
                .Select(node => MapNode(node, layout))
                .ToList(),
            Edges = canvasSurface.Links
                .Select(MapEdge)
                .ToList(),
            UiState = new WebGlWorkbenchUiState
            {
                SelectedNodeIds = [.. canvasSurface.UiState.SelectedNodeIds],
                ActiveViewPreset = string.IsNullOrWhiteSpace(options.ViewPreset)
                    ? WebGlWorkbenchViewPresets.Overview
                    : options.ViewPreset,
                LayoutMode = layoutMode,
                NodeSpacingFactor = nodeSpacingFactor,
                DeterministicMode = options.DeterministicMode,
                ShowDiagnostics = options.ShowDiagnostics,
                Camera = new WebGlWorkbenchCameraState
                {
                    ViewMode = WebGlWorkbenchCameraViewModes.Normalize(options.CameraViewMode, options.ProjectionMode),
                    ProjectionMode = WebGlWorkbenchCameraViewModes.ResolveProjectionMode(options.CameraViewMode),
                    Zoom = options.CameraState?.Zoom ?? 1,
                    TargetX = options.CameraState?.TargetX ?? 0,
                    TargetY = options.CameraState?.TargetY ?? 0,
                    TargetZ = options.CameraState?.TargetZ ?? 0,
                    Distance = options.CameraState?.Distance ?? 1180,
                    Azimuth = options.CameraState?.Azimuth ?? -0.72d,
                    Polar = options.CameraState?.Polar ?? 1.08d
                }
            },
            Chrome = new WebGlWorkbenchChrome
            {
                HintText = descriptor is null
                    ? $"{layoutMode} layout · {nodeSpacingFactor:0.##}x spacing · {canvasSurface.Nodes.Count} nodes / {canvasSurface.Links.Count} connections"
                    : $"{descriptor.Complexity} template · {layoutMode} layout · {nodeSpacingFactor:0.##}x spacing · {canvasSurface.Nodes.Count} nodes · {canvasSurface.Links.Count} connections",
                EmptyStateTitle = "No process nodes available",
                EmptyStateDescription = "The projected template did not produce any canvas nodes."
            }
        };
    }

    public void ApplyNodePositions(
        ProcessDefinitionEditorModel editor,
        IReadOnlyList<WebGlNodePositionChange> positions)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(positions);

        foreach (var position in positions)
        {
            if (string.IsNullOrWhiteSpace(position.NodeId))
            {
                continue;
            }

            var step = editor.Steps.FirstOrDefault(candidate =>
                string.Equals(ProcessCanvasBranching.BuildDefinitionStepNodeId(candidate), position.NodeId, StringComparison.Ordinal));
            if (step is not null)
            {
                step.CanvasX = position.X;
                step.CanvasY = position.Y;
                continue;
            }

            var branchStep = editor.Steps.FirstOrDefault(candidate =>
                string.Equals(ProcessCanvasBranching.BuildDefinitionBranchNodeId(candidate), position.NodeId, StringComparison.Ordinal));
            if (branchStep is not null)
            {
                branchStep.BranchCanvasX = position.X;
                branchStep.BranchCanvasY = position.Y;
                continue;
            }

            var role = editor.Roles.FirstOrDefault(candidate =>
                string.Equals(ProcessCanvasBranching.BuildDefinitionRoleNodeId(candidate), position.NodeId, StringComparison.Ordinal));
            if (role is not null)
            {
                role.CanvasX = position.X;
                role.CanvasY = position.Y;
            }
        }
    }

    public bool ApplyConnectionChange(
        ProcessDefinitionEditorModel editor,
        WebGlConnectionChangeRequest request)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(request);

        var sourcePortId = ResolveCanvasPortId(request.SourceAnchorId, request.SourcePortId);
        var targetPortId = ResolveCanvasPortId(request.TargetAnchorId, request.TargetPortId);
        if (string.IsNullOrWhiteSpace(sourcePortId) || string.IsNullOrWhiteSpace(targetPortId))
        {
            return false;
        }

        var isDisconnect = string.Equals(request.ActionId, WebGlWorkbenchConnectionActions.Disconnect, StringComparison.Ordinal);
        var changed = false;

        if (TryResolveDecisionAuthorityChange(editor, request.SourceNodeId, request.TargetNodeId, sourcePortId, targetPortId, isDisconnect, out changed) && changed)
        {
            ProcessCanvasBranching.NormalizeDefinitionEditor(editor);
            return true;
        }

        if (TryResolveResponsibilityChange(editor, request.SourceNodeId, request.TargetNodeId, sourcePortId, targetPortId, isDisconnect, out changed) && changed)
        {
            ProcessCanvasBranching.NormalizeDefinitionEditor(editor);
            return true;
        }

        if (TryResolveMessagingChange(editor, request.SourceNodeId, request.TargetNodeId, sourcePortId, targetPortId, isDisconnect, out changed) && changed)
        {
            ProcessCanvasBranching.NormalizeDefinitionEditor(editor);
            return true;
        }

        if (TryResolveArtifactChange(editor, request.SourceNodeId, request.TargetNodeId, sourcePortId, targetPortId, isDisconnect, out changed) && changed)
        {
            ProcessCanvasBranching.NormalizeDefinitionEditor(editor);
            return true;
        }

        if (TryResolveDependencyChange(editor, request.SourceNodeId, request.TargetNodeId, sourcePortId, targetPortId, isDisconnect, out changed) && changed)
        {
            ProcessCanvasBranching.NormalizeDefinitionEditor(editor);
            return true;
        }

        return false;
    }

    private static WebGlWorkbenchNode MapNode(
        CanvasWorkbenchNode node,
        IReadOnlyDictionary<string, ProcessWebGlLayoutPosition> layout)
    {
        var position = ResolveNodeLayout(node.Id, layout);
        var (width, height, depth) = ResolveNodeSize(node);
        return new WebGlWorkbenchNode
        {
            Id = node.Id,
            Kind = node.Kind,
            Family = node.Family,
            Title = node.Title,
            Subtitle = node.Subtitle,
            Description = node.LeadText,
            Status = node.Status,
            AccentColor = ResolveAccentColor(node),
            FillColor = ResolveFillColor(node),
            BorderColor = ResolveBorderColor(node),
            X = position.X,
            Y = position.Y,
            Z = position.Z,
            Width = width,
            Height = height,
            Depth = depth,
            IsReadOnly = node.IsReadOnly,
            Tags = [.. node.Chips.Concat(node.FooterChips).Select(chip => chip.Text).Where(text => !string.IsNullOrWhiteSpace(text)).Take(6)],
            Anchors = BuildAnchors(node)
        };
    }

    private static WebGlWorkbenchEdge MapEdge(CanvasWorkbenchLink link)
    {
        var categoryKey = ResolveCategoryKey(link);
        var isPrimaryPath = IsPrimaryPath(link, categoryKey);
        return new WebGlWorkbenchEdge
        {
            Id = BuildEdgeId(link),
            SourceNodeId = link.SourceId,
            SourceAnchorId = BuildAnchorId(link.SourceId, link.SourcePortId),
            SourcePortId = link.SourcePortId,
            TargetNodeId = link.TargetId,
            TargetAnchorId = BuildAnchorId(link.TargetId, link.TargetPortId),
            TargetPortId = link.TargetPortId,
            Kind = string.IsNullOrWhiteSpace(link.Kind) ? "flow" : link.Kind,
            CategoryKey = categoryKey,
            Label = ResolveEdgeLabel(link),
            AccentColor = ResolveEdgeAccentColor(categoryKey),
            DepthOffset = ResolveEdgeDepthOffset(categoryKey, isPrimaryPath),
            Emphasis = ResolveEdgeEmphasis(categoryKey, isPrimaryPath),
            Opacity = ResolveEdgeOpacity(categoryKey, isPrimaryPath),
            IsPrimaryPath = isPrimaryPath,
            IsUserAuthored = link.IsUserAuthored
        };
    }

    private static List<WebGlWorkbenchAnchor> BuildAnchors(CanvasWorkbenchNode node)
    {
        var anchors = new List<WebGlWorkbenchAnchor>();
        AppendAnchors(anchors, node, node.InputPorts, WebGlWorkbenchAnchorRoles.Input);
        AppendAnchors(anchors, node, node.OutputPorts, WebGlWorkbenchAnchorRoles.Output);
        return anchors;
    }

    private static void AppendAnchors(
        List<WebGlWorkbenchAnchor> anchors,
        CanvasWorkbenchNode node,
        IReadOnlyList<CanvasWorkbenchPort> ports,
        string role)
    {
        var portsBySide = ports
            .GroupBy(port => ResolveAnchorSide(port, role), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        foreach (var port in ports)
        {
            var side = ResolveAnchorSide(port, role);
            var siblings = portsBySide[side];
            anchors.Add(new WebGlWorkbenchAnchor
            {
                Id = BuildAnchorId(node.Id, port.Id),
                NodeId = node.Id,
                PortId = port.Id,
                Label = string.IsNullOrWhiteSpace(port.Label) ? port.Id : port.Label,
                Role = role,
                Side = side,
                CategoryKey = port.CategoryKey,
                AccentColor = string.IsNullOrWhiteSpace(port.AccentColor)
                    ? ResolveAccentColor(node)
                    : port.AccentColor,
                IsRequired = port.IsRequired,
                Order = siblings.IndexOf(port),
                TotalOnSide = siblings.Count
            });
        }
    }
    private static double NormalizeNodeSpacingFactor(double value)
        => Math.Round(Math.Clamp(double.IsFinite(value) ? value : 1d, 0.75d, 1.85d), 2, MidpointRounding.AwayFromZero);

    private static ProcessWebGlLayoutPosition ResolveNodeLayout(
        string nodeId,
        IReadOnlyDictionary<string, ProcessWebGlLayoutPosition> layout)
    {
        return layout.TryGetValue(nodeId, out var position)
            ? position
            : new ProcessWebGlLayoutPosition(0, 0, 0);
    }

    private static string ResolveDefinitionName(string templateKey)
    {
        return RepresentativeTemplates
            .FirstOrDefault(item => string.Equals(item.Key, templateKey, StringComparison.OrdinalIgnoreCase))
            ?.Key switch
        {
            "customer-onboarding" => "Customer onboarding concept",
            "architecture-decision-governance" => "Architecture decision governance concept",
            "branching-code-review" => "Branching code review concept",
            "software-delivery" => "Software delivery concept",
            "ai-assisted-change-delivery" => "AI-assisted change delivery concept",
            "release-readiness-and-deployment" => "Release readiness and deployment concept",
            _ => templateKey
        };
    }

    private static void StabilizeEditorIds(ProcessDefinitionEditorModel editor, string templateKey)
    {
        var roleIds = new Dictionary<Guid, Guid>();
        var stepIds = new Dictionary<Guid, Guid>();
        var branchOutcomeIds = new Dictionary<Guid, Guid>();
        var artifactIds = new Dictionary<Guid, Guid>();

        editor.Id = CreateStableGuid(templateKey, "definition");
        editor.WorkingVersionId = CreateStableGuid(templateKey, "working-version");
        editor.DefinitionConcurrencyToken = CreateStableGuid(templateKey, "definition-token");
        editor.WorkingVersionConcurrencyToken = CreateStableGuid(templateKey, "working-version-token");

        for (var index = 0; index < editor.Roles.Count; index++)
        {
            var role = editor.Roles[index];
            if (role.Id.HasValue)
            {
                roleIds[role.Id.Value] = CreateStableGuid(templateKey, "role", role.Key, index.ToString());
                role.Id = roleIds[role.Id.Value];
            }
            else
            {
                role.Id = CreateStableGuid(templateKey, "role", role.Key, index.ToString());
            }
        }

        for (var index = 0; index < editor.Steps.Count; index++)
        {
            var step = editor.Steps[index];
            if (step.Id.HasValue)
            {
                stepIds[step.Id.Value] = CreateStableGuid(templateKey, "step", step.Key, index.ToString());
                step.Id = stepIds[step.Id.Value];
            }
            else
            {
                step.Id = CreateStableGuid(templateKey, "step", step.Key, index.ToString());
            }

            for (var branchIndex = 0; branchIndex < step.BranchOutcomes.Count; branchIndex++)
            {
                var outcome = step.BranchOutcomes[branchIndex];
                var stableId = CreateStableGuid(templateKey, "branch-outcome", step.Key, outcome.Key, branchIndex.ToString());
                if (outcome.Id.HasValue)
                {
                    branchOutcomeIds[outcome.Id.Value] = stableId;
                }

                outcome.Id = stableId;
            }

            for (var artifactIndex = 0; artifactIndex < step.ArtifactExpectations.Count; artifactIndex++)
            {
                var artifact = step.ArtifactExpectations[artifactIndex];
                var stableId = CreateStableGuid(templateKey, "artifact", step.Key, artifact.Title, artifactIndex.ToString());
                if (artifact.Id.HasValue)
                {
                    artifactIds[artifact.Id.Value] = stableId;
                }

                artifact.Id = stableId;
            }

            for (var assignmentIndex = 0; assignmentIndex < step.RoleAssignments.Count; assignmentIndex++)
            {
                step.RoleAssignments[assignmentIndex].Id = CreateStableGuid(templateKey, "assignment", step.Key, assignmentIndex.ToString());
            }

            for (var dependencyIndex = 0; dependencyIndex < step.Dependencies.Count; dependencyIndex++)
            {
                step.Dependencies[dependencyIndex].Id = CreateStableGuid(templateKey, "dependency", step.Key, dependencyIndex.ToString());
            }

            for (var artifactInputIndex = 0; artifactInputIndex < step.ArtifactInputs.Count; artifactInputIndex++)
            {
                step.ArtifactInputs[artifactInputIndex].Id = CreateStableGuid(templateKey, "artifact-input", step.Key, artifactInputIndex.ToString());
            }
        }

        foreach (var policy in editor.MessagingPolicies.Select((item, index) => (item, index)))
        {
            policy.item.Id = CreateStableGuid(templateKey, "messaging", policy.index.ToString());
            if (policy.item.SourceRoleRequirementId.HasValue &&
                roleIds.TryGetValue(policy.item.SourceRoleRequirementId.Value, out var stableSourceRoleId))
            {
                policy.item.SourceRoleRequirementId = stableSourceRoleId;
            }

            if (policy.item.TargetRoleRequirementId.HasValue &&
                roleIds.TryGetValue(policy.item.TargetRoleRequirementId.Value, out var stableTargetRoleId))
            {
                policy.item.TargetRoleRequirementId = stableTargetRoleId;
            }
        }

        foreach (var step in editor.Steps)
        {
            if (step.DecisionRoleRequirementId.HasValue &&
                roleIds.TryGetValue(step.DecisionRoleRequirementId.Value, out var stableDecisionRoleId))
            {
                step.DecisionRoleRequirementId = stableDecisionRoleId;
            }

            foreach (var dependency in step.Dependencies)
            {
                if (dependency.DependsOnStepId.HasValue &&
                    stepIds.TryGetValue(dependency.DependsOnStepId.Value, out var stableStepId))
                {
                    dependency.DependsOnStepId = stableStepId;
                }

                if (dependency.DependsOnBranchOutcomeId.HasValue &&
                    branchOutcomeIds.TryGetValue(dependency.DependsOnBranchOutcomeId.Value, out var stableOutcomeId))
                {
                    dependency.DependsOnBranchOutcomeId = stableOutcomeId;
                }
            }

            foreach (var assignment in step.RoleAssignments)
            {
                if (assignment.RoleRequirementId.HasValue &&
                    roleIds.TryGetValue(assignment.RoleRequirementId.Value, out var stableRoleId))
                {
                    assignment.RoleRequirementId = stableRoleId;
                }
            }

            foreach (var artifactInput in step.ArtifactInputs)
            {
                if (artifactInput.ArtifactExpectationId.HasValue &&
                    artifactIds.TryGetValue(artifactInput.ArtifactExpectationId.Value, out var stableArtifactId))
                {
                    artifactInput.ArtifactExpectationId = stableArtifactId;
                }
            }
        }
    }

    private static Guid CreateStableGuid(params string[] parts)
    {
        using var sha256 = SHA256.Create();
        var payload = string.Join("::", parts.Select(part => part?.Trim() ?? string.Empty));
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(payload));
        hash[6] = (byte)((hash[6] & 0x0f) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
        return new Guid(hash[..16]);
    }

    private static (double Width, double Height, double Depth) ResolveNodeSize(CanvasWorkbenchNode node)
    {
        if (IsRoleNode(node))
        {
            return (214, 118, 118);
        }

        if (IsBranchNode(node))
        {
            return (202, 112, 126);
        }

        var computedWidth = 228 + Math.Min(74, (node.Title?.Length ?? 0) * 2d);
        var computedHeight = 128 + Math.Min(28, (node.Chips.Count + node.FooterChips.Count) * 3d);
        return (computedWidth, computedHeight, 122);
    }

    private static bool IsRoleNode(CanvasWorkbenchNode node)
        => node.Kind.Contains("role", StringComparison.OrdinalIgnoreCase);

    private static bool IsBranchNode(CanvasWorkbenchNode node)
        => node.Kind.Contains("branch", StringComparison.OrdinalIgnoreCase);

    private static string ResolveFillColor(CanvasWorkbenchNode node)
    {
        if (IsRoleNode(node))
        {
            return "#261306";
        }

        if (IsBranchNode(node))
        {
            return "#241034";
        }

        return node.PaletteKey.ToLowerInvariant() switch
        {
            "sky" => "#062236",
            "amber" => "#2f1707",
            "violet" => "#3a1020",
            "mint" => "#08261d",
            "neutral" => "#240913",
            _ => "#1f172a"
        };
    }

    private static string ResolveBorderColor(CanvasWorkbenchNode node)
    {
        if (IsRoleNode(node))
        {
            return "#fb923c";
        }

        if (IsBranchNode(node))
        {
            return "#a855f7";
        }

        return node.PaletteKey.ToLowerInvariant() switch
        {
            "sky" => "#38bdf8",
            "amber" => "#f59e0b",
            "violet" => "#f43f5e",
            "mint" => "#34d399",
            _ => "#ef4444"
        };
    }

    private static string ResolveAccentColor(CanvasWorkbenchNode node)
    {
        if (IsRoleNode(node))
        {
            return "#f97316";
        }

        if (IsBranchNode(node))
        {
            return "#c084fc";
        }

        if (!string.IsNullOrWhiteSpace(node.AccentColor))
        {
            return node.AccentColor;
        }

        return node.PaletteKey.ToLowerInvariant() switch
        {
            "sky" => "#38bdf8",
            "amber" => "#f59e0b",
            "violet" => "#f43f5e",
            "mint" => "#34d399",
            _ => "#ef4444"
        };
    }

    private static string ResolveAnchorSide(CanvasWorkbenchPort port, string role)
    {
        if (!string.IsNullOrWhiteSpace(port.Side))
        {
            return port.Side.ToLowerInvariant();
        }

        var resolvedFromPort = CanvasWorkbenchAnchorPorts.ResolveSide(port.Id);
        if (!string.IsNullOrWhiteSpace(resolvedFromPort))
        {
            return resolvedFromPort;
        }

        return string.Equals(role, WebGlWorkbenchAnchorRoles.Output, StringComparison.Ordinal)
            ? "right"
            : "left";
    }

    private static string BuildAnchorId(string nodeId, string portId)
        => $"{nodeId}::{portId}";

    private static string BuildEdgeId(CanvasWorkbenchLink link)
        => string.Join(
            "::",
            link.SourceId,
            link.SourcePortId,
            link.TargetId,
            link.TargetPortId,
            string.IsNullOrWhiteSpace(link.Kind) ? "flow" : link.Kind);

    private static string ResolveCategoryKey(CanvasWorkbenchLink link)
    {
        return link.Kind.ToLowerInvariant() switch
        {
            "messaging" => ProcessCanvasCatalog.ConnectionCategories.Messaging,
            _ when link.SourcePortId.StartsWith(ProcessCanvasCatalog.DefinitionPorts.BranchOutcomeOutputPrefix, StringComparison.Ordinal)
                => ProcessCanvasCatalog.ConnectionCategories.BranchRoute,
            _ when link.SourcePortId.StartsWith(ProcessCanvasCatalog.DefinitionPorts.StepArtifactOutputPrefix, StringComparison.Ordinal)
                => ProcessCanvasCatalog.ConnectionCategories.Artifact,
            _ when ProcessCanvasCatalog.DefinitionPorts.TryGetRoleResponsibilityKind(link.SourcePortId, out var responsibilityKind)
                => ProcessCanvasCatalog.GetResponsibilityVisual(responsibilityKind).CategoryKey,
            _ when string.Equals(link.SourcePortId, ProcessCanvasBranching.RoleDecisionOutputPortId, StringComparison.Ordinal)
                => ProcessCanvasCatalog.ConnectionCategories.DecisionAuthority,
            _ => ProcessCanvasCatalog.ConnectionCategories.Structural
        };
    }

    private static bool IsPrimaryPath(CanvasWorkbenchLink link, string categoryKey)
    {
        return string.Equals(categoryKey, ProcessCanvasCatalog.ConnectionCategories.Structural, StringComparison.Ordinal) ||
            string.Equals(categoryKey, ProcessCanvasCatalog.ConnectionCategories.BranchRoute, StringComparison.Ordinal);
    }

    private static string ResolveEdgeAccentColor(string categoryKey)
    {
        return categoryKey switch
        {
            ProcessCanvasCatalog.ConnectionCategories.Messaging => "#0f766e",
            ProcessCanvasCatalog.ConnectionCategories.DecisionAuthority => "#8b5cf6",
            ProcessCanvasCatalog.ConnectionCategories.BranchRoute => "#7c3aed",
            ProcessCanvasCatalog.ConnectionCategories.Artifact => "#db2777",
            ProcessCanvasCatalog.ConnectionCategories.ResponsibilityResponsible => "#0ea5e9",
            ProcessCanvasCatalog.ConnectionCategories.ResponsibilityReviewer => "#6366f1",
            ProcessCanvasCatalog.ConnectionCategories.ResponsibilityApprover => "#16a34a",
            ProcessCanvasCatalog.ConnectionCategories.ResponsibilityBackup => "#d97706",
            _ => "#2563eb"
        };
    }

    private static string ResolveEdgeLabel(CanvasWorkbenchLink link)
    {
        if (link.SourcePortId.StartsWith(ProcessCanvasCatalog.DefinitionPorts.BranchOutcomeOutputPrefix, StringComparison.Ordinal))
        {
            return "Route";
        }

        if (link.SourcePortId.StartsWith(ProcessCanvasCatalog.DefinitionPorts.StepArtifactOutputPrefix, StringComparison.Ordinal))
        {
            return "Artifact";
        }

        if (ProcessCanvasCatalog.DefinitionPorts.TryGetRoleResponsibilityKind(link.SourcePortId, out var responsibilityKind))
        {
            return ProcessCanvasCatalog.DefinitionPorts.GetResponsibilityLabel(responsibilityKind);
        }

        if (string.Equals(link.SourcePortId, ProcessCanvasCatalog.DefinitionPorts.RoleMessagingOutput, StringComparison.Ordinal))
        {
            return "Messaging";
        }

        if (string.Equals(link.SourcePortId, ProcessCanvasBranching.RoleDecisionOutputPortId, StringComparison.Ordinal))
        {
            return "Decision";
        }

        return string.Empty;
    }

    private static double ResolveEdgeDepthOffset(string categoryKey, bool isPrimaryPath)
    {
        if (isPrimaryPath && string.Equals(categoryKey, ProcessCanvasCatalog.ConnectionCategories.Structural, StringComparison.Ordinal))
        {
            return 46d;
        }

        return categoryKey switch
        {
            ProcessCanvasCatalog.ConnectionCategories.BranchRoute => 38d,
            ProcessCanvasCatalog.ConnectionCategories.DecisionAuthority => 24,
            ProcessCanvasCatalog.ConnectionCategories.Messaging => 18,
            ProcessCanvasCatalog.ConnectionCategories.Artifact => 14,
            _ => 8
        };
    }

    private static double ResolveEdgeEmphasis(string categoryKey, bool isPrimaryPath)
    {
        if (isPrimaryPath)
        {
            return string.Equals(categoryKey, ProcessCanvasCatalog.ConnectionCategories.Structural, StringComparison.Ordinal)
                ? 1.92d
                : 1.58d;
        }

        return categoryKey switch
        {
            ProcessCanvasCatalog.ConnectionCategories.DecisionAuthority => 0.96d,
            ProcessCanvasCatalog.ConnectionCategories.Messaging => 0.88d,
            ProcessCanvasCatalog.ConnectionCategories.Artifact => 0.84d,
            _ => 0.74d
        };
    }

    private static double ResolveEdgeOpacity(string categoryKey, bool isPrimaryPath)
    {
        if (isPrimaryPath)
        {
            return 0.96d;
        }

        return categoryKey switch
        {
            ProcessCanvasCatalog.ConnectionCategories.DecisionAuthority => 0.78d,
            ProcessCanvasCatalog.ConnectionCategories.Messaging => 0.66d,
            ProcessCanvasCatalog.ConnectionCategories.Artifact => 0.62d,
            _ => 0.54d
        };
    }

    private static string ResolveCanvasPortId(string? anchorId, string? portId)
    {
        if (!string.IsNullOrWhiteSpace(portId))
        {
            return portId;
        }

        if (string.IsNullOrWhiteSpace(anchorId))
        {
            return string.Empty;
        }

        var separatorIndex = anchorId.IndexOf("::", StringComparison.Ordinal);
        return separatorIndex >= 0 && separatorIndex < anchorId.Length - 2
            ? anchorId[(separatorIndex + 2)..]
            : anchorId;
    }

    private static ProcessRoleEditorModel? ResolveDefinitionRoleByNodeId(
        ProcessDefinitionEditorModel editor,
        string nodeId)
    {
        if (!ProcessCanvasBranching.TryResolveDefinitionRoleToken(nodeId, out var roleToken))
        {
            return null;
        }

        if (Guid.TryParse(roleToken, out var roleId))
        {
            return editor.Roles.FirstOrDefault(role => role.Id == roleId);
        }

        return editor.Roles.FirstOrDefault(role =>
            string.Equals(role.Key, roleToken, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role.DisplayName.Replace(' ', '-'), roleToken, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryResolveDecisionAuthorityChange(
        ProcessDefinitionEditorModel editor,
        string sourceNodeId,
        string targetNodeId,
        string sourcePortId,
        string targetPortId,
        bool isDisconnect,
        out bool changed)
    {
        changed = false;
        if (!string.Equals(sourcePortId, ProcessCanvasBranching.RoleDecisionOutputPortId, StringComparison.Ordinal) ||
            !string.Equals(targetPortId, ProcessCanvasBranching.DecisionRoleInputPortId, StringComparison.Ordinal))
        {
            return false;
        }

        var sourceRole = ResolveDefinitionRoleByNodeId(editor, sourceNodeId);
        var targetStep = editor.Steps.FirstOrDefault(step =>
            string.Equals(ProcessCanvasBranching.BuildDefinitionBranchNodeId(step), targetNodeId, StringComparison.Ordinal) ||
            string.Equals(ProcessCanvasBranching.BuildDefinitionStepNodeId(step), targetNodeId, StringComparison.Ordinal));
        if (sourceRole?.Id is null || targetStep is null)
        {
            return true;
        }

        if (isDisconnect)
        {
            changed = targetStep.DecisionRoleRequirementId == sourceRole.Id;
            if (changed)
            {
                targetStep.DecisionRoleRequirementId = null;
            }

            return true;
        }

        changed = targetStep.DecisionRoleRequirementId != sourceRole.Id;
        targetStep.DecisionRoleRequirementId = sourceRole.Id;
        return true;
    }

    private static bool TryResolveResponsibilityChange(
        ProcessDefinitionEditorModel editor,
        string sourceNodeId,
        string targetNodeId,
        string sourcePortId,
        string targetPortId,
        bool isDisconnect,
        out bool changed)
    {
        changed = false;
        if (!ProcessCanvasCatalog.DefinitionPorts.TryGetRoleResponsibilityKind(sourcePortId, out var sourceResponsibilityKind) ||
            !ProcessCanvasCatalog.DefinitionPorts.TryGetStepResponsibilityKind(targetPortId, out var targetResponsibilityKind) ||
            sourceResponsibilityKind != targetResponsibilityKind)
        {
            return false;
        }

        var sourceRole = ResolveDefinitionRoleByNodeId(editor, sourceNodeId);
        var targetStep = editor.Steps.FirstOrDefault(step =>
            string.Equals(ProcessCanvasBranching.BuildDefinitionStepNodeId(step), targetNodeId, StringComparison.Ordinal));
        if (sourceRole?.Id is null || targetStep is null)
        {
            return true;
        }

        var existingAssignment = targetStep.RoleAssignments.FirstOrDefault(assignment =>
            assignment.RoleRequirementId == sourceRole.Id &&
            assignment.ResponsibilityKind == sourceResponsibilityKind);
        if (isDisconnect)
        {
            changed = existingAssignment is not null;
            if (changed)
            {
                targetStep.RoleAssignments = targetStep.RoleAssignments
                    .Where(assignment => assignment != existingAssignment)
                    .ToList();
            }

            return true;
        }

        if (existingAssignment is not null)
        {
            return true;
        }

        targetStep.RoleAssignments.Add(new ProcessStepRoleRequirementEditorModel
        {
            Id = Guid.NewGuid(),
            RoleRequirementId = sourceRole.Id,
            ResponsibilityKind = sourceResponsibilityKind,
            IsRequired = true,
            RebindPolicySummary = "Sandbox-authored WebGL responsibility link."
        });
        changed = true;
        return true;
    }

    private static bool TryResolveMessagingChange(
        ProcessDefinitionEditorModel editor,
        string sourceNodeId,
        string targetNodeId,
        string sourcePortId,
        string targetPortId,
        bool isDisconnect,
        out bool changed)
    {
        changed = false;
        if (!string.Equals(sourcePortId, ProcessCanvasCatalog.DefinitionPorts.RoleMessagingOutput, StringComparison.Ordinal) ||
            !string.Equals(targetPortId, ProcessCanvasCatalog.DefinitionPorts.RoleMessagingInput, StringComparison.Ordinal))
        {
            return false;
        }

        var sourceRole = ResolveDefinitionRoleByNodeId(editor, sourceNodeId);
        var targetRole = ResolveDefinitionRoleByNodeId(editor, targetNodeId);
        if (sourceRole?.Id is null || targetRole?.Id is null)
        {
            return true;
        }

        var existingPolicy = editor.MessagingPolicies.FirstOrDefault(policy =>
            policy.SourceRoleRequirementId == sourceRole.Id &&
            policy.TargetRoleRequirementId == targetRole.Id);
        if (isDisconnect)
        {
            changed = existingPolicy is not null;
            if (changed)
            {
                editor.MessagingPolicies = editor.MessagingPolicies
                    .Where(policy => policy != existingPolicy)
                    .ToList();
            }

            return true;
        }

        if (existingPolicy is not null)
        {
            return true;
        }

        editor.MessagingPolicies.Add(new ProcessRoleMessagingPolicyEditorModel
        {
            Id = Guid.NewGuid(),
            SourceRoleRequirementId = sourceRole.Id,
            TargetRoleRequirementId = targetRole.Id
        });
        changed = true;
        return true;
    }

    private static bool TryResolveArtifactChange(
        ProcessDefinitionEditorModel editor,
        string sourceNodeId,
        string targetNodeId,
        string sourcePortId,
        string targetPortId,
        bool isDisconnect,
        out bool changed)
    {
        changed = false;
        if (!sourcePortId.StartsWith(ProcessCanvasCatalog.DefinitionPorts.StepArtifactOutputPrefix, StringComparison.Ordinal) ||
            !string.Equals(targetPortId, ProcessCanvasCatalog.DefinitionPorts.StepArtifactInputs, StringComparison.Ordinal))
        {
            return false;
        }

        var sourceStep = editor.Steps.FirstOrDefault(step =>
            string.Equals(ProcessCanvasBranching.BuildDefinitionStepNodeId(step), sourceNodeId, StringComparison.Ordinal));
        var targetStep = editor.Steps.FirstOrDefault(step =>
            string.Equals(ProcessCanvasBranching.BuildDefinitionStepNodeId(step), targetNodeId, StringComparison.Ordinal));
        if (sourceStep?.Id is null || targetStep is null)
        {
            return true;
        }

        var sourceArtifact = sourceStep.ArtifactExpectations.FirstOrDefault(artifact =>
            string.Equals(ProcessCanvasCatalog.DefinitionPorts.BuildStepArtifactOutputPortId(artifact), sourcePortId, StringComparison.Ordinal));
        if (sourceArtifact?.Id is null)
        {
            return true;
        }

        var existingArtifactInput = targetStep.ArtifactInputs.FirstOrDefault(input => input.ArtifactExpectationId == sourceArtifact.Id);
        if (isDisconnect)
        {
            changed = existingArtifactInput is not null;
            if (changed)
            {
                targetStep.ArtifactInputs = targetStep.ArtifactInputs
                    .Where(input => input != existingArtifactInput)
                    .ToList();
            }

            return true;
        }

        if (existingArtifactInput is null)
        {
            targetStep.ArtifactInputs.Add(new ProcessStepArtifactInputEditorModel
            {
                Id = Guid.NewGuid(),
                ArtifactExpectationId = sourceArtifact.Id
            });
            changed = true;
        }

        changed = UpsertDependency(targetStep, sourceStep.Id.Value, null) || changed;
        return true;
    }

    private static bool TryResolveDependencyChange(
        ProcessDefinitionEditorModel editor,
        string sourceNodeId,
        string targetNodeId,
        string sourcePortId,
        string targetPortId,
        bool isDisconnect,
        out bool changed)
    {
        changed = false;

        var sourceStep = editor.Steps.FirstOrDefault(step =>
            string.Equals(ProcessCanvasBranching.BuildDefinitionStepNodeId(step), sourceNodeId, StringComparison.Ordinal) ||
            string.Equals(ProcessCanvasBranching.BuildDefinitionBranchNodeId(step), sourceNodeId, StringComparison.Ordinal));
        var targetStep = editor.Steps.FirstOrDefault(step =>
            string.Equals(ProcessCanvasBranching.BuildDefinitionStepNodeId(step), targetNodeId, StringComparison.Ordinal));
        if (sourceStep?.Id is null || targetStep is null)
        {
            return false;
        }

        Guid? branchOutcomeId = null;
        var isBranchDependency = sourcePortId.StartsWith(ProcessCanvasCatalog.DefinitionPorts.BranchOutcomeOutputPrefix, StringComparison.Ordinal);
        if (isBranchDependency)
        {
            branchOutcomeId = sourceStep.BranchOutcomes
                .FirstOrDefault(outcome =>
                    string.Equals(ProcessCanvasBranching.BuildOutcomePortId(outcome), sourcePortId, StringComparison.Ordinal))
                ?.Id;
        }

        var isStructural = ProcessCanvasCatalog.DefinitionPorts.IsStepStructuralOutputPortId(sourcePortId) &&
            ProcessCanvasCatalog.DefinitionPorts.IsStepStructuralInputPortId(targetPortId);
        if (!isStructural && !isBranchDependency)
        {
            return false;
        }

        if (isDisconnect)
        {
            changed = RemoveDependency(targetStep, sourceStep.Id.Value, branchOutcomeId);
            return true;
        }

        changed = UpsertDependency(targetStep, sourceStep.Id.Value, branchOutcomeId);
        return true;
    }

    private static bool UpsertDependency(ProcessStepEditorModel targetStep, Guid sourceStepId, Guid? branchOutcomeId)
    {
        var dependencies = ProcessCanvasBranching.GetOrderedDependencies(targetStep).ToList();
        if (dependencies.Any(dependency =>
                dependency.DependsOnStepId == sourceStepId &&
                dependency.DependsOnBranchOutcomeId == branchOutcomeId))
        {
            return false;
        }

        dependencies.Add(ProcessStepDependencyCollection.CreateEditorDependency(sourceStepId, branchOutcomeId));
        ProcessStepDependencyCollection.SetEditorDependencies(targetStep, dependencies);
        return true;
    }

    private static bool RemoveDependency(ProcessStepEditorModel targetStep, Guid sourceStepId, Guid? branchOutcomeId)
    {
        var dependencies = ProcessCanvasBranching.GetOrderedDependencies(targetStep).ToList();
        var removed = dependencies.RemoveAll(dependency =>
            dependency.DependsOnStepId == sourceStepId &&
            dependency.DependsOnBranchOutcomeId == branchOutcomeId);
        if (removed == 0)
        {
            return false;
        }

        ProcessStepDependencyCollection.SetEditorDependencies(targetStep, dependencies);
        return true;
    }

    private sealed record RepresentativeTemplateDefinition(
        string Key,
        string Complexity,
        string Summary);
}

public sealed record ProcessWebGlTemplateDescriptor(
    string Key,
    string DisplayName,
    string Complexity,
    string Summary,
    int StepCount);

public sealed record ProcessWebGlSceneOptions(
    string TemplateKey,
    string ProjectionMode,
    string CameraViewMode,
    string ViewPreset,
    string? SelectedNodeId,
    string? LayoutMode = null,
    double NodeSpacingFactor = 1,
    WebGlWorkbenchCameraState? CameraState = null,
    bool DeterministicMode = true,
    bool ShowDiagnostics = false);
