# Process Hardening Target

## Problem

The current workflow can still pass because a report contains plausible semantic evidence labels. The validator is not artifact-backed.

## Required Proof Model

Each critical subbundle must create a proof manifest under `proof/SBxx/manifest.json` or `proof/SBxx/manifest.md` containing:

- subbundle id and requirement ids,
- raw notes covered,
- changed production files with before/after hashes,
- changed test files with before/after hashes,
- command transcript paths,
- failing-first transcript path for negative tests,
- passing transcript path after implementation,
- source-level assertions to inspect,
- anti-stub scan results,
- red-team verdict.

## Required Validator Behavior

Completed-stage validation must read the proof manifests and fail if:

- a cited transcript path does not exist,
- a cited test name does not appear in the transcript,
- the failing-first transcript did not fail for the expected reason,
- the passing transcript did not pass after implementation,
- a changed-file hash is missing,
- source-level assertions reference a file that does not exist,
- the anti-stub scan is missing,
- a critical subbundle is completed without red-team verification.

## Required Workflow Skill Behavior

The workflow skill must explicitly block dependent subbundles until the proof manifest for the prerequisite subbundle is validated. The agent must not continue from report prose.
