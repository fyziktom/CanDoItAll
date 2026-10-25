# Original accepted Process execution input

Status: accepted before implementation, after the initial business binding's 177 distinct
passing behaviors. This is a persistence prerequisite; executor activation remains gated.

## Problem and ownership

The accepted source projection intentionally omits the prompt. Reading a repaired current
assignment would replace original intent; decoding the full prepared launch per child
restores the measured cost proportional to every step and prompt.

Keep a separately selectable original typed assignment beside its existing accepted
metadata row. Application owns the typed read port and result. Persistence owns encoding,
storage, validation and the indexed query. Module.Processes owns registration and later
input composition. Core and MAF gain no Process dependency. No new table, project,
registry, runtime partial or service locator is needed.

Reuse the existing canonical envelope encoder and fingerprint implementation. A dedicated
input codec validates one cohesive protocol; it does not acquire authority or compose a
provider prompt. The existing metadata codec continues to exclude prompt bytes.

## Persistence and failure contract

Add an explicit version and nullable input JSON/fingerprint to the accepted assignment.
Version zero means uncaptured, with both nullable fields absent. Version one retains the
complete original `ProcessRuntimeStepAssignment` and binds its fingerprint to the original
header and assignment fingerprints. Its metadata projection must match those exact bytes.

Capture only assignments with an admitted decision execution contract, in the existing
first-acceptance transaction. Ordinary assignments remain uncaptured and keep their current
path. Retries never recapture or repair missing input. Failed capture rolls back runtime,
source and idempotency changes together. Existing rows are not backfilled automatically.

Use a separate 4 MiB encoded UTF-8 input bound and at most 4096 entries in each selected
assignment collection. Metadata retains its 1 MiB bound. Validate byte size before JSON
decoding, complete required fields, identifiers, collections, UTC timestamps, policy
binding and canonical round trip. These bounds apply to the newly supported decision
contract; they do not impose a prompt limit on ordinary Process execution.

The read port returns the original assignment, admission/root identity, source authority
and fingerprint. It selects one runtime, accepted header, metadata row and input through
their existing keys. It checks runtime/plan, original Project lifetime and database profile.
It never selects the current prompt, neighboring input or full launch payload. This read
does not grant dispatch or publication authority; existing current fences remain mandatory.

Legacy/uncaptured input is an explicit recovery requirement. Missing, corrupt, substituted
or unsupported evidence fails without fallback. The migration preserves legacy bytes and
refuses destructive downgrade while captured input remains.

## Static intent and dynamic context

The original prompt, role and static artifact expectations are launch intent. Connected
upstream artifacts do not exist at launch. A later supported adapter must bind those
dynamic receipts under its exact dispatch claim, or refuse unsupported required context
before enrolling a child. It must not silently omit artifacts, subprocess requirements or
host capabilities. The generic Process outcome prompt must not be mixed into the registered
task-plan response schema.

RunSnapshot fixes registered business material across the Process run. StepSnapshot fixes
it per Process step. In both cases the inherited Agent uses the selected material unchanged
through its child run; its child policy resolves to RunSnapshot with no independent replan.

## Architecture and proof

Reuse the five-project CodeAnalytics baseline `snap-20261021031959-77361fd7`, whose project
references and cycles were unchanged in the preceding binding proof. Review the final
delta, including DI and new protocol types, before closure. Generated EF migration partials
are the only justified new partials.

Unit proof covers exact round trip, original metadata binding, malformed/missing/duplicate
fields, substituted fingerprints and separate payload/collection bounds. SQL proof covers
first acceptance, rollback, retry, current-assignment repair, legacy refusal, corruption,
profile/lifetime mismatch and registration. Migration proof covers upgrade, restart,
constraints, legacy preservation and guarded downgrade. Historical migration tests must
query historical columns explicitly rather than use a newly expanded entity against an
older schema.

Measure one, 128 and 1024-step selected reads, including large neighboring prompts. Record
query count, bytes, allocations and latency, plus first-acceptance cost. The performance
contract is bounded selected reads, not constant acceptance cost.

Build changed production projects and owning test projects in standard Release outputs.
Confirm exact test discovery, retain failed candidates, run full portability enforcement,
check source/runtime identities and reconcile owned test resources. Reuse the existing
isolated PostgreSQL server.

Startup failure accounting, budget capacity, waiting-step manager completion, cancellation,
refresh, Workflow and settings/SPA integration remain separate activation requirements.
The SPA branch stays `codex/concurrent-mutations`; it is not merged.
