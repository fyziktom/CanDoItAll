# 04-plugin-executor-contract-and-sandbox-hardening

## Status

- `Completed`

## Objective

Harden workflow plugin executors so external capabilities can be used safely, deterministically, and consistently through the MAF workflow runtime.

## Success Criteria

- Plugin executors register through a central descriptor/registry contract.
- Each executor descriptor includes capability flags, input/output shape, settings schema, default policy, permission policy, approval requirement, and deterministic test mode support.
- Executor activation propagates run/node/user/security context, cancellation, policy, artifact writer, telemetry, and approval service.
- Gmail/Office365/Email/Docker executors have fake-mode tests and do not require live credentials for default test proof.
- Dangerous side effects require approval by policy.

## Covered Inputs

- R07, R08, R09, R10, R12, R15

## Prerequisites

- SB03 executor activation and typed message boundary passed.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Plugins`
- `repo://src/CanDoItAll.Plugins.Abstractions`
- `repo://src/plugins/CanDoItAll.Plugin.Email`
- `repo://src/plugins/CanDoItAll.Plugin.Gmail`
- `repo://src/plugins/CanDoItAll.Plugin.Office365`
- `repo://src/plugins/CanDoItAll.Plugin.Docker`
- `repo://src/CanDoItAll.AgentFramework.Maf`
- `repo://tests/CanDoItAll.Tests.Unit`

## Deliverables

- Hardened plugin executor descriptor model.
- Registry and factory validation.
- Fake plugin executor test harness.
- Permission/approval enforcement tests.
- Timeout/retry/cancellation tests.
- Artifact/tool receipt tests.
- Secret redaction tests.

## Dependency Impact

- SB05 event/artifact semantics depend on stable plugin executor descriptors and runtime receipts.
- SB06 executor availability, approval, and capability UI depends on this registry contract.
- Any gap in side-effect approval or cancellation must reopen SB04 before SB05/SB06 closure.

## Validation Depth

- Critical foundation with semantic proof required under `proof/SB04/manifest.md` and `proof/SB04/semantic-invariants.md`.
- Requires adversarial negative proof for unknown executor IDs, invalid settings, denied approvals, and cancellation.
- Requires positive fake executor proof for success, failure, retry where supported, artifact capture, telemetry, and redaction without live external services.

## Implementation Steps

1. Map current plugin abstractions and invocation paths from SB01.
2. Define or harden `WorkflowExecutorDescriptor` and registry snapshot.
3. Add settings schema validation at workflow definition validation time and executor activation time.
4. Wrap plugin calls in policy-aware MAF executor adapters.
5. Add approval checks for side effects and sensitive data access.
6. Add sandbox/resource guard points for Docker/shell-like or script-like actions.
7. Add fake connectors and deterministic tests.
8. Update proof and execution report.

## Scope Exceptions

- Live Gmail/Office365/Docker tests are optional/manual unless secrets and services are explicitly configured.
- UI for plugin configuration belongs to SB06 unless minimal configuration is required for tests.

## Do Not Do

- Do not inject arbitrary `IServiceProvider` into executor runtime and resolve unknown services dynamically.
- Do not let plugin code write directly to workflow artifacts without the artifact policy/writer.
- Do not call external APIs in default tests.
- Do not swallow cancellation exceptions or retry non-idempotent operations blindly.

## Acceptance Checklist

- Unknown executor IDs fail validation before execution.
- Invalid plugin settings fail with actionable diagnostics.
- Approval-required operations block until approved and reject cleanly when denied.
- Fake plugin executors prove success/failure/retry/cancellation/artifact paths.

## Proof Required

- Unit/integration test transcript.
- Source assertions for descriptor registration and approval enforcement.
- Redaction test proof.

## Browser Validation Logging

- N/A unless plugin configuration or executor availability UI is changed in this subbundle; if it is, log route, viewport, Playwright evidence, screenshots, and result.

## Progression Gate

- SB05 may finalize runtime event/checkpoint alignment only after plugin executor event/artifact semantics are stable and SB04 closure proof cites `proof/SB04/manifest.md` plus `proof/SB04/semantic-invariants.md`.

## Suggested Agent Prompt

```text
Implement SB04 only. Harden plugin executors as governed workflow executors with descriptors, schemas, policies, approval, cancellation, artifacts, telemetry, and deterministic tests.
```
