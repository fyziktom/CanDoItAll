# Reclaim Process root sequence locks

The static root-sequence dictionary retains one semaphore per root indefinitely. The
actual standard Release assembly retained 45,009 entries after every lease had ended in
each of three exploratory probe runs. The releaser also uses a non-atomic disposed flag.
Both affect the existing event store and runtime unit of work, including relational commits.

Keep their ownership and acquisition API unchanged. Use a private registry with a short
mutex, counting every holder and waiter before awaiting its semaphore. Never await, query
SQL or perform owner work while holding the registry mutex. Cancellation drops the pending
reference and releases already acquired roots in reverse. Release signals the semaphore,
then removes/disposes its entry only when the last reference has ended. A new acquirer
must either retain that exact entry or create one after its complete retirement. Never
evict by semaphore count alone: a waiter could then hold an obsolete semaphore while a
replacement admits another writer for the same root.

Preserve distinct sorted multi-root acquisition and reverse release. Use Interlocked for
concurrent-idempotent lease disposal. Empty input remains a no-op. Registry entry count
follows live holders/waiters; dictionary capacity may retain the peak concurrent size, not
the total historical root count. No new project dependency, public interface, DI service,
transaction or coarse lock across asynchronous work is needed.

Prove same-root exclusion under contention and cancellation/churn; independent-root
progress while another root is blocked; reversed multi-root orders; cancellation on a
later root; cancellation with surviving waiters; concurrent disposal; and high-cardinality
reclamation. Use the existing Unit friend assembly and a nonparallel collection for exact
registry lifetime assertions. Repeat owning event-store/unit-of-work cases and actual
PostgreSQL Project/root ordering, competing budget reservations and rollback/recovery.

Compare the same standalone probe against the actual baseline/candidate Release assembly
in fresh processes, recording input digests, retained counts, allocation and latency.
Its isolated registry timings exclude SQL/provider work and do not establish production
capacity; baseline timing includes concurrent validation load. Review any material
parallel-root slowdown before accepting the mutex design. Prefer measured correction
over introducing a sharded or generic locking framework preemptively.

The first candidate reclaimed all entries but measured about 38 ms versus a baseline
median of 10.24 ms for 40,000 parallel independent-root acquisitions. Refine the registry
to 64 fixed short mutexes selected by root hash, retaining a thread-safe dictionary and
the same exact per-root reference accounting. Hash collisions affect registry bookkeeping
only: no mutex remains held during a semaphore wait or owner work. The fixed array is
bounded independent of root history. Keep the initial measurements and compare again;
this refinement introduces no public configuration or reusable locking framework.

Build Persistence and owning tests in standard Release; discover exact selected counts,
enforce full portability, inspect scoped architecture, freeze inputs/outputs, observe tests,
retain proof and reconcile resources. Remove the owned standalone probe build after its
last measurement, preserving reviewed source and measurement evidence. SPA is unchanged.
