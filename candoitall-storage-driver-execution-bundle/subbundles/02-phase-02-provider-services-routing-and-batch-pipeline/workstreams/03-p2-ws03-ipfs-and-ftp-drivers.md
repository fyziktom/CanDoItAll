# P2-WS03 IPFS and FTP drivers

## Objective

Implement content-addressed IPFS storage and remote-path FTP storage behind the same contract, with honest capability limits.

## Touchpoints From Workbook

| Touchpoint | Surface | Module | Scope | Required change | Proof route |
| --- | --- | --- | --- | --- | --- |
| TP-019 | Database snapshots | Infrastructure | In scope | Refactor onto storage providers and transfer pipeline, preserving snapshot manifest behavior. | Integration tests |
| TP-034 | SFTP transport implementation | Support Pattern | Adjacent | Reuse design ideas only; do not couple storage driver to MCP transport layer. | Code review |

## Exact Source References

- C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/ControlPlane/DatabaseSnapshots.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Resources/ResourceModels.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Mcp.SshOps/Transport/SshNetTransport.cs
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Support/FakeIpfsTestServer.cs

## Ordered Implementation Tasks

1. Wrap IPFS add/get/pin or equivalent node API behind the provider interface for local or remote nodes.
2. Implement FTP directory probing, upload, download, and list operations with clear retry and error mapping rules.
3. Expose immutable-vs-mutable behavior honestly through capabilities.
4. Do not claim FTP completion unless at least one real protocol-backed proof path exists; if unavailable, keep the workstream blocked.

## Acceptance Checklist

- IPFS and FTP drivers compile behind the shared interface and advertise provider-specific capabilities clearly.
- No module directly imports IPFS- or FTP-specific code paths.
- Blocked proof is recorded honestly when environment support is missing.

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
Implement workstream P2-WS03 only.

Objective:
Implement content-addressed IPFS storage and remote-path FTP storage behind the same contract, with honest capability limits.

Mandatory files to read first:
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/README.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/subbundles/02-phase-02-provider-services-routing-and-batch-pipeline/README.md
- C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/ControlPlane/DatabaseSnapshots.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Resources/ResourceModels.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Mcp.SshOps/Transport/SshNetTransport.cs
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Support/FakeIpfsTestServer.cs

Mandatory execution behavior:
- Keep comments in English.
- Update reviews/01-execution-report.md with the exact commands, screenshots, and findings for this workstream.
- Do not mark the workstream complete if required proof is blocked.
- If this workstream touches UI, run Playwright automation plus manual headed Playwright MCP with screenshots at 1900x1200 and 1366x900.
- If a screenshot shows overlap, clipping, overflow, or broken action gating, fix it before closure.
```

