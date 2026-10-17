using System.Text;
using System.Text.Json.Nodes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectDeletionCompletionCodecTests {
    [Fact]
    public void Retained_completion_preserves_ordered_original_obligations_after_caller_collection_changes() {
        var original = Original();
        var first = new ProjectDeletionObligation(new("first-owner"), Guid.NewGuid());
        var second = new ProjectDeletionObligation(new("second-owner"), Guid.NewGuid());
        var caller = new List<ProjectDeletionObligation> { first, second };
        var binding = new ProjectDeletionCompletionBinding(original, caller);
        var retained = ProjectDeletionCompletionCodec.Write(binding);
        caller.Clear();

        var recovered = ProjectDeletionCompletionCodec.Read(retained, original);
        Assert.Equal(original, recovered.Original);
        Assert.Equal([first, second], recovered.Obligations);
        Assert.Throws<NotSupportedException>(() => ((IList<ProjectDeletionObligation>)recovered.Obligations).Clear());
        Assert.Equal(retained, ProjectDeletionCompletionCodec.Write(recovered));
    }

    [Fact]
    public void A_completion_for_another_profile_namespace_identity_project_or_lifetime_is_refused() {
        var original = Original();
        var retained = ProjectDeletionCompletionCodec.Write(new(original, []));
        var profile = Guid.NewGuid();
        var mismatches = new[] {
            new ProjectDeletionPreparationContext(new(profile, original.Completion.Namespace, original.Completion.Id),
                new(profile, original.Project.ProjectId, original.Project.LifetimeId)),
            new ProjectDeletionPreparationContext(new(original.Completion.DatabaseProfileId, new(Guid.NewGuid()), original.Completion.Id), original.Project),
            new ProjectDeletionPreparationContext(new(original.Completion.DatabaseProfileId, original.Completion.Namespace, new(Guid.NewGuid())), original.Project),
            new ProjectDeletionPreparationContext(original.Completion, new(original.Project.DatabaseProfileId, Guid.NewGuid(), original.Project.LifetimeId)),
            new ProjectDeletionPreparationContext(original.Completion, new(original.Project.DatabaseProfileId, original.Project.ProjectId, Guid.NewGuid()))
        };
        foreach (var mismatch in mismatches) {
            Assert.Throws<InvalidDataException>(() => ProjectDeletionCompletionCodec.Read(retained, mismatch));
        }
    }

    [Fact]
    public void Maximum_escaped_participant_set_fits_the_bound_and_preserves_exact_unicode() {
        var original = Original();
        var obligations = Enumerable.Range(0, ProjectDeletionCompletionBinding.MaximumParticipants)
            .Select(index => new ProjectDeletionObligation(new(index.ToString("X2") + new string('é', 31)), Guid.NewGuid())).ToArray();
        var retained = ProjectDeletionCompletionCodec.Write(new(original, obligations));
        Assert.True(retained.Length <= ProjectDeletionCompletionCodec.MaximumEncodedBytes);
        Assert.Equal(obligations, ProjectDeletionCompletionCodec.Read(retained, original).Obligations);
        var empty = ProjectDeletionCompletionCodec.Write(new(original, []));
        Assert.Empty(ProjectDeletionCompletionCodec.Read(empty, original).Obligations);
    }

    [Fact]
    public void Unknown_duplicate_reordered_or_noncanonical_fields_cannot_change_a_retained_completion() {
        var original = Original();
        var retained = ProjectDeletionCompletionCodec.Write(new(original, [new(new("owner"), Guid.NewGuid())]));
        var text = Encoding.UTF8.GetString(retained);
        var variants = new[] {
            text.Insert(1, "\"unexpected\":true,"),
            text.Insert(1, "\"version\":1,"),
            text.Insert(text.IndexOf("\"participant\"", StringComparison.Ordinal), "\"participant\":\"owner\","),
            text.Replace("\"version\":1", "\"version\":2", StringComparison.Ordinal),
            text.Replace("projects:deletion-completion:v1", "projects:definition-intent:v1", StringComparison.Ordinal),
            " " + text,
            text + " ",
            text.Replace("\"owner\"", "\"\\u006fwner\"", StringComparison.Ordinal),
            text.Replace("\"version\":1", "\"version\":1.0", StringComparison.Ordinal)
        };
        foreach (var variant in variants) {
            Assert.Throws<InvalidDataException>(() => ProjectDeletionCompletionCodec.Read(Encoding.UTF8.GetBytes(variant), original));
        }
        var reordered = JsonNode.Parse(retained)!.AsObject();
        var domain = reordered["domain"]!.DeepClone();
        reordered.Remove("domain");
        reordered.Add("domain", domain);
        Assert.Throws<InvalidDataException>(() => ProjectDeletionCompletionCodec.Read(Encoding.UTF8.GetBytes(reordered.ToJsonString()), original));
    }

    [Fact]
    public void Corrupt_obligations_and_malformed_text_are_refused_before_recovery() {
        var original = Original();
        var retained = ProjectDeletionCompletionCodec.Write(new(original, [new(new("owner"), Guid.NewGuid())]));
        List<byte[]> variants = [[], new byte[ProjectDeletionCompletionCodec.MaximumEncodedBytes + 1], [0xff], "null"u8.ToArray()];
        foreach (var participant in new[] { "", " ", new string('x', 65), "\\ud800", "\\udc00" }) {
            var text = Encoding.UTF8.GetString(retained).Replace("\"owner\"", "\"" + participant + "\"", StringComparison.Ordinal);
            variants.Add(Encoding.UTF8.GetBytes(text));
        }
        var duplicate = JsonNode.Parse(retained)!.AsObject();
        var list = duplicate["obligations"]!.AsArray();
        list.Add(list[0]!.DeepClone());
        variants.Add(Encoding.UTF8.GetBytes(duplicate.ToJsonString()));
        var excessive = JsonNode.Parse(retained)!.AsObject();
        var many = excessive["obligations"]!.AsArray();
        many.Clear();
        for (var index = 0; index <= ProjectDeletionCompletionBinding.MaximumParticipants; index++) {
            many.Add(new JsonObject { ["participant"] = "owner-" + index, ["recovery"] = Guid.NewGuid().ToString("D") });
        }
        variants.Add(Encoding.UTF8.GetBytes(excessive.ToJsonString()));
        foreach (var variant in variants) {
            Assert.Throws<InvalidDataException>(() => ProjectDeletionCompletionCodec.Read(variant, original));
        }
    }

    private static ProjectDeletionPreparationContext Original() {
        var profile = Guid.NewGuid();
        return new(new(profile, new DatasetNamespaceId(Guid.NewGuid()), new(Guid.NewGuid())),
            new(profile, Guid.NewGuid(), Guid.NewGuid()));
    }
}
