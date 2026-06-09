# Assumptions And Risks

## Assumptions
- Branch: `maf-processes-refactor`.
- Primary proof target: large desktop and backend/service tests.
- Live OpenAI tests are allowed only as explicit opt-in.
- Process runtime restoration does not require a generic driver runtime host.

## Critical Path Risks
- UI proof passes on seeded baseline but not on a real fresh run.
- Live OpenAI tests leak secrets or become flaky.
- Scheduler/workflow "hooks" are confused with driver hooks.
- Driver diagnostics mutate process state or artifacts.
- Test code reintroduces `codex/bundles/<name>` path reads.
- Process Core becomes domain-specific.

## Validation Risks
- Build/unit tests pass but app startup fails.
- UI route loads but launch API fails.
- Service-level run starts but background worker/outbox does not drain.
- Deterministic process scenario passes but live provider fails due to configuration.
- Live provider succeeds but artifact projection is not navigable from UI.

## Reopen Triggers
- Any long-lived source/test reads a concrete bundle folder.
- Any generic driver runtime host/registry/selector appears.
- Any UI proof uses small/medium/mobile instead of large desktop.
- Any live test logs secret values.
- Any Process Core file references Modules, Infrastructure, AgentFramework, driver packages, EF, workspace/storage, UI, scheduler, or workflow runtime.
