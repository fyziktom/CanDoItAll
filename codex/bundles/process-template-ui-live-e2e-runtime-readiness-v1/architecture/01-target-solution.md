# Target Solution

## Runtime Boundaries
- Process Core remains deterministic and generic with no UI, EF, storage, scheduler, workflow, AgentFramework, OpenAI, template-family, or driver concepts.
- Process Module Runtime owns templates, launch plans, run lifecycle, outbox dispatch, finalizers, artifacts, project/project-structure integration, scheduler/workflow-origin launch, and operator readback.
- AgentFramework and process-mock runtime remain the execution boundary used by automation tests.
- Runtime-host verification remains read-only or dry-run-only and exposes diagnostics without approval or mutation side effects.

## Product Proof Path
- Prove process launch from a visible project/project-structure UI surface before treating backend E2E proof as user-ready.
- Prove Blazor/.NET, software-delivery, and business-plan templates through production-path dispatch and persisted readback.
- Attach runtime-host verification readback to real process run ids and step run ids.
- Prove scheduler/workflow-origin launch through process-owned service/facade paths, not driver hooks.

## Release Decision
- The branch is closer to merge only when UI launch, representative automation, runtime-host readback, source scans, build, tests, and the code-first ratio all agree.
