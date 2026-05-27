# Target Solution

## Architectural Direction

- Keep MAF integration inside `CanDoItAll.AgentFramework.Maf` and adapter-facing contracts.
- Keep Processes domain/runtime models independent from MAF concrete types.
- Let process artifact validation flow through shared production validation logic used by dispatch, finalizer, read model, recovery, and UI/API projection surfaces.
- Treat MAF 1.6 features as explicit adapter-level capabilities: adopted when they reduce runtime failure risk, deferred when provider support or local proof is insufficient, and blocked when local packages cannot support the feature safely.

## Target Boundaries

- MAF package and adapter audit: `repo://src/CanDoItAll.AgentFramework.Maf`.
- Agent execution, approvals, and telemetry: `repo://src/CanDoItAll.AgentFramework.Core` and `repo://src/CanDoItAll.AgentFramework.Maf`.
- Process runtime, artifacts, finalizer, recovery, read model, and UI: `repo://src/CanDoItAll.Modules.Processes`.
- Regression proof: `repo://tests/CanDoItAll.Tests.Unit`, `repo://tests/CanDoItAll.Tests.Integration`, and `repo://tests/CanDoItAll.Tests.Components`.

## Validation Strategy

- First prove the bundle structure and source inventory.
- Then run package, source, and test audits before code changes.
- Add failing-first/adversarial tests before behavior-changing fixes.
- Close with restore, build, targeted tests, web-app startup, browser proof, and simple agent communication proof.
