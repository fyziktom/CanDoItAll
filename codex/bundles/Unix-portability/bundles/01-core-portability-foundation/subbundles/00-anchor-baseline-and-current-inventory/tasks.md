# A00 tasks

## Entry checklist

- [ ] Verify exact checkout and preserve unrelated working-tree changes.
- [ ] Verify prerequisite gate evidence.
- [ ] Reproduce focused baseline/characterization.
- [ ] Confirm every source hotspot after materialization.

## A00-T01 — Anchor and preserve the checkout

- [ ] Record branch, HEAD, merge base against prepared anchor, SDK, OS/architecture, git status, submodules, and every unrelated change. Stop rather than reset, clean, or overwrite operator work.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A00-T02 — Revalidate the source-reference manifest

- [ ] Verify every exact path, classify renamed/deleted files, add newly discovered portability surfaces, and update evidence status from Search-confirmed to Inspected where applicable.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A00-T03 — Run stable baseline and host characterization

- [ ] Run restore/build/stable tests on the available Windows host and on real Ubuntu/macOS runners or machines. Capture failures without making portability edits.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A00-T04 — Generate the full portability scan

- [ ] Run the supplied scanner, review every hit, and classify by logical path, physical path, filesystem, secret, process, desktop, hosting, test, or external dependency.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A00-T05 — Build the path-field inventory

- [ ] Map every persisted/configured/runtime string that can represent a route, logical locator, physical path, executable, URL, script, or opaque command. Record writer, reader, comparer, migration owner, and trust boundary.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A00-T06 — Build the persistence and migration inventory

- [ ] Map database columns, control-plane JSON, vault payloads, Data Protection key ring, storage tokens, runtime-node metadata, and host-bound preferences. Include backup/rollback and restart dependencies.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A00-T07 — Reconfirm architecture ownership

- [ ] Use the latest MAF refactor ADRs and project graph to approve owners for platform primitives, security, Workbench presentation, Manager supervision, MAF runtime, Plugins, and Processes semantics.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A00-T08 — Issue Gate C0

- [ ] No implementation starts until all P0/P1 findings are classified, the source anchor is current, baseline evidence is stored, and the revised work graph is internally consistent.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [ ] Every owned requirement has evidence and status.
- [ ] Focused validation and required stable regression pass.
- [ ] Source references/findings/ADRs/traceability are current.
- [ ] Artifacts are redacted.
- [ ] Required independent reviewers record GO.
- [ ] Handoff identifies the next eligible subbundle or conditional stop.
