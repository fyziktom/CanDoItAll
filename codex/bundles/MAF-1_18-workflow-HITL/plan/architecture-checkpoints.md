# C# Architecture Checkpoints

## CP-WB0 — pre-implementation architecture gate

Status: **Prepared / SB03 entry-approved**.

Required and satisfied:

- current-state, boundary, dependency, pattern, and testability artifacts exist;
- re-anchor snapshot `snap-20260820203442-90bdd166` and focused architecture snapshot `snap-20260820220112-5cb38069` are recorded;
- MAF 1.18 restore/build/API inspection is the active source of truth;
- no new project is planned and forbidden references are explicit;
- no-production-partial and direct-test policies are explicit;
- SB03/SB04 README architecture sections link to these gates.

This is a planning gate only. It does not prove SB03 implementation.

## CP-WB1 — SB03 native foundation

Status: **Proven / Pass**. SB04 unlocked.

Satisfied evidence:

- dependency graph: final snapshot `snap-20260821002934-bf844210` has no project cycle,
  no new cycle/reference edge relative to `snap-20260820220112-5cb38069`, exactly the two
  unchanged baseline non-project cycles, and no MAF SDK leak across neutral boundaries;
- partial policy: no new handwritten production partial or nested extracted service;
- testability: checkpoint adapter, binding compiler/factory, correlator, verifier/drivers,
  turn-result mapper, and approval logic are directly tested, including focused negatives;
- old-class shrink: compiler and backend are thin delegating facades; native start and result
  mapping are top-level collaborators; no native exception-as-pause path remains;
- production composition: resume capability requires both the real store and exact catalog,
  ordinary non-HITL execution stays on the legacy path, and the backend remains non-durable;
- behavioral proof: real MAF checkpoint, disposed-run reconstruction, exact identity and
  topology rejection, pre-wait marker count one, approval/denial, and consecutive waits pass;
- governed proof: `proof/SB03` records hashes, transcripts, source assertions, anti-stub,
  production composition, downstream smoke, red-team, and the exact 203/203 test selector.

Unlock decision: every CP-WB1 item is Proven. SB04 entered and subsequently passed CP-WB2.

## CP-WB2 — SB04 durable state machine

Status: **Proven / Pass**. SB05 unlocked and dependency-ready.

Satisfied evidence:

- CP-WB1 remains valid;
- dependency graph: final snapshot `snap-20260821044013-44e660f5` covers 9 projects
  and 478 documents, has no project cycle, retains exactly the same two named baseline
  non-project cycles, and introduces no Core-to-EF/MAF edge;
- partial policy: transition rules, continuation, persistent stores, and the dedup
  decorator are separate top-level types; no handwritten production partial, nested
  extraction, service locator, or append-only growth in `PersistentWorkflowStores.cs`
  was introduced;
- testability: direct state/continuation/lease/dedup tests pass in the immutable 419/419
  Unit selector, and the exact three-class Integration selector passes 16/16 with real
  PostgreSQL persistence proof and executable production composition;
- old-class shrink: `WorkflowRuntimeManager` is 738 lines versus the 748-line baseline;
  public entry points are thin factory delegates, response submission is a one-line
  delegate, and the compatibility construction path is test-only;
- production composition: persistent ports replace proof stores and exactly one dedup
  decorator wraps the existing invoker without recursion;
- durability proof: migration `20260821021747_AddWorkflowHitlRecovery`, no pending model
  changes, constraints, CAS, heartbeat/lease recovery, ordinal index, all four crash
  windows, cancellation, consecutive wait, exact version/topology, corruption, and legacy
  fail-closed behavior pass;
- build/governance proof: ten affected Release project builds and the final post-fix
  Release Unit build pass with zero warnings/errors, and the upgraded-package scanner
  passes;
- guarantee language is exactly-once response acceptance and deduplicated participating
  governed effects; arbitrary external exactly-once behavior is explicitly excluded;
- governed SB04 artifacts under `proof/SB04` contain the manifest, transcripts, source
  assertions, anti-stub evidence, progression failures, and passing proof.

Unlock decision: every CP-WB2 item is Proven. SB05 entered and subsequently passed
CP-WB3.

## CP-WB3 — SB05 governed API boundary

Status: **Proven / Pass**. SB06 entered from this approved state and subsequently passed
CP-WB4.

Satisfied evidence:

- CP-WB2 remains valid. No new production project or relational persistence surface was
  introduced; launch scope and response policy/lifetime/fingerprint use existing
  `OriginJson` and `AuthorizationPolicyJson`, and the SB04 operation remains the durable
  reconstruction/audit record.
- Exactly one `IWorkflowExternalResponseService` facade owns authorize -> validate ->
  create/replay -> continue/status orchestration. Focused authorizer, validator, grant
  factory, result mapper, recovery coordinator, and startup worker collaborators are
  top-level and directly tested.
