# P3-WS02 Integration tests and harness expansion

## Objective

Add end-to-end service-level proof for access routes, provider behavior, migrations, and transfer pipeline semantics.

## Touchpoints From Workbook

| Touchpoint | Surface | Module | Scope | Required change | Proof route |
| --- | --- | --- | --- | --- | --- |
| TP-031 | Managed files storage integration tests | Integration Tests | In scope | Expand to new unified access endpoint and provider routing. | dotnet test |
| TP-032 | Profile harness integration tests | Integration Tests | In scope | Keep compatibility and route tests across profiles. | dotnet test |
| TP-033 | Fake IPFS server | Test Support | In scope | Reuse and extend for storage driver contract tests; do not claim real-node proof when only fake-server coverage exists. | Integration tests + honest gap logging |

## Exact Source References

- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Integration/ManagedFilesStorageIntegrationTests.cs
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Integration/ProfileHarnessIntegrationTests.cs
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Support/FakeIpfsTestServer.cs

## Ordered Implementation Tasks

1. Extend integration coverage for unified access routes and profile isolation.
2. Reuse and expand the fake IPFS server for contract tests.
3. Add a real FTP-host-backed integration test if the environment supports it; otherwise leave a blocked proof record.
4. Add batch transfer/snapshot migration integration coverage.

## Acceptance Checklist

- Integration tests exercise the app runtime, not just pure unit seams.
- The execution report can show exactly which provider proofs are real and which remain blocked.
- IPFS coverage proves both upload and retrieval path semantics.

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
Implement workstream P3-WS02 only.

Objective:
Add end-to-end service-level proof for access routes, provider behavior, migrations, and transfer pipeline semantics.

Mandatory files to read first:
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/README.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/subbundles/03-phase-03-test-coverage-and-proof-harness/README.md
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Integration/ManagedFilesStorageIntegrationTests.cs
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Integration/ProfileHarnessIntegrationTests.cs
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Support/FakeIpfsTestServer.cs

Mandatory execution behavior:
- Keep comments in English.
- Update reviews/01-execution-report.md with the exact commands, screenshots, and findings for this workstream.
- Do not mark the workstream complete if required proof is blocked.
- If this workstream touches UI, run Playwright automation plus manual headed Playwright MCP with screenshots at 1900x1200 and 1366x900.
- If a screenshot shows overlap, clipping, overflow, or broken action gating, fix it before closure.
```

