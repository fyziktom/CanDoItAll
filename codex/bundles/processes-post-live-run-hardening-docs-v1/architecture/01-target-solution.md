# Target Solution

## Architecture Target

Processes remains the lifecycle owner. Workflows and agents execute process steps underneath Processes and report artifacts, diagnostics, approvals, and runtime evidence back to process-owned services.

## Service Boundary Targets

- Artifact status projection: one typed mapping for validation, read models, health, recovery, API, and UI.
- Artifact identity and storage: one typed service for projection identity, content hash, dedupe, and stale-record precedence.
- Output grounding: one testable service for project-structure target extraction, scoring, confidence, and final delivery proof requirements.
- Run folder projection: one explicit policy for run roots, generated product roots, external output roots, and ignored receipt internals.
- Manager resolution: one resolver that prefers configured and selected-run assignments, then capability/tag signals, with text fallback only as explainable last resort.
- Proof harness: named deterministic slices instead of one timeout-prone broad filter.

## Validation Target

Each critical runtime behavior must have an adversarial negative test, a semantic positive test, a source assertion, an anti-stub audit, and transcript-backed proof under `proof/SBxx/` before downstream phases rely on it.
