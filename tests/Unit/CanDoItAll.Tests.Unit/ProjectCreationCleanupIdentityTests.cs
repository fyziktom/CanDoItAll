using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectCreationCleanupIdentityTests {
    private static readonly Guid Profile = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DatasetNamespaceId Namespace = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    private static readonly ProjectWriteAdmission Project = new(Profile,
        Guid.Parse("33333333-3333-3333-3333-333333333333"), Guid.Parse("44444444-4444-4444-4444-444444444444"));
    private static readonly ProjectCreationReservation Reservation = new(
        Guid.Parse("55555555-5555-5555-5555-555555555555"), Profile, Project.ProjectId, Project.LifetimeId,
        Guid.Parse("66666666-6666-6666-6666-666666666666"), null, null);

    [Fact]
    public void Same_original_creation_has_one_stable_versioned_completion_identity() {
        var receipt = new ProjectCreationReceipt(Project, Namespace, Reservation, new string('a', 64));
        var first = ProjectCreationCompensationRequest.FromReceipt(receipt);
        var replay = ProjectCreationCompensationRequest.FromReceipt(new(Project, Namespace,
            Reservation with { }, new string('a', 64)));
        Assert.Equal(first.Original, replay.Original);
        Assert.Equal("0baec98c-3492-847d-ae7c-1718b4f78aa1", first.Original.Completion.Id.Value.ToString("D"));
        Assert.Same(receipt, first.Receipt);
    }

    [Fact]
    public void Original_namespace_lifetime_fingerprint_reservation_and_domain_remain_distinct() {
        var original = ProjectCreationCompensationRequest.FromReceipt(new(Project, Namespace, Reservation, new string('a', 64)));
        var altered = new[] {
            ProjectCreationCompensationRequest.FromReceipt(new(Project, new(Guid.NewGuid()), Reservation, new string('a', 64))),
            ProjectCreationCompensationRequest.FromReceipt(new(new(Profile, Project.ProjectId, Guid.NewGuid()), Namespace, null, new string('a', 64))),
            ProjectCreationCompensationRequest.FromReceipt(new(Project, Namespace, Reservation, new string('b', 64))),
            ProjectCreationCompensationRequest.FromReceipt(new(Project, Namespace, Reservation with { Id = Guid.NewGuid() }, new string('a', 64)))
        };
        Assert.All(altered, value => Assert.NotEqual(original.Original.Completion.Id, value.Original.Completion.Id));
        var cancellation = new ProjectReservationCancellationRequest(Namespace, Reservation);
        Assert.NotEqual(original.Original.Completion.Id, cancellation.Original.Completion.Id);
        Assert.Equal(cancellation, new ProjectReservationCancellationRequest(Namespace, Reservation with { }));
    }

    [Fact]
    public void Malformed_or_foreign_creation_evidence_is_refused_without_normalization() {
        Assert.Throws<ArgumentException>(() => ProjectCreationCompensationRequest.FromReceipt(
            new(Project, Namespace, Reservation, new string('A', 64))));
        Assert.Throws<ArgumentException>(() => ProjectCreationCompensationRequest.FromReceipt(
            new(Project, Namespace, Reservation with { LifetimeId = Guid.NewGuid() }, new string('a', 64))));
        Assert.Throws<ArgumentException>(() => new ProjectReservationCancellationRequest(Namespace,
            Reservation with { ParentProjectId = Guid.NewGuid() }));
        Assert.Throws<ArgumentException>(() => new ProjectReservationCancellationRequest(Namespace,
            Reservation with { RequesterId = Guid.Empty }));
    }
}
