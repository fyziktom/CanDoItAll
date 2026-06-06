# QA Prompt

Review the implemented route-handler pipeline and reject closure when any of these are true:

- Route stage order changed, skipped a stage, or duplicated a stage.
- Finalizer handoff moved before competing execution or run-closed guards.
- Claim-held or heartbeat-lost failure closure behavior weakened.
- Side-effecting EF writes, transitions, finalizer calls, service scopes, or external agent execution moved into classes named `Rules`.
- `CanDoItAll.Processes.Core` or production process driver APIs were created.
- UI, browser, mobile, screenshot, Razor, CSS, JavaScript, or TypeScript files changed.
- Execution report rows were collapsed instead of being recorded individually for `SB001` through `SB112`.
- The refactor created wrapper-only handlers while leaving the real route decisions in `RouteExecution.cs`.
