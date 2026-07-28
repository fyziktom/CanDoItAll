# SB08 Final Validation Evidence

## Package and Bundle Gates

- Bundle validator: passed for `151` files.
- Package alignment validator: passed.
- Stable packages: `1.15.0`.
- Preview hosting/A2A packages: `1.15.0-preview.260722.1`.
- No observed Microsoft Agent Framework `1.13` package remained in generated
  asset graphs.
- `git diff --check`: exit `0`; only configured LF-to-CRLF notices were emitted.

## Build and Test Gates

| Validation | Result |
|---|---|
| Clean solution rebuild, operation `op_441c0b94463648c68f25f0dccc985c5b` | Passed, exit `0`, 393900 ms |
| Final MAF/approval/activity unit slice, operation `op_fb7745a704f447b096be0558e6198fad` | 71/71 passed |
| Direct and streaming handoff integration slice | 6/6 passed |
| Project Structure, Scheduler, Workflow, process-step, and Recruiting unit slice | 107/107 passed |
| Recruiting retained-context component slice | 2/2 passed |
| A2A metadata/card/remote-tool slice, operation `op_89fcfc0dd0cf45ab9d4d0fb5acd911f2` | 9/9 passed |
| Scheduler-to-workflow launch boundary integration | 1/1 passed |
| Relevant component slice | 69/70 passed; the single workflow status timeout passed 1/1 on exact rerun |
| Selected Playwright slice | 2/3 passed; the single WorkflowShell startup timeout passed 1/1 on exact rerun |

No process E2E suite was run, as requested.

## Live UI Validation

| Surface | Proof |
|---|---|
| Agent shell | `.NET Application Developer` completed `MAF-1.15-LIVE-OK` and a second message in the same durable thread. |
| Approval | Exact bound file request reached actionable approval, survived session persistence, and remained non-mutating after rejection. |
| Workflow editor | `Example: Meeting Notes Action Extractor` completed through the real OpenAI provider with 33 events and an authoritative terminal `WorkflowOutputEvent`. |
| Project Structure | TetrisGame contextual `.NET Application Developer` chat completed `PROJECT-CONTEXT-OK`; workflow launch/inspect is covered by the selected Playwright test. |
| Scheduler | Contextual Scheduler Agent completed `SCHEDULER-CONTEXT-OK`; scheduler-to-workflow persisted lineage/idempotency integration passed. |
| Workflow process step | Focused process-step executor and MAF hardening tests passed; process E2E was deliberately excluded. |
| Recruiting | Closing Viktor Petrov's record retained the `applicationId`, browser highlight, and typed context. HR Staffing Manager completed in 2m38s and answered exactly `Viktor Petrov`. |

## Hosting Parity

The product did not map an inbound A2A route at the 1.13 baseline and still does
not map one after the upgrade. The 1.15 hosting packages build and the existing
metadata/card/outbound-remote-tool surface passes 9/9 tests. On the final host:

- `/health` returned `200`;
- `/.well-known/agent-card.json` returned `404`, confirming that the migration
  did not accidentally expose an unauthenticated A2A endpoint.

Adding an inbound A2A server would be a separately reviewed feature, not a
compatibility edit.

## Final Development Runtime

- URL: `http://localhost:5032`
- Managed session: `app_b996d1823dfa4a279288dee34e196a85`
- Health: `Healthy`
- Revision: `candoitall-web-5032:1:g0`
- Runtime PID: `10104`
- Watcher PID: `51576`
- Launch override:
  `--Processes:RuntimeDispatchQueue:EnableRecovery=false`

The override disables only the stale process-recovery scanner whose unrelated
EF query was making development health fail. Scheduler, workflow, agent, and UI
surfaces remain active. The instance is intentionally left running.

## Residual Risks

- `System.Security.Cryptography.Xml` `10.0.7` still emits five inherited
  high-severity `NU1903` advisories.
- Inbound A2A is inactive by design, so no live A2A message/stream endpoint was
  claimed or tested.
- Production canary and rollback execution were not authorized by the local
  development-runtime request.
