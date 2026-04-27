# Codex QA Prompt - Round 3 Verification

You are reviewing a completed implementation of CanDoItAll MAF round 3 rework/recovery stabilization.

Verify the following without trusting the implementation summary:

1. Search for real-looking secrets. Fail if any `sk-...` key remains.
2. Confirm process mutation tools classify as `Mutation`.
3. Confirm process mutation tools are approval-governed or explicitly suppressed only in safe internal automation.
4. Confirm required-finalizer sequence validation treats process mutations after finalizer as violations.
5. Confirm typed `AgentRecoveryDecision` and `AgentReworkPacket` or equivalents exist.
6. Confirm QA rejection and manual rerun can create/use a typed packet.
7. Confirm failed sessions are not blindly replayed.
8. Confirm approval continuation keeps compatible session context.
9. Confirm build/test/browser proof reuse uses fingerprints and invalidates after relevant changes.
10. Confirm provider approval matrix matches actual installed MAF package behavior.
11. Confirm tests are behavior-level, not only static source scans.
12. Run build and tests.
13. Check docs truthfulness: no claims about tests that do not exist or were not run.

Return a concise QA report with pass/fail for each item, exact evidence, and remaining risks.
