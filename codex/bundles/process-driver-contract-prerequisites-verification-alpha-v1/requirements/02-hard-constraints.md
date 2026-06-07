# Hard Constraints

- Do not broaden Process Core into runtime orchestration.
- Do not move EF, DbContext, workspace/storage/filesystem, AgentFramework execution, claims, transitions, finalizer application, projection persistence, validation orchestration, or process mutation into Core.
- Do not add production driver APIs, registries, DI registrations, runtime selectors, manager commands, shell execution, Office/Graph runtime calls, workspace writes, storage writes, or business-record mutation.
- Do not add small/medium/mobile/browser proof unless UI files unexpectedly change.
- Do not weaken existing architecture guards.
- Do not use docs-only claims as proof where executable tests are required.
- Do not combine first production driver implementation with permission/audit/sandbox prerequisite work.
