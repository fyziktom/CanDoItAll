using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectWorkbenchDeletionBindingTests {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Legacy_header_binding_uses_the_captured_lifetime_without_rewriting_payload_bytes() {
        var original = Original();
        var payload = new DeleteProjectMutationPayload([], [], SourceReference: ProjectAssignmentReference.From(original.Project));
        string encoded = JsonSerializer.Serialize(payload, Json);
        var record = new ProjectCrossModuleMutationRecord {
            ProjectId = original.Project.ProjectId, PayloadJson = encoded,
            CompletionNamespaceId = original.Completion.Namespace.Value, CompletionId = original.Completion.Id.Value
        };
        var decoded = ProjectWorkbenchDeletionPayload.Read(record);
        Assert.True(ProjectWorkbenchDeletionPayload.Matches(decoded, original));
        Assert.Equal(encoded, record.PayloadJson);
        Assert.Null(ProjectWorkbenchDeletionPayload.Read(encoded).CompletionContext);
    }

    [Theory]
    [InlineData(InvalidBinding.NamespaceOnly)]
    [InlineData(InvalidBinding.CompletionOnly)]
    [InlineData(InvalidBinding.EmptyNamespace)]
    [InlineData(InvalidBinding.EmptyCompletion)]
    [InlineData(InvalidBinding.UnboundLifetime)]
    [InlineData(InvalidBinding.MissingSource)]
    [InlineData(InvalidBinding.ConflictingEmbeddedContext)]
    public void Header_binding_cannot_invent_missing_identity_or_override_a_saved_context(InvalidBinding fault) {
        var original = Original();
        var payload = new DeleteProjectMutationPayload([], [], SourceReference: fault switch {
            InvalidBinding.UnboundLifetime => new(original.Project.DatabaseProfileId, original.Project.ProjectId, null),
            InvalidBinding.MissingSource => null,
            _ => ProjectAssignmentReference.From(original.Project)
        }, CompletionContext: fault == InvalidBinding.ConflictingEmbeddedContext ? Original() : null);
        Guid? ns = fault switch {
            InvalidBinding.CompletionOnly => null,
            InvalidBinding.EmptyNamespace => Guid.Empty,
            _ => original.Completion.Namespace.Value
        };
        Guid? completion = fault switch {
            InvalidBinding.NamespaceOnly => null,
            InvalidBinding.EmptyCompletion => Guid.Empty,
            _ => original.Completion.Id.Value
        };
        Assert.NotNull(Record.Exception(() => ProjectWorkbenchDeletionPayload.Read(JsonSerializer.Serialize(payload, Json), ns, completion)));
    }

    [Fact]
    public void An_identical_embedded_and_header_binding_is_accepted() {
        var original = Original();
        string encoded = JsonSerializer.Serialize(new DeleteProjectMutationPayload([], [],
            SourceReference: ProjectAssignmentReference.From(original.Project), CompletionContext: original), Json);
        Assert.True(ProjectWorkbenchDeletionPayload.Matches(ProjectWorkbenchDeletionPayload.Read(encoded,
            original.Completion.Namespace.Value, original.Completion.Id.Value), original));
    }

    private static ProjectDeletionPreparationContext Original() {
        Guid profile = Guid.NewGuid();
        return new(new(profile, new(Guid.NewGuid()), new(Guid.NewGuid())), new(profile, Guid.NewGuid(), Guid.NewGuid()));
    }

    public enum InvalidBinding {
        NamespaceOnly, CompletionOnly, EmptyNamespace, EmptyCompletion, UnboundLifetime, MissingSource, ConflictingEmbeddedContext
    }
}
