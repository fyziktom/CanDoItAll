# P3-WS01 Unit and contract tests

## Objective

Add focused unit coverage for the new contracts, routing policy, capability gating, and compatibility adapters.

## Touchpoints From Workbook

| Touchpoint | Surface | Module | Scope | Required change | Proof route |
| --- | --- | --- | --- | --- | --- |
| TP-029 | Current local storage unit tests | Unit Tests | In scope | Expand into provider-contract, routing, compatibility, and capability tests. | dotnet test |
| TP-030 | Path guard tests | Unit Tests | In scope | Keep and adapt for compatibility route and access services. | dotnet test |

## Exact Source References

- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Unit/LocalFileStorageTests.cs
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Unit/WorkspacePathResolverGuardTests.cs

## Ordered Implementation Tasks

1. Add provider-contract tests reusable across FileSystem/IPFS/FTP drivers.
2. Add routing and recommendation tests that use ProjectFileSubtype and usage-purpose inputs.
3. Add capability-gating tests for local-open, preview, delete, and mutability.
4. Keep traversal/path safety regression tests alive.

## Acceptance Checklist

- Unit tests fail if provider capability declarations drift from expected behavior.
- Routing rules are deterministic and explainable under test.
- Legacy adapter coverage prevents silent regression for unchanged call sites.

## Proof Required

- Update `reviews/01-execution-report.md` with this workstream's command output or browser evidence.
- Add or update automated tests if the task changes executable behavior.
- If the task affects a UI surface, attach both desktop and narrow screenshot paths plus written findings.
- If anything is blocked, record the blocker explicitly instead of downgrading the requirement silently.

## Reopen Triggers

- A workbook touchpoint owned by this workstream has no implementation note, proof route, or linked evidence.
- Any required test command fails or is skipped.
- Any screenshot reveals clipping, overlap, overflow, inaccessible wizard navigation, or incorrect enabled/disabled actions.
- A provider is marked supported without a real protocol-backed validation path.

## Suggested Codex Prompt

```text
Implement workstream P3-WS01 only.

Objective:
Add focused unit coverage for the new contracts, routing policy, capability gating, and compatibility adapters.

Mandatory files to read first:
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/README.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/subbundles/03-phase-03-test-coverage-and-proof-harness/README.md
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Unit/LocalFileStorageTests.cs
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Unit/WorkspacePathResolverGuardTests.cs

Mandatory execution behavior:
- Keep comments in English.
- Update reviews/01-execution-report.md with the exact commands, screenshots, and findings for this workstream.
- Do not mark the workstream complete if required proof is blocked.
- If this workstream touches UI, run Playwright automation plus manual headed Playwright MCP with screenshots at 1900x1200 and 1366x900.
- If a screenshot shows overlap, clipping, overflow, or broken action gating, fix it before closure.
```

