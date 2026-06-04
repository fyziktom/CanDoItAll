# Shared QA Prompt

Review the completed subbundle as an independent senior C# architect.

Check:

- Does the implementation use MAF capabilities rather than bypassing them?
- Are workflow definitions validated before execution?
- Are plugin executors governed by schemas, permissions, approval, cancellation, retry, timeout, artifacts, and telemetry?
- Are preview and durable production paths separated by policy?
- Are tests deterministic and free of live-service secrets by default?
- Are user-managed definitions protected from managed seed refreshes?
- Did Codex update proof and `reviews/01-execution-report.md`?

Reject the subbundle if it adds hidden runtime logic in pages, seed services, or plugin code that bypasses the central workflow compiler/runtime runner.
