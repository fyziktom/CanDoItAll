using CanDoItAll.Modules.CrmHr.Components;

namespace CanDoItAll.Tests.Unit.CrmHr;

// The admission rule the routed hosts use for their mutations: one write per editor lifetime at a time, and a retired
// lifetime never blocks or releases its successor.
public sealed class CrmHrMutationGateTests
{
    [Fact]
    public void A_second_dispatch_of_the_same_lifetime_is_dropped_until_the_first_returns()
    {
        var gate = new CrmHrMutationGate();

        var first = gate.TryEnter(lifetime: 4);
        Assert.NotNull(first);
        Assert.True(gate.IsInFlight(4));
        Assert.Null(gate.TryEnter(lifetime: 4));

        first.Dispose();

        Assert.False(gate.IsInFlight(4));
        using var next = gate.TryEnter(lifetime: 4);
        Assert.NotNull(next);
    }

    [Fact]
    public void A_retired_lifetime_neither_blocks_nor_releases_its_successor()
    {
        var gate = new CrmHrMutationGate();
        var retired = gate.TryEnter(lifetime: 1);
        Assert.NotNull(retired);

        // The host switched its target while the first write was still in flight.
        var successor = gate.TryEnter(lifetime: 2);
        Assert.NotNull(successor);
        Assert.True(gate.IsInFlight(2));
        Assert.False(gate.IsInFlight(1));

        // The late return of the retired write must not free the successor's admission.
        retired.Dispose();
        Assert.True(gate.IsInFlight(2));
        Assert.Null(gate.TryEnter(lifetime: 2));

        successor.Dispose();
        Assert.False(gate.IsInFlight(2));
    }

    [Fact]
    public void Disposing_an_admission_twice_releases_once()
    {
        var gate = new CrmHrMutationGate();
        var first = gate.TryEnter(lifetime: 7);
        Assert.NotNull(first);
        first.Dispose();

        var second = gate.TryEnter(lifetime: 7);
        Assert.NotNull(second);

        // A repeated disposal of the first admission cannot release the second one.
        first.Dispose();
        Assert.True(gate.IsInFlight(7));
        Assert.Null(gate.TryEnter(lifetime: 7));
        second.Dispose();
    }

    [Fact]
    public async Task Concurrent_dispatches_of_one_lifetime_admit_exactly_one()
    {
        var gate = new CrmHrMutationGate();
        using var start = new ManualResetEventSlim();

        var attempts = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() =>
            {
                start.Wait();
                return gate.TryEnter(lifetime: 3);
            }))
            .ToArray();
        start.Set();
        var admissions = await Task.WhenAll(attempts);

        Assert.Single(admissions, admission => admission is not null);
    }
}
