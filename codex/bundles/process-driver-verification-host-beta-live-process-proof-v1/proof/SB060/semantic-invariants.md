# SB060 Semantic Invariants

## SB060_INV_001 Process Docs Describe Operator Verification Readback
- Source raw note: SB058 requires Processes README and runbook updates.
- Expected behavior: the module README, operator runbook, and runtime ledger describe `IProcessManagerReadOnlyVerificationFacade.VerifyForReadbackAsync`, `ProcessManagerReadOnlyVerificationReadbackDto`, `auditRecords`, `observationHash`, denial category/code/message, and mutation-denial flags.
- Disallowed shallow implementation: report-only docs closure, mentioning manager diagnostics without the readback fields, or documenting a UI route that was not implemented.
- Positive proof: `bundle://proof/SB058/transcripts/process-docs-operator-readback-focused-tests.txt`, `bundle://proof/SB058/transcripts/process-readme-runbook-docs-source-assertions.txt`.

## SB060_INV_002 Driver Host Beta Migration Guide Keeps Runtime Host Not Approved
- Source raw note: SB059 requires driver host beta migration guide updates.
- Expected behavior: the migration text keeps read-only verification migration available, keeps runtime-host status `Not approved`, and blocks runtime host, registry, selector, DI registration, manager commands, scheduler/workflow hooks, external calls, workspace/storage writes, and process mutation.
- Disallowed shallow implementation: optimistic beta wording that implies production verification host registration is ready, or backlog text that treats diagnostics/audit readback as execution approval.
- Positive proof: `bundle://proof/SB059/transcripts/driver-host-beta-migration-guide-source-assertions.txt`.
- Red-team negative case: `bundle://proof/SB060/transcripts/red-team-docs-parity-shallow-proof-rejection.txt`.

## SB060_INV_003 Docs Parity Preserves Source Boundaries
- Expected behavior: Gate T changes only docs and a focused doc guard test; no current bundle path leaks into `src`, `tests`, or docs; changed docs/tests contain no `codex/bundles` coupling; inherited historical docs references are classified; no UI route or Playwright source changes occur.
- Disallowed shallow implementation: current-bundle path coupling, unclassified historical proof paths, UI screenshot claims for unchanged UI, or hidden runtime approval claims.
- Source scan proof: `bundle://proof/SB060/transcripts/gate-t-docs-parity-source-scan.txt`.
- Anti-stub audit: `bundle://proof/SB060/transcripts/gate-t-docs-parity-anti-stub-audit.txt`.

## Production Behavior Artifact Matrix
| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Operator readback docs | SB058 docs/test guard | Runbook and module README | Gate T focused matrix | Red-team rejects field-light diagnostics docs |
| Driver host beta migration guide | SB059 source assertions | Module README and runtime ledger | Gate T focused matrix | Red-team rejects runtime-host approval wording |
| Docs parity guard | Unit doc guard | Future docs edits | Gate T proof index | Source scan rejects forbidden approval claims |
| No UI drift | Gate T source scan | Browser validation logging | Gate T manifest | Red-team rejects screenshot claims without UI change |

## Gate Result
Gate T is semantically adequate for docs parity. The runbook, module README, and ledger now document the operator verification readback contract and runtime-host denial posture, with focused tests, source assertions, source scans, anti-stub audit, and red-team proof.
