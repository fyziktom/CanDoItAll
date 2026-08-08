# B01 tasks

## Entry checklist

- [ ] Verify exact checkout and preserve unrelated working-tree changes.
- [ ] Verify prerequisite gate evidence.
- [ ] Reproduce focused baseline/characterization.
- [ ] Confirm every source hotspot after materialization.

## B01-T01 — Define the canonical execution plan

- [ ] Use immutable executable/argv/working-directory/environment/timeout/output/boundary/side-effect metadata. Display command text is a projection only.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B01-T02 — Consolidate low-level process semantics

- [ ] Make LocalWorkspaceProcessHost or a smaller extracted primitive authoritative. External tools and injected plugin runtimes wrap/reuse it rather than copy Process code.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B01-T03 — Implement host-correct executable resolution

- [ ] Handle explicit paths, PATH order, Windows PATHEXT, Unix execute bits and shebang expectations, case behavior, symlinks, missing/ambiguous candidates, and stable diagnostics.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B01-T04 — Implement environment semantics

- [ ] Preserve OS key comparison, define safe common and OS/tool-specific inherited sets, require explicit secret bindings, and keep values out of receipts.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B01-T05 — Prove cancellation and process-tree cleanup

- [ ] Characterize existing Kill(entireProcessTree). Add TERM/grace/KILL or native process-group/Job Object behavior only where tests prove it is required.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B01-T06 — Unify lifecycle ownership

- [ ] Ensure one process host/registry instance per workspace/runtime aggregate, one disposal path, and explicit kept-alive process leases.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B01-T07 — Normalize/redact receipts

- [ ] Record logical paths and approved environment names, cap stdout/stderr, redact sentinel secrets, and report actual isolation strength rather than aspirational sandbox claims.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B01-T08 — Remove neutral Windows suffix assumptions

- [ ] No `.exe/.cmd/.bat` probing or case-insensitive allowlist remains in OS-neutral code except explicit compatibility fixtures.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B01-T09 — Issue execution foundation gate R1a

- [ ] Independent runtime/security review must accept cancellation, ownership, environment, executable, and receipt behavior.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [ ] Every owned requirement has evidence and status.
- [ ] Focused validation and required stable regression pass.
- [ ] Source references/findings/ADRs/traceability are current.
- [ ] Artifacts are redacted.
- [ ] Required independent reviewers record GO.
- [ ] Handoff identifies the next eligible subbundle or conditional stop.
