# Artifact-Backed Proof Manifest

Critical subbundles must leave machine-checkable proof artifacts, not only execution-report prose.

## Required Manifest

Create `proof/SBxx/manifest.md` before closing each critical subbundle. The manifest must include:

- subbundle id, status, owned requirements, and raw notes;
- changed-file manifest with before and after SHA-256 hashes for source, test, skill, and bundle files touched by the subbundle;
- command transcript paths for every required validation command;
- failing-first transcript paths for adversarial tests that must fail before production changes;
- passing transcript paths for the same tests after implementation;
- source-level assertion evidence that the intended production behavior exists outside test fixtures;
- anti-stub audit command and transcript covering production `TODO`, `NotImplemented`, fixture-specific branching, and template-only output;
- browser, screenshot, or host proof paths when the subbundle changes user-visible or host-visible behavior;
- downstream smoke proof when the subbundle is a critical foundation for later phases;
- red-team or verifier artifact path for final closure subbundles.

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

A critical subbundle is not complete when the manifest is missing, when a manifest path points to a missing file, when failing-first proof is absent for behavior-changing work, or when only prose/table evidence exists.

If the manifest cannot be produced, stop the phase, mark the subbundle `Blocked`, and repair the bundle or tooling before downstream work starts.

Final bundle closure is also blocked until the red-team or verifier artifact re-reads the proof manifests, rejects fake proof fixtures, and records whether every critical subbundle has artifact-backed negative and positive evidence.
