# Senior process manager review

The earlier bundle was already strong on module boundaries, actor binding, staffing templates, handoffs, approvals, runtime journaling, and future AgentFramework seams.

A further senior process-manager pass checked the bundle against the user's clarified intent: **the process model itself should become the primary way humans and agents are connected in production work**.

## Main findings from this pass

1. The bundle needed to say more explicitly that the process is the **canonical collaboration graph**, not just a documentation layer above hidden runtime topology.
2. Triage and routing had to remain visible as governed process behavior instead of disappearing into an opaque runtime wrapper.
3. Shared projects and project structures needed a harder separation from process orchestration so CanDoItAll does not accidentally create a second hidden scheduling language.
4. Runtime supervision on the same canvas is operationally very valuable, but only if projection and canonical state stay separate.

## Actions taken

- Added `PRM-F22` for process-native work briefs, baton handoffs, and governed triage routing.
- Added `ADR-PROC-021` to lock the process definition as the canonical collaboration and handoff graph.
- Added `ADR-PROC-026` to separate project work breakdown from process orchestration while still allowing typed links.
- Added `PRM-F24` so operators can supervise live execution on the same process canvas without corrupting canonical state.
- Added new entities for work briefs, triage decisions, executor correlations, and runtime overlay projection.

## Result

The bundle now behaves more like a **business-owned orchestration model** and less like a process designer that could later be bypassed by hidden runtime agent wiring.
