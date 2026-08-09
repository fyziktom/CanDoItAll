# A00 tasks

## Entry checklist

- [x] Verify exact checkout and preserve unrelated working-tree changes.
- [x] Verify prerequisite gate evidence.
- [x] Reproduce focused baseline/characterization.
- [x] Confirm every source hotspot after materialization.

## A00-T01 — Anchor and preserve the checkout

- [x] Record branch, HEAD, merge base against prepared anchor, SDK, OS/architecture, git status, submodules, and every unrelated change. Stop rather than reset, clean, or overwrite operator work.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A00-T02 — Revalidate the source-reference manifest

- [x] Verify every exact path, classify renamed/deleted files, add newly discovered portability surfaces, and update evidence status from Search-confirmed to Inspected where applicable.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A00-T03 — Run stable baseline and host characterization

- [x] Run restore/build/stable tests on the available Windows host and on real Ubuntu/macOS runners or machines. Capture failures without making portability edits.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A00-T04 — Generate the full portability scan

- [x] Run the supplied scanner, review every hit, and classify by logical path, physical path, filesystem, secret, process, desktop, hosting, test, or external dependency.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A00-T05 — Build the path-field inventory

- [x] Map every persisted/configured/runtime string that can represent a route, logical locator, physical path, executable, URL, script, or opaque command. Record writer, reader, comparer, migration owner, and trust boundary.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A00-T06 — Build the persistence and migration inventory

- [x] Map database columns, control-plane JSON, vault payloads, Data Protection key ring, storage tokens, runtime-node metadata, and host-bound preferences. Include backup/rollback and restart dependencies.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A00-T07 — Reconfirm architecture ownership

- [x] Use the latest MAF refactor ADRs and project graph to approve owners for platform primitives, security, Workbench presentation, Manager supervision, MAF runtime, Plugins, and Processes semantics.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A00-T08 — Issue Gate C0

- [x] No implementation starts until all P0/P1 findings are classified, the source anchor is current, baseline evidence is stored, and the revised work graph is internally consistent.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [x] Every owned requirement has evidence and status.
- [x] Focused validation and required stable regression pass.
- [x] Source references/findings/ADRs/traceability are current.
- [x] Artifacts are redacted.
- [x] Required architecture/subbundle validation records GO.
- [x] Handoff identifies the next eligible subbundle or conditional stop.
