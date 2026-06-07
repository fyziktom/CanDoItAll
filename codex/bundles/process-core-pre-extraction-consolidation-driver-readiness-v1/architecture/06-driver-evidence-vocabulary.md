# Driver Evidence Manifest Vocabulary

## Scope

This vocabulary is verification-only. It is not a production driver API, runtime registry, DI registration model, manager tool, or helper-driver contract.

## Evidence Manifest Families

| Family | Evidence fields | Verification use |
| --- | --- | --- |
| Route helper evidence | Candidate route stage, input DTO type, output DTO type, adapter edge, side-effect owner, parity tests. | Proves a future helper-driver discussion has explicit route-boundary facts without registering a driver. |
| Artifact helper evidence | Expectation snapshot type, matcher rule, projection source kind, lineage key rule, storage/workspace side-effect owner, validation tests. | Proves artifact helper behavior is understood without moving storage or workspace IO. |
| Runtime helper evidence | Execution input model, retry/no-progress/provider behavior owner, finalizer adapter edge, integration proof. | Proves runtime helper behavior stays application-local. |
| Domain helper evidence | Pure rule owner, accepted inputs, deterministic output, denied dependencies, focused tests. | Identifies pure candidates that may later be discussed for extraction. |
| Permission negative evidence | Missing production API, missing registry, missing DI hook, missing runtime selector, missing manager command. | Proves the bundle remains documentation/test-only. |

## Required Manifest Labels

Each future verification-only manifest should include these labels:

| Label | Meaning |
| --- | --- |
| `Evidence family` | Route, artifact, runtime, domain, or permission negative. |
| `Current owner` | Existing module-local source file or bundle document. |
| `Observed behavior` | Behavior proved by tests or source assertions. |
| `Side-effect owner` | Application/infrastructure component that must not move into pure rules. |
| `Denied production surface` | API, registry, DI hook, runtime hook, or manager tool that remains absent. |
| `Proof transcript` | Build, architecture, integration, or source-scan proof path. |
| `Reopen trigger` | Exact condition that invalidates readiness. |

## Non-Goals

- Do not define production driver interfaces.
- Do not add registry types.
- Do not add service registration examples.
- Do not add runtime selection hooks.
- Do not add manager commands.
- Do not move side effects into pure-rule candidates.
