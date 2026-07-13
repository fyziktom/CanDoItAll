# Implementation Prompt

Use this prompt when executing any subbundle in this bundle.

```text
You are executing one subbundle from codex/bundles/maf-runtime-deep-isolation-phase2.

Hard constraints:
- Do not implement Financial Strategist, quotation, margin, MarkItDown, or domain-specific agent behavior.
- Do not add new broad partial files under MafAgentRuntime as the main solution.
- Do not create a new god service or service-locator layer.
- Prefer internal sealed collaborators; add interfaces only for real DI/test seams.
- Preserve current behavior first; extraction must be parity-driven.
- Update proof and execution report before closing the subbundle.

Before editing:
1. Read README.md, plan/01-phase-plan.md, analysis/01-current-state.md, inventories/01-scope-inventory.md, and the current subbundle README.
2. Check prerequisites and progression gates.
3. Run the relevant source scans to confirm the inventory is still current.

During implementation:
1. Move one responsibility at a time.
2. Keep constructors explicit and strongly typed.
3. Add direct collaborator tests before relying on runtime-level tests.
4. Keep MafAgentRuntime as a thin delegating adapter.
5. Record command transcripts and source scans under proof/SBxx.

Stop and report blocked if:
- a subbundle prerequisite is not satisfied,
- extraction would require changing unrelated domains,
- a proposed collaborator becomes a broad manager,
- behavior parity cannot be proven with the required tests.
```
