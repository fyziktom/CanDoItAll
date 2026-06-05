# Assumptions And Risks

## Critical Path Risks

1. **Retry behavior drift**: A small change in no-progress compression, retry reasons, or carried proof can cause the dispatcher to stop too early or retry forever.
2. **Provider repair side effects hidden in pure helpers**: Provider fallback may mutate technical agents through `SaveAgentAsync`; this must remain explicit in a coordinator, not a pure rule class.
3. **Recovered execution adoption drift**: Recovered or concurrent execution runs must preserve chat session, response text, and attempt-number behavior.
4. **Recovery journal drift**: Retry/rework/provider recovery journal records must retain original mode, packet, next-attempt, and correlation semantics.
5. **Premature Process Core pressure**: Extracting these concepts into public Core contracts before they are stable would freeze unstable implementation details.

## Validation Risks

- Broad architecture test classes may still include unrelated historical bundle fixture failures; this bundle must use focused tests and explicitly record any known unrelated failures.
- Source scans must distinguish a valid no-match result from an error. `rg` exit code 1 for no matches is expected in no-core/no-driver scans.
- No browser/viewport proof should be created unless UI files unexpectedly change. Runtime/service refactor should keep browser validation N/A.

## Reopen Triggers

Reopen the most recent production movement subbundle if any of these are observed:

- `ExecuteUntilSettledAsync` loses a recovery path or skips provider fallback.
- No-progress compression changes event type, correlation id, or replay context semantics.
- Provider repair mutates agent configuration without explicit coordinator proof.
- A helper introduces `CanDoItAll.Processes.Core`, `IProcessDriverPack`, `ProcessDriverRegistry`, or driver package references.
- Any moved helper contains TODO, NotImplementedException, `return default` stub behavior, or changes branch order without explicit test proof.
