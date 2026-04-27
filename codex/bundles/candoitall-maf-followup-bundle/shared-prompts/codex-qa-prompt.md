# Codex QA prompt — verify MAF follow-up implementation

You are reviewing a completed implementation of the MAF stabilization follow-up bundle. Do not assume it is correct.

Verify these invariants:

1. Runtime finalizer tools/instructions are attached only when effective mode is `Required` or `Shadow`.
2. `Disabled` finalizer mode does not attach a finalizer tool and does not append finalizer instructions.
3. Required finalizer mode still fails missing, duplicate, malformed, and invalid finalizer calls.
4. Required finalizer output replaces `ResponseText` before assistant transcript persistence.
5. Assistant final response instructions remain compatible with configured JSON schema `ResponseFormat`.
6. Tool policy blocks are represented by a dedicated policy exception.
7. Real tool failures are not mislabeled as policy blocks.
8. Workspace provider persistence/UI capability flags match central provider feature matrix behavior.
9. Provider transport is persisted/read from metadata before name-based fallback.
10. Verification docs list only tests that exist in the repository.
11. The focused hardening test filter actually discovers and runs the intended tests.
12. Repair behavior is accurately documented and tested.
13. Process-context output checks still prevent governed completion without valid structured outcome.
14. Build and focused tests pass.

Run the build/test commands from `reviews/readiness-gate.md` and report exact results. If any test names in docs do not exist, treat that as a failure.
