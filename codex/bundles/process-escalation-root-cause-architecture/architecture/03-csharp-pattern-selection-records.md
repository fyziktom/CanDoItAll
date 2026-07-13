# C# Pattern Selection Records

## PSR01 Typed Diagnostic Projection

- Problem force: result receipts currently preserve hashes/status but not enough actionable failure context.
- Selected pattern: immutable diagnostic record plus projector/enricher.
- Rejected alternatives: storing only raw provider text; adding more strings to projection payloads; parsing prompt text later.
- New types/projects: `ProcessStrategyResultDiagnosticRecord` in runtime/contracts and `IProcessBlockedDiagnosticProjector` in application/projections.
- Testability improvement: diagnostics can be unit-tested without dispatching a live agent.
- Proof required: unit tests for persistence/projection plus API readback integration test.

## PSR02 Readiness Resolver

- Problem force: process steps need scoped tools, MCPs, skills, suppressions, and instructions without changing global agent settings.
- Selected pattern: resolver plus immutable readiness contract.
- Rejected alternatives: prompt-only instructions; global agent setting mutation; adding one-off checks in HR matching UI.
- New types/projects: `ProcessStepReadinessContract`, `ProcessStepReadinessDiagnostic`, `IProcessStepReadinessResolver`.
- Testability improvement: missing/denied/suppressed capability cases can be tested with fake agent/tool catalogs.
- Proof required: unit tests for every diagnostic category and integration tests in launch preview/dispatch.

## PSR03 Driver Recovery Strategy

- Problem force: manager fallback needs domain-specific recovery without leaking domain logic into runtime.
- Selected pattern: strategy interface registered by process driver.
- Rejected alternatives: switch statements in dispatcher; hardcoded .NET recovery in generic adapter; silent fallback.
- New types/projects: `IProcessDriverRecoveryClassifier`, `ProcessRecoveryDecision`, `ProcessFailureCategory`.
- Testability improvement: driver recovery can be tested with synthetic diagnostics and assignments.
- Proof required: unit tests for generic and .NET driver decisions, including no-recovery cases.

## PSR04 Catalog-Based Capability Policy

- Problem force: tool/MCP/skill readiness needs efficient lookup without expensive per-step graph construction.
- Selected pattern: reusable immutable catalog plus per-step context evaluation.
- Rejected alternatives: service locator; runtime reflection per dispatch; rebuilding heavy service graphs per step.
- New types/projects: catalog interfaces near existing capability/tool registries and adapter composition.
- Testability improvement: fake catalogs can drive deterministic readiness tests.
- Proof required: performance-safe unit tests and no per-step heavy instantiation in code review.

## PSR05 Template Policy Extraction

- Problem force: long JSON/markdown process prompts encode deterministic .NET execution policy in brittle prose.
- Selected pattern: extract repeated policy fragments into driver-owned builders/descriptors while leaving final templates readable.
- Rejected alternatives: more copy-pasted prompt text; moving .NET text into generic runtime; broad template rewrite without tests.
- New types/projects: driver-owned policy builders or template fragment providers, only if SB04/SB05 prove they reduce duplication and improve tests.
- Testability improvement: policy descriptors can be parsed/validated without launching a process.
- Proof required: template parsing tests and fixture diversity beyond Calculator/Tetris.
