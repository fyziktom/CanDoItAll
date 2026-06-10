# Current Findings

## Good
- Live process-run OpenAI proof now exists and is process-run grounded.
- Host beta has async API and structured denials.
- Manager facade exists.
- EF audit store exists.
- Core reverse-dependency scans are clean.

## Problem to fix first
`EfCoreProcessVerificationAuditStore` exists but `AddProcessVerificationRuntimeHost` currently still registers `InMemoryProcessVerificationAuditStore` as the default `IProcessVerificationAuditStore`. Production readiness requires EF-backed audit by default, or an explicit options-based store selector where production/test choices are source-backed and tested.

## Immediate direction
Fix durable audit wiring, then harden manager/API/UI readback and scheduler/workflow verification job execution.
