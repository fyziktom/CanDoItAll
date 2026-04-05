using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private sealed class ProjectStructurePartyEditorState
    {
        public Guid? SelectedPartyId { get; set; }

        public HashSet<Guid> SelectedMeetingPartyIds { get; } = [];

        public bool KeepProjectLocalOnly { get; set; }

        public ProjectPartyQuickCreateRequest QuickCreate { get; set; } = new();

        public string Message { get; set; } = string.Empty;

        public string MessageTone { get; set; } = "neutral";
    }

    private IReadOnlyList<ProjectPartyOption> partyEditorOptions = [];
    private IReadOnlyList<ProjectPartyAssignmentDetail> projectPartyAssignments = [];
    private ProjectStructurePartyEditorState partyEditor = new();
    private bool isPartyEditorBusy;
    private bool isPartyEditorLoading;

    [Inject]
    private IProjectNodeAssignmentPolicyBridge NodeAssignmentPolicyBridge { get; set; } = default!;

    private bool CanShowPartyEditor => selectedNode is not null && ResolveNodeAssignmentSemantics(selectedNode).ReplacementRoles.Count > 0;

    private bool IsParticipantPartyEditor => selectedNode?.ObjectType == ProjectObjectType.Participant;

    private bool IsMeetingPartyEditor => selectedNode?.ObjectType == ProjectObjectType.Meeting;

    private bool IsWorkItemPartyEditor => selectedNode?.ObjectType == ProjectObjectType.WorkItem;

    private string PartyEditorTitle => selectedNode?.ObjectType switch
    {
        ProjectObjectType.Participant => "Participant directory sync",
        ProjectObjectType.Meeting => "Meeting party assignments",
        ProjectObjectType.WorkItem => "Work-item party assignment",
        _ => "Party integration"
    };

    private string PartyEditorDescription => selectedNode?.ObjectType switch
    {
        ProjectObjectType.Participant => "Link this participant to an existing party, create a new one, or keep the node intentionally local to the project.",
        ProjectObjectType.Meeting => "Meetings can reference real customer, partner, team, and AI-agent parties without removing the local participant graph.",
        ProjectObjectType.WorkItem => "Assign a real party so delivery ownership is reusable across project, CRM, and HR views.",
        _ => "Keep project and directory identity aligned."
    };

    private string PartyEditorStatus
    {
        get
        {
            if (IsMeetingPartyEditor)
            {
                return partyEditor.SelectedMeetingPartyIds.Count == 0
                    ? "No parties selected"
                    : "Parties selected";
            }

            if (IsParticipantPartyEditor)
            {
                return partyEditor.KeepProjectLocalOnly || !partyEditor.SelectedPartyId.HasValue
                    ? "Project local"
                    : "Directory linked";
            }

            return partyEditor.SelectedPartyId.HasValue
                ? "Directory linked"
                : "No central assignee";
        }
    }

    private string PartyEditorTone => PartyEditorStatus switch
    {
        "Directory linked" => "mint",
        "Parties selected" => "mint",
        "No central assignee" => "warn",
        "No parties selected" => "warn",
        _ => "neutral"
    };

    private string PartyEditorLinkedPartyName
    {
        get
        {
            if (IsMeetingPartyEditor)
            {
                return string.Join(", ",
                    partyEditorOptions
                        .Where(option => partyEditor.SelectedMeetingPartyIds.Contains(option.PartyId))
                        .Select(option => option.DisplayName)
                        .Take(3));
            }

            return partyEditor.SelectedPartyId.HasValue
                ? partyEditorOptions.FirstOrDefault(option => option.PartyId == partyEditor.SelectedPartyId.Value)?.DisplayName ?? string.Empty
                : string.Empty;
        }
    }

    private string PartyEditorLead => selectedNode is null
        ? string.Empty
        : $"{selectedNode.Title} stays on the project canvas while its reusable party identity is managed here.";

    private string ParticipantPartyValue
    {
        get => partyEditor.SelectedPartyId?.ToString("D") ?? string.Empty;
        set
        {
            partyEditor.SelectedPartyId = Guid.TryParse(value, out var partyId) ? partyId : null;
            if (partyEditor.SelectedPartyId.HasValue)
            {
                partyEditor.KeepProjectLocalOnly = false;
            }
        }
    }

    private string WorkItemPartyValue
    {
        get => partyEditor.SelectedPartyId?.ToString("D") ?? string.Empty;
        set => partyEditor.SelectedPartyId = Guid.TryParse(value, out var partyId) ? partyId : null;
    }

    private async Task LoadPartyEditorAsync()
    {
        if (!CanShowPartyEditor || selectedNode is null)
        {
            isPartyEditorLoading = false;
            ResetPartyEditor();
            await InvokeAsync(StateHasChanged);
            return;
        }

        isPartyEditorLoading = true;
        ResetPartyEditor();
        await InvokeAsync(StateHasChanged);

        try
        {
            partyEditorOptions = await ProjectPartyIntegrationBridge.ListPartyOptionsAsync(ProjectId);
            projectPartyAssignments = await ProjectPartyIntegrationBridge.ListAssignmentsDetailedAsync(ProjectId);
            partyEditor = new ProjectStructurePartyEditorState
            {
                QuickCreate = new ProjectPartyQuickCreateRequest
                {
                    ProjectId = ProjectId,
                    PartyKind = ProjectPartyQuickCreateKind.Person
                }
            };

            switch (selectedNode.ObjectType)
            {
                case ProjectObjectType.Participant:
                    partyEditor.SelectedPartyId = ResolvePrimaryNodeAssignment(selectedNode.Id, ResolveNodeAssignmentRoles(selectedNode))?.PartyId;
                    partyEditor.KeepProjectLocalOnly = !partyEditor.SelectedPartyId.HasValue;
                    break;
                case ProjectObjectType.Meeting:
                    foreach (var item in ResolveNodeAssignments(selectedNode.Id, ResolveNodeAssignmentRoles(selectedNode)))
                    {
                        partyEditor.SelectedMeetingPartyIds.Add(item.PartyId);
                    }
                    break;
                case ProjectObjectType.WorkItem:
                    partyEditor.SelectedPartyId = ResolvePrimaryNodeAssignment(selectedNode.Id, ResolveNodeAssignmentRoles(selectedNode))?.PartyId;
                    break;
            }
        }
        finally
        {
            isPartyEditorLoading = false;
        }
    }

    private async Task CreateParticipantPartyAsync()
    {
        isPartyEditorBusy = true;
        try
        {
            var result = await ProjectPartyIntegrationBridge.CreatePartyAsync(partyEditor.QuickCreate);
            if (!result.IsSuccess)
            {
                SetPartyEditorMessage(result.Errors.FirstOrDefault()?.Message ?? "Unable to create the party.", "danger");
                return;
            }

            var createdParty = result.Value
                ?? throw new InvalidOperationException("Party creation succeeded without a created party result.");
            partyEditorOptions = await ProjectPartyIntegrationBridge.ListPartyOptionsAsync(ProjectId);
            partyEditor.SelectedPartyId = createdParty.PartyId;
            partyEditor.KeepProjectLocalOnly = false;
            partyEditor.QuickCreate = new ProjectPartyQuickCreateRequest
            {
                ProjectId = ProjectId,
                PartyKind = ProjectPartyQuickCreateKind.Person
            };
            SetPartyEditorMessage("Directory party created from the participant flow.", "mint");
        }
        finally
        {
            isPartyEditorBusy = false;
        }
    }

    private Task HandleMeetingPartyChanged(Guid partyId, ChangeEventArgs args)
    {
        var isChecked = args.Value is bool booleanValue
            ? booleanValue
            : string.Equals(args.Value?.ToString(), "true", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(args.Value?.ToString(), "on", StringComparison.OrdinalIgnoreCase);
        if (isChecked)
        {
            partyEditor.SelectedMeetingPartyIds.Add(partyId);
        }
        else
        {
            partyEditor.SelectedMeetingPartyIds.Remove(partyId);
        }

        return Task.CompletedTask;
    }

    private Task ApplyMeetingProjectDefaultsAsync()
    {
        partyEditor.SelectedMeetingPartyIds.Clear();
        foreach (var assignment in projectPartyAssignments.Where(item => string.IsNullOrWhiteSpace(item.NodeKey)))
        {
            partyEditor.SelectedMeetingPartyIds.Add(assignment.PartyId);
        }

        SetPartyEditorMessage("Project-level parties copied into the meeting editor.", "mint");
        return Task.CompletedTask;
    }

    private async Task SaveParticipantPartyAsync()
    {
        if (selectedNode is null)
        {
            return;
        }

        isPartyEditorBusy = true;
        try
        {
            var metadata = ProjectObjectMetadataSerializer.Parse(selectedNode.MetadataJson);
            metadata.Participant ??= new ProjectParticipantMetadata();
            if (partyEditor.KeepProjectLocalOnly || !partyEditor.SelectedPartyId.HasValue)
            {
                if (!await ReplaceNodeAssignmentsAsync(selectedNode.Id, [], ResolveNodeAssignmentRoles(selectedNode)))
                {
                    return;
                }

                metadata.Participant.LinkedPartyDisplayName = string.Empty;
                await SaveNodeMetadataAsync(selectedNode, metadata);
                SetPartyEditorMessage("Participant kept project-local only.", "neutral");
                return;
            }

            var option = await ProjectPartyIntegrationBridge.GetPartyOptionAsync(partyEditor.SelectedPartyId.Value);
            if (option is null)
            {
                SetPartyEditorMessage("The selected party could not be loaded.", "danger");
                return;
            }

            metadata.Participant.LinkedPartyDisplayName = option.DisplayName;
            metadata.Participant.Email = option.PrimaryEmail;
            metadata.Participant.Phone = option.PrimaryPhone;
            if (string.IsNullOrWhiteSpace(metadata.Participant.Organization))
            {
                metadata.Participant.Organization = option.PartyTypeLabel;
            }

            if (!await ReplaceNodeAssignmentsAsync(
                    selectedNode.Id,
                    [
                        new ProjectPartyAssignmentUpsertRequest
                        {
                            ProjectId = ProjectId,
                            PartyId = option.PartyId,
                            Role = ResolvePreferredNodeAssignmentRole(selectedNode),
                            NodeKey = selectedNode.Id,
                            IsPrimary = true,
                            Source = "project-structure"
                        }
                    ],
                    ResolveNodeAssignmentRoles(selectedNode)))
            {
                return;
            }

            var updatedNode = await ProjectWorkbenchService.UpdateObjectAsync(
                ProjectId,
                selectedNode.Id,
                new ProjectObjectEditRequest(
                    option.DisplayName,
                    selectedNode.Subtitle,
                    selectedNode.Notes,
                    selectedNode.StartUtc,
                    selectedNode.EndUtc,
                    ProjectObjectMetadataSerializer.Serialize(metadata)));
            if (updatedNode is not null)
            {
                await ApplySurfaceNodeUpdatesAsync([updatedNode]);
            }

            SetPartyEditorMessage("Participant linked to the directory.", "mint");
        }
        finally
        {
            isPartyEditorBusy = false;
        }
    }

    private async Task SaveMeetingPartiesAsync()
    {
        if (selectedNode is null)
        {
            return;
        }

        isPartyEditorBusy = true;
        try
        {
            var metadata = ProjectObjectMetadataSerializer.Parse(selectedNode.MetadataJson);
            metadata.Meeting ??= new ProjectMeetingMetadata();
            var assignmentRoles = ResolveNodeAssignmentRoles(selectedNode);
            var preferredRole = ResolvePreferredNodeAssignmentRole(selectedNode);
            var selectedOptions = partyEditorOptions
                .Where(option => partyEditor.SelectedMeetingPartyIds.Contains(option.PartyId))
                .ToList();
            if (!await ReplaceNodeAssignmentsAsync(
                    selectedNode.Id,
                    selectedOptions.Select((option, index) => new ProjectPartyAssignmentUpsertRequest
                    {
                        ProjectId = ProjectId,
                        PartyId = option.PartyId,
                        Role = preferredRole,
                        NodeKey = selectedNode.Id,
                        IsPrimary = index == 0,
                        Source = "project-structure"
                    }).ToList(),
                    assignmentRoles))
            {
                return;
            }

            metadata.Meeting.RelatedPartySummary = string.Join(", ", selectedOptions.Select(option => option.DisplayName));
            await SaveNodeMetadataAsync(selectedNode, metadata);
            SetPartyEditorMessage("Meeting parties saved.", "mint");
        }
        finally
        {
            isPartyEditorBusy = false;
        }
    }

    private async Task SaveWorkItemPartyAsync()
    {
        if (selectedNode is null)
        {
            return;
        }

        isPartyEditorBusy = true;
        try
        {
            var metadata = ProjectObjectMetadataSerializer.Parse(selectedNode.MetadataJson);
            metadata.WorkItem ??= new ProjectWorkItemMetadata();
            var assignmentRoles = ResolveNodeAssignmentRoles(selectedNode);
            var preferredRole = ResolvePreferredNodeAssignmentRole(selectedNode);
            if (!partyEditor.SelectedPartyId.HasValue)
            {
                if (!await ReplaceNodeAssignmentsAsync(selectedNode.Id, [], assignmentRoles))
                {
                    return;
                }

                metadata.WorkItem.AssigneePartyDisplayName = string.Empty;
                await SaveNodeMetadataAsync(selectedNode, metadata);
                SetPartyEditorMessage("Central work-item assignee cleared.", "neutral");
                return;
            }

            var option = await ProjectPartyIntegrationBridge.GetPartyOptionAsync(partyEditor.SelectedPartyId.Value);
            if (option is null)
            {
                SetPartyEditorMessage("The selected assignee could not be loaded.", "danger");
                return;
            }

            if (!await ReplaceNodeAssignmentsAsync(
                    selectedNode.Id,
                    [
                        new ProjectPartyAssignmentUpsertRequest
                        {
                            ProjectId = ProjectId,
                            PartyId = option.PartyId,
                            Role = preferredRole,
                            NodeKey = selectedNode.Id,
                            IsPrimary = true,
                            Source = "project-structure"
                        }
                    ],
                    assignmentRoles))
            {
                return;
            }

            metadata.WorkItem.AssigneePartyDisplayName = option.DisplayName;
            await SaveNodeMetadataAsync(selectedNode, metadata);
            SetPartyEditorMessage("Work-item assignee saved.", "mint");
        }
        finally
        {
            isPartyEditorBusy = false;
        }
    }

    private async Task SaveNodeMetadataAsync(ProjectStructureNode node, ProjectObjectMetadataEnvelope metadata)
    {
        var updatedNode = await ProjectWorkbenchService.UpdateObjectMetadataAsync(
            ProjectId,
            node.Id,
            ProjectObjectMetadataSerializer.Serialize(metadata),
            node.Notes);
        if (updatedNode is not null)
        {
            await ApplySurfaceNodeUpdatesAsync([updatedNode]);
        }
    }

    private async Task<bool> ReplaceNodeAssignmentsAsync(
        string nodeKey,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
        IReadOnlyList<ProjectPartyAssignmentRole> targetRoles)
    {
        var result = await ProjectPartyIntegrationBridge.ReplaceNodeAssignmentsAsync(
            ProjectId,
            new ProjectNodeReference(nodeKey),
            desiredAssignments,
            targetRoles);
        if (result.IsFailure)
        {
            SetPartyEditorMessage(
                result.Errors.FirstOrDefault()?.Message ?? "Unable to save the canonical party assignments.",
                "danger");
            return false;
        }

        projectPartyAssignments = await ProjectPartyIntegrationBridge.ListAssignmentsDetailedAsync(ProjectId);
        return true;
    }

    private IReadOnlyList<ProjectPartyAssignmentDetail> ResolveNodeAssignments(
        string nodeKey,
        IReadOnlyList<ProjectPartyAssignmentRole> roles)
    {
        return projectPartyAssignments
            .Where(item =>
                string.Equals(item.NodeKey, nodeKey, StringComparison.Ordinal) &&
                roles.Contains(item.Role))
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.PartyDisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private ProjectPartyAssignmentDetail? ResolvePrimaryNodeAssignment(
        string nodeKey,
        IReadOnlyList<ProjectPartyAssignmentRole> roles)
    {
        return ResolveNodeAssignments(nodeKey, roles).FirstOrDefault();
    }

    private ProjectNodeAssignmentSemantics ResolveNodeAssignmentSemantics(ProjectStructureNode node)
    {
        return NodeAssignmentPolicyBridge.Resolve(node.ObjectType, node.ObjectSubtype);
    }

    private IReadOnlyList<ProjectPartyAssignmentRole> ResolveNodeAssignmentRoles(ProjectStructureNode node)
    {
        return ResolveNodeAssignmentSemantics(node).ReplacementRoles;
    }

    private ProjectPartyAssignmentRole ResolvePreferredNodeAssignmentRole(ProjectStructureNode node)
    {
        return ResolveNodeAssignmentSemantics(node).PreferredRole ?? ProjectPartyAssignmentRole.TeamMember;
    }

    private void ResetPartyEditor()
    {
        partyEditorOptions = [];
        projectPartyAssignments = [];
        partyEditor = new ProjectStructurePartyEditorState
        {
            QuickCreate = new ProjectPartyQuickCreateRequest
            {
                ProjectId = ProjectId
            }
        };
    }

    private void SetPartyEditorMessage(string message, string tone)
    {
        partyEditor.Message = message;
        partyEditor.MessageTone = tone;
    }

    private static string ResolveQuickCreateKindLabel(ProjectPartyQuickCreateKind kind)
    {
        return kind switch
        {
            ProjectPartyQuickCreateKind.OrganizationUnit => "Organization unit",
            ProjectPartyQuickCreateKind.AiAgent => "AI agent",
            _ => kind.ToString()
        };
    }
}
