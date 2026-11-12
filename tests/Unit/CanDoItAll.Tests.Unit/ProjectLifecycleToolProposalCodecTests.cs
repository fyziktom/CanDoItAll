using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectLifecycleToolProposalCodecTests {
    private static readonly Guid Profile = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Agent = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Authority = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly ProjectWriteAdmission Parent = new(Profile, Guid.Parse("44444444-4444-4444-4444-444444444444"), Guid.Parse("55555555-5555-5555-5555-555555555555"));
    private static readonly ProjectWriteAdmission Child = new(Profile, Guid.Parse("66666666-6666-6666-6666-666666666666"), Guid.Parse("77777777-7777-7777-7777-777777777777"));

    [Fact]
    public void Exact_restore_preserves_original_source_epoch_endpoint_lifetimes_and_business_intent() {
        var source = Source(9007199254740993);
        var input = new ProjectLifecycleToolArguments(Parent.ProjectId, new(Child.ProjectId) {
            ExpectedParentAdmission = Parent, ExpectedChildAdmission = Child
        });
        var prepared = Prepare(input, source);
        var restored = ProjectLifecycleToolProposalCodec.Read(prepared);
        var intent = new AgentToolBusinessIntentId(Guid.NewGuid());
        var original = ProjectLifecycleToolProposalCodec.Submission(source, intent, input);
        var replay = ProjectLifecycleToolProposalCodec.Submission(restored.Source, intent, restored.Input);
        Assert.Equal(original, replay);
        Assert.Equal(source.Epoch, replay.Operation.Epoch);
        Assert.Equal(intent.Value, replay.Operation.Id);
        Assert.Equal(source.CallerBinding, replay.Operation.CallerBinding);
        Assert.Equal(new ProjectLifecycleCommand.EnsureLink(Parent, Child), replay.Command);
        Assert.Equal(AgentToolProposalRecovery.OwnerReceipt, prepared.Recovery);
        Assert.Equal(prepared, Prepare(restored.Input, restored.Source));
        Assert.NotEqual(prepared.Digest, Prepare(input, Source(9007199254740994)).Digest);
    }

    [Fact]
    public void Reconnect_digest_preserves_original_edge_and_rejects_partial_or_foreign_witnesses() {
        var previous = new ProjectWriteAdmission(Profile, Guid.NewGuid(), Guid.NewGuid());
        var edge = new ProjectHierarchyEdgeExpectation(Guid.NewGuid(), previous, Child);
        var request = new ProjectStructureSubprojectChangeRequest(Child.ProjectId, previous.ProjectId) {
            ExpectedParentAdmission = Parent, ExpectedChildAdmission = Child, ExpectedCurrentEdge = edge
        };
        var input = new ProjectLifecycleToolArguments(Parent.ProjectId, request);
        var original = Prepare(input, Source());
        var restored = ProjectLifecycleToolProposalCodec.Read(original);
        Assert.Equal(edge, restored.Input.Request.ExpectedCurrentEdge);
        Assert.Equal(new ProjectLifecycleCommand.ReconnectLink(edge, Parent), ProjectLifecycleToolProposalCodec.Command(restored.Input, Profile));
        Assert.NotEqual(original.Digest, Prepare(input with { Request = request with { ExpectedCurrentEdge = new(Guid.NewGuid(), previous, Child) } }, Source()).Digest);
        Assert.Throws<AgentToolAdmissionException>(() => Prepare(input with { Request = request with { ExpectedCurrentEdge = null } }, Source()));
        Assert.Throws<AgentToolAdmissionException>(() => Prepare(input with { Request = request with { ExpectedChildAdmission = new(Profile, Child.ProjectId, Guid.NewGuid()) } }, Source()));
        Assert.Throws<AgentToolAdmissionException>(() => Prepare(input with { Request = request with { ExpectedParentAdmission = new(Guid.NewGuid(), Parent.ProjectId, Parent.LifetimeId) } }, Source()));
    }

    [Fact]
    public void Identity_only_and_oversize_inputs_do_not_prepare_owner_recoverable_hierarchy_work() {
        var input = new ProjectLifecycleToolArguments(Parent.ProjectId, new(Child.ProjectId));
        Assert.Throws<AgentToolAdmissionException>(() => Prepare(input, Source()));
        var request = input.Request with { ExpectedParentAdmission = Parent, ExpectedChildAdmission = Child, LeaseToken = new('a', 513) };
        Assert.Throws<AgentToolAdmissionException>(() => Prepare(input with { Request = request }, Source()));
        request = request with { LeaseToken = new('é', ProjectLifecycleToolProposalCodec.MaximumArgumentUtf8Bytes) };
        Assert.Throws<AgentToolAdmissionException>(() => Prepare(input with { Request = request }, Source()));
    }

    private static AgentToolPreparedPayload Prepare(ProjectLifecycleToolArguments input, ProjectDefinitionToolSource source)
        => ProjectLifecycleToolProposalCodec.Prepare(ProjectStructureToolPolicy.ProjectStructureSubprojectLink,
            JsonSerializer.SerializeToElement(input, ProjectDefinitionToolProposalCodec.Json), ProjectDefinitionToolProposalCodec.WriteSource(source));

    private static ProjectDefinitionToolSource Source(long epoch = 7) {
        var session = new AgentToolSessionAdmission(new(Guid.Parse("88888888-8888-8888-8888-888888888888"), Guid.Parse("99999999-9999-9999-9999-999999999999"), new(Authority)),
            Agent, AgentRuntimeContextPurpose.InteractiveChat, new(Profile, "profile", new(0)));
        var governance = new AgentExecutionGovernanceSnapshot(new(Authority), Agent, Profile, new(0),
            WorkspaceScopeDescriptor.Organization(Profile.ToString("N")), true, true, "1", "original-policy",
            [ProjectStructureToolPolicy.ProjectStructureSubprojectLink]);
        var dataset = new DatasetNamespaceId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        return new(session, dataset, new(epoch), ProjectDefinitionGovernedBindings.AgentCaller(session).Bind(dataset),
            ProjectDefinitionRequestVersion.VersionOne, new(new() { CanRead = true, CanCreateSubprojects = true,
                CanWriteNonTaskStructure = true, AllowAllProjects = true }, governance, null), null);
    }
}
