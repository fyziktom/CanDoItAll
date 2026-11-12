using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDecisionRefreshCaptureIdentityTests {
    [Fact]
    public void Restart_retains_distinct_stages_and_existing_successor_identity() {
        RuntimeCommandId admission = new(Guid.Parse("b0736b25-97df-47cd-8325-459216ca8ed3"));
        ProcessDecisionRefreshCaptureIdentity identity = new(admission);
        ProcessDecisionRefreshCaptureIdentity recovered = new(new(Guid.Parse(admission.Value.ToString("D"))));
        Assert.Equal(Identities(identity), Identities(recovered));
        Assert.Equal(Guid.Parse("1f60b66d-66df-6687-65ee-2319acf4a513"), identity.RootReserve.Value);
        Assert.Equal(Guid.Parse("54ae712c-10f7-0d32-5646-29d8cbc3b957"), identity.LocalReserve.Value);
        Assert.Equal(Guid.Parse("702db2dd-65ec-e6ab-c81a-de278b440219"), identity.Material.Value);
        Assert.Equal(9, Identities(identity).Distinct().Count());
        Assert.DoesNotContain(admission.Value, Identities(identity));
        Assert.DoesNotContain(new ProcessDecisionRefreshAccounting(admission).CommandId.Value, Identities(identity));
        Assert.Equal(new ProcessDecisionRefreshSuccessor(admission).CommandId, identity.Successor);
    }

    [Fact]
    public void Another_admission_cannot_reuse_capture_commands_allocations_or_material() {
        var original = Identities(new(RuntimeCommandId.New()));
        var next = Identities(new(RuntimeCommandId.New()));
        Assert.Empty(original.Intersect(next));
    }

    [Fact]
    public void Missing_admission_cannot_create_any_stage() {
        ProcessDecisionRefreshCaptureIdentity identity = new(default);
        Func<Guid>[] accessors = [() => identity.RootReserve.Value, () => identity.RootComplete.Value,
            () => identity.RootAbort.Value, () => identity.LocalReserve.Value, () => identity.LocalAbort.Value,
            () => identity.Successor.Value, () => identity.RootAllocation.Value, () => identity.LocalAllocation.Value,
            () => identity.Material.Value];
        foreach (var accessor in accessors) {
            Assert.Throws<ArgumentOutOfRangeException>(() => accessor());
        }
    }

    private static Guid[] Identities(ProcessDecisionRefreshCaptureIdentity identity)
        => [identity.RootReserve.Value, identity.RootComplete.Value, identity.RootAbort.Value, identity.LocalReserve.Value,
            identity.LocalAbort.Value, identity.Successor.Value, identity.RootAllocation.Value, identity.LocalAllocation.Value, identity.Material.Value];
}
