# Cognitive Memory Curator Conversation

This bundle coordinates the implementation of a fluent Cognitive Memory curator conversation mode.

## Profile

- `initiative`

## Mission

Add a Cognitive Memory mode where an operator can talk with the memory curator in a free-running chat, optionally by voice, and every correction or new fact from that conversation becomes source-backed, high-priority memory-improvement input. The mode must support both an agent-backed curator and a direct LLM-call curator, while preserving the recall traces and memory records used for each answer so corrections can improve the specific wrong memories instead of only adding disconnected notes. A follow-up requires short, medium, and long conversation depth modes that control reply detail and recall/aggregation breadth.

## Outcome Contract

- Requested outcome: A new curator conversation surface under Cognitive Memory with text chat, bidirectional voice controls, agent/direct-LLM mode selection, short/medium/long response depth, automatic extraction of user corrections/new knowledge, and memory-improvement artifacts that bypass manual approval in this trusted operator mode.
- Hard constraints: Keep Cognitive Memory logic in module services, keep Blazor UI focused on orchestration, reuse AgentFramework voice services, use existing BaseLib/Radzen-style components already present, avoid stringly typed mode/action identifiers, and do not silently hide provider or memory-write errors.
- Evidence required before closure: Targeted unit/component tests, `dotnet test` for relevant projects, build proof, and browser proof on `/cognitive-memory` showing the Curator tab, mode switch, text turn, voice controls, and captured improvement state.
- Known blockers or explicit scope exceptions: Real microphone/OpenAI audio provider proof may be configuration-dependent; when provider credentials are unavailable, validate voice UI and service calls with existing test doubles and record the external-provider gap explicitly.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-01-curator-contracts-and-capture-pipeline`
2. `subbundles/02-02-curator-runtime-modes-and-memory-routing`
3. `subbundles/03-03-curator-ui-and-voice`
4. `subbundles/05-05-conversation-depth-modes`
5. `subbundles/04-04-validation-and-bundle-closure`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed`
- Subbundle gate review: `All subbundles completed`
- Final closure gate: `Passed with residual baseline-suite failures documented`
- Browser validation analytics: `Passed, including follow-up depth selector proof`
