# Workspace source-inspection guidance (2026-07-11)

## Trigger

The clean Tetris regression run reached the new manager-assisted repair route, then stopped in `dotnet-quality-repair/diagnose-quality-failure`. The analyst listed the grounded external target but did not read an owning product file. The generic completion gate correctly retried once and stopped the identical second failure.

The failure was not Tetris, Blazor, or scaffold specific. It was a conflict between a template-declared external evidence obligation and the generic own-output bootstrap wording, which told an evidence-producing step to write its managed artifact first.

## Responsibility split

| Responsibility | Previous owner | Target owner | Test seam |
| --- | --- | --- | --- |
| Source-read receipt policy and completion evidence | `RuntimeIntegration/Completion` | `Drivers/Workspace` | Direct contribution tests with synthetic receipts |
| Template-aware source-read prompt obligation | Ad-hoc template prose plus recovery prompt | Existing template `CapabilityScope` | Template/prompt materialization tests |
| Managed-artifact bootstrap ordering | AgentFramework brief builder | Same generic builder, expressed without source semantics | Brief characterization test |

## Decision

Use the existing completion-contribution model for enforcement and the existing template `CapabilityScope` for prompt obligations. The quality-repair template declares a current-run `workspace_read_file` receipt and an ordered source-inspection instruction fragment. Generic orchestration already carries capability scope without choosing a source file or knowing .NET/Blazor/project names.

The own-output bootstrap rule remains strict about avoiding self-discovery of managed artifact files, but permits explicitly declared external evidence obligations before the primary managed artifact. This prevents a contradictory prompt while retaining the no-placeholder-artifact rule.

## Rejected designs

- Do not add a Tetris, Calculator, Blazor, or scaffold exception.
- Do not weaken the current-run source-read completion gate.
- Do not add another retry policy; the existing fingerprint limit already stops the repeated failure.
- Do not put source inspection back into the generic dispatcher or a partial class.

## Pattern selection

No new prompt-contribution abstraction is needed: `CapabilityScope` is the repository's existing typed template-to-prompt contract. A direct source-inspection conditional in the AgentFramework brief builder remains rejected because it would make that generic adapter grow with workspace/browser/document policy.

## Testability contract

- The workspace source-inspection contribution is tested directly with no runtime host or filesystem.
- The template capability scope is materialized and visible in the process step brief.
- A prompt characterization test proves explicit external evidence may precede a managed output while self-discovery remains forbidden.
- A DI smoke proves the workspace contribution is used by the production prompt builder.
