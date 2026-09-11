using CanDoItAll.AppComponents;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private sealed class ProjectStructurePartyEditorState
    {
        public ProjectWriteAdmission? ExpectedProjectAdmission { get; init; }

        public ProjectStructureNode? OpenedNode { get; init; }

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

    private bool CanShowPartyEditor =>
        selectedNode is { ObjectType: ProjectObjectType.Participant or ProjectObjectType.Meeting } &&
        ResolveNodeAssignmentSemantics(selectedNode).ReplacementRoles.Count > 0;

    private bool IsParticipantPartyEditor => selectedNode?.ObjectType == ProjectObjectType.Participant;

    private bool IsMeetingPartyEditor => selectedNode?.ObjectType == ProjectObjectType.Meeting;

    private string PartyEditorTitle => selectedNode?.ObjectType switch
    {
        ProjectObjectType.Participant => "Participant directory sync",
        ProjectObjectType.Meeting => "Meeting party assignments",
        _ => "Party integration"
    };

    private string PartyEditorDescription => selectedNode?.ObjectType switch
    {
        ProjectObjectType.Participant => "Link this participant to an existing party, create a new one, or keep the node intentionally local to the project.",
        ProjectObjectType.Meeting => "Meetings can reference real customer, partner, team, and AI-agent parties without removing the local participant graph.",
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

            return "Party integration";
        }
    }

    private string PartyEditorTone => PartyEditorStatus switch
    {
        "Directory linked" => "mint",
        "Parties selected" => "mint",
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

    private IReadOnlyList<ResourceCardPickerOption<Guid>> ParticipantPartyPickerOptions
        => BuildPartyPickerOptions("project-structure-participant-party-option");

    private Task HandleParticipantPartySelectedAsync(Guid partyId)
    {
        partyEditor.SelectedPartyId = partyId;
        partyEditor.KeepProjectLocalOnly = false;
        return Task.CompletedTask;
    }

    private Task ClearPartySelectionAsync()
    {
        partyEditor.SelectedPartyId = null;
        return Task.CompletedTask;
    }

    private IReadOnlyList<ResourceCardPickerOption<Guid>> BuildPartyPickerOptions(
        string testIdPrefix,
        Func<ProjectPartyOption, bool>? predicate = null)
    {
        return partyEditorOptions
            .Where(option => predicate?.Invoke(option) ?? true)
            .Select(option => new ResourceCardPickerOption<Guid>(
                option.PartyId,
                option.DisplayName,
                option.PartyTypeLabel)
            {
                Subtitle = ResolvePartyPickerSubtitle(option),
                Description = option.IsSensitive ? "Sensitive directory record" : string.Empty,
                Meta = ResolvePartyPickerMeta(option),
                Icon = ResolvePartyPickerIcon(option.PartyType),
                VisualKind = UsesPartyAvatar(option.PartyType)
                    ? ResourceCardPickerVisualKind.Avatar
                    : ResourceCardPickerVisualKind.Icon,
                AdditionalSearchText = option.IsSensitive
                    ? option.PartyTypeLabel
                    : string.Join(' ', option.PrimaryEmail, option.PrimaryPhone, option.PartyTypeLabel),
                IsSelected = partyEditor.SelectedPartyId == option.PartyId,
                TestId = $"{testIdPrefix}-{option.PartyId:N}"
            })
            .ToList();
    }

    private static string ResolvePartyPickerSubtitle(ProjectPartyOption option)
    {
        if (option.IsSensitive)
        {
            return "Contact details hidden";
        }

        return !string.IsNullOrWhiteSpace(option.PrimaryEmail)
            ? option.PrimaryEmail
            : option.PrimaryPhone;
    }

    private static string ResolvePartyPickerMeta(ProjectPartyOption option)
    {
        if (option.IsSensitive || string.IsNullOrWhiteSpace(option.PrimaryEmail) || string.IsNullOrWhiteSpace(option.PrimaryPhone))
        {
            return string.Empty;
        }

        return option.PrimaryPhone;
    }

    private static bool UsesPartyAvatar(ProjectPartyType partyType)
    {
        return partyType is ProjectPartyType.Person or ProjectPartyType.AiAgent;
    }

    private static string ResolvePartyPickerIcon(ProjectPartyType partyType)
    {
        return partyType switch
        {
            ProjectPartyType.OrganizationUnit => "account_tree",
            ProjectPartyType.Organization => "business",
            ProjectPartyType.AiAgent => "smart_toy",
            _ => "person"
        };
    }

    private async Task LoadPartyEditorAsync()
    {
        if (!CanShowPartyEditor || selectedNode is null)
        {
            isPartyEditorLoading = false;
            ResetPartyEditor();
            return;
        }

        var openedNode = selectedNode;
        var expected = surface?.ExpectedProjectAdmission
            ?? throw new InvalidOperationException("Reload the project before opening its party editor.");
        isPartyEditorLoading = true;
        ResetPartyEditor();
        await InvokeAsync(StateHasChanged);

        try
        {
            partyEditorOptions = await ProjectPartyIntegrationBridge.ListPartyOptionsAsync(expected.ProjectId);
            projectPartyAssignments = await ProjectPartyIntegrationBridge.ListAssignmentsDetailedAsync(expected.ProjectId);
            if (selectedNode?.Id != openedNode.Id || surface?.ExpectedProjectAdmission != expected) {
                return;
            }
            partyEditor = new ProjectStructurePartyEditorState
            {
                ExpectedProjectAdmission = expected,
                OpenedNode = openedNode,
                QuickCreate = new ProjectPartyQuickCreateRequest
                {
                    ProjectId = ProjectId,
                    PartyKind = ProjectPartyQuickCreateKind.Person
                }
            };

            switch (openedNode.ObjectType)
            {
                case ProjectObjectType.Participant:
                    partyEditor.SelectedPartyId = ResolvePrimaryNodeAssignment(openedNode.Id, GetNodeAssignmentRoles(openedNode))?.PartyId;
                    partyEditor.KeepProjectLocalOnly = !partyEditor.SelectedPartyId.HasValue;
                    break;
                case ProjectObjectType.Meeting:
                    foreach (var item in ResolveNodeAssignments(openedNode.Id, GetNodeAssignmentRoles(openedNode)))
                    {
                        partyEditor.SelectedMeetingPartyIds.Add(item.PartyId);
                    }
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
        var edit = partyEditor;
        var node = edit.OpenedNode;
        var expected = edit.ExpectedProjectAdmission;
        if (node is null || expected is null) {
            return;
        }

        isPartyEditorBusy = true;
        try
        {
            var metadata = ProjectObjectMetadataSerializer.Parse(node.MetadataJson);
            metadata.Participant ??= new ProjectParticipantMetadata();
            if (edit.KeepProjectLocalOnly || !edit.SelectedPartyId.HasValue)
            {
                if (!await ReplaceNodeAssignmentsAsync(expected, node.Id, [], GetNodeAssignmentRoles(node)))
                {
                    return;
                }

                metadata.Participant.LinkedPartyDisplayName = string.Empty;
                await SaveNodeMetadataAsync(expected, node, metadata);
                SetPartyEditorMessage("Participant kept project-local only.", "neutral");
                return;
            }

            var option = await ProjectPartyIntegrationBridge.GetPartyOptionAsync(edit.SelectedPartyId.Value);
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
                metadata.Participant.Organization =
                    option.Affiliation?.OrganizationName ??
                    option.PartyTypeLabel;
            }

            if (!await ReplaceNodeAssignmentsAsync(
                    expected, node.Id,
                    [
                        new ProjectPartyAssignmentUpsertRequest
                        {
                            ProjectId = expected.ProjectId,
                            ExpectedProjectAdmission = expected,
                            PartyId = option.PartyId,
                            PartyAffiliationId =
                                option.Affiliation?.AffiliationId,
                            Role = GetPreferredNodeAssignmentRole(node),
                            NodeKey = node.Id,
                            IsPrimary = true,
                            Source = "project-structure"
                        }
                    ],
                    GetNodeAssignmentRoles(node)))
            {
                return;
            }

            var updatedNode = await ProjectWorkbenchService.UpdateObjectAsync(
                expected.ProjectId,
                node.Id,
                new ProjectObjectEditRequest(
                    option.DisplayName,
                    node.Subtitle,
                    node.Notes,
                    node.StartUtc,
                    node.EndUtc,
                    ProjectObjectMetadataSerializer.Serialize(metadata)) { ExpectedProjectAdmission = expected });
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
        var edit = partyEditor;
        var node = edit.OpenedNode;
        var expected = edit.ExpectedProjectAdmission;
        if (node is null || expected is null) {
            return;
        }

        isPartyEditorBusy = true;
        try
        {
            var metadata = ProjectObjectMetadataSerializer.Parse(node.MetadataJson);
            metadata.Meeting ??= new ProjectMeetingMetadata();
            var assignmentRoles = GetNodeAssignmentRoles(node);
            var preferredRole = GetPreferredNodeAssignmentRole(node);
            var selectedOptions = partyEditorOptions
                .Where(option => edit.SelectedMeetingPartyIds.Contains(option.PartyId))
                .ToList();
            if (!await ReplaceNodeAssignmentsAsync(
                    expected, node.Id,
                    selectedOptions.Select((option, index) => new ProjectPartyAssignmentUpsertRequest
                    {
                        ProjectId = expected.ProjectId,
                            ExpectedProjectAdmission = expected,
                        PartyId = option.PartyId,
                        PartyAffiliationId =
                            option.Affiliation?.AffiliationId,
                        Role = preferredRole,
                        NodeKey = node.Id,
                        IsPrimary = index == 0,
                        Source = "project-structure"
                    }).ToList(),
                    assignmentRoles))
            {
                return;
            }

            metadata.Meeting.RelatedPartySummary = string.Join(", ", selectedOptions.Select(option => option.DisplayName));
            await SaveNodeMetadataAsync(expected, node, metadata);
            SetPartyEditorMessage("Meeting parties saved.", "mint");
        }
        finally
        {
            isPartyEditorBusy = false;
        }
    }

    private async Task SaveNodeMetadataAsync(ProjectWriteAdmission expected, ProjectStructureNode node, ProjectObjectMetadataEnvelope metadata)
    {
        var updatedNode = await ProjectWorkbenchService.UpdateObjectMetadataAsync(
            expected.ProjectId,
            node.Id,
            ProjectObjectMetadataSerializer.Serialize(metadata),
            node.Notes, expectedProjectAdmission: expected);
        if (updatedNode is not null)
        {
            await ApplySurfaceNodeUpdatesAsync([updatedNode]);
        }
    }

    private async Task<bool> ReplaceNodeAssignmentsAsync(
        ProjectWriteAdmission expected, string nodeKey,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
        IReadOnlyList<ProjectPartyAssignmentRole> targetRoles)
    {
        var result = await ProjectPartyIntegrationBridge.ReplaceNodeAssignmentsAsync(
            expected.ProjectId,
            new ProjectNodeReference(nodeKey),
            desiredAssignments,
            targetRoles, expectedProjectAdmission: expected);
        if (result.IsFailure)
        {
            SetPartyEditorMessage(
                result.Errors.FirstOrDefault()?.Message ?? "Unable to save the canonical party assignments.",
                "danger");
            return false;
        }

        if (surface?.ExpectedProjectAdmission == expected) {
            projectPartyAssignments = await ProjectPartyIntegrationBridge.ListAssignmentsDetailedAsync(expected.ProjectId);
        }
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

    private IReadOnlyList<ProjectPartyAssignmentRole> GetNodeAssignmentRoles(ProjectStructureNode node)
    {
        return ResolveNodeAssignmentSemantics(node).ReplacementRoles;
    }

    private ProjectPartyAssignmentRole GetPreferredNodeAssignmentRole(ProjectStructureNode node)
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
