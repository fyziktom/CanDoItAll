# Planning review read boundary

Status: implemented with 67 passing correctness cases; isolated capacity fails and
remains open. See the [checkpoint](../validation/2026-10-21-review-read-boundary.md).

## Evidence and responsibility

The frozen P07 activity probe passed three diagnostic cases with five observations
each. The complete GET remained about 0.9–1.3 seconds and 80–82 MB of process
allocation. Process SQL execution was about 128–182 ms. The probe is not a latency
qualification. A second, unchanged-source diagnostic run passed three cases while
sampling managed stacks of its owned test host. Both runs had zero new surviving
process, database-name or temporary-directory deltas.

The sampled stacks establish that `ProjectTaskPlanProcessReviewReader` constructs
`CanDoItAllAgentWorkspaceFactory` and MAF capability dependencies, including workflow
executor descriptors and plugin availability, while serving a GET. The trace also
contains repeated seed normalization. Its inclusive timings include setup and async
continuations; they do not establish disjoint costs or precise per-request allocation.

Separate inherited-run disclosure from execution composition first. Core owns the
background-journal disclosure check and unique inherited participant selection.
Workbench owns the Process-specific disclosure checks, presentation and original
parent consistency. The Agent module owns current-profile store and journal wiring.
MAF, Workflow executors, plugins and Memory are execution dependencies, not read
dependencies. No project reference, singleton lifetime or authorization cache is added.

## Minimal design

Extract the existing inherited disclosure and participant-selection logic into one
internal Core owner. The existing coordinator delegates to it so startup, execution
and review continue to use identical rules. Add a small concrete Core run reader
over the existing execution-run store, journal and registered inherited participants.
It returns the actual retained detail only after disclosure succeeds. This needs no
new interface: the existing store and participant ports already provide test seams.

Register the reader in the Agent module with the same scoped store, current profile,
generation, background policies and clock. Workbench uses it for both initial detail
and final revision recheck. Preserve manager/source checks, profile fences, Process
observation, retained approval consistency, original intent bytes and HTTP mapping.
Keep the ordinary execution facade available to its existing callers.

Rejected alternatives: a singleton workspace would retain scoped dependencies and
change disposal/profile ownership; bypassing journal disclosure would weaken access;
caching authorization across requests would miss revocation. A historical-file read
alone does not supply the existing detail/approval and current-disclosure contract.
Do not optimize catalog normalization until this change has separate measurements.

## Risks and acceptance

- No divergence between ordinary coordinator reads and the new read owner. Shared
  rejection paths must cover missing/unsupported/ambiguous participants and denied
  background authority.
- Missing/legacy/non-inherited runs cannot become authorized through the new reader.
- A profile, source, manager or child change still invalidates the original observation.
- GET must work while creation of the execution workspace factory is forbidden after
  host startup. This detects accidental dependency regression through DI.
- Keep the original latency threshold. Run all six unprofiled shapes with all five
  samples, exact expectations, unchanged child revision and no additional provider
  invocation or Project writes. A failing shape remains a failed capacity gate.
- Re-run inherited owner/coordinator and actual HTTP review/restart/recovery cases.
  Confirm discovery before execution; build affected Core, Agent module, Workbench,
  Web and test outputs serially in standard Release. Enforce full portability without
  rewriting an unchanged baseline, verify frozen identities and reconcile leases.

Before snapshots: Core/Workbench `snap-20261021185512-26594287` (1,958 types,
15,980 members); Agent module `snap-20261021185717-99e80758`. The former project
closure excludes the Agent module, which is why the second scope is explicit.
Workspace diagnostics are retained and direct builds must validate compilation.
Compare project edges, cycles, owner members and findings after implementation.

Parent transitions, lost-request reconciliation, executor activation, Workflow,
settings/UI, remaining writers and sustained/broad closure remain subsequent work.
SPA stays on its separate branch and is not merged into main.
