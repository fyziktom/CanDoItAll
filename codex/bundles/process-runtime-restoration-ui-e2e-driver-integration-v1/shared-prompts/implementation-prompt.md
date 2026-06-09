# Implementation Agent Prompt

You are implementing `process-runtime-restoration-ui-e2e-driver-integration-v1`.

Work phase by phase. Do not jump ahead. Every critical gate must include:
- source-backed proof,
- failing/adversarial negative proof,
- semantic positive proof,
- anti-stub audit,
- raw-note closure,
- changed-file hashes,
- command transcripts.

Primary mission: restore proof that processes can be launched and executed from the application.

Hard stop if:
- any test/source still requires `codex/bundles/<bundle-name>` after SB006,
- the app cannot start,
- UI process start cannot be proven on large desktop viewport,
- process runtime cannot create/advance a run,
- Core references drivers/modules/infrastructure/UI/storage/workspace/AgentFramework,
- driver integration mutates process state,
- runtime host/registry/selector/DI/manager/scheduler/workflow hooks appear without explicit approved scope.
