# Structured Input

## Normalized Request
- Review the completed `maf-processes-refactor` branch and preserve the previous bundle's successful Process Core cutline.
- Fix or explicitly govern the remaining process-core stabilization issues, especially warning drift.
- Implement the next stabilization phases toward a complete stable Process Core.
- Prepare future domain-driver contracts as documentation and tests only, without production driver APIs.

## Hard Constraints
- Preserve process runtime behavior.
- Keep EF, workspace/storage/filesystem, AgentFramework execution, claims, transitions, finalizers, projections, and validation orchestration outside Core.
- Do not introduce production process-driver APIs, registries, runtime selectors, manager commands, DI registrations, or execution-capable helper drivers.
- Do not add UI/mobile/small/medium browser proof unless UI drift is discovered; UI drift reopens scope.
