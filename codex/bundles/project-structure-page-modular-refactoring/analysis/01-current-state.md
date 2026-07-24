# Current State

## Measured Concentration

- Route owner: `ProjectStructurePage.razor`, `/projects/{ProjectId:guid}/structure`.
- Aggregate source: the Razor file plus 22 explicit `partial class ProjectStructurePage` files, 23 source parts and 11,137 lines total.
- Largest parts: `ProjectStructurePage.razor` 2,937 lines; `ProjectStructurePage.Processes.cs` 1,769 lines; `ProjectStructurePage.Workflows.cs` 1,030 lines; `ProjectStructurePage.SelectionPanel.cs` 767 lines.
- Adjacent large service: `ProjectStructureProcessNodeService.cs` 1,853 lines.
- The page has at least 11 explicit `[Inject]` properties in partial files plus host-level Razor injections.

## Duplicated Responsibility

The page and `ProjectStructureProcessNodeService` independently implement:

- project-structure context traversal and ordering;
- 40-row summary limits and 8-asset limits;
- visual-target classification and generated-evidence exclusion;
- path redaction and text normalization;
- output-root metadata parsing and ancestor precedence;
- output-root launch-variable aliases.

This is a production divergence risk: UI-launched and agent-launched processes can evolve differently.

## Source-of-truth Constraints

- `ProjectStructureSurface` is a read projection combining persisted user nodes with synthetic system-managed contributor nodes.
- Canonical records remain `ProjectObjectRecord`, `ProjectObjectLinkRecord`, and `ProjectWorkbenchViewStateRecord`.
- Page-side surface patches are projection optimizations, not canonical mutation policy.
- The selected extraction consumes `ProjectStructureSurface` read-only and must not become a repository or writable aggregate.

## Existing Coverage

- direct page component coverage spans database switching, moves, simple mutations, task assignment creation, and web preview;
- integration coverage already exercises an agent-started process context summary, including visual targets, path redaction, and generated-evidence exclusion;
- no direct unit suite currently owns the duplicated summary/output-root algorithm or hierarchy cycle rules.

## Baseline Validation Observation

The existing integration characterization test was discovered and invoked. A normal build attempt was blocked by the already-running Web host locking `src/App/CanDoItAll.Web/bin`; `--no-build` reached the test but the restricted sandbox denied its test bootstrap write under the user secret vault. This is an environment limitation, not a behavior failure, and direct unit/component validation remains available.
