# Program sequence and independent downstream work

## Dependency flow

~~~mermaid
flowchart TD
    Base[Shared decisions and behavior classification]
    Seams[One coherent in-place seam]
    Freeze[Frozen boundary and consumer checkpoint]
    Extract[Small physical UI extraction]
    Sandbox[Browser sandbox and measured iteration]
    Navigation[Feature location binding and history]
    Second[Different UI archetype]
    Patterns[Review shared boundary patterns]
    Rollout[Further module capability slices]
    Tooling[Direct watch tuning then Manager integration]
    Docs[Durable documentation and branch closure]

    Base --> Seams --> Freeze
    Freeze --> Extract --> Sandbox
    Freeze --> Navigation
    Freeze --> Second
    Sandbox --> Patterns
    Second --> Patterns
    Navigation -. navigation evidence when available .-> Patterns
    Patterns --> Rollout
    Sandbox --> Tooling
    Rollout --> Docs
    Tooling --> Docs
~~~

There is deliberately no Navigation -> Extract/Sandbox prerequisite. A second archetype can
begin independently. Navigation supplies evidence as it becomes available; it does not block
shared boundary review or further in-place module work. Validate navigation-specific rules
when their own implementation exists.

## First delivery path

The owner's 2026-09-05 review supersedes the earlier sandbox-immediately-after-Agents scheduling preference:

1. Close Agents SB09: initial load/reload overlap and session-owned nested dialogs.
2. Execute CDA-UI-SEAMS-PROVIDERS-01: typed state, selection, session and reads in place.
3. Prepare/execute CDA-UI-SEAMS-PROVIDERS-02 separately: commands and owned effects, based on the actual registry commit boundary.
4. Freeze AgentCatalogPanel for a light UI assembly, small catalog sandbox and real warm dotnet-watch measurement.
5. Prepare PROVIDER-HISTORY-01 independently, covering typed queries, selection and profile-change lifetime.
6. Then choose the next module hotspot. Do not combine provider history/shared backend/ProjectStructure into the provider-profile slice.

The current request executes steps 1 and 2. Subsequent steps need their own concrete scope/proof; neither routing nor full editor extraction is implied.

An earlier sandbox delivery from SB03 is possible only through an explicit coordinated
scope handoff/frozen branch. Moving files concurrently with SB04–SB07 on the same checkout
would invalidate their references and proof. No such parallel implementation is authorized here.

## Broader rollout

| Candidate | Intended learning / boundary |
|---|---|
| Agents catalog/editor | Controlled selection, mutable editor session, cross-module pickers |
| Provider administration/history | Applied queries, partial data, independent read regions |
| Small Resources or Collaboration slice | Check that shared state/host rules work outside Agents |
| ProcessWorkspaceShell | Remove service location through explicit operational capabilities |
| ProjectFilesDialog | File/browser/native effects, session and cancellation |
| ProjectStructure | Capability-by-capability extraction of a long-lived workspace |
| Remaining CRM/HR, Workspace, Scheduler, Test Lab | Own state/behavior matrix per cluster |

The candidate register is not a complete application audit. Each child refreshes its
scope and all affected consumers. Do not impose an identical controller or project layout.

## Size, rollback, and coordination

Use coherent behavior boundaries. Separate logical ownership, physical movement, routing,
visual redesign, and defect fixes unless an explicit dependency requires combining them.
Every slice has a reversible source checkpoint, operation evidence, and revalidation keys.
A Git rollback is not a rollback of committed user data or external side effects.

The old independent development-test-repair preparation hold is superseded by the current
observed Agents child. Baseline freshness remains mandatory, without restarting historical work.

## Documentation

Transfer proven durable rules before removal of temporary bundles. Record active consumers,
approved version compatibility, and the destination of each rule. Cleanup/merge/remote writes
are separately authorized tasks, not effects of this reference.
