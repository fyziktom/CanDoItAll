# Assumptions And Risks

## Working Assumptions
- The previous stabilization bundle evidence is available under `repo://codex/bundles/process-runtime-stabilization-release-closure-v1`.
- `OPENAI_API_KEY` may be present in the process environment, but its value must never be printed.
- PostgreSQL-backed tests may depend on local test infrastructure and must be classified as pass, skip, or blocker from actual output.

## Critical Path Risks
- Live provider failure could block merge readiness even when deterministic runtime evidence is green.
- A UI rerun could expose an operator-readback regression that earlier proof missed.
- Boundary scans could find accidental runtime extraction, driver registration, reflection discovery, or scheduler driver hooks.

## Validation Risks
- Skipped live OpenAI tests must not be counted as live proof.
- Code-first ratio must not be used as the sole functional runtime blocker.
- Prose-only proof is insufficient for critical subbundles; transcripts and manifests must exist.

## Reopen Triggers
- Reopen SB01 if release classification still conflates advisory proof churn with functional runtime failure.
- Reopen SB02 if the live smoke skips, leaks secret values, or fails without exact classification.
- Reopen SB03 if deterministic proof uses `SuppressAutomationDispatch=true` for automation evidence.
- Reopen SB04 if Playwright proof lacks completed status, artifacts, completed/skipped steps, or runtime-host readback classification.
- Reopen SB05 if scans find execution-capable drivers, fallback selectors, reflection discovery, or Process Core leakage.
