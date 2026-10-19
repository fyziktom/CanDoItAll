# Persisted planning decision and work accounting

This source card refines the reviewed C4/D1 design before implementation. The saved-effect
adapter now has 70/70 passing focused tests. This change adds the dormant schema-six
decision graph and central writer rules before admitting any product planning run.

Models owns the bounded immutable decision, provenance, generations, bases, logical
steps and active work record, plus its strict codec. Core owns legal state transitions
and persistence comparison. Existing journal/file writers invoke that comparison; no
module, MAF or Workspace implementation dependency enters either project. There is
no new project, partial class, store, lock or cache. Existing schemas 1–5 keep their bytes
and allowed transitions; neither reading nor replacing an old run may promote it to six.
Existing SDK/saved-effect entry points continue refusing unsupported schema-six work
until the live owner is connected. A valid copied record is not runtime authority.

The graph binds every actual segment, provider request (including requests with no
native tools), response and proposed publication to one original decision step. Every
publication preserves its source envelope and selected basis. Prefixes and original
policy/target/provenance cannot be rewritten. Refresh/replan requires known no-effect
history, retires the old generation and preserves all work spent. Work and history limits
refuse further activity rather than pruning evidence. Schema-six adds its encoded bytes
to the existing aggregate journal bound; old receipt interpretation remains unchanged.

One live work interval belongs to the actual dispatch lease and logical step. Advancing
to another model step settles the previous interval and opens the next at exactly the
same observed instant in one atomic publication: no uncharged gap between steps.
The charge is max(monotonic elapsed, nonnegative UTC elapsed). Explicit drained human
wait closes the interval; approval later opens another without charging that wait.
Rollback/overflow/budget exhaustion refuse new activity. Cancellation needs no new
clock observation and preserves an open interval until drainage; it cannot invent a
zero-duration settlement. A copied or restarted open interval has no live monotonic
owner and must not authorize mutation. That owner check is the next runtime slice.

Validation must exercise strict encoding, old-version preservation, forged prefixes,
graph/origin/basis replacement, actual file update/full-save perimeter, rollback and
overflow, exact-bound exhaustion, no-gap two-step work, deliberately excluded wait,
spent-work/replan preservation, cancellation plus late response/settlement, and real
aggregate-size accounting. Use deterministic clocks/data and ordinary isolated file
fixtures, standard Release builds and the mandatory final portability gate. These
contract/file tests do not claim actual provider or UI policy activation.
