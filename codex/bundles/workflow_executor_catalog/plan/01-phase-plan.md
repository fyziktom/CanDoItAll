# Phase Plan

```mermaid
flowchart TD
  SB01[SB01 Validator catalog guardrails] --> SB02[SB02 Artifact content store]
  SB02 --> SB03[SB03 Workspace file/folder executor]
  SB03 --> SB04[SB04 JSON transform executor]
  SB04 --> SB05[SB05 Markdown/report executor]
  SB05 --> SB06[SB06 Delay/approval/control helpers]
  SB06 --> SB07[SB07 HTTP download/document ingestion]
  SB07 --> SB08[SB08 Agent/subworkflow/artifact node policy]
  SB08 --> SB09[SB09 Templates and UI authoring pack]
  SB09 --> SB10[SB10 Regression scenario harness]
```

## Gate rules

- SB01 is mandatory before all executor expansion.
- SB02 is mandatory before any new executor claims artifact output.
- SB03 should produce practical local folder/file workflows.
- SB04/SB05 should remove the need to use LLMs for deterministic JSON/Markdown tasks.
- SB06 must not implement unsafe host command execution.
- SB07 must not bypass network or workspace safety.
- SB08 must prevent runtime pass-through surprises.
- SB09 must seed examples without overwriting user-managed definitions.
- SB10 must run restore/build and targeted unit/integration/component tests.

## Validation commands Codex should run

Adjust project/test filters to the current repo, but capture proof for:

- `dotnet restore CanDoItAll.slnx`
- `dotnet build CanDoItAll.slnx --no-restore`
- unit tests for workflow validator/executors/payload/artifacts
- integration tests for workflow API and persistent stores
- component tests for Workflows page/canvas executor catalog
- scenario harness for local folder -> ingest -> transform -> markdown -> write output
