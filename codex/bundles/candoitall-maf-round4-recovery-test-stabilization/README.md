# CanDoItAll MAF Round 4 Recovery, Rework, Tool Policy, and Test Stabilization Bundle

This bundle is an execution-grade implementation package for Codex. It is based on the attached repository snapshot after the claimed round 3 implementation. The snapshot evidence does not match several claims in the Codex report, so the first phase is to re-establish repository truth and then finish the missing implementation.

Do not treat this bundle as advisory only. Implement the required changes, add behavior-level tests, and produce a truthful execution report with command output summaries.


## Critical findings

1. A real-looking OpenAI API key is still committed in `src/CanDoItAll.Web/appsettings.json` at line 33. The value must not be copied into logs, docs, bundles, prompts, or test output. Treat it as compromised and rotate/revoke it outside the repository.
2. The Codex round 3 report claims files and features that are not present in the attached snapshot: `SecretScanningTests.cs`, `AgentRecoveryModels.cs`, `AgentRecoveryModelsTests.cs`, typed rework packets, proof fingerprints, and recovery ledgers were not found.
3. Process mutation tools are still classified as `Read` by the generic tool policy because `AgentToolInvocationPolicyMetadata.IsMutationTool(...)` only knows workspace mutation tools.
4. Recovery still retries the current process step with a textual directive and a fresh session. It does not yet implement typed recovery decisions, rework packets, proof fingerprints, or efficient QA repair continuation.
5. The broad test suite is not green and contains clear infrastructure problems: Release/no-build Playwright fixtures launching Debug output, hardcoded Windows repo roots, hardcoded Debug MCP assembly paths, long-running live-process tests without a clear category/gate, and brittle component tests.

## Bundle structure

- `audit/` — snapshot audit and evidence map.
- `analysis/` — Codex report mismatch, process recovery analysis, and test failure taxonomy.
- `architecture/` — target architecture for recovery/rework and test stabilization.
- `requirements/` — normalized implementation requirements.
- `subbundles/` — focused execution packages for Codex.
- `shared-prompts/` — master and QA prompts.
- `reviews/` — release readiness gate.
- `scripts/validate_bundle.py` — structure and secret-safety validation for this bundle.

## Priority order

1. Emergency secret removal, rotation guidance, and secret scanning.
2. Snapshot integrity checks so Codex cannot claim missing files/features exist.
3. Process tool policy classification and approval/finalizer-sequence significance.
4. Typed recovery decisions and typed rework packets.
5. Efficient context selection and session boundary policy.
6. QA return to rework loop.
7. Proof fingerprints and receipt reuse.
8. Retry ledger, backoff, and loop control.
9. Full test suite stabilization, including Release/no-build Playwright and MCP path fixes.
10. Documentation truthfulness and execution-report verification.

## Non-negotiables

- Never print or re-commit the exposed provider key.
- Do not claim a class/file/test exists unless it exists in the final snapshot.
- Do not claim `dotnet test CanDoItAll.slnx --configuration Release --no-build` is green unless it actually passes.
- If some tests are intentionally excluded from the default green gate, move them behind explicit categories and document why.
- Add behavior-level tests for tool policy, recovery decisions, proof reuse, and test harness fixes. Static grep tests alone are insufficient.
- Keep source-code comments in English.
