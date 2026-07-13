# Assumptions And Risks

## Assumptions

- Implementation can keep extracted classes `internal` in `CanDoItAll.AgentFramework.Maf` unless a shared helper belongs in `CanDoItAll.SharedKernel`.
- `MafAgentRuntime` should continue implementing the same runtime contract and should delegate to collaborators rather than expose new public runtime APIs.
- Existing tests can be expanded instead of creating a separate new test project.
- Refactor acceptance can use file-size and static scan thresholds as guardrails, but behavior tests are the real closure proof.

## Critical Path Risks

- SB01 is a critical foundation because a wrong responsibility map leads to moving code to the wrong layer and invalidates all downstream proof.
- SB02 is a critical foundation because shared hashing placement affects project dependencies and downstream helper usage.
- SB03, SB04, and SB05 are critical foundations because they extract builders that feed every runtime run.
- SB06 is a critical foundation because finalizer behavior controls process completion, recovery, usage attribution, and transcript persistence.
- SB07 depends on SB02 through SB06 and must not proceed if any extracted collaborator is only a wrapper around unchanged runtime static methods.
- SB08 depends on all earlier phases and must reopen earlier work if UI or integration proof exposes behavior drift.

## Validation Risks

- Some runtime flows require provider simulation or existing integration fixtures; a shallow unit-only pass would miss finalizer sequencing and recovery regressions.
- UI tests can pass route load while runtime diagnostics or chat behavior regress. Browser proof must include visible runtime activity or seeded scenario interaction where feasible.
- File-size targets can be gamed by moving code into a new large utility file. Static scans must check responsibility names and max-size thresholds for new collaborators.
- Stable hash helper extraction can accidentally change hash format or casing. Tests must lock exact current outputs and intended shared format.
- Session serialization tests must include request-scoped attachment stripping and provider conversation-id restoration edge cases.

## Reopen Triggers

- Reopen SB01 if implementation discovers additional `MafAgentRuntime` responsibilities not represented in the inventory or workbook.
- Reopen SB02 if shared hashing creates an outward dependency from `CanDoItAll.SharedKernel` or changes existing process hash formats.
- Reopen SB03 if approval continuation, provider-managed conversations, or request-scoped attachments regress.
- Reopen SB04 if model temperature, reasoning effort, or retry diagnostics change for OpenAI, Azure OpenAI, Ollama, or unsupported transports.
- Reopen SB05 if context manifest totals, source inclusion/exclusion, or token estimates change without explicit acceptance.
- Reopen SB06 if finalizer validation order, sequence checks, recovery, transcript persistence, or usage attribution changes.
- Reopen SB07 if `MafAgentRuntime.cs` remains a catch-all above the accepted threshold or new extracted files become unbounded catch-alls.
- Reopen SB08 if Playwright proof only loads pages without validating visible agent/workflow/process runtime state.
