# Assumptions And Risks

## Assumptions

- The current transitional client was intentionally allowed to return AgentFramework types only for the first boundary step.
- Dispatcher behavior must remain semantically identical after snapshot conversion.
- The new process snapshots should model only data the dispatcher already consumes.
- The client may remain inside `CanDoItAll.Modules.Processes` for now; the goal is not module extraction yet.
- Process Contracts must stay free of EF, Razor, AgentFramework, Workbench, Project, Storage, and MAF references.

## Critical Path Risks

- If snapshots omit data currently used by artifact, receipt, cost, or recovery code, the dispatcher may silently lose evidence.
- If exception normalization changes how failed runs are inspected, recovery/rework behavior can regress.
- If receipt metadata mapping is incomplete, required-tool and provider-lineage validation may become less trustworthy.
- If the implementation tries to move too much at once, the huge dispatcher partial set becomes brittle.

## Validation Risks

- Build-only proof is insufficient; the bundle must include source scans and behavior tests.
- Integration tests may be slow; if a full process filter times out, use targeted integration smoke plus final build, but record the timeout clearly.
- The direct type scan must distinguish allowed adapter file usage from forbidden dispatcher usage.

## Reopen Triggers

- Any dispatcher partial still imports or uses AgentFramework execution result/detail/exception types after SB07.
- Contract project gains any package or project reference.
- Process runtime provider decoupling regresses.
- Required-tool detection or artifact lineage tests fail.
- Small/medium/mobile proof artifacts appear.
