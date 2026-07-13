# Implementation Prompt

Use this prompt for each subbundle.

```text
Implement only the selected subbundle from repo://codex/bundles/candoitall-process-maf-hardening-implementation.

Before editing:
- Read the bundle README, the selected subbundle README, plan/01-phase-plan.md, traceability/01-requirement-traceability.md, reviews/csharp-architecture-gate.md, and relevant architecture files.
- Confirm prerequisites and upstream proof are complete.
- Re-run or refresh exact source inventory if the code has changed.
- Use CodeAnalytics for architecture-heavy source orientation when available.

Implementation rules:
- Make the smallest correct change set.
- Use strongly typed C# records/options/enums for identifiers, branch categories, materialization modes, diagnostics, and contracts.
- Do not add final logic as another partial-class dump.
- Do not add fallback behavior that silently hides missing child evidence, missing tools, missing artifacts, or missing diagnostics.
- Keep runtime/domain contracts free of module/provider SDK details.
- Keep .NET delivery specifics in templates, typed metadata, drivers, or module integration.
- Add direct unit tests around extracted behavior.

Proof rules:
- Capture failing-first and passing transcripts for behavior changes.
- Update proof/SBxx/manifest.md and proof/SBxx/semantic-invariants.md.
- Record changed-file hashes, source assertions, anti-stub audit, and production behavior artifact matrix when new production records/signals/states/events are introduced.
- Update reviews/01-execution-report.md.
- Run the C# architecture gate before downstream phases proceed.

Stop conditions:
- A dependency cycle appears.
- A new partial class becomes the real boundary.
- Tests require live provider/LLM/network for unit behavior.
- A template hard gate remains prose-only without an explicit exception.
- A parent bridge can complete from child folder existence instead of accepted artifact proof.
```
