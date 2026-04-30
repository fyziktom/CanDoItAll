# Codex Master Prompt — Round 4 Recovery, Rework, Tool Policy, and Test Stabilization

You are a senior .NET architect, Microsoft Agent Framework engineer, and test-infrastructure engineer.

You are working on the attached CanDoItAll repository snapshot. The previous Codex report claimed round 3 was implemented, but the snapshot contradicts several claims. Your first job is to verify repository truth, then implement the missing functionality and stabilize the test suite.

## Non-negotiable rules

- All source-code comments must be in English.
- Never print, commit, or include raw secrets. If you find a secret, redact it and report only the file/path/category.
- Do not claim files/classes/tests exist unless they exist in the final snapshot.
- Do not claim `dotnet test CanDoItAll.slnx --configuration Release --no-build` is green unless that exact command passed.
- If tests are excluded from the default gate, tag them intentionally and document the exact command for the default gate and each extended gate.
- Do not hide failing tests by broad skipping. Fix them, delete obsolete tests with rationale, or quarantine explicitly with owner/action.
- Prefer behavior tests over static grep tests for critical invariants.

## Phase 0 — Snapshot integrity and secret emergency

1. Verify whether `src/CanDoItAll.Web/appsettings.json` still contains a real-looking provider key. Do not print the value.
2. Remove any committed real provider key material.
3. Add secret scanning tests/scripts.
4. Document external rotation/revocation requirement.
5. Verify claimed round 3 artifacts exist or implement them.

## Phase 1 — MAF/tool-policy hardening

1. Preserve finalizer mode-aware runtime behavior and dedicated policy-block exception behavior.
2. Implement a central tool metadata catalog.
3. Classify all process tools correctly.
4. Govern process mutation tools with approval/policy rules.
5. Ensure finalizer sequence validation treats process mutation tools as significant.

## Phase 2 — Typed recovery and efficient rework

1. Implement `AgentRecoveryDecision` and `AgentRecoveryMode`.
2. Implement `AgentReworkPacket` and related models.
3. Implement explicit context strategy selection.
4. Implement proof fingerprints and proof receipt reuse/invalidation.
5. Implement retry ledger/backoff/loop control.
6. Wire QA rejection into typed rework packets and repair-step prompts.

## Phase 3 — Test suite stabilization

1. Fix Playwright Release/no-build fixtures.
2. Fix MCP stdio hardcoded repo roots and Debug assembly paths.
3. Stabilize ProjectStructure host lifetime registration.
4. Triage component/canvas failures and update/delete/quarantine obsolete tests.
5. Stabilize storage/project-structure integration tests.
6. Gate live-process/dotnetwatch tests appropriately.
7. Establish a documented default green gate.

## Phase 4 — Verification

Run at minimum:

```bash
dotnet --info
dotnet restore CanDoItAll.slnx
dotnet build CanDoItAll.slnx --configuration Release --no-restore
# Default green gate; either full suite or documented filtered gate
dotnet test CanDoItAll.slnx --configuration Release --no-build
# If full suite remains intentionally separated, also run the documented default filtered command.
# Run all targeted tests you added or changed.
git diff --check
```

If the full no-filter test command still fails, you must provide:

- exact failing tests;
- whether each is fixed, obsolete, quarantined, or intentionally extended-only;
- the default green command that passes;
- the extended commands and their status;
- rationale for every exclusion.

## Required final report

Produce `execution-report.md` with:

1. Summary.
2. Files changed.
3. New files created.
4. Tests added/updated/deleted/quarantined.
5. Commands run and outcomes.
6. Full-suite status.
7. Default-gate status.
8. Extended-gate status.
9. Secret-remediation status.
10. Remaining risks.

Do not mark the work complete until the repository evidence supports every claim.
