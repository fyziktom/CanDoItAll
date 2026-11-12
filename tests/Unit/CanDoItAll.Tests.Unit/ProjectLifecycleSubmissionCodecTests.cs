using System.Text;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectLifecycleSubmissionCodecTests {
    [Fact]
    public void Every_lifecycle_command_retains_its_original_address_revision_and_edge_with_full_epoch_precision() {
        foreach (var original in Originals()) {
            var bytes = ProjectLifecycleOperationCodec.EncodeSubmission(original);
            var restored = ProjectLifecycleOperationCodec.DecodeSubmission(bytes);
            Assert.Equal(original, restored);
            Assert.Equal(long.MaxValue - 1, restored.Operation.Epoch.Value);
            Assert.Equal(bytes, ProjectLifecycleOperationCodec.EncodeSubmission(restored));
            Assert.InRange(bytes.Length, 1, 4096);
            bytes[0] = 0;
            Assert.Equal(original, restored);
        }
    }

    [Fact]
    public void Pending_decode_refuses_noncanonical_or_unsupported_evidence_before_it_can_be_dispatched() {
        var original = Originals()[3];
        var json = Encoding.UTF8.GetString(ProjectLifecycleOperationCodec.EncodeSubmission(original));
        var invalid = new[] {
            json + " ",
            json.Insert(1, "\"unknown\":1,"),
            json.Insert(1, "\"schema\":1,"),
            json.Replace("\"schema\":1", "\"schema\":2", StringComparison.Ordinal),
            json.Replace("\"kind\":4", "\"kind\":99", StringComparison.Ordinal),
            json.Replace("projects:lifecycle-submission:v1", "projects:lifecycle-result:v1", StringComparison.Ordinal),
            json.Replace("\"epoch\":9223372036854775806", "\"epoch\":9.223372036854776e18", StringComparison.Ordinal),
            "{}",
            "null"
        };
        foreach (var payload in invalid) {
            Assert.Throws<InvalidDataException>(() => ProjectLifecycleOperationCodec.DecodeSubmission(Encoding.UTF8.GetBytes(payload)));
        }
        Assert.Throws<InvalidDataException>(() => ProjectLifecycleOperationCodec.DecodeSubmission([]));
        Assert.Throws<InvalidDataException>(() => ProjectLifecycleOperationCodec.DecodeSubmission(new byte[4097]));
        Assert.Throws<InvalidDataException>(() => ProjectLifecycleOperationCodec.DecodeSubmission([0xff]));
        Assert.Equal(original, ProjectLifecycleOperationCodec.DecodeSubmission(ProjectLifecycleOperationCodec.EncodeSubmission(original)));
    }

    private static ProjectLifecycleSubmission[] Originals() {
        var profile = Guid.NewGuid();
        var ns = new DatasetNamespaceId(Guid.NewGuid());
        var operation = new ProjectLifecycleOperation(profile, ns, new(long.MaxValue - 1),
            ProjectMutationCallerScope.LocalWorkspace.Bind(ns), Guid.NewGuid());
        var first = new ProjectWriteAdmission(profile, Guid.NewGuid(), Guid.NewGuid());
        var second = new ProjectWriteAdmission(profile, Guid.NewGuid(), Guid.NewGuid());
        var third = new ProjectWriteAdmission(profile, Guid.NewGuid(), Guid.NewGuid());
        var edge = new ProjectHierarchyEdgeExpectation(Guid.NewGuid(), first, second);
        ProjectLifecycleCommand[] commands = [
            new ProjectLifecycleCommand.Delete(new(first, Guid.NewGuid())),
            new ProjectLifecycleCommand.EnsureLink(first, second),
            new ProjectLifecycleCommand.RemoveLink(edge),
            new ProjectLifecycleCommand.ReconnectLink(edge, third)
        ];
        return commands.Select(command => new ProjectLifecycleSubmission(operation,
            ProjectLifecycleInterpretation.VersionOne, command)).ToArray();
    }
}
