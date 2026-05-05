# Assumptions And Risks

## Working Assumptions

- The process manager override can reference either a party id or a technical agent id because existing runtime matching accepts both.
- Manager chat can use the existing selected manager agent's default/latest chat session rather than introducing per-run chat sessions.
- Run selection should be optional because users may want to discuss the definition itself before a run exists.
- The small-app validation can use local configured agents when provider credentials and agent bindings exist; otherwise the blocker must be recorded.

## Critical Path Risks

- A default manager may not have a bound `TechnicalAgentId`, which would prevent opening a standard AgentFramework chat.
- Loading all run details for manager chat would be expensive; the run selector should use run list summaries and only include selected run identifiers/status in prompts.
- A too-specific implementation subprocess would make the dispatcher brittle instead of improving agent process instructions.

## Validation Risks

- The current running web app can lock the web output folder; targeted module builds may be safer unless the app is stopped.
- Real-agent delivery depends on local provider configuration and may fail for environmental reasons that are not code defects.
- Browser proof must include the open run-selection modal, not just the new tab.

## Reopen Triggers

- Manager chat writes transcripts outside AgentFramework.
- Manager prompts lose the selected process/run context.
- Template import cannot resolve the nested feature/function subprocess.
- Browser validation shows tab overflow, clipped modal content, or unusable chat controls.
