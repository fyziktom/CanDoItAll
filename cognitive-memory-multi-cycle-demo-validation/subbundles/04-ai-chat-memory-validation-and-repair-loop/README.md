# AI chat memory validation and repair loop

## Status

- `Completed`

## Objective

- Test whether an AI agent can answer project-specific questions using the curated Cognitive Memory state, then create repair subbundles for discovered failures.

## Success Criteria

- Chat probes from the XLSX tracker are executed or a chat API blocker is documented.
- Answers are scored against expected project, stage, source, and memory evidence.
- Failures are categorized and tied to memory, retrieval, chat integration, source attribution, or prompt behavior.
- Blocking failures produce repair subbundles and rerun proof.

## Covered Inputs

- R8 AI chat validation.
- R9 on-the-fly repair subbundles.
- R10 closure evidence.

## Prerequisites

- Subbundle 03 closure gate passed.
- Approved memory set and backward analysis are available.

## Exact Source References

- `C:\repositories\CanDoItAll\cognitive-memory-multi-cycle-demo-validation\sample-data\trackers\cognitive-memory-demo-source-tracker.xlsx`
- `C:\repositories\CanDoItAll\cognitive-memory-multi-cycle-demo-validation\sample-data\source-manifest.json`
- `C:\repositories\CanDoItAll\cognitive-memory-multi-cycle-demo-validation\templates\discovered-repair-subbundle-template.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Hosting`

## Deliverables

- Chat transcript evidence.
- Chat scoring matrix.
- Failure analysis.
- Repair subbundles for blocking defects.
- Rerun proof after repairs.

## Dependency Impact

- This is the final closure phase. Without chat proof, the bundle only proves data storage and recall, not whether Cognitive Memory helps an AI agent during development/testing.

## Validation Depth

- End-to-end regression and closure.

## Implementation Steps

1. Discover the current chat/agent API path that can use Cognitive Memory for project-specific answers.
2. Execute tracker-defined chat probes for each project and stage.
3. Score answers against expected evidence, source ids, memory titles, and project boundaries.
4. Categorize failures.
5. If failures indicate implementation defects, create repair subbundles under `subbundles/` before final closure.
6. Execute repair subbundles and rerun affected cycle/chat probes.
7. Record final chat validation and raw-note closure.

## Scope Exceptions

- If no stable chat API is available, document the blocker and create a repair subbundle for chat-memory integration. Direct recall probes may be used only as fallback evidence, not as full closure.

## Do Not Do

- Do not inject staged source text directly into chat prompts as hidden context.
- Do not accept plausible answers without source/memory traceability.
- Do not collapse all failures into prompt wording if memory retrieval or source selection is the actual defect.
- Do not finish without creating repair subbundles for blocking failures.

## Acceptance Checklist

- Completed: Chat API/path is identified or blocker is documented.
- Completed: Chat probes are executed and scored.
- Completed: At least one question per project validates current accepted decisions after all stages.
- Completed: Failures have categories and repair actions.
- Completed: Final closure report lists repair subbundles and rerun results.

## Proof Required

- Chat transcript JSON/Markdown.
- Scoring matrix tied to tracker rows.
- Recall/context evidence supporting chat answers.
- Repair subbundle paths and proof if repairs are needed.
- Final completed-stage bundle validation.

## Browser Validation Logging

- Target route or window: the chat UI or agent UI discovered during execution.
- Required viewport: large desktop viewport for chat transcript readability.
- Actions: open chat, ask selected project-specific questions, capture answer, inspect cited memories/sources where available.
- Screenshots: representative passed answer, representative failed answer before repair, representative rerun after repair.
- Review question: did the answer rely on Cognitive Memory and cite the correct project/source evidence?

## Progression Gate

- Final bundle closure may proceed only after chat validation passes or a blocking chat-memory integration defect is represented by a repair subbundle with honest status.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Use the XLSX tracker chat probes. Do not paste source files into the prompt as hidden context. Validate whether the agent uses Cognitive Memory for project-specific answers. Score each answer, categorize failures, and create on-the-fly repair subbundles for memory or chat integration defects before final closure.
```
