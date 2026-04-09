# Implementation Prompt

Implement exactly one subbundle at a time.

Required workflow:

1. Read the root `README.md`, `plan/01-phase-plan.md`, the selected subbundle README, and the relevant rows in `traceability/01-requirement-traceability.md`.
2. Run the entry gate with the `candoitall-subbundle-validator` skill before editing.
3. Use `candoitall-codeanalytics-mcp` for repo inspection instead of broad ad hoc searching.
4. For UI work, use `candoitall-components-mcp` before introducing layout markup and prefer BaseLib or CanvasLib primitives over raw `div`, `span`, or page-local structural CSS.
5. Keep roles, templates, provider profiles, and process truth aligned with the canonical ownership rules in `architecture/01-target-solution.md` and `architecture/02-cross-repo-convergence-and-registry-rules.md`.
6. Do not add a compile-time dependency on `CanDoItAll.AgentFramework` during the first process-module merge.
7. When the subbundle changes browser-visible behavior, use `playwright` or `candoitall-watch-playwright-loop`, capture large-screen screenshots first, then verify narrower widths if layout changed.
8. After implementation, run the closure gate with the `candoitall-subbundle-validator` skill and update `reviews/01-execution-report.md` while the proof is fresh.
9. If the subbundle is the last one in a phase, stop and execute the corresponding `post-implementation-bundle-phaseXX` generation subbundle before starting the next phase.

Non-negotiable rules:

- Do not turn Workbench into a hidden canonical process store.
- Do not let runtime-side agent or provider models become new durable truth.
- Do not treat logs alone as sufficient proof when the bundle asks for explicit decision, trust, or UI evidence.
