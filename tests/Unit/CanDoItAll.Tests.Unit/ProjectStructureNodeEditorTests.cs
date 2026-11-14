using System.Globalization;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Workbench.Planning.UI;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructureNodeEditorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Task_form_hydrates_the_saved_repository_for_display_and_comparison_without_allowing_reference_edits(bool linked) {
        Guid? repositoryId = linked ? Guid.Parse("a9526f56-b720-4d6e-b460-4805849f4ccc") : null;
        var node = DateNode() with { ObjectSubtype = "task", NodeReferences = new() { WorkItemRepositoryResourceId = repositoryId } };
        var snapshot = new ProjectStructureTaskSnapshot(node, ProjectStructureTaskEditStatePolicy.Read(node),
            new(ProjectStructureTaskAssigneeSelectionStatus.None, null, []), null);

        var request = ProjectStructureTaskForm.StructureRequest(snapshot, DateRequest(node, []));
        var input = Assert.IsType<PlanningTaskEditorInput.Structure>(ProjectStructureTaskForm.Input(snapshot, false, "USD"));
        var expected = repositoryId is { } id ? $"custom:{id:N}" : null;
        Assert.Equal(expected, request.InputValues!.SingleOrDefault(value => value.Key == ProjectTaskInputKeys.RepositoryRef)?.Value);
        Assert.Equal(expected, input.Repository);
        Assert.False(ProjectStructureNodeEditor.SupportsEditingField(ProjectTaskInputKeys.RepositoryRef));

        Assert.True(ProjectStructureCanvasCatalog.TryResolveCreateDefinition(node.ObjectType, node.ObjectSubtype, out var definition));
        var edited = ProjectStructureNodeEditor.ComposeUpdate(definition, node, request with {
            Title = "Renamed task",
            InputValues = [new() { Key = ProjectTaskInputKeys.RepositoryRef, Value = "custom:27e755198e6142d9a53d8568ce5dc06c" }]
        });
        Assert.Equal("Renamed task", edited.Title);
        Assert.Equal(repositoryId, edited.NodeReferences?.WorkItemRepositoryResourceId);
    }

    [Fact]
    public void Generic_group_can_be_created_and_renamed_with_only_ordinary_details() {
        Assert.True(ProjectStructureCanvasCatalog.TryResolveCreateDefinition(
            ProjectObjectType.ProjectBlock, string.Empty, out var definition));
        var node = DateNode() with { ObjectType = ProjectObjectType.ProjectBlock, ObjectSubtype = string.Empty };
        var create = DateRequest(node, []) with { Title = "Meetings", Subtitle = "Weekly planning", Notes = "Notes and follow-up work" };
        var prepared = ProjectStructureCreateRequestComposer.Compose(definition, create, node.ParentId, (12, 34));
        Assert.Equal(ProjectObjectType.ProjectBlock, prepared.Request.ObjectType);
        Assert.Equal(string.Empty, prepared.Request.ObjectSubtype);
        Assert.Equal(node.ParentId, prepared.Request.ParentNodeKey);
        Assert.Equal("Meetings", prepared.Request.Title);
        Assert.Empty(prepared.PendingLinks);
        var created = node with { Title = prepared.Request.Title, Subtitle = prepared.Request.Subtitle,
            Notes = prepared.Request.Notes, MetadataJson = prepared.Request.MetadataJson ?? string.Empty };

        var edited = ProjectStructureNodeEditor.ComposeUpdate(definition, created,
            create with { PlacementKind = "edit", Title = "Meeting notes", Notes = "Retained decisions and actions" });

        Assert.Equal("Meeting notes", edited.Title);
        Assert.Equal("Weekly planning", edited.Subtitle);
        Assert.Equal("Retained decisions and actions", edited.Notes);
        Assert.Empty(ProjectStructureNodeEditor.BuildInputValues(definition, created));
        Assert.Null(ProjectObjectMetadataSerializer.Parse(edited.MetadataJson).Repository);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Renaming_delivery_preserves_optional_file_locations_whether_collapsed_values_are_omitted_or_submitted(bool includeValues) {
        var metadata = new ProjectObjectMetadataEnvelope {
            ProjectBlock = new() { OutputRoot = "outputs", TargetRoot = "deliveries", RepositoryRoot = "sources", WorkspaceRoot = "workspace" }
        };
        var node = DateNode() with { ObjectType = ProjectObjectType.ProjectBlock, ObjectSubtype = "delivery",
            Title = "Meetings", MetadataJson = ProjectObjectMetadataSerializer.Serialize(metadata) };
        Assert.True(ProjectStructureCanvasCatalog.TryResolveCreateDefinition(node.ObjectType, node.ObjectSubtype, out var definition));
        Assert.All(definition.InputFields!, field => Assert.False(field.IsRequired));
        var values = includeValues ? ProjectStructureNodeEditor.BuildInputValues(definition, node) : [];

        var update = ProjectStructureNodeEditor.ComposeUpdate(definition, node,
            DateRequest(node, values) with { Title = "Weekly meetings", PlacementKind = "edit" });

        Assert.Equal("Weekly meetings", update.Title);
        var saved = ProjectObjectMetadataSerializer.Parse(update.MetadataJson).ProjectBlock!;
        Assert.Equal(metadata.ProjectBlock.OutputRoot, saved.OutputRoot);
        Assert.Equal(metadata.ProjectBlock.TargetRoot, saved.TargetRoot);
        Assert.Equal(metadata.ProjectBlock.RepositoryRoot, saved.RepositoryRoot);
        Assert.Equal(metadata.ProjectBlock.WorkspaceRoot, saved.WorkspaceRoot);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Transcript_uses_one_visible_body_and_accepts_existing_legacy_payloads(bool legacyPayload) {
        var node = DateNode() with { ObjectType = ProjectObjectType.Transcript, ObjectSubtype = string.Empty };
        Assert.True(ProjectStructureCanvasCatalog.TryResolveCreateDefinition(node.ObjectType, node.ObjectSubtype, out var definition));
        Assert.True(definition.ShowDefaultTextFields);
        Assert.DoesNotContain(definition.InputFields!, field => field.Key == "transcriptText");
        const string transcript = "Speaker: Review the decisions.\nColleague: Record the next action.";
        var request = DateRequest(node, legacyPayload ? [new() { Key = "transcriptText", Value = transcript }] : []) with {
            Notes = legacyPayload ? string.Empty : transcript
        };
        var created = ProjectStructureCreateRequestComposer.Compose(definition, request, node.ParentId, (null, null)).Request;
        var normalized = ProjectNodeKindRegistry.NormalizeMetadata(created.ObjectType, created.ObjectSubtype,
            ProjectObjectMetadataSerializer.Parse(created.MetadataJson), created.Notes, null);
        Assert.Equal(transcript, created.Notes);
        Assert.Equal(transcript, normalized.Transcript!.TranscriptText);
        var existing = node with { Notes = created.Notes, MetadataJson = ProjectObjectMetadataSerializer.Serialize(normalized) };
        const string replacement = "Revised transcript with the accepted decision.";

        var edited = ProjectStructureNodeEditor.ComposeUpdate(definition, existing,
            DateRequest(existing, ProjectStructureNodeEditor.BuildInputValues(definition, existing)) with {
                PlacementKind = "edit", Notes = replacement
            });

        Assert.Equal(replacement, edited.Notes);
        Assert.Equal(replacement, ProjectObjectMetadataSerializer.Parse(edited.MetadataJson).Transcript!.TranscriptText);
    }

    [Theory]
    [InlineData("2026-10-26T10:00:00-04:00")]
    [InlineData("2026-10-26T10:00:00+05:30")]
    [InlineData("2026-10-26T10:00")]
    public void Created_and_changed_dates_preserve_the_instant_as_utc(string input) {
        var expected = DateTimeOffset.Parse(input, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal).ToUniversalTime();
        var node = DateNode();
        Assert.True(ProjectStructureCanvasCatalog.TryResolveCreateDefinition(node.ObjectType, node.ObjectSubtype, out var definition));
        var request = DateRequest(node, [new() { Key = "startUtc", Value = input },
            new() { Key = "endUtc", Value = input }, new() { Key = "dueUtc", Value = input }]);
        var created = ProjectStructureCreateRequestComposer.Compose(definition, request, node.ParentId, (null, null)).Request;
        var edited = ProjectStructureNodeEditor.ComposeUpdate(definition, node, request);
        DateTimeOffset?[] dates = [created.StartUtc, created.EndUtc,
            ProjectObjectMetadataSerializer.Parse(created.MetadataJson).WorkItem?.DueUtc,
            edited.StartUtc, edited.EndUtc, ProjectObjectMetadataSerializer.Parse(edited.MetadataJson).WorkItem?.DueUtc];
        Assert.All(dates, value => {
            Assert.Equal(expected, value);
            Assert.Equal(TimeSpan.Zero, value!.Value.Offset);
        });
    }

    [Theory]
    [InlineData("startUtc")]
    [InlineData("endUtc")]
    [InlineData("dueUtc")]
    public void Invalid_creation_date_is_rejected_before_dispatch(string key) {
        var node = DateNode();
        Assert.True(ProjectStructureCanvasCatalog.TryResolveCreateDefinition(node.ObjectType, node.ObjectSubtype, out var definition));
        Assert.Throws<ArgumentException>(() => ProjectStructureCreateRequestComposer.Compose(definition,
            DateRequest(node, [new() { Key = key, Value = "not-a-date" }]), node.ParentId, (null, null)));
    }

    [Fact]
    public void Missing_and_blank_optional_creation_dates_remain_unset() {
        var node = DateNode();
        Assert.True(ProjectStructureCanvasCatalog.TryResolveCreateDefinition(node.ObjectType, node.ObjectSubtype, out var definition));
        var created = ProjectStructureCreateRequestComposer.Compose(definition,
            DateRequest(node, [new() { Key = "endUtc", Value = " " }]), node.ParentId, (null, null)).Request;
        Assert.Null(created.StartUtc);
        Assert.Null(created.EndUtc);
        Assert.Null(ProjectObjectMetadataSerializer.Parse(created.MetadataJson).WorkItem?.DueUtc);
    }

    private static CanvasWorkbenchCreateActionRequest DateRequest(ProjectStructureNode node, IReadOnlyList<CanvasWorkbenchInputValue> values)
        => new("date-fixture", node.Id, 0, 0, node.ParentId, node.Title, node.Subtitle, node.Notes,
            "child", "dialog", node.ObjectSubtype, null, values);

    private static ProjectStructureNode DateNode() => new("node", "parent", ProjectObjectType.WorkItem, "issue", "Issue", "", "Draft", "", "", "", null,
        "", "", "", 0, 0, new("rect", "#0369a1", "I", "Issue"), [], "progress", 0, "", "", "", [], 0);

    [Theory]
    [InlineData("mermaidText")]
    [InlineData("imageProviderProfileId")]
    [InlineData("imageModel")]
    [InlineData("imageSize")]
    [InlineData("imageQuality")]
    [InlineData("imageOutputFormat")]
    public void Content_generation_fields_are_not_advertised_as_metadata_edit_fields(string fieldKey)
    {
        Assert.False(ProjectStructureNodeEditor.SupportsEditingField(fieldKey));
    }
}
