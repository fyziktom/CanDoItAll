# B01 tasks

## Entry checklist

- [x] Verify exact checkout and preserve unrelated working-tree changes.
- [x] Verify prerequisite gate evidence.
- [x] Reproduce focused baseline/characterization.
- [x] Confirm every source hotspot after materialization.

## B01-T01 — Define the canonical execution plan

- [x] Use immutable executable/argv/working-directory/environment/timeout/output/boundary/side-effect metadata. Display command text is a projection only.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B01-T02 — Consolidate low-level process semantics

- [x] Make LocalWorkspaceProcessHost or a smaller extracted primitive authoritative. External tools and injected plugin runtimes wrap/reuse it rather than copy Process code.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B01-T03 — Implement host-correct executable resolution

- [x] Handle explicit paths, PATH order, Windows PATHEXT, Unix execute bits and shebang expectations, case behavior, symlinks, missing/ambiguous candidates, and stable diagnostics.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B01-T04 — Implement environment semantics

- [x] Preserve OS key comparison, define safe common and OS/tool-specific inherited sets, require explicit secret bindings, and keep values out of receipts.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B01-T05 — Prove cancellation and process-tree cleanup

- [x] Characterize existing Kill(entireProcessTree). Add TERM/grace/KILL or native process-group/Job Object behavior only where tests prove it is required.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B01-T06 — Unify lifecycle ownership

- [x] Ensure one process host/registry instance per workspace/runtime aggregate, one disposal path, and explicit kept-alive process leases.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B01-T07 — Normalize/redact receipts

- [x] Record logical paths and approved environment names, cap stdout/stderr, redact sentinel secrets, and report actual isolation strength rather than aspirational sandbox claims.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B01-T08 — Remove neutral Windows suffix assumptions

- [x] No `.exe/.cmd/.bat` probing or case-insensitive allowlist remains in OS-neutral code except explicit compatibility fixtures.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B01-T09 — Issue execution foundation gate R1a

- [x] Independent runtime/security review must accept cancellation, ownership, environment, executable, and receipt behavior.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [x] Every owned requirement has evidence and status.
- [x] Focused validation and required stable regression pass.
- [x] Source references/findings/ADRs/traceability are current.
- [x] Artifacts are redacted.
- [x] Required independent reviewers record GO.
- [x] Handoff identifies B02 as the next eligible subbundle.
