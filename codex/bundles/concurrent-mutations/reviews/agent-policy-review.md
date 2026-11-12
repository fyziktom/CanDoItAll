# Agent/process policy architecture cross-review

Date: 2026-10-16. Reviewer: delegated agent/process architecture investigation.
Scope: `docs/architecture/concurrent-mutations/README.md`,
`04-performance-and-risk-plan.md` and `02-agent-process-policies.md`.
Read-only source investigation plus documentation changes only. No production changes,
builds, test discovery or executed tests are asserted.

## Result

The architecture is acceptable for the staged implementation direction. The first
Projects definition/transaction foundation can proceed independently. The later planning
publication lane is subject to the explicit AP-0 through AP-7 gates in the policy design;
architecture acceptance does not mark those implementation/evidence gates complete.

The default is resolved: `StepSnapshot`, `RequireCurrent`, `PauseForReview`, zero
automatic replans for governed planning with a durable review host. At the owner write
boundary stale publication is always rejected with no effect. Pause is the host's
durable response after releasing resources; fail is an explicitly chosen terminal
response. A stateless API supports fail only unless it genuinely persists continuation.
No API/editor/model preference can reduce an owner-mandated current-input requirement.

## Findings addressed in the policy document

| Severity | Previous ambiguity / unsafe assumption | Resolution |
| --- | --- | --- |
| P1 | “Pause” could be implemented by waiting with an open request/claim/transaction, contrary to README invariant 9. | Specified durable review-required state after all resources release; unsupported stateless pause rejects. |
| P1 | Policy precedence could be read as letting an invocation override weaken owner freshness. | Server derives mandate/supported modes from durable owner data, rejects untrusted effective-policy/mandatory-fact substitutions, persists resolved policy, and revalidates at publication. |
| P1 | Settings UI without persistence/runtime wiring could appear complete. | Added minimal AP-1 through AP-7 code integration sequence covering Workspace settings owner, Agent metadata/editor/copy/save, Process authoring/compiled assignment/prepared launch, Workflow immutable version/checkpoint, actual tool seam and runtime response. Controls remain unavailable until enforcing consumer is wired. |
| P1 | Existing Workspace settings whole-save should not be assumed to protect concurrent policy edits. | New policy settings require original owner-settings revision; this is a separate owner gate, not Projects content revision. An explicitly unavailable workspace override is permissible during rollout; task closure still requires the full settings experience. |
| P2 | 02's numerical pilot limits could contradict 04's prohibition on silently rejecting existing projects. | Restricted candidate limits to the new bounded planning operation, required retained-data preflight and measurement before enablement, and preserved existing editor/process/workflow limits. |
| P2 | A broad task witness could contradict README's “unrelated tasks must not invalidate” statement. | Defined the first material collection as the selected project's complete canonical planning-task set. Other projects/logs/activity do not invalidate. Future subset operations must explicitly document conservative invalidation or implement a narrower witness. |
| P2 | Healthy complete-product graph requirement could unnecessarily prevent a source-local cleanup repair while CodeAnalytics has unrelated diagnostics. | Architecture accepts concrete current boundaries with limitation recorded; healthy affected evaluated graph required before reference changes and at integration, not before independent cleanup. |
| P2 | Replan deadline versus human pause was underspecified. | Active planning work is accumulated durably; human waits do not spend that counter, while absolute run deadline, basis age and claim expiry still apply. Expired basis requires new admission; old claim cannot be rewritten. |

## Cross-document agreement

- README target/source/authority/operation separation and 02's three policy axes agree.
  A pinned computation cannot bypass conditional target write or current authority.
- Existing tool journal/approval digest, Process claims, immutable definitions and owner
  receipts remain authoritative. The proposed basis record does not replace them.
- RuntimeCore/Processes keep provider-neutral dependencies; Projects freshness and
  Workbench task semantics stay with their owners. No general mutation engine is needed.
- 04's owner-batched validation, no polling per fact, request-size validation before
  locks and prohibition of remote/model work in transactions apply to all new modes.
- Unknown effect is reconciliation work, never semantic conflict/replan work. Known
  committed effects remain committed when cleanup or response fails.
- Legacy history may be read; missing original basis cannot be reconstructed from
  current rows to make a protected mutation executable.

## Remaining gated evidence, not design contradictions

1. AP-0 must prove every enabled publisher and every source/task-set writer participates.
   CRM's existing planning read tool is not a publication implementation.
2. Full lock-class order must include source/catalog, Process claim, Projects, Workbench
   and any actual storage participant. Two processes and deterministic rollback races
   are mandatory for this publication claim.
3. Exact Process/Workflow/Agent persisted schema and retention migration must be reviewed
   before enabling resume. User settings cannot reset consumed budgets or old approvals.
4. The terminal detail/deduplication horizon and restore-incarnation fence remain explicit
   lifecycle gates. A process-local profile generation is not yet proof of restore safety.
5. Candidate batch/age/cost bounds need representative retained-data and performance
   evidence before production enablement. There is no measured capacity claim.
6. Snapshot `snap-20261016142521-bf7089bf` loaded four scoped projects and 361 documents
   but has 187 diagnostics and blocking load errors. Use its positive source evidence,
   not absence of references, as proof. Inspect/evaluate the changed graph separately.

## Measurement caution for the performance plan

The initial 100-operation samples are suitable for investigation, but p99 at that size
is effectively a single tail observation. Report it descriptively with sample count and
variation; do not treat it as a stable production p99 estimate or an absolute SLO. The
plan currently uses p95/throughput investigation thresholds, so this is not a blocker.
Increase samples only when tail behavior or a proposed release SLO actually needs proof.

## Deterministic verification targets

02 now names proposed tests for conservative defaults, untrusted client weakening,
stateless pause rejection, restart/nested retry budgets, concurrent settings editors,
immutable definition policy, legacy checkpoint refusal, objective change, task phantom,
cross-project independence, reapproval, pause resource release, unknown-effect recovery,
retired claims, two-host enforcement, retention and native settings/review UI.
These are specification names, not discovered passing tests. Existing regression seeds
and actual source method names remain documented separately.

Review verdict: no remaining contradiction blocks the staged architecture direction;
AP-0/AP-1 gates must precede the planning source implementation, and each subsequent gate
must pass before its feature is enabled. This verdict does not authorize treating the
entire concurrency initiative or its later publication guarantees as complete.
