# Repair agent chat persistence and project marker memory

## Status

- `Completed`

## Objective

- Repair agent-chat validation defects discovered after recall itself was healthy: persisted chats could fail on Windows file replacement, and project-scoped Cognitive Memory was not available to normal organization-scoped chat prompts without manually pasting context.

## Success Criteria

- Persisted agent chat no longer fails on the observed Windows file replacement path.
- A normal chat prompt containing only `CognitiveMemoryProjectId` and a question receives project-scoped Cognitive Memory context.
- Contributed context includes source locators.
- Automatic project-marker chat validation passes for ClinicFlow, Docker Platform, and Regional Economy S04 probes.

## Covered Inputs

- R8 AI chat validation.
- R9 on-the-fly repair subbundles.
- R10 final closure evidence.

## Prerequisites

- Subbundle 05 recall repair passed.
- Agent provider health is sufficient for chat validation.
- PostgreSQL `_03` runtime remains active.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\Storage\FileSandboxWorkspaceJsonStore.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryMafIntegration.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\AgentContextContributionTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration`
- `C:\repositories\CanDoItAll\cognitive-memory-multi-cycle-demo-validation\validation\run-agent-chat-project-marker-validation.ps1`

## Deliverables

- File-store overwrite-copy retry fallback after bounded `File.Replace` retries.
- Cognitive Memory project marker extraction for chat scope.
- Prompt-control query normalization before recall.
- Source locator rendering in MAF contributed context.
- Unit and integration tests.
- Automatic project-marker chat evidence.

## Dependency Impact

- This is the final end-to-end gate. Without automatic chat contribution, the bundle would only prove that direct recall works, not that a development agent can use Cognitive Memory during normal testing.

## Validation Depth

- End-to-end repair and closure gate.

## Implementation Steps

1. Reproduce or capture the persisted-chat file replacement failure.
2. Add explicit retry fallback that still fails predictably if the overwrite path cannot complete.
3. Add project marker parsing for `CognitiveMemoryProjectId` and `ProjectId`.
4. Strip chat prompt-control lines before recall and prefer explicit `Question:` text.
5. Render source locators into contributed Cognitive Memory context.
6. Add unit coverage for marker parsing, query normalization, and locator rendering.
7. Add or rerun integration coverage for persistence fallback.
8. Rerun automatic project-marker chat validation against PostgreSQL `_03`.

## Scope Exceptions

- The existing agent text-search provider may still contribute independent document context. The final validation records the exact Cognitive Memory locator inside the serialized MAF session context for each chat run so memory contribution is explicitly proven.

## Do Not Do

- Do not paste the staged source text or recall context pack into the chat prompt for final proof.
- Do not hide persistence failures by swallowing exceptions.
- Do not accept chat answers unless both semantic content and source/context evidence match.

## Acceptance Checklist

- Completed: Automatic chat prompt contains only marker plus question.
- Completed: Chat answers pass all required semantic checks.
- Completed: Each serialized session context contains the expected S04 Cognitive Memory locator.
- Completed: Unit and integration tests pass.

## Proof Required

- `validation/evidence/20260517-181521-agent-chat-project-marker-20260517-190859/agent-chat-project-marker-validation-summary.json`
- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~AgentContextContributionTests|FullyQualifiedName~CognitiveMemoryRecallOrchestratorTests" --no-restore`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter FullyQualifiedName~AgentFrameworkPersistenceIntegrationTests --no-restore`

## Browser Validation Logging

- Browser proof for the Cognitive Memory UI is recorded in `validation/evidence/20260517-181521/browser`.
- Agent chat proof is API-level JSON evidence because the API captures full transcript, execution run id, metric, and serialized session context.

## Progression Gate

- Final bundle closure may proceed only after automatic project-marker chat validation passes 3/3 and all repaired tests pass.

## Suggested Agent Prompt

```text
Implement this repair subbundle only.
Fix persisted agent chat failures and make Cognitive Memory available through normal chat prompts that carry only CognitiveMemoryProjectId plus the user question. Do not paste memory context into the final chat prompt. Prove the serialized MAF session context contains the expected source locators and rerun the project-marker chat validation before closure.
```
