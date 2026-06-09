# process-runtime-live-e2e-openai-hardening-v1

## Status
Prepared for Codex implementation.

## Purpose
Restore and prove the process runtime from the user-facing perspective: start the app, select/start processes from UI/project structure/API/scheduler/workflow-origin paths, dispatch and finalize runs, prove deterministic and optional live OpenAI scenarios, and keep Process Core generic while domain drivers remain read-only diagnostics.

## Main Decision
Do not build a generic process-driver runtime host yet. The immediate goal is normal process runtime restoration. Current required execution should flow through `ProcessesService`, process dispatch/finalizer, MAF/workflow/direct-agent execution, and existing scheduler/workflow start paths.

## Bundle Shape
- 16 phases.
- 48 coherent subbundles.
- Critical gate every three subbundles.
- XLSX checklist under `evidence/checklists`.
- Large desktop UI proof only.

## Hard Constraints
- Do not introduce transient bundle path dependencies in long-lived tests or source.
- Do not introduce generic process-driver runtime host, registry, selector, driver DI auto-registration, manager driver command, scheduler driver hook, or workflow driver hook.
- Do not move runtime orchestration into Process Core.
- Do not leak .NET/Office/business-specific terms into generic Process Core.
- Do not log OpenAI API keys or secret values.
- Do not claim live OpenAI proof unless the opt-in flag/key are present and sanitized transcripts are captured.

## Prepared At
2026-06-09T16:24:38
