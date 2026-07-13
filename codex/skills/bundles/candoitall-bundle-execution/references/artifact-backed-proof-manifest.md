# Artifact-Backed Proof Manifest

Governed-proof subbundles must leave machine-checkable proof artifacts, not only execution-report prose. Standard and Behavioral subbundles do not require this manifest unless the user explicitly asks for it.

## Required Manifest

Create `proof/SBxx/manifest.md` before closing each Governed subbundle. The manifest must include:

- subbundle id, status, owned requirements, and raw notes;
- a portable semantic invariant contract path, `bundle://proof/SBxx/semantic-invariants.md` or `.json`;
- changed-file manifest with before and after SHA-256 hashes for source, test, skill, and bundle files touched by the subbundle;
- command transcript paths for every required validation command;
- failing-first transcript paths for adversarial tests that must fail before production changes;
- passing transcript paths for the same tests after implementation;
- source-level assertion evidence that the intended production behavior exists outside test fixtures;
- anti-stub audit command and transcript covering production `TODO`, `NotImplemented`, fixture-specific branching, and template-only output;
- browser, screenshot, or host proof paths when the subbundle changes user-visible or host-visible behavior;
- downstream smoke proof when the subbundle is a critical foundation for later phases;
- red-team or verifier artifact path for final closure subbundles.

Use `repo://relative/path` for repository files and `bundle://relative/path` for bundle-owned proof artifacts. Native absolute paths may appear as local context, but a Governed manifest must not rely on machine-specific absolute paths as its only durable references.

When a critical subbundle introduces or relies on a production signal, state, record, or event, the manifest must include:

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `ArtifactName` | `repo://...` or `bundle://...` transcript proving the production emitter | `repo://...` or transcript proving production consumption | scheduler/review/cleanup path that runs it automatically | failing-first/adversarial test proving retrieval, consumer-only code, or manual test seeding is insufficient |

Positive feature tests must not manually seed production-only signals unless the test is explicitly a migration, backfill, or validator fixture. Otherwise the test must exercise the production producer.

## Semantic Invariant Contract

Create `proof/SBxx/semantic-invariants.md` or `.json` for each critical subbundle. Each invariant must include:

- invariant id;
- source raw note;
- expected behavior;
- disallowed shallow implementation;
- failing-first test and transcript;
- passing test and transcript;
- changed source files and hashes;
- production assertions;
- red-team negative case;
- downstream dependency check.
- production behavior artifact matrix when the invariant names a production signal, state, record, or event.

The invariant id must appear in at least one command transcript cited by the manifest so the completed-stage validator can prove that the transcript is tied to the invariant contract.

For skill-installation subbundles, the manifest must also include the repository skill path, active Codex skill-root path, and before or after SHA-256 hashes for both copies. A subbundle that changes skill instructions is not complete until the active skill root has been synchronized and reopened by the agent.

## Transcript Rules

Write command output to files under `proof/SBxx/transcripts/`. A transcript must show:

- command line;
- working directory;
- start time or run label;
- exit code;
- output sufficient to prove pass or fail.

Do not cite a command in `reviews/01-execution-report.md` unless the transcript exists or the subbundle explicitly records why transcript capture was impossible.

## Blocking Rule

A Governed subbundle is not complete when the manifest is missing, when a manifest path points to a missing file, when failing-first proof is absent for behavior-changing work, or when only prose/table evidence exists.

If the manifest cannot be produced, stop the phase, mark the subbundle `Blocked`, and repair the bundle or tooling before downstream work starts.

Final bundle closure is also blocked until the red-team or verifier artifact re-reads the proof manifests, rejects fake proof fixtures, and records whether every critical subbundle has artifact-backed negative and positive evidence.
