# Structured Input

## Raw Inputs

- Original user request in `bundle://inputs/00-original-request.md`.
- Previous preparation bundle copied under `bundle://inputs/original-prep/`.
- CodeAnalytics snapshot evidence gathered during preparation: `snap-20260707234748-ac72a0ea`.
- Read-only NuGet CLI checks for current package references and current available package updates.

## Normalized Request

Prepare an implementation-ready bundle for a conservative Microsoft Agent Framework package update. The bundle must include phase decomposition, architecture gates, source-grounded risk maps, exact validation commands, proof requirements, traceability, subbundle prompts, self-review, and a detailed `.xlsx` checklist workbook. It must not start implementation.

## Constraints

- Package target is the MAF 1.13 line unless implementation-time NuGet evidence contradicts it.
- Stable `Microsoft.Agents.AI`, `Microsoft.Agents.AI.OpenAI`, and `Microsoft.Agents.AI.Workflows` update to `1.13.0`.
- Preview package versions are not guessed. A2A and Mem0 decisions must be based on `dotnet list package --outdated --include-prerelease`.
- Current CanDoItAll process runtime semantics remain in the product/process layers.
- Architecture fixes must be minimal, typed, testable, and bounded to existing adapter seams.
- Large runtime refactoring is out of scope for this package update.
- All C# comments added during implementation, if any, must be in English.

## Validation Expectations

- Restore and build proof.
- Focused unit and integration tests for MAF runtime, providers, finalizers, approvals, workflow adapter, process dispatch, and project-structure bridge behavior.
- Architecture drift review before broad validation.
- Source scans proving no new process runtime tool provider, no route expansion, no stale stable MAF 1.8 references, no broad warning suppression, and no unrelated package family updates.
- Evidence note under `docs/maf-1.13-update-evidence.md`.
- Detailed workbook checklist under `bundle://checklists/maf-1.13-phase-checklists.xlsx`.
