# SB16 Semantic Invariants

## Invariants

- Invariant ID: `SB16-INV-001`
- Source raw note: RN02 - architecture map and service-boundary cleanup after runtime hardening.
- Expected behavior: Runtime policies hardened in SB03-SB08 remain behind typed services, and no broad runtime regression is introduced before final red-team closure.
- Disallowed shallow implementation: inventing a new abstraction with no boundary value, making docs-only claims without runtime tests, hiding duplicated logic in partials, or closing while artifact status, output grounding, manager resolution, identity hashing, or health recovery diverge between consumers.
- Failing-first test: bundle://proof/SB16/transcripts/failing-first.txt records an adversarial duplicate-helper search that exits 1 for the old private helper shapes.
- Passing test: bundle://proof/SB16/transcripts/passing.txt records the clean entry gate, isolated integration build, and 37 passing focused runtime-service tests.
- Changed source files: none; this is a refactor checkpoint. Audited runtime and test hashes are recorded in bundle://proof/SB16/transcripts/changed-file-hashes.txt.
- Production assertions: artifact status mapping is consumed by read models and health; external-target grounding is consumed by prompts, metadata, project paths, and validation; manager resolution is consumed by manager chat and observation services; identity hashing is consumed by artifact recording and invariant auditing.
- Red-team negative case: a future refactor cannot reintroduce private one-off status mapping, grounding target parsing, assigned-manager selection, or health recovery logic without failing the duplicate-helper audit and focused tests.
- Downstream dependency check: SB18 can use SB16 as the no-regression checkpoint for runtime service boundaries.

## Production Behavior Artifact Matrix

| artifact | producer | consumer | lifecycle | negative |
| --- | --- | --- | --- | --- |
| Typed runtime service boundaries | `ProcessArtifactStatusProjectionService`, `ProcessArtifactIdentityService`, `ProcessExternalTargetGroundingService`, `ProcessManagerAgentResolver`, and `ProcessHealthInvariantAuditor`. | Dispatch partials, read models, manager chat, operator UI loaders, and runtime invariant audits. | Updated only when a policy boundary changes; consumers call the service instead of copying rules. | Duplicate-helper source audit in `bundle://proof/SB16/transcripts/failing-first.txt`. |
| Runtime no-regression test slice | Integration test project. | Runtime maintainers and SB18 final red-team. | Built once to an isolated SB16 output directory and run with `--no-build`. | Passing proof in `bundle://proof/SB16/transcripts/passing.txt`. |
| Source-boundary evidence | `rg` source assertions. | Bundle closure validator and human reviewers. | Captures current producers and consumers for each service boundary. | Anti-stub audit in `bundle://proof/SB16/transcripts/anti-stub-audit.txt`. |

## Validation

- Failing-first/adversarial proof: bundle://proof/SB16/transcripts/failing-first.txt.
- Passing proof: bundle://proof/SB16/transcripts/passing.txt.
- Source assertions: bundle://proof/SB16/transcripts/source-assertions.txt.
- Anti-stub audit: bundle://proof/SB16/transcripts/anti-stub-audit.txt.
- Changed-file hashes: bundle://proof/SB16/transcripts/changed-file-hashes.txt.
