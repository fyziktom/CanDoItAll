namespace CanDoItAll.Modules.CrmHr.Components;

// Admits one mutation at a time for one editor lifetime of a routed host. A host captures its action stamp, enters
// the gate with the stamp's lifetime and only then takes its independent submission and calls the owner. A second
// dispatch of the same lifetime while the first is in flight is dropped, so a double click, Enter on the form and an
// alternate action of the same form cannot produce two writes. A retired lifetime never blocks its successor: the
// host advances its lifetime when the target changes, and the retired mutation's exit cannot release the new one.
public sealed class CrmHrMutationGate
{
    private readonly object sync = new();
    private Admission? current;

    // True while a mutation of this lifetime is in flight; a host can show it as busy state.
    public bool IsInFlight(long lifetime)
    {
        lock (sync)
        {
            return current is { } admission && admission.Lifetime == lifetime;
        }
    }

    // Null when a mutation of the same lifetime is already in flight. Dispose the admission when the owner returned.
    public Admission? TryEnter(long lifetime)
    {
        lock (sync)
        {
            if (current is { } existing && existing.Lifetime == lifetime)
            {
                return null;
            }

            var admission = new Admission(this, lifetime);
            current = admission;
            return admission;
        }
    }

    private void Exit(Admission admission)
    {
        lock (sync)
        {
            if (ReferenceEquals(current, admission))
            {
                current = null;
            }
        }
    }

    public sealed class Admission : IDisposable
    {
        private readonly CrmHrMutationGate gate;
        private int disposed;

        internal Admission(CrmHrMutationGate gate, long lifetime)
        {
            this.gate = gate;
            Lifetime = lifetime;
        }

        public long Lifetime { get; }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                gate.Exit(this);
            }
        }
    }
}