- The Web POST, `WorkflowsPage.razor.cs`, and
  `WorkflowAgentRuntimeToolProvider` are exactly the three mutation callers. All call the
  common service; raw manager/coordinator response mutation is absent from production.
- Trusted profile/scope/capability evidence is resolved at the appropriate Web/Module
  adapter edge. Initial and startup/lease recovery reconstruct and revalidate durable
  authorization, fail closed on incomplete/expired/mismatched evidence, and derive action
  from the contract-validated protected payload before backend/executor delivery.
- Focused Web types own strict typed JSON binding, status mapping, OpenAPI metadata, and
  allow-list projections for run/detail, event/SSE, artifact, checkpoint, pending request,
  response, and operation status. Neutral Core/Runtime remain free of ASP.NET, EF/Npgsql,
  and MAF dependencies; Web has no persistence-entity or MAF dependency.
- No SB05 table, column, entity, model-snapshot edit, or migration exists. The EF
  pending-model check passes with `20260821021747_AddWorkflowHitlRecovery` remaining the
  latest HITL migration.
- The exact 22-class Unit selector passes 297/297 and the exact 11-class Integration
  selector passes 137/137 with zero skipped tests. The latter includes real authenticated
  Web/service/PostgreSQL/MAF completion and stable replay plus real scope, payload-conflict,
  and cancellation adversarial cases. All affected Release builds pass at 0W/0E.
- Final CodeAnalytics snapshot `snap-20260821072204-bf844210` reports zero project cycles,
  unchanged project references, and exactly the same two non-project cycles as SB04
  snapshot `snap-20260821044013-44e660f5`.
- Governed evidence under `proof/SB05` records exact selectors, raw green and honest red
  artifacts, source/no-bypass/safe-projection/no-migration assertions, production
  composition, static validation, architecture review, and the source/schema/API freeze.

Unlock decision: every CP-WB3 item was Proven and SB06 entered. Downstream E2E and broad
diagnostics did trigger the declared reopen rule; the affected SB03/SB04/SB05 claims were
repaired and recorded in append-only supplements before the final FG-01 freeze. Frozen
parent evidence was not rewritten.

## CP-WB4 — final frozen gate and closeout

Status: **Proven / Pass**. SB06 and the parent bundle are closed.

Satisfied evidence:

- all 17 E2E scenarios are Proven through the real MAF/PostgreSQL/Web/service boundaries,
  with direct reconstructed-host recovery, crash windows, race, denial, corruption,
  version/topology, legacy, redaction, and no-prefix-replay cases;
- snapshot `snap-20260821092959-44e660f5` covers 9 projects and 499 documents, reports no
  blocking error or project cycle, and retains only the two unchanged baseline non-project
  cycles; the subsequent narrow plugin/runtime/test repairs add no project reference;
- strict source review approved the public-only implicit plugin export boundary and the
  internal typed redacted-acceptance seam. Native compatibility fails during construction
  if that capability is missing; legacy no-checkpoint compatibility remains supported;
- cancellation transition logic is a cohesive top-level helper rather than a partial or
  weakened line-budget exception. The in-memory operation store is back under the existing
  checkpoint budget, and runtime lifecycle reproof passes 14/14;
- safe public workflow-event text excludes unresolved native source identifiers while
  retaining them only as internal payload metadata; SB03 and SB05 append-only supplements
  preserve the reopened ownership record;
- PostgreSQL completion precision and exact-expiry fencing have direct red/green proof and
  an append-only SB04 supplement; production PostgreSQL remains the authoritative CAS and
  durability boundary;
- the valid frozen FG-01 checkpoint ran once from `2026-08-21T12:52:49.8229732Z` through
  `2026-08-21T13:59:43.2785414Z`: product and Stable builds passed 0W/0E and the exact
  Stable selector passed 8,471/8,471 with zero failed/skipped;
- source HEAD `af425ac371b251447f9858b15476092531c686da`, Components
  `8372c1d55f21b349f8e859470b02eeb4421e96ca`, and FileTools
  `c95dd07208a6d48724443317cdc6cfe67a13020a` remained pinned throughout the accepted gate;
- architecture, security/source-isolation, package, documentation, traceability, input,
  and closure surfaces agree on Proven / Pass.

The accepted guarantee remains exactly-once response acceptance and deduplicated
participating governed effects. Direct coordinator recovery on a reconstructed host is not
misrepresented as automatic hosted-worker startup. In-memory stores remain process-local,
non-durable, and non-snapshot-isolated.

Any later semantic source/schema/API/fixture edit invalidates this freeze and must reopen
the owning subbundle plus affected downstream proof.
