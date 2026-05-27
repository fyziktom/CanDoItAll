# Reviewed State

## MAF package state

`CanDoItAll.AgentFramework.Maf.csproj` uses:

- `Microsoft.Agents.AI` 1.6.2
- `Microsoft.Agents.AI.A2A` 1.6.2-preview.260521.1
- `Microsoft.Agents.AI.OpenAI` 1.6.2
- `Microsoft.Agents.AI.Workflows` 1.6.2

`CanDoItAll.AgentFramework.Hosting.csproj` uses:

- `Microsoft.Agents.AI.Hosting.A2A` 1.6.2-preview.260521.1

## MAF adoption state

The implementation has real MAF 1.6 contact points:

- `MessageAIContextProvider`
- `AIAgent`
- `AgentSession`
- `ChatClientAgentOptions`
- `ChatClientAgentRunOptions`
- `WorkflowBuilder`
- A2A types
- response format support via `ChatResponseFormat`

But previous proof also says:

- `IChatMessageInjector` was not available in loaded assemblies.
- `AgentSessionFiles` was not available in loaded assemblies.
- `SkillFrontmatter` was not available in loaded assemblies.
- `OpenTelemetryChatClient` was not available in loaded assemblies.
- workflow expected output / ground truth is deferred to process/workflow assertion tests.

This is acceptable only if documented as intentional compatibility/fallback design.

## Process runtime state

`RecordArtifactAsync` now checks existing artifacts with compatible `StepRunId` and `ArtifactExpectationId` before returning an existing artifact. If the same projection identity or external reference belongs to another step/expectation, it returns a scope conflict.

`ProcessCompletionArtifactValidator` checks current-run binding, producer mode, content availability, content hash, declared format, and placeholder signals.

`ProcessRuntimeReadQueryService` currently projects `ContentUnavailable` diagnostics into artifact ledger items, but the visible read-model code does not yet project all finalizer validation statuses.
