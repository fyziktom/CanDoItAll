# Normalized Requirements

## Functional Requirements

1. AgentFramework must be the sole editable source of truth for AI agents in the target profile, and CRM-HR must consume that same catalog instead of maintaining a parallel registry.
2. Existing agents currently visible through CRM-HR, including legacy scenario agents such as `Showcase Lead Engineer`, must become visible and editable through the dedicated Agents page after canonicalization or migration.
3. CRM-HR edit flows for AI agents must remain bridged through AgentFramework-backed technical profiles and bindings after the source-of-truth repair.
4. The correct OpenAI-backed delivery agents must carry the capabilities, instructions, and skills needed for C# and Blazor implementation, code review, UI review, QA, and release work.
5. UI and QA agents used in the serious run must have validated access to `playwright-local-mcp`, must be able to capture browser proof, and must reason over screenshot evidence instead of only textual assertions.
6. A serious project must be created in the target SQLite profile for a Blazor SSR basic-units-converter application, without showcase naming or showcase-specific framing.
7. The project must include a real project structure with feature blocks, delivery phases, descriptions, and progress surfaces that support phased execution.
8. The delivery plan must attach template-driven processes and roles for intake, architecture, implementation, review, QA, security or governance, release, and post-run learning where applicable.
9. The human role for approvals and launch decisions must be explicit, while the delivery work itself is executed by real CanDoItAll AI agents created through AgentFramework.
10. The run must produce durable artifacts, update project-structure progress, and surface resulting file-output folders or equivalent traceability nodes where the runtime writes durable results.
11. The live serious run must be observed end to end so missing steps, weak instructions, capability gaps, architectural defects, and cross-module bugs are harvested from reality rather than speculation.
12. Post-run improvements must update code, templates, process composition, and architecture where required, including splitting oversized files when the live run proves the current composition is too entangled.
13. Final closure requires a rerun that demonstrates the repaired flow works end to end, with agents handing off artifacts properly until the units-converter app is completed and checked.

## Non-Functional Requirements

1. Prefer the smallest correct code change for source-of-truth repair, but do not preserve a flawed ownership model just because it is already in production.
2. Keep all new logic strongly typed and cross-module boundaries explicit.
3. Avoid hardcoded one-off provisioning paths when the template system can own the same behavior.
4. Record proof in the bundle while execution is happening, not later from memory.
