# Structured Input

## Observations

- `Migration and rollout preparation checklist` is a required output of the implementation step in the rich software-delivery template.
- The template requirement is defensible, but the name is misleading for DB-free app work unless prompts explicitly say `no database/data migration` is valid only when justified.
- The implementation agent repeatedly wrote the same files and hit the repeated-tool guard. The failure is behaviorally upstream of the missing artifact.
- The dispatcher retried the same step five times; it did not distinguish missing current-step artifacts from missing upstream artifact inputs.
- Mock/process tests need to cover missing current-step artifacts, missing upstream artifact inputs, repeated tool-call failure, and artifact completion after validation.
- Whole-process browser runs are too expensive for the first proof. The first phase must isolate one agent performing one implementation job.

## Constraints

- Keep changes small and testable.
- Do not hide artifact failures by silently auto-satisfying requirements.
- Do not weaken strict governed completion.
- Do not require real LLM/provider calls for deterministic tests.
- Do not use the real user DB as a mutable test fixture.
- Prefer existing process, AgentFramework, and mock-runtime patterns.

## Desired End State

- A real or deterministic single-agent implementation lane can complete a narrow app-building task and produce every required artifact.
- The process prompt makes required artifacts impossible to miss, including DB-free migration/rollout checklists.
- Retry/recovery policy distinguishes current-step omissions from missing upstream inputs.
- Mock agents can simulate artifact omissions, repeated-write failures, validation omissions, and recovery success.
- A simpler three-agent process proves artifact handoff without the full software-delivery workflow cost.
