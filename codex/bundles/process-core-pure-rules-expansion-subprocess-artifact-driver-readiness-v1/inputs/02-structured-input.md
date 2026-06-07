# Structured Input

## Objectives
- Review the completed narrow Core seed on `maf-processes-refactor`.
- Plan and execute the next narrow Process Core expansion without broad extraction.
- Move only pure deterministic subprocess and artifact rule/read-model families into Core.
- Keep future process-helper-driver readiness documentation/test-only.

## Hard Constraints
- Do not move persistence, workspace/storage/filesystem, AgentFramework execution, claim lifecycle, finalizers, process state mutation, or runtime driver dispatch into Core.
- Do not add production process driver APIs, registries, DI registrations, selectors, manager commands, or runtime helpers.
- Do not create UI/browser/mobile proof unless UI files unexpectedly change; UI file changes are out of scope.

## Validation Expectations
- Build and focused unit/integration parity proof where behavior moves.
- Core forbidden dependency scans.
- Production driver token scans.
- Anti-stub scans.
- Artifact-backed proof manifests for critical subbundles.
