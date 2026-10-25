using System.Text;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectLifecycleContractTests {
    [Fact]
    public void Each_lifecycle_intent_retains_its_original_profile_lifetimes_and_observation() {
        var profile = Guid.NewGuid();
        var parent = Admission(profile);
        var child = Admission(profile);
        var newParent = Admission(profile);
        var expected = new ProjectDefinitionExpectation(child, Guid.NewGuid());
        var edge = new ProjectHierarchyEdgeExpectation(Guid.NewGuid(), parent, child);
        var operation = Operation(profile);
        ProjectLifecycleCommand[] commands = [new ProjectLifecycleCommand.Delete(expected),
            new ProjectLifecycleCommand.EnsureLink(parent, child), new ProjectLifecycleCommand.RemoveLink(edge),
            new ProjectLifecycleCommand.ReconnectLink(edge, newParent)];

        foreach (var command in commands) {
            var submitted = new ProjectLifecycleSubmission(operation, ProjectLifecycleInterpretation.VersionOne, command);
            Assert.Same(operation, submitted.Operation);
            Assert.Same(command, submitted.Command);
            Assert.Equal(profile, submitted.Command.DatabaseProfileId);
        }
        Assert.Same(expected, Assert.IsType<ProjectLifecycleCommand.Delete>(commands[0]).Expected);
        Assert.Same(edge, Assert.IsType<ProjectLifecycleCommand.RemoveLink>(commands[2]).Expected);
        Assert.Same(edge, Assert.IsType<ProjectLifecycleCommand.ReconnectLink>(commands[3]).Expected);
        Assert.Same(newParent, Assert.IsType<ProjectLifecycleCommand.ReconnectLink>(commands[3]).NewParent);
    }

    [Fact]
    public void Relationship_intents_refuse_ambiguous_self_cross_profile_or_reselected_source_edges() {
        var parent = Admission(Guid.NewGuid());
        var child = Admission(parent.DatabaseProfileId);
        var foreign = Admission(Guid.NewGuid());
        var edge = new ProjectHierarchyEdgeExpectation(Guid.NewGuid(), parent, child);

        Assert.Throws<ArgumentException>(() => new ProjectHierarchyEdgeExpectation(Guid.Empty, parent, child));
        Assert.Throws<ArgumentException>(() => new ProjectHierarchyEdgeExpectation(Guid.NewGuid(), parent, parent));
        Assert.Throws<ArgumentException>(() => new ProjectLifecycleCommand.EnsureLink(parent, foreign));
        Assert.Throws<ArgumentException>(() => new ProjectLifecycleCommand.ReconnectLink(edge, parent));
        Assert.Throws<ArgumentException>(() => new ProjectLifecycleCommand.ReconnectLink(edge, child));
        Assert.Throws<ArgumentException>(() => new ProjectLifecycleCommand.ReconnectLink(edge, foreign));
        Assert.Throws<ArgumentException>(() => new ProjectLifecycleSubmission(Operation(foreign.DatabaseProfileId),
            ProjectLifecycleInterpretation.VersionOne, new ProjectLifecycleCommand.RemoveLink(edge)));
    }

    [Fact]
    public void Completion_binding_freezes_ordered_obligations_without_granting_release() {
        var context = CompletionContext(Guid.NewGuid());
        var first = new ProjectDeletionObligation(new("workbench"), Guid.NewGuid());
        var second = new ProjectDeletionObligation(new("agent-project-structure-access"), Guid.NewGuid());
        ProjectDeletionObligation[] supplied = [first, second];
        var binding = new ProjectDeletionCompletionBinding(context, supplied);

        supplied[0] = new(new("replacement"), Guid.NewGuid());

        Assert.Same(context, binding.Original);
        Assert.Equal(new[] { first, second }, binding.Obligations);
        Assert.Throws<NotSupportedException>(() => ((IList<ProjectDeletionObligation>)binding.Obligations)[0] = supplied[0]);
        Assert.Empty(new ProjectDeletionCompletionBinding(context, []).Obligations);
        Assert.Throws<ArgumentException>(() => new ProjectDeletionPreparationContext(context.Completion, Admission(Guid.NewGuid())));
    }

    [Fact]
    public void Completion_refuses_duplicate_missing_or_excessive_participant_obligations() {
        var context = CompletionContext(Guid.NewGuid());
        var first = new ProjectDeletionObligation(new("workbench"), Guid.NewGuid());
        Assert.Throws<ArgumentException>(() => new ProjectDeletionCompletionBinding(context, [first, first]));
        Assert.Throws<ArgumentException>(() => new ProjectDeletionCompletionBinding(context, [null!]));
        Assert.Throws<ArgumentException>(() => new ProjectDeletionCompletionBinding(context, new ChangingCountObligations(first)));
        Assert.Throws<ArgumentException>(() => new ProjectDeletionCompletionBinding(context, new ChangingCountObligations(first, -1)));
        var maximum = Enumerable.Range(0, ProjectDeletionCompletionBinding.MaximumParticipants)
            .Select(index => new ProjectDeletionObligation(new($"participant-{index}"), Guid.NewGuid())).ToArray();
        Assert.Equal(maximum.Length, new ProjectDeletionCompletionBinding(context, maximum).Obligations.Count);
        Assert.Throws<ArgumentException>(() => new ProjectDeletionCompletionBinding(context, [.. maximum, first]));
        Assert.Throws<ArgumentException>(() => new ProjectDeletionObligation(default, Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => new ProjectDeletionObligation(new("workbench"), Guid.Empty));
    }

    [Fact]
    public void Participant_limit_counts_strict_utf8_and_rejects_unpaired_surrogates() {
        var recovery = Guid.NewGuid();
        Assert.Equal(64, Encoding.UTF8.GetByteCount(new ProjectDeletionObligation(new(new string('é', 32)), recovery).Participant.Value));
        Assert.Throws<ArgumentException>(() => new ProjectDeletionObligation(new(new string('é', 33)), recovery));
        Assert.Throws<ArgumentException>(() => new ProjectDeletionObligation(new(new string('a', 65)), recovery));
        Assert.Throws<EncoderFallbackException>(() => new ProjectDeletionObligation(new("invalid\ud800"), recovery));
        Assert.Throws<EncoderFallbackException>(() => new ProjectDeletionObligation(new("invalid\udc00"), recovery));
    }

    private static ProjectWriteAdmission Admission(Guid profile) => new(profile, Guid.NewGuid(), Guid.NewGuid());

    private static ProjectLifecycleOperation Operation(Guid profile) =>
        new(profile, new(Guid.NewGuid()), new(17), new("pc1:" + new string('a', 64)), Guid.NewGuid());

    private static ProjectDeletionPreparationContext CompletionContext(Guid profile) =>
        new(new(profile, new(Guid.NewGuid()), new(Guid.NewGuid())), Admission(profile));

    private sealed class ChangingCountObligations(ProjectDeletionObligation value, int initialCount = 1)
        : IReadOnlyList<ProjectDeletionObligation> {
        private bool read;
        public int Count => read ? initialCount + 1 : initialCount;

        public ProjectDeletionObligation this[int index] {
            get {
                if (index != 0) {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }
                read = true;
                return value;
            }
        }

        public IEnumerator<ProjectDeletionObligation> GetEnumerator() => throw new NotSupportedException();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
