# QA Prompt

Validate the closure bundle from artifacts, not intention.

## Checks
- Confirm skipped live OpenAI output is not counted as live proof.
- Confirm code-first ratio is classified as advisory unless it exposes missing source/test evidence.
- Confirm deterministic tests use production launch/outbox/dispatch/finalizer paths.
- Confirm Playwright screenshots show completed run state, artifacts/evidence, completed/skipped steps, and runtime-host readback classification.
- Confirm boundary scans show no Process Core leakage, fallback selector, reflection discovery, self-registration, execution-capable drivers, or hidden scheduler driver hooks.
- Confirm raw note closure cites transcripts, source files, browser screenshots, or proof manifests.
