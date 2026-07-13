# Architecture Checkpoints

## Checkpoint After SB01

- Confirm package references and direct MAF package owners.
- Confirm branch and clean/dirty state.
- Confirm any pre-existing build warnings or failures.
- Confirm CodeAnalytics snapshot id or record unavailability.

Unlock decision:

- `SB02` can start only if the package surface and baseline failures are recorded.

## Checkpoint After SB02

- Confirm stable MAF package updates are package-only.
- Confirm A2A/Mem0 decisions were based on NuGet CLI output.
- Confirm no unrelated package family was updated.
- Confirm central package management was not introduced.

Unlock decision:

- `SB03` can start only after restore succeeds or the failure is strictly package-compatibility work.

## Checkpoint After SB03

- Dependency graph review: no new project references unless separately justified.
- Partial-class policy review: no new final partial class split; temporary partials require removal plan.
- Testability review: new adapter/helper behavior has direct unit tests.
- Old-class shrink proof: N/A unless implementation moved behavior; if moved, source assertion must show old owner no longer owns it.
- Governance review: approvals/finalizers/provider gates/session/context/telemetry paths remain.

Unlock decision:

- `SB04` must pass before `SB05` starts.

## Checkpoint After SB04

- Diff review confirms bounded changes.
- Source scans confirm no process direct provider or API route expansion.
- Dependency direction remains valid.
- Pattern selection records are updated if implementation introduced new adapters/factories/builders.
- Testability contracts are updated if implementation introduced new helper types.

Unlock decision:

- `SB05` can start only when `SB04` records `Pass` or an explicit accepted blocker with user approval.

## Checkpoint After SB05

- Focused tests cover package-update risks.
- Replacement tests preserve the original validation intent.
- Optional service/UI smokes are run or skipped with exact environment reason.

Unlock decision:

- `SB06` can start only when focused validation is meaningful and documented.
