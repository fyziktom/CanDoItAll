# Execution proof placement

This directory now contains actual SB01-SB07 execution artifacts. See SB06/manifest.md for final focused 130 Components / 28 Unit proof and SB07 for governed integration closure. Historical failed/invalidated attempts remain labeled; they are not acceptance evidence.

Authorized execution records evidence in proof/SB01 through proof/SB07. Every phase keeps a concise manifest.md with source SHA/dirty state and sibling SHAs/mode, owned R/B rows and raw notes, P/S/U classification, changed files, chosen production contracts/constructor dependencies, exact commands/selectors, expected test names/data/counts frozen before edits, actual discovery/results/exit codes, UI composition decision, artifacts and reopen/invalidation outcome.

Standard SB01 needs baseline/characterization/reference/measurement evidence. Behavioral phases may keep exact commands/results in their report without separate full transcript manifests. They must still show semantic adequacy: literal/normalized owned note, production behavior and source, test proof, shallow-pass trap, realistic positive case, adversarial negative case and anti-stub audit.

For actual behavior-changing safeguards, capture failing-first adversarial proof and passing proof using meaningful production behavior, not a missing-type compilation failure. For preserved behavior, use baseline/after evidence; do not manufacture a failure. Record which classification applies before implementation.

## Governed SB07 manifest

Create **proof/SB07/manifest.md**; a machine-readable manifest.json may supplement it. Use portable repo:// paths for source and bundle:// paths for bundle artifacts. Machine-specific absolute paths may supplement these, not replace them.

Required fields:

- phase status, requirements/raw notes, repository/source identity, dirty-patch identity and sibling/environment state;
- portable link to proof/SB07/semantic-invariants.md or .json;
- changed-file manifest with before/after SHA-256 for all source/test/bundle files touched by the governed phase, and links to earlier owning-phase changes needed for closure;
- each required command, working directory, run label/start time, exit code and actual transcript path;
- expected and actual test methods/data/discovery and requirement/B-row mapping;
- failing-first and passing transcripts for behavior-changing safeguards inherited from their owning phases;
- production source assertions, anti-stub audit command/transcript, actual adapter/composition and downstream host evidence;
- browser actions, screenshots, console results, visual review questions and findings;
- final verifier/independent architecture review artifact that re-reads the evidence and rejects fake proof;
- artifact paths, purpose, source association and SHA-256 for final transcripts/screenshots/reports;
- stable/portability no-write results, invalidation/reopen decisions, six readiness verdicts and owned follow-ups.

## Semantic invariants and production state

For each critical invariant record ID, raw note, expected behavior, disallowed shallow implementation, positive/negative tests and transcripts, changed source/hashes, production assertions, adversarial review and downstream dependency check. Carry invariant IDs into cited transcript run labels so evidence can be matched to the contract.

When claiming production state/events/results such as loaded session identity, selected context or command outcomes, include a producer/consumer/lifecycle/negative-proof matrix. Exercise production producers; manually seeding a fake session cannot alone prove real loading, version propagation or host lifecycle. This requirement does not introduce a new runtime event framework.

Write governed command output under proof/SB07/transcripts or link valid earlier phase artifacts. A report cannot cite a missing transcript as executed proof. Missing required manifests, negative/positive evidence, hashes or verifier artifacts block governed closure.

## Integrity and data handling

Verify every path/hash/source association and reject stale evidence. Root MANIFEST.sha256 authenticates bundle documents; it does not substitute for runtime proof.

Store bounded masked logs and disposable fixture identifiers. Do not retain credentials, connection strings, full database dumps, runtime caches or packages. Source rollback cannot undo validation mutations; record fixture cleanup separately.

The bundle-local ignore rule exposes the reviewed SB01-SB07 proof, while redundant raw portability scan files remain local; the complete final JSON scan is delivered as gzip. During authorized execution, deliberately include required reviewed proof artifacts through a scoped tracking rule or explicit file staging; never let ignored/missing evidence masquerade as delivered closure.
